



<details> <summary>File System Enumeration</summary>

# Search for sensitive files
Get-ChildItem -Path C:\ -Include *.kdbx,*.ppk -File -Recurse -ErrorAction SilentlyContinue
Get-ChildItem -Path C:\ -Filter "unattended.xml" -File -Recurse -ErrorAction SilentlyContinue
Get-ChildItem -Path C:\xampp -Include *.txt,*.ini -File -Recurse -ErrorAction SilentlyContinue 
Get-ChildItem -Path C:\Users -Include *.txt,*.ini,*.log -File -Recurse -ErrorAction SilentlyContinue
Get-ChildItem -Path C:\Users -Include *.txt,*.bak,*.ini,*.pdf,*.xls,*.xlsx,*.doc,*.docx,*.log,*.kdbx,*.db,*.xml -File -Recurse -ErrorAction SilentlyContinue


</details>