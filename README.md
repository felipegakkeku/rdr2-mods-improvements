# RDR2 Mods, Patches & Improvements Lab (`_mod-lab`)

Repositório de mods autorais, patches de tradução, ferramentas de engenharia reversa e presets de otimização (4K + Frame Generation) desenvolvidos e adaptados para **Red Dead Redemption 2 (PC, Story Mode, build `1.0.1491.50`)**.

---

## Sumário de Projetos e Modificações

| Módulo / Projeto | Tipo | Status | Descrição Técnica e Modificações Realizadas |
| :--- | :--- | :--- | :--- |
| **[`AutoSaque/`](AutoSaque/)** (`v2.0`) | Mod C# (`.dll`) | Autoral | Saque automático de corpos humanos à distância sem animação (`Modo=rapido`, raio padrão de `35m`) ou com animação vanilla (`TASK_LOOT_ENTITY`). Implementa rastreamento multi-fonte (`GET_PED_NEARBY_PEDS` + mira livre/travada + `World.GetAllPeds`) para contornar o retorno zero do `World.GetAllPeds()` na build `1491.50`, funciona a cavalo e em combate, entrega dinheiro e munição com `ADD_REASON_LOOTED` no HUD nativo da Rockstar e ignora NPCs agonizando do `PDO Reloaded` até morrerem. |
| **[`GoreVisceral/`](GoreVisceral/)** (`v1.0`) | Mod C# (`.dll`) | Autoral | Amplificador de gore arterial (`_SET_PED_ACTIVATE_WOUND_EFFECT`), poças de sangue dinâmicas (`_ADD_BLOOD_POOLS_FOR_PED_WITH_PARAMS`), desmembramento craniano (`EXPLODE_PED_HEAD`), aumento de escala/LOD de partículas de impacto (`SET_PARTICLE_FX_BULLET_IMPACT_SCALE`) e impulso cinético Euphoria localizado no osso atingido (`APPLY_FORCE_TO_ENTITY`). Detecta acertos por delta de vida (`GET_ENTITY_HEALTH`) sem chamar `CLEAR_PED_LAST_DAMAGE_BONE`, mantendo 100% de compatibilidade com o raio-X do `enRAGE (BulletImpact)` e `PDO Reloaded`. |
| **[`NemesisPtBR/`](NemesisPtBR/)** (`v5`) | Patch IL + Mod C# | Patch / Tradução | Tradução completa para português do Brasil (`16.512` strings traduzidas em `mapa-ptbr.tsv` e `traducao/*.tsv`) e ponte de notificações nativas (`NemesisAvisos.cs` via `_UI_FEED_POST_SAMPLE_TOAST` e `_UI_FEED_POST_HELP_TEXT`) para o mod **enRAGE v0.9.5.x**. Inclui o injetor IL `CecilStr.cs` e o pipeline `montar.ps1` (preserva chaves internas de telégrafo `what:`/`read:`/`sent:`, pronomes dinâmicos e placeholders). **Nota de licença:** O binário original `NemesisMod.dll` não é distribuído neste repositório; o script aplica o patch sobre a DLL oficial baixada pelo usuário no Nexus Mods. |
| **[`pacotes/OutlawPact/`](pacotes/OutlawPact/)** (`Beta 0.4.4`) | Mod C++ (`.asi`) | Autoral / Modificado | Sistema de recrutamento de gangue persistente (até 12 membros), comandos táticos individuais, assaltos, acampamento adaptável a terrenos inclinados (até 32 graus) e suporte a 13 idiomas. Na `Beta 0.4.4`, o painel padrão usa renderização nativa leve (`UseImagePanel=0`, limitado a 32 retângulos e 30 rótulos por quadro com cache a 4 Hz) sem criar janelas Win32 (`UpdateLayeredWindow`) e com `CustomFlag=0` para evitar conflitos de frame pacing com DX12 Frame Generation. |
| **[`NativeProbe/`](NativeProbe/), [`ddshot/`](ddshot/), [`memstr/`](memstr/)** | Ferramentas C# | Autoral | Kit de engenharia reversa e validação em tempo de execução para RDR2 `1.0.1491.50`:<br>1. `NativeProbe`: sonda automatizada para `UIFEED`, `ANIMSCENE`, `COMPANION`, `GUARDZONE` e `PERSCHAR` com recuperação pós-crash.<br>2. `ddshot`: captura de tela via DXGI Desktop Duplication com janela fantasma DWM para evitar capturas pretas em Vulkan/DX12 borderless.<br>3. `memstr` / `memctx`: leitores `ReadProcessMemory` que extraem nomes reais de dicionários `script@...`, playlists `pbl_...` e papéis de `ANIMSCENE` direto da RAM do processo (contornando a cifragem de nomes em arquivos RPF8). |
| **[`presets/4k-framegen-rtx4070super/`](presets/4k-framegen-rtx4070super/)** | Presets `.ini`/`.xml` + Script `.ps1` | Configuração / Automação | Configuração e script automatizado (`instalar-nvidia-dlssg.ps1`) para rodar RDR2 em **4K DSR (`3840x2160` Fullscreen)** com **NVIDIA DLSS Frame Generation nativo (`FGOutput = dlssg`, `nvngx_dlssg.dll v310.9.16` + `Streamline v2.14.16.0`)** e **DLSS 4 Super Resolution (`v310.5.3` Preset K)** em GPUs RTX série 40 (testado em Ryzen 7 5700X3D + RTX 4070 SUPER). Inclui correção automática do conflito de dupla instância OTA (`C:\ProgramData\NVIDIA\NGX\models\sl_*_override_0`) que causava crash `0xc0000005` em `sl.reflex.dll`, além do `system.xml` calibrado (~75 FPS base -> ~140-150 FPS com DLSS-G). |

---

## Fontes, Créditos e Referências Técnicas

Este laboratório utiliza interfaces, bancos de dados de funções nativas e ferramentas das seguintes fontes da comunidade (cada qual regida por sua respectiva licença):

1. **ScriptHookRDR2 & ScriptHookRDR2 V2**
   - **Autores:** Alexander Blade ([dev-c.com/rdr2/scripthookrdr2](http://www.dev-c.com/rdr2/scripthookrdr2/)) e Rage Modding Collective / Keplyx ([ScriptHookRDR2 V2 no Nexus Mods](https://www.nexusmods.com/reddeadredemption2/mods/5687)).
   - **Uso:** Carregamento de plugins `.asi` e expansão dos pools de script (`CGameScriptHandler`, `fwScriptGuid`, `CScriptEntityExtension`, `atDScriptObjectNode`). Binários não redistribuídos.

2. **ScriptHookRDR2DotNet V2 (`v2.2.0`)**
   - **Autor:** Halen84 e colaboradores ([github.com/Halen84/ScriptHookRDR2DotNet-V2](https://github.com/Halen84/ScriptHookRDR2DotNet-V2)).
   - **Uso:** Ponte `.NET` e biblioteca de referência (`ScriptHookRDRNetAPI.dll`) contra a qual os módulos `AutoSaque`, `GoreVisceral`, `NemesisAvisos` e `NativeProbe` são compilados, além da extração por reflexão de [`natives-catalogo.csv`](natives-catalogo.csv), [`api-enums.tsv`](api-enums.tsv) e [`api-tipos.tsv`](api-tipos.tsv).

3. **RDR3 Native Database & Flags/Enums**
   - **Autores:** alloc8or ([github.com/alloc8or/rdr3-nativedb-data](https://github.com/alloc8or/rdr3-nativedb-data)) e Halen84 ([github.com/Halen84/RDR3-Native-Flags-And-Enums](https://github.com/Halen84/RDR3-Native-Flags-And-Enums)).
   - **Uso:** Assinaturas de funções nativas da engine RAGE (`UIFEED`, `ANIMSCENE`, `PED`, `GRAPHICS`, `ENTITY`, `MONEY`, `WEAPON`) e identificadores de ossos (`BoneID`) usados no `GoreVisceral` e `AutoSaque`.

4. **RDR3 Discoveries**
   - **Autor:** femga ([github.com/femga/rdr3_discoveries](https://github.com/femga/rdr3_discoveries)).
   - **Uso:** Hashes de modelos, dicionários de texturas (`generic_textures`), animações e parâmetros de mundo da engine RAGE.

5. **enRAGE (A Nemesis System for Red Dead Redemption 2)**
   - **Autor:** Autor original do mod enRAGE no Nexus Mods.
   - **Uso:** Alvo do patch de tradução PT-BR e do redirecionamento de avisos em [`NemesisPtBR/`](NemesisPtBR/). Conforme a licença do autor original, este repositório disponibiliza exclusivamente os arquivos de tradução (`.tsv`), o módulo auxiliar `NemesisAvisos.cs` e o aplicador de patch (`CecilStr.cs` + `montar.ps1`), sem redistribuir o arquivo `NemesisMod.dll` original.

6. **Ped Damage Overhaul (PDO Reloaded)**
   - **Autores:** fitfondue / H4ever.
   - **Uso:** Referência de compatibilidade para estados de agonia/sangramento no `AutoSaque v2.0` e preservação de leitura de ossos (`GET_PED_LAST_DAMAGE_BONE`) no `GoreVisceral v1.0`.

7. **OptiScaler**
   - **Autores:** Equipe OptiScaler ([github.com/optiscaler/OptiScaler](https://github.com/optiscaler/OptiScaler)).
   - **Uso:** Base para o preset de upscaling (DLSS 4 Preset K Transformer), nitidez por profundidade (`DA`) e geração de quadros (`XeFG` / `FSR-FG`) em [`presets/4k-framegen-rtx4070super/`](presets/4k-framegen-rtx4070super/).

8. **Mono.Cecil & Fontes de Interface**
   - **Mono.Cecil:** Jb Evain ([github.com/jbevain/cecil](https://github.com/jbevain/cecil), licença MIT), utilizado pelo `CecilStr.cs` para inspeção e reescrita de instruções `ldstr` e chamadas de método no IL.
   - **Fonte Rye (OutlawPact):** Nicole Fally / Google Fonts (SIL Open Font License 1.1, detalhada em [`pacotes/OutlawPact/OutlawPact/assets/ui/OFL.txt`](pacotes/OutlawPact/OutlawPact/assets/ui/OFL.txt)).

---

## Como Compilar os Módulos C# (`.NET Framework 4.8`)

Com o compilador Roslyn (`csc.exe` do Visual Studio 2022 Build Tools) e o `ScriptHookRDRNetAPI.dll` na raiz do jogo:

```powershell
$csc = "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe"
$fw  = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"

# 1. Compilar AutoSaque v2.0
& $csc /noconfig /nostdlib /target:library /unsafe /optimize /platform:x64 `
    /out:"AutoSaque\AutoSaque.dll" `
    /r:"$fw\mscorlib.dll" /r:"$fw\System.dll" /r:"$fw\System.Core.dll" /r:"$fw\System.Drawing.dll" /r:"$fw\System.Windows.Forms.dll" `
    /r:"..\ScriptHookRDRNetAPI.dll" "AutoSaque\AutoSaque.cs"

# 2. Compilar GoreVisceral v1.0
& $csc /noconfig /nostdlib /target:library /unsafe /optimize /platform:x64 `
    /out:"GoreVisceral\GoreVisceral.dll" `
    /r:"$fw\mscorlib.dll" /r:"$fw\System.dll" /r:"$fw\System.Core.dll" /r:"$fw\System.Drawing.dll" /r:"$fw\System.Windows.Forms.dll" `
    /r:"..\ScriptHookRDRNetAPI.dll" "GoreVisceral\GoreVisceral.cs"

# 3. Compilar NemesisAvisos
& $csc /noconfig /nostdlib /target:library /unsafe /optimize /platform:x64 `
    /out:"NemesisPtBR\NemesisAvisos.dll" `
    /r:"$fw\mscorlib.dll" /r:"$fw\System.dll" /r:"$fw\System.Core.dll" `
    /r:"..\ScriptHookRDRNetAPI.dll" "NemesisPtBR\NemesisAvisos.cs"
```

Para instalar no jogo, copie os arquivos `.dll` e `.ini` correspondentes para a pasta `scripts\` na raiz do `RDR2.exe`.
