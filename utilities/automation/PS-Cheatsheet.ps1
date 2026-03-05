# get pid / proc name
$myId=[System.Diagnostics.Process]::GetCurrentProcess().Id
get-process | findstr $myId

# determine if 64-bit ps
[Environment]::Is64BitProcess

# determine if running in syswow64
$env:PROCESSOR_ARCHITEW6432 -eq 'AMD64'

# list semi-important dirs in user home
Get-ChildItem -Recurse -Attributes Hidden .\Desktop\,.\Documents\,.\Downloads\

# Check current language mode
$ExecutionContext.SessionState.LanguageMode