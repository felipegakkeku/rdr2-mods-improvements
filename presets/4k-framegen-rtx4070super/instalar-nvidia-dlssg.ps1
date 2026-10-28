<#
.SYNOPSIS
    Configura e aplica o NVIDIA DLSS Frame Generation nativo (FGOutput = dlssg) + DLSS 4 no RDR2 via OptiScaler.
.DESCRIPTION
    1. Copia o OptiScaler.ini calibrado (FGOutput = dlssg, DLSS 4 Preset K, Depth-Aware Sharpening 0.25, FPS cap 158) para a raiz do jogo.
    2. Desativa permanentemente a leitura do manifesto OTA (nvngx_config.txt -> nvngx_no_ota.txt) na cópia local de OptiScaler\streamline\sl.interposer.dll, impedindo que o nvngx_update.exe da NVIDIA recrie plugins OTA duplicados em C:\ProgramData\NVIDIA\NGX\models e cause crash 0xc0000005 em sl.reflex.dll.
    3. Garante que nvngx_dlssg.dll esteja presente em OptiScaler\streamline\ e OptiScaler\.
#>
param(
    [string]$GameDir = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path
)

$ErrorActionPreference = "Stop"
$ngxModels = "C:\ProgramData\NVIDIA\NGX\models"

# 1. Garantir nvngx_dlssg.dll em OptiScaler\streamline
$optiDir = Join-Path $GameDir "OptiScaler"
$slDir   = Join-Path $optiDir "streamline"
if (-not (Test-Path $slDir)) {
    New-Item -ItemType Directory -Path $slDir -Force | Out-Null
}

$dlssgCandidates = @(
    "$ngxModels\dlss_override\versions\20317696\files\160_E658700\nvngx_dlssg.dll",
    "$optiDir\nvngx_dlssg.dll"
)
foreach ($src in $dlssgCandidates) {
    if (Test-Path $src) {
        Copy-Item $src -Destination (Join-Path $slDir "nvngx_dlssg.dll") -Force
        Copy-Item $src -Destination (Join-Path $optiDir "nvngx_dlssg.dll") -Force
        Write-Host "[OK] nvngx_dlssg.dll sincronizado a partir de: $src"
        break
    }
}

# 2. Patch permanente de isolamento OTA no sl.interposer.dll local (nvngx_config.txt -> nvngx_no_ota.txt)
$slInterposer = Join-Path $slDir "sl.interposer.dll"
if (Test-Path $slInterposer) {
    python -c "
p = r'$slInterposer'
data = open(p, 'rb').read()
old_u16 = 'nvngx_config.txt'.encode('utf-16le')
new_u16 = 'nvngx_no_ota.txt'.encode('utf-16le')
old_asc = b'nvngx_config.txt'
new_asc = b'nvngx_no_ota.txt'
if old_u16 in data or old_asc in data:
    data = data.replace(old_u16, new_u16).replace(old_asc, new_asc)
    open(p, 'wb').write(data)
    print('[OK] Patch anti-OTA aplicado em sl.interposer.dll')
else:
    print('[OK] sl.interposer.dll ja possui o patch anti-OTA')
"
}

# 3. Aplicar OptiScaler.ini
$presetIni = Join-Path $PSScriptRoot "OptiScaler.ini"
if (Test-Path $presetIni) {
    Copy-Item $presetIni -Destination (Join-Path $GameDir "OptiScaler.ini") -Force
    Write-Host "[OK] OptiScaler.ini aplicado em: $GameDir\OptiScaler.ini"
}

Write-Host "[CONCLUIDO] NVIDIA DLSS-G (FGOutput = dlssg) configurado de forma permanente."
