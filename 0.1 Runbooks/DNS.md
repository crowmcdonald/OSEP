# DNS Pentest Commands

## Key Indicators
- Non-standard TXT records (often contain API keys, verification tokens)
- Development subdomains (dev, staging, test)
- Internal hostnames leaked in records
- Weak SPF records (all, include:*)
- CNAMEs pointing to unregistered services
- Missing PTR records for mail servers
- Version information in DNS banners
- Overly permissive zone transfers
 
## **Quick Wins**
1. Zone transfer successful
2. SPF record with +all
3. Internal IPs in public DNS
4. Anonymous SMB access
5. Abandoned cloud service subdomains
6. DNS recursion enabled
7. Version information exposed
8. PTR records missing for mail servers

## **Basic Enumeration**

### Initial Recon
```bash
# WHOIS
whois example.com
# Look for: Admin contacts, related domains, non-redacted info

# Basic DNS
dig example.com ANY
dig example.com NS
dig example.com MX
dig example.com TXT
dig example.com SOA
dig -x <IP_Address>
# Look for: 
# - Internal IPs in records
# - Weak SPF (v=spf1 +all)
# - Cloud services in MX
# - Version info in TXT

# Certificate logs
curl -s https://crt.sh/\?q\=example.com\&output\=json | jq .
curl -s https://crt.sh/\?q\=example.com\&output\=json | jq . | grep name | cut -d":" -f2 | grep -v "CN=" | cut -d'"' -f2 | awk '{gsub(/\\n/,"\n");}1;' | sort -u
# Look for: Dev sites, internal names, expired certs
```
### Host Enumeration
```bash
# Check subdomain IPs
for i in $(cat subdomainlist); do
    host $i | grep "has address" | cut -d" " -f1,4
done
# Look for: Different IP ranges, cloud IPs, internal addresses

# Quick enumeration script
#!/bin/bash
domain=$1
dig +short $domain A
dig +short $domain AAAA
dig +short $domain MX
dig +short $domain TXT
curl -s "https://crt.sh/?q=%25.$domain&output=json" | jq -r '.[] | .name_value' | sort -u
# Look for: Mismatched ranges, legacy records
```

## Advanced Enumeration

### Subdomain Discovery
```bash
# Sublist3r
sublist3r -d example.com -o sublist3r-results.txt
# Good for: Quick passive recon, historical data

# Amass
amass enum -passive -d example.com -o amass-results.txt
amass enum -brute -d example.com -o amass-brute-results.txt
amass intel -d example.com -o amass-intel-results.txt
# Look for: Legacy apps, dev environments, forgotten assets

# DNSRecon
dnsrecon -d example.com -t std -o dnsrecon-results.txt -D /usr/share/seclists/Discovery/DNS/subdomains-top1million-5000.txt
dnsrecon -d example.com -t axfr -o dnsrecon-zone-transfer.txt
# Focus on: Zone transfer attempts, wildcard responses
```

### Virtual Hosts
```bash
# wfuzz
wfuzz -u http://10.10.11.166 -H "Host: FUZZ.trick.htb" -w /usr/share/seclists/Discovery/DNS/subdomains-top1million-5000.txt
# Look for: Different response sizes, unique status codes

# Advanced wfuzz
wfuzz -c -w /usr/share/wordlists/seclists/Discovery/DNS/subdomains-top1million-5000.txt -H "Host: FUZZ.example.com" -H "X-Forwarded-For: FUZ2Z" -u http://192.168.1.10/ -t 100 --sc 200
# Watch for: Non-standard headers, admin panels

# ffuf
ffuf -w /usr/share/wordlists/seclists/Discovery/DNS/subdomains-top1million-5000.txt:FUZZ -u http://example.com -H "Host: FUZZ.example.com" -fs 123
# Note differences in: Response times, content lengths
```
### Service Enumeration
```bash
# Nmap DNS scripts
sudo nmap -sC -sV -sU -p 53 192.168.112.71 --script=dns*
# Key finds: 
# - Version numbers
# - Recursive queries enabled
# - Zone transfer possible

# Zone transfers
dig axfr @ns1.example.com example.com
# Success indicates critical misconfiguration

# SMB check
smbclient //192.168.112.71/backups -N
# Look for: Anonymous access, readable shares
```
### Service Records
```bash
dig _ldap._tcp.example.com SRV
dig _kerberos._udp.example.com SRV
# Check for: Internal service info, domain controllers
```
### Subdomain Takeover
```bash
# Subjack
subjack -w subdomains.txt -t 100 -timeout 30 -o results.txt -ssl
# Look for: Unclaimed services (GitHub Pages, Heroku, AWS)

# Subzy
subzy -targets subdomains.txt
# Watch for: Discontinued services, dead CNAMEs
```

## Common Patterns
- dev.*, staging.*, test.* (Development environments)
- admin.*, portal.*, internal.* (Admin interfaces)
- old.*, backup.*, archive.* (Legacy systems)
- api.*, ws.*, services.* (Service endpoints)
- vpn.*, remote.*, citrix.* (Remote access)

## Wordlists
- `/usr/share/seclists/Discovery/DNS/subdomains-top1million-5000.txt`
- `/usr/share/seclists/Discovery/DNS/namelist.txt`
- `/usr/share/seclists/Discovery/DNS/bitquark-subdomains-top100000.txt`

# DNS Discovery

DNSRecon: 

- dnsrecon -d www.example.com -a 
- dnsrecon -d www.example.com -t axfr
- dnsrecon -d <startIP-endIP>
- dnsrecon -d www.example.com -D <namelist> -t brt

Dig: 

- dig www.example.com + short
- dig www.example.com MX
- dig www.example.com NS
- dig www.example.com> SOA
- dig www.example.com ANY +noall +answer
- dig -x www.example.com
- dig -4 www.example.com (For IPv4)
- dig -6 www.example.com (For IPv6)
- dig www.example.com mx +noall +answer example.com ns +noall +answer
- dig -t AXFR www.example.com

Dnsenum Enumeration:

- dnsenum --dnsserver 172.21.0.0 -enum intranet.megacorpone.xx
- dnsenum --dnsserver 172.21.0.0 -enum management.megacorpone.xx
- dnsenum --dnsserver 172.21.0.0 -enum www.megacorpone.xx

dnsX Enumeration: 
- dnsx -l domains.txt -resp -a -aaaa -cname -mx -ns -soa -txt
- dnsx -silent -d megacorpone.com -w /usr/share/seclists/Discovery/DNS/dns-Jhaddix.txt

Using with subfinder: 
- subfinder -silent -d megacorpone.com | dnsx -silent
- subfinder -silent -d megacorpone.com | dnsx -silent -a -resp
- subfinder -silent -d megacorpone.com | dnsx -silent -a -resp-only
- subfinder -silent -d megacorpone.com | dnsx -silent -cname -resp
- subfinder -silent -d megacorpone.com | dnsx -silent -asn 


Nmap Enumeration: 
```
$ ls -lh /usr/share/nmap/scripts/ | grep dns
-rw-r--r-- 1 root root  1499 Oct 12 09:29 broadcast-dns-service-discovery.nse
-rw-r--r-- 1 root root  5329 Oct 12 09:29 dns-blacklist.nse
-rw-r--r-- 1 root root 10100 Oct 12 09:29 dns-brute.nse
-rw-r--r-- 1 root root  6639 Oct 12 09:29 dns-cache-snoop.nse
-rw-r--r-- 1 root root 15152 Oct 12 09:29 dns-check-zone.nse
-rw-r--r-- 1 root root 14826 Oct 12 09:29 dns-client-subnet-scan.nse
-rw-r--r-- 1 root root 10168 Oct 12 09:29 dns-fuzz.nse
-rw-r--r-- 1 root root  3803 Oct 12 09:29 dns-ip6-arpa-scan.nse
-rw-r--r-- 1 root root 12702 Oct 12 09:29 dns-nsec3-enum.nse
-rw-r--r-- 1 root root 10580 Oct 12 09:29 dns-nsec-enum.nse
-rw-r--r-- 1 root root  3441 Oct 12 09:29 dns-nsid.nse
-rw-r--r-- 1 root root  4364 Oct 12 09:29 dns-random-srcport.nse
-rw-r--r-- 1 root root  4363 Oct 12 09:29 dns-random-txid.nse
-rw-r--r-- 1 root root  1456 Oct 12 09:29 dns-recursion.nse
-rw-r--r-- 1 root root  2195 Oct 12 09:29 dns-service-discovery.nse
-rw-r--r-- 1 root root  5679 Oct 12 09:29 dns-srv-enum.nse
-rw-r--r-- 1 root root  5765 Oct 12 09:29 dns-update.nse
-rw-r--r-- 1 root root  2123 Oct 12 09:29 dns-zeustracker.nse
-rw-r--r-- 1 root root 26574 Oct 12 09:29 dns-zone-transfer.nse
-rw-r--r-- 1 root root  3910 Oct 12 09:29 fcrdns.nse
```
-nmap x.x.x.x -v -p 53 --script=exampleScript1.nse,exampleScript2.nse



# Domain Discovery

Sublis3r:

- Sublist3r -d www.example.com
- Sublist3r -v -d www.example.com -p 80,443

Subfinder: 
- subfinder -d megacorpone.com

OWASP AMASS: 

- amass enum -d www.example.com
- amass intel -whois -d www.example.com
- amass intel -active 172.21.0.0-64 -p 80,443,8080,8443
- amass intel -ipv4 -whois -d www.example.com
- amass intel -ipv6 -whois -d www.example.com



# NetDiscover (ARP Scanning):
- netdiscover -i eth0
- netdiscover -r 172.21.10.0/24

# Dsniff Arpspoof

First enable Linux box to act as a router:

`echo 1 > /proc/sys/net/ipv4/ip_forward`

Then run `arpspoof`:

`arpspoof -i <interface> -t <target> -r <host>`

For example, to intercept traffic between targets, use:

`arpspoof -i eth0 -t 192.168.4.11 -r 192.168.4.16`

# Nmap:

- nmap -sn 172.21.10.0/24
- nmap -sn 172.21.10.1-253
- nmap -sn 172.21.10.*

You can also grep out the IPs and cut out fluf:
```
nmap -sn 172.x.x.x/24 | grep "172" | cut -f 5 -d ' '
```

A slower, more stealthier approach that utilizes the files containing the IP address split (as seen in the first section above) would be:
```
nmap --randomize-hosts -sn -T2 -oN nmap_discoveryScan_x.x.x.x-16.txt -iL x.x.x.x_IP_range.split.txt
```
This will export the results into a text file (`-oN`). Randomized hosts is optional, depending on the customer and the testing situation. The flag, `-oA`, can be used in place of `-oX` or `-oN`, as `-oA` will output the results to all output formats. 

The results for both command options shown above will be the list of hosts that responded to the ping, thus are up and alive.

# Nbtscan: 
- nbtscan -r 172.21.1.0/24

# Masscan
- masscan 172.21.10.0/24 --ping

# Ping Sweeps

## Linux Ping Sweep (Bash)

- for i in {1..254} ;do (ping -c 1 172.21.10.$i | grep "bytes from" &) ;done

## Windows Ping Sweep (Run on Windows System)

- for /L %i in (1,1,255) do @ping -n 1 -w 200 172.21.10.%i > nul && echo 172.21.1.%i is up.

## Powershell Ping Sweep: 
Note: This command can also run on powershell for Linux

- 1..20 | % {"172.21.10.$($_): $(Test-Connection -count 1 -comp 172.21.10.$($_) -quiet)"}
- Get-PingSweep Subnet 172.21.10
```
# Reference: https://gist.github.com/joegasper/93ff8ae44fa8712747d85aa92c2b4c78
function ResolveIp($IpAddress) {
    try {
        (Resolve-DnsName $IpAddress -QuickTimeout -ErrorAction SilentlyContinue).NameHost
    } catch {
        $null
    }
}

function Invoke-PingSweep {
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$true)]
        [string]$SubNet,
        [switch]$ResolveName
    )
    $ips = 1..254 | ForEach-Object {"$($SubNet).$_"}
    $ps = foreach ($ip in $ips) {
        (New-Object Net.NetworkInformation.Ping).SendPingAsync($ip, 250)
        #[Net.NetworkInformation.Ping]::New().SendPingAsync($ip, 250) # or if on PowerShell v5
    }
    [Threading.Tasks.Task]::WaitAll($ps)
    $ps.Result | Where-Object -FilterScript {$_.Status -eq 'Success' -and $_.Address -like "$subnet*"} 
    Select-Object Address,Status,RoundtripTime -Unique |
    ForEach-Object {
        if ($_.Status -eq 'Success') {
            if (!$ResolveName) {
                $_
            } else {
                $_ | Select-Object Address, @{Expression={ResolveIp($_.Address)};Label='Name'}, Status, RoundtripTime
            }
        }
    }
}
```

## Python Ping Sweep:

The following python script can be used to perform a ping scan. 
```
#!/usr/bin/env python3
import ipaddress
from subprocess import Popen, DEVNULL

for ping in range(1, 254):
        address = "x.x.x.%d" % ping
        response = Popen(["ping", "-c1", address], stdout=DEVNULL)
        output = response.communicate()[0]
        val1 = response.returncode
        if val1 == 0:
                print(address)
```
This script is specifically used for a /24 network. Modification required for other network types. 