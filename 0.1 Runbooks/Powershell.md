# Windows Privilege Escalation & Enumeration — Quick Reference

**References:**

- https://www.noobsec.net/privesc-windows/
- https://book.hacktricks.xyz/windows-hardening/checklist-windows-privilege-escalation
- https://book.hacktricks.xyz/windows-hardening/windows-local-privilege-escalation#version-exploits
- https://gist.github.com/HarmJ0y/184f9822b195c52dd50c379ed3117993
## Initial Situational Awareness

```powershell
whoami /all
hostname
systeminfo
echo %userdomain%
[environment]::Is64BitOperatingSystem
[environment]::Is64BitProcess
$ExecutionContext.SessionState.LanguageMode
```
## User & Group Enumeration

```powershell
# Local
whoami /priv
whoami /groups
whoami /logonid
whoami /fqdn
net user
net localgroup
query user

# Domain
net user /domain
net group /domain
net group "Domain Controllers" /domain
systeminfo | findstr /B /C:"Domain"
nltest /dsgetdc:<domain>
nltest /domain_trusts
```
### Generate Domain User List (PowerView)

```powershell
Set-ExecutionPolicy Bypass -Scope Process
Import-Module .\PowerView.ps1
Get-DomainUser * | Select-Object -ExpandProperty samaccountname | Foreach {$_.TrimEnd()} | Set-Content adusers.txt
Get-Content .\adusers.txt | select -First 10
```
Or with AD module:

```powershell
Get-ADUser -Filter * -Property DisplayName | Select-Object DisplayName
```
### PowerView Domain Recon

```powershell
Get-NetDomain
Get-NetDomainController
Get-DomainUser -SPN
```
## Network

```powershell
ipconfig /all
ipconfig /displaydns
netstat -ano
arp -a
route print
net use
net share
net session              # requires admin
netsh advfirewall show allprofiles
```
## Services, Processes & Scheduled Tasks

```powershell
net start
sc query state= all
tasklist
schtasks /query /fo LIST /v
wmic product get name,version
wmic startup get caption,command
```
### AppLocker Policy

```powershell
Get-AppLockerPolicy -Effective
Get-AppLockerPolicy -Effective | select -ExpandProperty RuleCollections
```
## File System & Permissions

```powershell
icacls <file_or_folder>
icacls * /T | findstr "(F)"           # find writable folders/files
```
### Hunt for SAM File

```powershell
Get-ChildItem -Path C:\ -Recurse -Force -ErrorAction SilentlyContinue | Where-Object { $_.Name -eq "SAM" }
```
## Event Logs & Audit Policy

```powershell
wevtutil el
auditpol /get /category:*
set                                   # environment variables
```
## Useful Tricks

### Add User to Local Admins

```powershell
Add-LocalGroupMember -Group "Administrators" -Member "Username"
```
### Enable Color Output in PowerShell

```powershell
Set-PSReadLineOption -Colors @{
    Command   = "Yellow"
    Operator  = "Cyan"
    Parameter = "Green"
    String    = "Magenta"
    Variable  = "White"
    Type      = "Blue"
    Number    = "DarkCyan"
}
```
## Watching Scheduled Tasks / Processes

Useful when you suspect a hidden scheduled task or background process. If you find a script running, consider replacing it with a reverse shell to escalate.
### Watch Running Scheduled Tasks (Log to File)

```powershell
$logFile = "C:\Users\Documents\scheduled_tasks_watch.txt"

while ($true) {
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Get-ScheduledTask | Where-Object {$_.State -eq 'Running'} | ForEach-Object {
        "$timestamp - TaskName: $($_.TaskName), State: $($_.State)" | Tee-Object -FilePath $logFile -Append
    }
    Start-Sleep -Seconds 2
}
```
### Watch a Specific Process (Watch-Command)

```powershell
Get-Process backup -ErrorAction SilentlyContinue | Watch-Command -Difference -Continuous -Seconds 30
```
With more detail + log to file:

```powershell
Get-Process backup -ErrorAction SilentlyContinue | Select-Object Name, Id, Path | Watch-Command -Difference -Continuous -Seconds 30 >> backup-watch.txt
```

> **Tip:** Output can be noisy. In practice, once you identify a process/script name (e.g., `backup.exe`), just replace the binary with a rev shell named the same thing and wait for it to run as SYSTEM.