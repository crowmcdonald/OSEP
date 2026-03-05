# clm-bypass

One file: `psbypass.cs`. **Standalone** — compile and run to get a FullLanguage PowerShell session, bypassing both CLM and AMSI.

---

## psbypass.cs

**Constrained Language Mode (CLM)** restricts which .NET types PS can use. It's enforced by AppLocker/WDAC policy. Running PowerShell from inside a C# process that you control creates a new `Runspace` that is **not subject to CLM** — it always runs in FullLanguage mode.

This file also patches:
- `GetSystemLockdownPolicy()` → returns 0 (unlocked)
- `amsi.dll` functions → disables AMSI scanning in the new session

---

## Compile (do this on your Windows Visual Studio dev box)

**Visual Studio (Recommended):**

1. **File → New → Project → Console App (.NET Framework)** (NOT .NET Core)
   - Name: `psbypass`, Framework: **.NET Framework 4.8**
   - Click Create
2. Delete all generated code, paste in `psbypass.cs` contents
3. **Right-click References → Add Reference → Browse**
   Navigate to: `C:\Windows\assembly\GAC_MSIL\System.Management.Automation\1.0.0.0__31bf3856ad364e35\System.Management.Automation.dll`
   Click Add → OK
4. **Project → psbypass Properties → Build tab:**
   - Platform target: **x64**
   - Check **"Allow unsafe code"**
5. **Build → Build Solution** (`Ctrl+Shift+B`)
6. Output: `bin\x64\Debug\psbypass.exe`

**Alt (Windows cmd):**
```cmd
csc.exe /unsafe /platform:x64 /r:C:\Windows\assembly\GAC_MSIL\System.Management.Automation\1.0.0.0__31bf3856ad364e35\System.Management.Automation.dll /out:psbypass.exe psbypass.cs
```

---

## Run

```bash
# Direct (if AppLocker doesn't block your .exe)
.\psbypass.exe

# Via InstallUtil (if AppLocker blocks your .exe — combine with applocker-bypass)
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\InstallUtil.exe /logfile= /LogToConsole=false /U psbypass.exe
```

Once running, you'll have an interactive-style PS session in FullLanguage mode. You can run any PS commands from there, including AMSI-blocked ones.

---

## When to use

| Situation | Solution |
|-----------|----------|
| PS is in CLM, want FullLanguage | `psbypass.cs` (compile + run) |
| AMSI blocks PS scripts | `ps-amsi-bypass.ps1` (run in the same PS session) |
| Both CLM and AMSI, AppLocker blocks .exe | `psbypass.cs` via `InstallUtil.exe` |
| Just AMSI, no CLM | Use `amsi-bypass/ps-amsi-bypass.ps1` instead (simpler) |
