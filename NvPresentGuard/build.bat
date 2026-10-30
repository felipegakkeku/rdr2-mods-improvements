@echo off
call "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat" >nul
cd /d "%~dp0"
cl /nologo /O2 /MT /W4 /LD NvPresentGuard.c /link /OUT:NvPresentGuard.asi kernel32.lib
del /q NvPresentGuard.obj NvPresentGuard.exp NvPresentGuard.lib 2>nul
