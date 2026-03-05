1) For every new user you find, run nxc smb 0/24 -u newuser -p meow. If you get pwned, use secretsdump and lsassy to avoid the trouble of remoting into the system
2) for every new user you get, use mssqlpwner domain.com/newuser:pass@DB enumerate. If you also see a system is hosting a website, or any kind of frontend then run its machine account too. Any system with web or one you see has a service in need of a backend
3) run tree /F /A in C:\Users and inspect any users with a .ssh directory. If you have that users plaintext password, revert back to your Kali and use nxc ssh 0/24 -u user@domain.com -p. Inspect for other files and references present within the users directories. Inspect the console host powershell files as well. 
4) On Linux systems, usr tcpbump, and inspect /tmp directory post root. To do this better inject ur own ssh key after rooting the system to then be able to get a full root ssh session. 
5) for priv esc make sure you’re running pspy64 and sudo -l and then obviously you’re linpeas
6) Make sure you loot systems after rooting on Linux for other users ssh keys, secrets, and ansible vaults. 
7) For every new user you get access to; run bloodhound-ce-python. 
8) For every new domain or privilidged user in current domain run sharphound -s —recursedomains 
9) run a tool called bloodhound quick wins. It’s a Python script that’s pretty damn useful. 
10) make sure for every system you compromise and user, you’re constantly referring back to bloodhound and marking them as owned. 
11) inspect your owned users outbound ACLs, execution privlidges, and Local admin status. Ensure you’re also checking their group membership and looking at their groups outbound ACLs.
12) if you’re bothered, for every users NTLM hash u get; use NTLM.pw to see if u can get their plaintext instead(this lets u spray it against ssh systems) 
13) Start an nmap scan in the background for when you’ve exhausted all your AD quick wins and have creds to play with as there may be additional routes for you to move on to there
14) for every new user I obtained, I use bloodyAD to show me what the given user can write to; bloodyAD —dc-host -u -p get writeable. If you did the steps above for bloodhound/bloodhound quick wins then you should see this already
15) if you have a user with laps reader perms/membership or you just want to spray and pray; nxc smb 0/24 -u -p —laps will try to read laps password on all systems in the subnet with ur provided creds
16) If you’re stuck, ensure you’ve properly selected all your owned users as owned/systems. ensure you’ve ran bloodhound-ce-python on every new user, sharphound on every new major system, and ran bloodhound quick wins. If after this you don’t have a route; re visit each system and look at C:\Users with the previous command as well as inspect non default folders in C:\. if those too get you nowhere; put more effort into the nmap scan you would’ve started to see what u can attack.
17) for every system you compromise, extract the local administrator hash and spray it via nxc smb 0/24  -u Administrator -H hash —local-auth