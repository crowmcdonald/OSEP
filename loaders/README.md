# Loaders — Master Reference

> This is the raw/canonical source directory. The curated, documented versions live in `03-loaders/`.
> For plain English explanations of how each technique works, see `HOW-IT-WORKS.md` at the repo root.

---

## What's In Here (By Folder)

### `simple-runner/`
**Simplest possible shellcode runner.** VirtualAlloc → copy shellcode → CreateThread.
No encoding, no evasion. Use for testing that your shellcode works.

Files:
- `Program.cs` — C# version
- `Simple Shellcode Runner.ps1` — PowerShell version
- `Simple Shellcode Runner.vba` — VBA macro version

---

### `generic-runner/`
**XOR-encoded shellcode runner.** Same as simple-runner but with XOR decode loop before execution.

Files:
- `ShellcodeRunner.cs` — C# runner, expects XOR-encoded bytes
- `ipv4-obfuscate.py` — Python script to convert shellcode into IPv4 address strings (alternative encoding)
- `RevShell.cs` — IPv4 obfuscation C# runner (uses `RtlIpv4StringToAddressA`)

---

### `advanced-runners/`
**XOR-encoded runner with sandbox evasion.** Two-file pair: encoder + runner.

Files:
- `Adv_Xor.cs` — **Run this first.** Paste raw shellcode bytes in, it adds 2 to each byte and prints the encoded array. Copy output into Adv_Runner.
- `Adv_Runner.cs` — **The actual payload.** Contains encoded bytes, subtracts 2 at runtime to decode, checks for sandbox (Sleep timer + VirtualAllocExNuma), hides console window, executes.
- `DLL_Runner/` — Same Adv pair but output is a DLL loaded via PowerShell
- `Adv_Runner/` — Standalone version with commands.txt
- `Process_Injection/` — XOR-encoded remote process injection variant

---

### `disk-based/`
**AppLocker bypass via InstallUtil.** AES-encrypted shellcode that runs through the whitelisted `InstallUtil.exe` tool.

Run with: `InstallUtil.exe /logfile= /LogToConsole=false /U payload.exe`

Subfolders:
- `clrunner/clrunner.cs` — AES shellcode runner via InstallUtil
- `clinject/clinject.cs` — AES shellcode injector via InstallUtil (injects into target process)
- `clhollow/clhollow.cs` — AES process hollowing via InstallUtil + PPID spoofing

All three have hardcoded AES key/IV — replace before use. Shellcode placeholder bytes: `11-11-11-11` (LHOST), `22-22` (LPORT).

---

### `process-injector/`
**Remote process injection.** Opens an existing process, allocates memory inside it, writes shellcode, creates remote thread.

Files:
- `Program.cs` — Auto-selects spoolsv.exe (admin) or explorer.exe (user). XOR-encoded (key 0xfa).
- `inj_xor.cs` — Standalone XOR encoder for the injector
- `inj_runner.cs` — Injector without the XOR step (raw shellcode)

---

### `process-hollowing/`
**Process hollowing.** Creates svchost.exe suspended, overwrites its code with shellcode, resumes it. Appears as legitimate svchost in Task Manager.

Files:
- `Program.cs` — Core hollowing implementation

---

### `sections-injector/`
**NtCreateSection-based injection.** Avoids VirtualAllocEx + WriteProcessMemory (the calls EDR specifically hooks). Uses shared memory sections instead.

Files:
- `Program.cs` — Full sections injection implementation

---

### `assembly-loader/`
**Reflective .NET loading.** Load Rubeus, Mimikatz, SharpHound, or any .NET tool entirely from memory via PowerShell.

Files:
- `Invoke-SharpLoader.ps1` — **Production loader.** Patches AMSI + ETW, downloads AES-encrypted .NET EXE from URL, decrypts in memory, executes. Pass arguments directly.
- `Invoke-SharpEncrypt.ps1` — **Encrypt your tools.** AES+GZip+Base64 encode any .NET EXE for hosting.
- `DLL_Loader.ps1` — Loads a compiled DLL from URL, calls `ClassLibrary1.Class1.runner()`
- `DLL_Runner.cs` — Compile as .dll (Class Library). XOR-decoded shellcode runner exported as DLL.
- `DLL_Xor.cs` — Encoder for DLL_Runner (same +2 trick as Adv_Xor)
- `amsi-bypass.ps1` — Standalone AMSI bypass
- `cradle.ps1` — Simple download cradle

---

### `d-invoke/`
**EDR hook bypass via dynamic API resolution.** Resolves Windows API functions at runtime via PEB walking instead of IAT — bypasses EDR hooks on ntdll.dll.

Subfolders:
- `exe/` — Standalone EXE loader
- `dll/` — DLL version (reflective load)
- `service/` — Windows Service (persistence)
- `Builder/` — Automated builder
- `test/` — D/Invoke resolution test

---

### `ppid-spoof-dropper/`
**Parent PID spoofing.** Spawns a hidden PowerShell process that appears as a child of notepad.exe rather than your loader — hides in the process tree.

Files:
- `PPIDSpoofDropper.cs` — Finds/spawns notepad.exe, runs base64-encoded PS command as its child. Pass base64 as arg or hardcode it.

---

### `applocker-bypass/`
**AppLocker bypass via PowerShell Runspace.** Runs PowerShell code inside a .NET assembly when PowerShell.exe itself is blocked.

Files:
- `Program.cs` — PS runspace inside .NET (bypasses exe restriction)
- `altbypass.cs` — Alternative bypass method
- `psbypass.cs` — PS-level bypass
- `DLLInject64_DN2JS.cs` — DotNetToJScript 64-bit DLL injection variant
- `DLLRunner32_DN2JS.cs` — DotNetToJScript 32-bit runner variant
- `psBypass/` — PowerShell-based bypass scripts
- `altBypass/` — Alternative bypass methods

---

### `clm-bypass/`
**Constrained Language Mode bypass.** Escape from CLM (PowerShell restricted execution mode) via PowerShell Runspace in a .NET assembly.

Files:
- `README.md` — Which bypass to use when

---

### `defender-bypass/`
**Windows Defender bypass utilities.**

Files:
- `DefendersDeath.ps1` — Script to disable Defender components
- `uacbypass.ps1` — UAC bypass
- `ami.txt` — AMSI bypass snippets
- `FullBypass.csproj` — Combined bypass project

---

### `memory-based/`
**Python-based injection tools.** For Linux-side cross-platform injection or scripted attacks.

Files:
- `powerhollow.py` — Python process hollowing
- `powerinject.py` — Python remote injection

---

### `powershell-payloads/`
**Ready-to-use PowerShell payloads.**

Files:
- `PSmetrun.ps1` — Meterpreter PowerShell runner
- `powerrun.ps1` — Generic PS shellcode runner
- `rev.ps1` — Reverse shell
- `ami.ps1` — AMSI bypass inline
- `loaderbypass.ps1` — Combined loader with bypass

---

### `linux/`
**C-based Linux loaders.** For shellcode execution and privilege escalation on Linux targets.

Files:
- `simpleLoader.c` — Allocates mmap(PROT_EXEC), decodes XOR shellcode, executes
- `simpleXORencoder.c` — Encoder to XOR-encode shellcode for simpleLoader
- `sharedLibrary_LD_PRELOAD.c` — Shared library that hooks `geteuid()` → runs shellcode. Load with LD_PRELOAD against SUID binaries for privesc.
- `sharedLibrary_LD_LIBRARY_PATH.c` — Same via LD_LIBRARY_PATH

---

### `webshells/`
**ASPX shells for IIS.** Deploy to a compromised IIS server to get shellcode execution.

Files:
- `ASPX_Runner64.aspx` — Runs shellcode in current IIS process (w3wp.exe)
- `ASPX_Inject64.aspx` — Injects shellcode into w3wp.exe
- `Web_Xor.cs` — XOR encoder for webshell shellcode
- `Webshell_Runner/` — Additional webshell variants

---

### `phishing/`
**HTA and JS delivery templates.**

Files:
- `nonDN2J.hta` — HTA using DotNetToJScript technique
- `clickme.hta` / `custom.hta` — HTA delivery templates
- `test.js` — JScript delivery test
- `viewme.html` — HTML phishing page
- `osep-phishing-samples/` — Additional sample files

---

### `vba-macro/`
**Office macro templates.**

Files:
- `XorShellcodeRunner.vba` — VBA macro with XOR decode + shellcode execution via P/Invoke Declare statements
- `process_hollowing.vba` — Process hollowing from VBA
- `WordMacroRunner.vbs` / `WordMacroRunnerBasic.vbs` — VBS-based macro runners
- `WordPsCradle.vbs` — VBS that downloads and runs a PS payload
- `WordMacroInject.vbs` — VBS process injection
- `vbObfuscate.ps1` — VBA obfuscation helper

---

### `payload-generators/`
**D/Invoke builder variants.** Automated payload generation using the D/Invoke framework.

---

## Quick Lookup: I Need To...

| Goal | Go To |
|------|-------|
| Test shellcode works | `simple-runner/Program.cs` |
| Basic AV bypass (XOR + sandbox checks) | `advanced-runners/Adv_Xor.cs` → `Adv_Runner.cs` |
| AppLocker bypass | `disk-based/clrunner/` + InstallUtil |
| Inject into existing process | `process-injector/Program.cs` |
| Look like svchost in Task Manager | `process-hollowing/Program.cs` |
| Bypass EDR API hooks | `d-invoke/exe/` |
| Hide process parent | `ppid-spoof-dropper/PPIDSpoofDropper.cs` |
| Load Rubeus/Mimikatz in memory | `assembly-loader/Invoke-SharpLoader.ps1` |
| IIS webshell execution | `webshells/ASPX_Inject64.aspx` |
| Office macro delivery | `vba-macro/XorShellcodeRunner.vba` |
| HTA delivery | `phishing/nonDN2J.hta` |
| Linux shellcode execution | `linux/simpleLoader.c` |
| Linux LD_PRELOAD privesc | `linux/sharedLibrary_LD_PRELOAD.c` |
| Bypass CLM/constrained PS | `clm-bypass/` |

For full explanations of how each technique works → **`HOW-IT-WORKS.md`** (repo root)
