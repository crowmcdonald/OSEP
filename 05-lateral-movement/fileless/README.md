# fileless lateral movement

One file: `fileless-lateral.cs`. **Standalone** — compile and run from your already-compromised Windows host.

---

## fileless-lateral.cs

Moves laterally to a remote host by hijacking a Windows service via the Service Control Manager (SCM) API. Does **not** require PowerShell Remoting, WinRM, PsExec, or any third-party tools.

Functionally nearly identical to `PSLessExec.cs` (in `windows/`) — the key difference is this version does **not** include the InstallUtil AppLocker bypass wrapper.

**What it does:**
```
1. Opens the remote SCM (requires admin on the target)
2. Saves the target service's current binary path
3. PASS 1: Replaces binary path with MpCmdRun.exe -RemoveDefinitions -All
           → runs on target, wipes Windows Defender AV signatures
4. PASS 2: Replaces binary path with YOUR binary (e.g. runner.exe path on target)
           → starts the service → your payload runs as SYSTEM on the target
5. PASS 3: Restores the original binary path (covers tracks)
```

**Important:** Your payload binary must already be on the target system before running this (e.g. dropped via SMB, webshell, or previous session). This tool only executes it via the service mechanism.

---

## Compile (Visual Studio on Windows dev box)

1. **File → New → Project → Console App (.NET Framework)** (NOT .NET Core)
   - Name: `lateral`, Framework: **.NET Framework 4.8**
2. Delete defaults, paste `fileless-lateral.cs`
3. **Project → Properties → Build:**
   - Platform target: **x64**
   - Check **"Allow unsafe code"**
4. **Build → Build Solution** (`Ctrl+Shift+B`)
5. Output: `bin\x64\Debug\lateral.exe`

**Alt (Windows cmd):** `csc.exe /unsafe /platform:x64 /out:lateral.exe fileless-lateral.cs`

---

## Run

```bash
# From your compromised host — target is the next machine to pivot to
.\lateral.exe <TARGET_HOSTNAME_OR_IP> <SERVICE_NAME> <BINARY_PATH_ON_TARGET>

# Example:
.\lateral.exe dc01.corp.local SensorService "C:\Users\Public\runner.exe"
```

**What you need:**
- Admin credentials or token for the target machine (use `make_token` or `steal_token` in meterpreter first)
- Your payload dropped somewhere on the target (C:\Users\Public is usually writable)
- The name of a service on the target to hijack (use `sc \\target query` to list them)
- A listener running on Kali

---

## Notes

- Good service targets to hijack: any non-critical service that won't be immediately missed (e.g. `SensorService`, `MapsBroker`, `DiagTrack`)
- The service is started, runs your binary, then the service is restored — the service may show an error because your binary isn't a real service — this is normal
- For AppLocker environments, see `windows/PSLessExec.cs` which includes the InstallUtil wrapper
- Compare with `PSLessExec.cs`: nearly identical, but `PSLessExec.cs` has a 3-pass approach using `cmd /c` for the payload and a slightly different argument structure
