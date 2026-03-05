**Create SMB Share**
On linux.
* sudo apt install samba
	* if needed: sudo smbpasswd -a kali
Edit /etc/samba/smb.conf
* sudo subl /etc/samba/smb.conf
```
[global]
   workgroup = WORKGROUP ## Defines Windows workgroup name
   map to guest = Bad User ## Allows mapping of unknown users to guest
   server string = Samba Server ## Description of the Samba server
   usershare allow guests = yes ## Permits guest access to user shares
   allow insecure wide links = yes ## Enables insecure symbolic links

[share]
   path = /tmp/share ## Directory path to be shares
   writable = yes ## Allows write access to the share
   guest ok = yes ## Permits guest access without authentication
   guest only = yes ## Permit guest access without authentication
   browseable = yes ## Makes the share visible in network browsing
   create mask = 0777 ## Sets permissions for new files and directories
   directory mask = 0777 ## Sets permissions for new files and directories
   force user = nobody ## Forces all file operations to occur as the specific user
```
**Use this one without the comments from above**
```
[global]
   workgroup = WORKGROUP
   server string = Samba Server 
   security = user
   map to guest = Bad User 
   usershare allow guests = yes 
   allow insecure wide links = yes 
   unix extensions = no

[share]
   path = /home/parallels/Documents/All_Tools
   writable = yes 
   guest ok = yes 
   guest only = yes 
   browseable = yes 
   create mask = 0777 
   directory mask = 0777 
   force user = nobody 


```
* sudo mkdir /tmp/share
* sudo chmod 777 /tmp/share
* sudo chmod o+x /home/parallels
* sudo chmod o+x /home/parallels/Documents
* sudo systemctl start smbd nmbd
* sudo systemctl restart smbd nmbd
* sudo systemctl status smbd nmbd

On Windows:
* net use Z: "\\192.168.45.156\share"
* dir "\\192.168.45.156\share"
```
Copy file from Linux -> Windows
	copy "\\192.168.45.156\share\test.txt" "C:\Users\web_svc\Documents\test.txt"
	----> copy "\\192.168.45.156\share\test.txt" .

Copy file from Windows -> Linux
	copy "C:\Users\web_svc\Documents\filename.txt" "\\192.168.45.156\share\"
	----> copy "\\192.168.45.156\share\test.txt" .	
```

Disconnect the drive later if you want
* net use Z: /delete

Troubleshooting:
* testparm
* Validate share path exists and perms are correct