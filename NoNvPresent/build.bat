@echo off
call "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat" >nul
cd /d "%~dp0"
cl /nologo /O2 /MT /W4 /LD NoNvPresent.c /link /OUT:NoNvPresent.asi shlwapi.lib kernel32.lib
del /q NoNvPresent.obj NoNvPresent.exp NoNvPresent.lib 2>nul
