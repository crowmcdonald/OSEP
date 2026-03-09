# donut

One file: `donut.py`. **Standalone prep tool — runs on Kali.**

---

## donut.py

A Python wrapper around the [Donut](https://github.com/TheWover/donut) shellcode generator. Donut converts `.NET` assemblies (`.exe`, `.dll`), VBScript, JScript, and native PE files into position-independent shellcode (PIC).

**Why use Donut?**
You have a C# tool (e.g. a BloodHound collector, Rubeus, a custom loader) and you want to run it as shellcode injected into another process — without dropping a `.exe` to disk. Donut lets you take any `.NET` binary and turn it into raw bytes you can inject with any standard injection technique.

```
YourTool.exe  →  donut  →  raw shellcode  →  inject into any process
```

---

## Setup

```bash
# go-donut — recommended on Kali ARM (pip donut-shellcode is unreliable on ARM)
sudo apt install golang-go
go install github.com/Binject/go-donut@latest
echo 'export PATH=$PATH:~/go/bin' >> ~/.zshrc && source ~/.zshrc

# Verify
go-donut --help
```

---

## Usage

```bash
# Basic — convert a .NET exe to shellcode
go-donut --in YourTool.exe --out shellcode.bin

# With arguments passed to the assembly
go-donut --in YourTool.exe --params "arg1 arg2" --out shellcode.bin

# Convert a .NET DLL + call a specific method
go-donut --in YourTool.dll --class Namespace.Class --method MethodName --out shellcode.bin

# Force x64 architecture (default is usually x64, but explicit is safer)
go-donut --arch x64 --in YourTool.exe --out shellcode.bin
```

**Then inject the shellcode** using any loader from `03-loaders/`:
```bash
# Encode the donut output shellcode
python3 04-encoders/xor/xor_encoder.py shellcode.bin 0xfa xor --format csharp
# Paste into basic-injection.cs, sections-runner.cs, etc.
```

---

## Notes

- Use `go-donut` on Kali ARM — the pip `donut-shellcode` package has ARM build issues.
- Donut-generated shellcode loads the .NET CLR into the target process — works even in native (non-.NET) processes.
- Combine with `process-injection/` or `sections-injection/` for fileless .NET tool execution.
