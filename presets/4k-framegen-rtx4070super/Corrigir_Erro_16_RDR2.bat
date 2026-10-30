@echo off
title Corrigir Erro 16 Social Club - Red Dead Redemption 2
color 0A

:: Verificar privilegios de Administrador
net session >nul 2>&1
if %errorLevel% NEQ 0 (
    echo.
    echo =======================================================================
    echo  ATENCAO: Este script precisa ser executado como ADMINISTRADOR!
    echo.
    echo  Clique com o BOTAO DIREITO no arquivo 'Corrigir_Erro_16_RDR2.bat'
    echo  e selecione 'Executar como administrador'.
    echo =======================================================================
    echo.
    pause
    exit /b
)

echo ========================================================
echo  CORRIGINDO ERRO 16 (SOCIAL CLUB) - RDR2 DODI / 1911
echo ========================================================
echo.

set "GAME_DIR=C:\Program Files (x86)\DODI-Repacks\Red Dead Redemption 2"
set "RDR_EXE=%GAME_DIR%\RDR2.exe"
set "LAUNCH_EXE=%GAME_DIR%\Launcher.exe"

echo [1/5] Removendo pastas antigas conflitantes da Rockstar / Social Club...
takeown /f "C:\Program Files\Rockstar Games" /r /d y >nul 2>&1
icacls "C:\Program Files\Rockstar Games" /grant "*S-1-5-32-545:(OI)(CI)F" /t /c /q >nul 2>&1
rd /s /q "C:\Program Files\Rockstar Games" >nul 2>&1

takeown /f "C:\ProgramData\Rockstar Games" /r /d y >nul 2>&1
icacls "C:\ProgramData\Rockstar Games" /grant "*S-1-5-32-545:(OI)(CI)F" /t /c /q >nul 2>&1
rd /s /q "C:\ProgramData\Rockstar Games" >nul 2>&1
echo  -> Pastas antigas de Social Club e Launcher removidas!

echo.
echo [2/5] Configurando Regras de Firewall (Bloqueio de Saida + Permissao de Entrada)...
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
    "Get-NetFirewallRule -DisplayName '*RDR2*' -ErrorAction SilentlyContinue | Remove-NetFirewallRule; " ^
    "New-NetFirewallRule -DisplayName 'RDR2 Game Block Outbound' -Direction Outbound -Program '%RDR_EXE%' -Action Block -Profile Any | Out-Null; " ^
    "New-NetFirewallRule -DisplayName 'RDR2 Launcher Block Outbound' -Direction Outbound -Program '%LAUNCH_EXE%' -Action Block -Profile Any | Out-Null; " ^
    "New-NetFirewallRule -DisplayName 'RDR2 Game Allow Inbound' -Direction Inbound -Program '%RDR_EXE%' -Action Allow -Profile Any | Out-Null; " ^
    "New-NetFirewallRule -DisplayName 'RDR2 Launcher Allow Inbound' -Direction Inbound -Program '%LAUNCH_EXE%' -Action Allow -Profile Any | Out-Null"
echo  -> Regras de Firewall atualizadas: Saida Bloqueada e Entrada Permitida (IPC local liberado)!

echo.
echo [3/5] Configurando modo RUNASADMIN no Registro...
reg add "HKCU\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers" /v "%LAUNCH_EXE%" /t REG_SZ /d "~ RUNASADMIN" /f >nul
reg add "HKCU\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers" /v "%RDR_EXE%" /t REG_SZ /d "~ RUNASADMIN" /f >nul
reg add "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers" /v "%LAUNCH_EXE%" /t REG_SZ /d "~ RUNASADMIN" /f >nul
reg add "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers" /v "%RDR_EXE%" /t REG_SZ /d "~ RUNASADMIN" /f >nul
echo  -> Elevacao de Administrador registrada para Launcher e RDR2!

echo.
echo [4/5] Liberando permissoes totais no perfil .1911 e na dll de crack...
if exist "%APPDATA%\.1911" (
    attrib -r -s "%APPDATA%\.1911\*.*" /s /d >nul 2>&1
    icacls "%APPDATA%\.1911" /grant "*S-1-5-32-545:(OI)(CI)F" /T /C /Q >nul 2>&1
)
icacls "%GAME_DIR%\1911.dll" /grant "*S-1-5-32-545:F" /C >nul 2>&1
echo  -> Permissoes de escrita liberadas!

echo.
echo [5/5] Limpando caches corrompidos sga_*...
del /f /q "%USERPROFILE%\OneDrive\Documentos\Rockstar Games\Red Dead Redemption 2\Settings\sga_*" >nul 2>&1
del /f /q "%USERPROFILE%\Documents\Rockstar Games\Red Dead Redemption 2\Settings\sga_*" >nul 2>&1
echo  -> Caches limpos!

echo.
echo ========================================================
echo  TUDO PRONTO! Pode fechar esta janela e iniciar o jogo
echo  pelo atalho 'Red Dead Redemption 2' na Area de Trabalho.
echo ========================================================
echo.
pause
