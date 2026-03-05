

PsExec.exe \\targethost cmd.exe
wmic /node:"targethost" process call create "cmd.exe /c whoami"
Enter-PSSession -ComputerName targethost


