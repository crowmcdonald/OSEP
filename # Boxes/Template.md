
export ip=192.168.140.165
sudo nmap -Pn -n -sC -sV -p- --open  192.168.140.165
sudo nmap -Pn -n -sU --top-ports=250 --reason 192.168.140.165
nikto -h 192.168.140.165
medusa -M ftp -n 21 -h 192.168.140.165 -u  bobbob -P /usr/share/wordlists/rockyou.txt

```
export ip=192.168.208.127
wget -r ftp://anonymous:anonymous@$ip
smbclient //$ip/ -U anonymous -p 445
smbclient -N //$ip/DocumentsShare
crackmapexec smb $ip -u guest -p "" --rid-brute
crackmapexec smb $ip -u g123123uest -p "" --rid-brute
mysql -u root $ip
enum4linux -a $ip
```

```
feroxbuster  -r -x txt,php,html,aspx,jsp,cgi,pl,py,rb,conf,ini,log,bak,old,xml,json,csv,sql,js,css,ico,svg,png,jpg,jpeg,gif,asp,do,action -u http://192.168.140.165
```

Subdomain brute force
```
for sub in $(cat /usr/share/wordlists/seclists/Discovery/DNS/subdomains-top1million-110000.txt);do dig $sub.target.domain @192.168.140.165 | grep -v ';\|SOA' | sed -r '/^\s*$/d' | grep $sub | tee -a subdomains.txt;done
```

```
sudo nmap -sC -sV -p 21 -Pn --script=*ftp* 192.168.140.165
sudo nmap -sC -sV -p 22 -Pn --script=*ssh** 192.168.140.165
etc.

```


**TCP Nmap Scans**
```

```


**UDP Nmap Scans**

```

```