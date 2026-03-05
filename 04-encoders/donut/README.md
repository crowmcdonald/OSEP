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
# Install the donut tool (binary, not the Python wrapper)
# Option A — pip
pip3 install donut-shellcode

# Option B — compile from source
git clone https://github.com/TheWover/donut
cd donut && make
```

---

## Usage

```bash
# Basic — convert a .NET exe to shellcode
python3 donut.py -f YourTool.exe -o shellcode.bin

# With arguments passed to the assembly
python3 donut.py -f YourTool.exe -p "arg1 arg2" -o shellcode.bin

# Convert a .NET DLL + call a specific method
python3 donut.py -f YourTool.dll -c Namespace.Class -m MethodName -o shellcode.bin

# Or use the donut binary directly
donut -f YourTool.exe -o shellcode.bin
```

**Then inject the shellcode** using any loader from `03-loaders/`:
```bash
# Encode the donut output shellcode
python3 04-encoders/xor/xor_encoder.py shellcode.bin 0xfa xor --format csharp
# Paste into basic-injection.cs, sections-runner.cs, etc.
```

---

## Notes

- `donut.py` in this repo is currently a **placeholder wrapper** — install the standalone `donut` tool and use it directly if the wrapper doesn't work.
- Donut-generated shellcode loads the .NET CLR into the target process — works even in native (non-.NET) processes.
- Combine with `process-injection/` or `sections-injection/` for fileless .NET tool execution.
