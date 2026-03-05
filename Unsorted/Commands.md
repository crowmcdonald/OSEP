

Enum SQL instances on LDAP (can you do other services?)

setspn -T corp1 -Q MSSQLSvc/*

It is important to note that Net-NTLM relaying against SMB is only possible if [SMB signing](https://learn.microsoft.com/en-gb/archive/blogs/josebda/the-basics-of-smb-signing-covering-both-smb1-and-smb2) is not enabled. SMB signing is only enabled by default on domain controllers.


SQL find Impersonate privileges:

```
SELECT distinct b.name FROM sys.server_permissions a INNER JOIN sys.server_principals b ON a.grantor_principal_id = b.principal_id WHERE a.permission_name = 'IMPERSONATE'

```


SQL C# Console application