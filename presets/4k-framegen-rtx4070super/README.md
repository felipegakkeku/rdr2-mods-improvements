# Preset 4K DSR + NVIDIA DLSS Frame Generation Nativo (`dlssg`) - RTX 4070 SUPER

Preset completo de configuraÃ§Ã£o do **OptiScaler (`v10.0.0-dev`)** e **`system.xml`** para rodar **Red Dead Redemption 2 (`1.0.1491.50`, DirectX 12)** em **4K via DSR (`3840x2160` Tela Cheia Exclusiva `windowed=0` em monitor nativo `2560x1440` 165 Hz)** utilizando o **Frame Generation nativo da NVIDIA (`FGOutput = dlssg`)** na **GeForce RTX 4070 SUPER**.

---

## Arquivos IncluÃ­dos

| Arquivo | FunÃ§Ã£o |
| :--- | :--- |
| [`OptiScaler.ini`](OptiScaler.ini) | Configura `Dx12Upscaler = dlss` (DLSS 4 `v310.5.3` Preset K), `FGInput = Upscaler`, `FGOutput = dlssg` (NVIDIA DLSS-G `v310.9.16`), `HUDFix = true`, `FramerateLimit = 158.0`, Depth-Aware Sharpening (`Shader = da`, `0.25`) e `DlssNr.Enabled = false`. |
| [`system.xml`](system.xml) | Preset grÃ¡fico DX12 4K (`3840x2160`, `windowed=0`, DLSS Quality `2560x1440`, `NVIDIA Reflex On`) com ajustes cirÃºrgicos em reflexos (`Medium`), volumetria (`High`) e sombras suaves (`High`) para manter ~75 FPS nativos de base (~140 a 150 FPS com DLSS-G). |
| [`instalar-nvidia-dlssg.ps1`](instalar-nvidia-dlssg.ps1) | Script PowerShell idempotente que aplica o `OptiScaler.ini`, sincroniza o `nvngx_dlssg.dll` e isola as pastas OTA `sl_*_override_0` em `C:\ProgramData\NVIDIA\NGX\models` (evitando o crash `0xc0000005` em `sl.reflex.dll`). |

---

## DiagnÃ³stico TÃ©cnico: Crash `0xc0000005` em `sl.reflex.dll` (`offset 0x3bb0e` / `0x3b66e`)

Ao ativar `FGOutput = dlssg` no OptiScaler `v10.0.0-dev`, o jogo fechava na inicializaÃ§Ã£o dentro do mÃ³dulo `sl.reflex.dll` pelo seguinte motivo:

1. O OptiScaler inicializa o `sl.interposer.dll` passando `PreferenceFlags = 0xA5` (que inclui `eLoadDownloadedPlugins` e `eAllowOTA`).
2. Quando o NVIDIA App baixa atualizaÃ§Ãµes OTA do Streamline em `C:\ProgramData\NVIDIA\NGX\models\sl_*_override_0\versions\134672\files\190_E658700.dll`, o `sl.interposer.dll` substitui os plugins locais da pasta `OptiScaler\streamline\` pelas cÃ³pias OTA do `ProgramData` e descarrega as cÃ³pias locais.
3. Logo apÃ³s o `slInit`, a funÃ§Ã£o `StreamlineProxy::HookStreamlineReflex` do OptiScaler carrega explicitamente `OptiScaler\streamline\sl.reflex.dll` via `LoadLibraryExW`. Como o caminho difere de `C:\ProgramData\...\190_E658700.dll`, o Windows cria uma **segunda instÃ¢ncia (`HMODULE`) nÃ£o inicializada** na memÃ³ria (na qual `slOnPluginLoad` nunca foi chamado e o ponteiro global `g_ctx` Ã© `NULL`).
4. Quando `hkD3D12CreateDevice` chama `slReflexSetOptions` nessa segunda instÃ¢ncia nÃ£o inicializada, ocorre violaÃ§Ã£o de acesso imediata (`mov rdi, [r13+0x10]` com `r13 = 0`).

### SoluÃ§Ã£o Aplicada
O script [`instalar-nvidia-dlssg.ps1`](instalar-nvidia-dlssg.ps1) renomeia as pastas `C:\ProgramData\NVIDIA\NGX\models\sl_*` para `*_bak`, fazendo o `sl.interposer.dll` e o `StreamlineProxy` do OptiScaler compartilharem exatamente o mesmo `HMODULE` em `OptiScaler\streamline\` (`Streamline v2.14.16.0` + `nvngx_dlssg.dll v310.9.16`).

---

## Como Aplicar ou Reverter

```powershell
# Aplicar preset e isolamento OTA:
powershell -ExecutionPolicy Bypass -File .\instalar-nvidia-dlssg.ps1

# Reverter renomeaÃ§Ã£o das pastas OTA no ProgramData:
powershell -ExecutionPolicy Bypass -File .\instalar-nvidia-dlssg.ps1 -ReverterOTA
```

## Limite de FPS (testado 2026-10-28)
- Usar FramerateLimit = 0 no OptiScaler.ini. O limitador do Reflex do OptiScaler com DLSS-G piorou o 1% low (42 FPS com limite 100).
- Limite feito pelo jogo: V-Sync ligado + taxa de atualizacao em 120 Hz nas opcoes de video (DSR 4K tela cheia). Minima registrada 60 FPS, melhor que o limite pelo OptiScaler. O system.xml do preset NAO traz isso (vSync=0, 164.835 Hz); configurar no menu do jogo.
- Reflex deve ficar ligado (DLSS-G depende dele).
- O texto Current DLSSG state: OFF no menu do OptiScaler e cosmetico quando FGInput = Upscaler.

## Prevencao Definitiva de ERR_GFX_STATE
- Modo de Exibicao: SEMPRE manter `windowed = 2` (Janela sem bordas / Borderless) no `system.xml`. Em setups multi-monitor no Windows 11 com DX12, o modo Tela Cheia Exclusiva (`windowed = 0`) falha ao negociar o swapchain e lanca `ERR_GFX_STATE`.
- Arquivos SGA de Warmup: NUNCA criar arquivos vazios de 0 bytes nem travar como Somente-Leitura (Read-Only). O RDR2 exige permissao de escrita (`GENERIC_WRITE`) no `pipelineMarkerFile`; se estiver travado, o jogo aborta na hora.
- Cura limpa de cache: Se houver crash, apenas remover os arquivos `sga_*` da pasta `Settings` para o jogo recriar o warmup limpo.
