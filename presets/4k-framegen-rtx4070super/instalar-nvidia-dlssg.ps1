#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Configura e aplica o NVIDIA DLSS Frame Generation nativo (FGOutput = dlssg) + DLSS 4 no RDR2 via OptiScaler.
.DESCRIPTION
    1. Copia o OptiScaler.ini calibrado (FGOutput = dlssg, DLSS 4 Preset K, Depth-Aware Sharpening 0.25, FPS cap 158) para a raiz do jogo.
    2. Isola os diretórios OTA 'sl_*_override_0' em 'C:\ProgramData\NVIDIA\NGX\models' (renomeando para '*_bak') para evitar o crash 0xc0000005 em sl.reflex.dll causado por dupla instância HMODULE entre o sl.interposer.dll e o StreamlineProxy do OptiScaler.
    3. Garante que nvngx_dlssg.dll esteja presente em OptiScaler\streamline\ e OptiScaler\.
#>
param(
    [string]$GameDir = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path,
    [switch]$ReverterOTA
)

$ErrorActionPreference = "Stop"
$ngxModels = "C:\ProgramData\NVIDIA\NGX\models"

if ($ReverterOTA) {
    Get-ChildItem $ngxModels -Directory -Filter "sl_*_bak" -ErrorAction SilentlyContinue | ForEach-Object {
        $origName = $_.Name -replace '_bak$', ''
        Rename-Item $_.FullName $origName -Force
        Write-Host "[OK] Restaurado: $origName"
    }
    exit 0
}

# 1. Isolar cache OTA duplicado do Streamline no ProgramData
if (Test-Path $ngxModels) {
    Get-ChildItem $ngxModels -Directory -Filter "sl_*" -ErrorAction SilentlyContinue | ForEach-Object {
        if ($_.Name -notmatch '_bak$') {
            $newName = "$($_.Name)_bak"
            Rename-Item $_.FullName $newName -Force
            Write-Host "[OK] Isolado plugin OTA duplicado: $($_.Name) -> $newName"
        }
    }
}

# 2. Garantir nvngx_dlssg.dll em OptiScaler\streamline
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

# 3. Aplicar OptiScaler.ini
$presetIni = Join-Path $PSScriptRoot "OptiScaler.ini"
if (Test-Path $presetIni) {
    Copy-Item $presetIni -Destination (Join-Path $GameDir "OptiScaler.ini") -Force
    Write-Host "[OK] OptiScaler.ini aplicado em: $GameDir\OptiScaler.ini"
}

Write-Host "[CONCLUIDO] NVIDIA DLSS-G (FGOutput = dlssg) configurado e pronto."
