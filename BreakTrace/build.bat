@echo off
call "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat" >nul
cd /d "%~dp0"
cl /nologo /O2 /MT /W4 /LD BreakTrace.c /link /OUT:BreakTrace.asi kernel32.lib
del /q BreakTrace.obj BreakTrace.exp BreakTrace.lib 2>nul
