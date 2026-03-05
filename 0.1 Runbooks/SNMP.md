# SNMP Enumeration & Exploitation — Quick Reference

**References:**

- https://github.com/camercu/oscp-prep/blob/main/CHEATSHEET.md
- https://medium.com/rangeforce/snmp-arbitrary-command-execution-19a6088c888e
- https://github.com/mxrch/snmp-shell
## Setup

```bash
export VICTIM_IP=192.168.124.156
```
## Discovery & Community String Brute Force

```bash
# Nmap SNMP scripts (skip brute)
nmap --script "snmp* and not snmp-brute" $VICTIM_IP

# Full UDP scan (slow but thorough)
sudo nmap $VICTIM_IP -A -T4 -p- -sU -v -oN nmap-udpscan.txt

# Quick brute — onesixtyone
onesixtyone -c /usr/share/seclists/Discovery/SNMP/common-snmp-community-strings-onesixtyone.txt $VICTIM_IP -w 100

# Extended brute — hydra
hydra -P /usr/share/seclists/Discovery/SNMP/snmp.txt -v $VICTIM_IP snmp

# Quick check with default 'public' string
onesixtyone $VICTIM_IP public
```
## Enumeration

### Comprehensive (Start Here)

```bash
# snmp-check — friendlier output than snmpwalk, shows users/processes/software/etc.
snmp-check $VICTIM_IP

# Dump EVERYTHING from SNMP
snmpwalk -v2c -c public $VICTIM_IP
```
### System Info

```bash
# System description (like uname -a on Linux)
snmpwalk -v2c -c public $VICTIM_IP SNMPv2-MIB::sysDescr
snmpget -v2c -c public $VICTIM_IP SNMPv2-MIB::sysDescr.0
```
### Targeted Queries by OID

```bash
snmpwalk -c public -v2c $VICTIM_IP 1.3.6.1.4.1.77.1.2.25    # users
snmpwalk -c public -v2c $VICTIM_IP 1.3.6.1.2.1.25.4.2.1.2    # running processes
snmpwalk -c public -v2c $VICTIM_IP 1.3.6.1.2.1.6.13.1.3      # open TCP ports
snmpwalk -c public -v2c $VICTIM_IP 1.3.6.1.2.1.25.6.3.1.2    # installed software
snmpwalk -c public -v2c $VICTIM_IP HOST-RESOURCES-MIB::hrSWInstalledName  # software (named)
```
### Windows MIB OID Cheat Sheet

| OID                      | Description      |
| ------------------------ | ---------------- |
| `1.3.6.1.2.1.25.1.6.0`   | System Processes |
| `1.3.6.1.2.1.25.4.2.1.2` | Running Programs |
| `1.3.6.1.2.1.25.4.2.1.4` | Process Paths    |
| `1.3.6.1.2.1.25.2.3.1.4` | Storage Units    |
| `1.3.6.1.2.1.25.6.3.1.2` | Software Names   |
| `1.3.6.1.4.1.77.1.2.25`  | User Accounts    |
| `1.3.6.1.2.1.6.13.1.3`   | TCP Local Ports  |
## Extended Queries (NET-SNMP-EXTEND)

> **Always check this.** If SNMP has NET-SNMP-EXTEND configured, you can see output from custom scripts — and potentially get RCE.

```bash
snmpwalk -v2c -c public $VICTIM_IP NET-SNMP-EXTEND-MIB::nsExtendOutputFull
```
## Exploitation

### Password Reset via SNMP

If you have a writable community string (e.g., `private`), you can reset user passwords through SNMP SET operations.

### Reverse Shell via SNMP

If NET-SNMP-EXTEND is configured with a writable community string, you can inject commands for RCE.

**Tool:** https://github.com/mxrch/snmp-shell

> **Key takeaway:** If you find a writable community string + NET-SNMP-EXTEND permissions, you can get a full reverse shell through SNMP.