$ErrorActionPreference = 'Continue'
$log = "C:\Program Files (x86)\DODI-Repacks\Red Dead Redemption 2\_mod-lab\fix_social_club.log"

"=== INICIANDO FIX SOCIAL CLUB ERROR 16 ===" | Out-File $log -Encoding utf8

# 1. 1911.dll e pasta do jogo
try {
    takeown /f "C:\Program Files (x86)\DODI-Repacks\Red Dead Redemption 2\1911.dll" | Out-File $log -Append
    icacls "C:\Program Files (x86)\DODI-Repacks\Red Dead Redemption 2\1911.dll" /grant "*S-1-5-32-545:F" /C | Out-File $log -Append
    icacls "C:\Program Files (x86)\DODI-Repacks\Red Dead Redemption 2" /grant "*S-1-5-32-545:(OI)(CI)F" /C /Q | Out-File $log -Append
} catch {
    $_ | Out-File $log -Append
}

# 2. Bloqueio no Firewall de Saida
try {
    $rdrPath = "C:\Program Files (x86)\DODI-Repacks\Red Dead Redemption 2\RDR2.exe"
    $launchPath = "C:\Program Files (x86)\DODI-Repacks\Red Dead Redemption 2\Launcher.exe"
    Get-NetFirewallRule -DisplayName "RDR2 Block Outbound*" -ErrorAction SilentlyContinue | Remove-NetFirewallRule
    New-NetFirewallRule -DisplayName "RDR2 Block Outbound" -Direction Outbound -Program $rdrPath -Action Block -Profile Any | Out-File $log -Append
    New-NetFirewallRule -DisplayName "RDR2 Launcher Block Outbound" -Direction Outbound -Program $launchPath -Action Block -Profile Any | Out-File $log -Append
} catch {
    $_ | Out-File $log -Append
}

# 3. .1911 no AppData
try {
    $app1911 = "$env:APPDATA\.1911"
    if (Test-Path $app1911) {
        Get-ChildItem $app1911 -Recurse -Force -ErrorAction SilentlyContinue | ForEach-Object {
            if ($_.Attributes -band [IO.FileAttributes]::ReadOnly) {
                $_.Attributes = $_.Attributes -bxor [IO.FileAttributes]::ReadOnly
            }
        }
        icacls $app1911 /grant "*S-1-5-32-545:(OI)(CI)F" /T /C /Q | Out-File $log -Append
    }
} catch {
    $_ | Out-File $log -Append
}

# 4. AppCompatFlags RUNASADMIN
try {
    $hkcuLayers = 'HKCU:\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers'
    if (-not (Test-Path $hkcuLayers)) { New-Item -Path $hkcuLayers -Force | Out-Null }
    Set-ItemProperty -Path $hkcuLayers -Name $launchPath -Value '~ RUNASADMIN'
    Set-ItemProperty -Path $hkcuLayers -Name $rdrPath -Value '~ RUNASADMIN'

    $hklmLayers = 'HKLM:\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers'
    if (-not (Test-Path $hklmLayers)) { New-Item -Path $hklmLayers -Force | Out-Null }
    Set-ItemProperty -Path $hklmLayers -Name $launchPath -Value '~ RUNASADMIN'
    Set-ItemProperty -Path $hklmLayers -Name $rdrPath -Value '~ RUNASADMIN'
    "AppCompatFlags atualizados." | Out-File $log -Append
} catch {
    $_ | Out-File $log -Append
}

"=== FIX CONCLUIDO COM SUCESSO ===" | Out-File $log -Append
