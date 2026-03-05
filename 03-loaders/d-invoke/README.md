# D/Invoke Framework

D/Invoke (Dynamic Invocation) calls Windows APIs at runtime without static `[DllImport]` declarations. EDR products that hook the Import Address Table (IAT) are completely bypassed because the API is never in the IAT to begin with.

**Source:** The full implementation lives in `loaders/d-invoke/` (canonical directory). This directory is a surfaced reference — see canonical for compilable code.

---

## Why D/Invoke matters

Standard P/Invoke:
```csharp
[DllImport("kernel32")]
public static extern IntPtr VirtualAlloc(...);   // ← appears in IAT
```
EDR hooks `VirtualAlloc` in the IAT → logs/blocks every call.

D/Invoke equivalent:
```csharp
// Resolved at runtime — never in the IAT
IntPtr pVirtualAlloc = DynamicAPI.GetLibraryAddress("kernel32.dll", "VirtualAlloc");
object[] parameters = new object[] { IntPtr.Zero, (UInt32)shellcode.Length, 0x3000, 0x40 };
IntPtr allocAddress = (IntPtr)DynamicAPI.DynamicAPIInvoke("kernel32.dll", "VirtualAlloc", typeof(Win32.VirtualAllocDelegate), ref parameters);
```
The hook on the IAT entry is never triggered.

---

## How D/Invoke works (mechanically)

1. **GetLibraryAddress()** — walks the PEB's loaded module list to find the DLL in memory (no LoadLibrary call)
2. **GetProcedureAddress()** — reads the DLL's export table directly (no GetProcAddress call)
3. **DynamicAPIInvoke()** — uses `Marshal.GetDelegateForFunctionPointer()` to create a typed delegate and calls it
4. **Syscalls (optional)** — goes further: calls NT syscalls directly (NtAllocateVirtualMemory, NtWriteVirtualMemory, etc.) bypassing even the ntdll layer

---

## Project structure (canonical: loaders/d-invoke/)

```
loaders/d-invoke/
├── D_invoke.sln       — solution file
├── exe/               — builds as standalone .exe (with InstallUtil uninstall wrapper)
│   ├── Program.cs     — main loader: AES-decrypt shellcode → D/Invoke inject
│   ├── Template.cs    — template with <IMPORTS>, <MAIN>, <CLASSES> placeholders
│   └── ...
├── dll/               — builds as .dll (for reflective loading)
│   ├── Class1.cs      — DLL entry point
│   ├── Template.cs    — same placeholders
│   └── ...
├── service/           — builds as Windows service (persistent)
├── Builder/           — Python builder script that fills in the templates
└── test/              — unit tests
```

---

## The loader flow (Program.cs walkthrough)

1. **Sandbox detection** — `Sleep(5000)`, check elapsed time. If < 4.5s, exit (sandbox sped up time).
2. **AES decrypt** — shellcode is embedded as AES-256-CBC encrypted bytes with hardcoded key/IV (change before use!).
3. **D/Invoke VirtualAlloc** — allocates RW memory via dynamic API resolution (no IAT entry).
4. **Copy shellcode** — `Marshal.Copy()` to allocated buffer.
5. **D/Invoke VirtualProtect** — change memory to RX.
6. **D/Invoke CreateRemoteThread / NtCreateThreadEx** — execute shellcode via thread creation.

---

## Customize and use

```bash
# 1. Generate AES-encrypted shellcode:
python3 loaders/d-invoke/Builder/build.py shell.bin --key YOUR_KEY --iv YOUR_IV

# 2. Open D_invoke.sln in Visual Studio
# 3. Replace Key/IV constants in Program.cs with yours
# 4. Build → exe/ or dll/ depending on delivery method

# 5. Run as .exe:
.\d-invoke-loader.exe

# 6. Or via InstallUtil (AppLocker bypass — the InstallUtil Uninstall() hook is built in):
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\InstallUtil.exe /logfile= /LogToConsole=false /U d-invoke-loader.exe
```

**CRITICAL:** Change the default AES key and IV (`MyKey` / `Myiv` in Program.cs) before any real use. The default key is shared in the source code.

---

## Comparison: P/Invoke vs D/Invoke vs Syscalls

| | P/Invoke | D/Invoke | Direct Syscalls |
|--|----------|---------|----------------|
| IAT visibility | Yes (hookable) | No | No |
| API resolution | Static | Runtime (PEB walk) | N/A |
| EDR hook evasion | None | IAT hooks bypassed | All usermode hooks bypassed |
| Complexity | Low | Medium | High |
| Where to use | Testing only | Production evasion | Maximum evasion |

---

## Key APIs called via D/Invoke in the loader

| Win32 API | Why D/Invoke instead of direct |
|-----------|-------------------------------|
| `VirtualAlloc` | Most-watched API for shellcode staging |
| `VirtualProtect` | Changing RW→RX is a red flag in EDR |
| `CreateRemoteThread` | Remote injection is highly monitored |
| `NtCreateThreadEx` | NT layer — harder to hook than Win32 layer |
| `NtWriteVirtualMemory` | Bypass WriteProcessMemory hooks |
