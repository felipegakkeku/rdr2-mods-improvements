# Laboratório de mods RDR2

Gerado em 25/10/2026 a partir de uma passada do REA na instalação (RDR2.exe 1.0.1491.50).

## Arquivos

- `natives-catalogo.csv`: 7.133 natives expostos pelo ScriptHookRDRNetAPI 2.2.0, colunas `C` (categoria) e `N` (nome). Nome começando com `_0x` = native sem nome público, só hash.
- `api-enums.tsv`: todos os enums da API (PedHash, eEventType, ePedScriptConfigFlags, eBlipSprite, BoneID...).
- `api-tipos.tsv`: classes da API com contagem de membros.

## Sonda de natives (`NativeProbe\`)

Script .NET instalado em `scripts\NativeProbe.dll`. Roda sozinho 15 s depois que o jogador fica controlável
(ou F9) as seções UIFEED, ANIMSCENE, COMPANION, GUARDZONE e PERSCHAR, nessa ordem, e grava:

- `scripts\NativeProbe.log`: cada chamada com retorno;
- `scripts\NativeProbe-resumo.txt`: veredito por seção (OK, FALHOU, TRAVOU, SEM REACAO) e, para cada aviso do
  feed, se apareceu na tela (mudança por quadrante entre o print de antes e o de depois, SIM a partir de 6);
- `scripts\NativeProbe-shots\<data-hora>\`: os prints;
- `scripts\NativeProbe.state`: a seção que estava rodando quando o jogo caiu vira TRAVOU e é pulada depois.

Configuração em `scripts\NativeProbe.ini`, lida direto do arquivo (o `ScriptSettings` do SHRDN não lia).
O jogador fica parado: o guarda é levado até ele. Seção com missão ou cutscene ativa é pulada.
Resultados das rodadas: `.ai-memory\handoff\C--Program-Files--x86--DODI-Repacks-Red-Dead-Redemption-2\TESTES-PENDENTES.md`.

Recompilar (Roslyn do BuildTools 2022, .NET Framework 4.8):

```
csc /noconfig /nostdlib /target:library /unsafe /optimize /platform:x64 /langversion:latest /out:NativeProbe.dll
    /r:<Framework64\v4.0.30319>\{mscorlib,System,System.Core,System.Drawing,System.Windows.Forms}.dll
    /r:..\..\ScriptHookRDRNetAPI.dll NativeProbe.cs
```

## Print da tela do jogo (`ddshot\`)

O RDR2 em Vulkan sem borda sai preto em captura GDI e no print do NVIDIA overlay. `ddshot.exe` usa DXGI
Desktop Duplication com uma janela quase transparente por cima (força o DWM a compor) e descarta quadros pretos:

```
ddshot.exe <saida.png|jpg> [x y] [largura]
```

Imprime o caminho, o monitor e o brilho médio (0 a 255; abaixo de 4 é falha de captura). `DDSHOT_DEBUG=1` mostra
cada tentativa no stderr. Duas capturas ao mesmo tempo podem falhar em série com INVALID_CALL.

## Nomes reais tirados da memória do jogo (`memstr\`)

Os RPF desta versão são RPF8: o índice é cifrado e só guarda hash, então nome de dicionário não sai dos arquivos.
Com o jogo aberto, as tabelas de strings dos scripts carregados têm os nomes em texto. As duas ferramentas só leem
(ReadProcessMemory), e uma varredura completa leva de 1,5 a 4 minutos para uns 12 GB:

```
memstr.exe RDR2 <saida.txt> [minimo=8] [trecho=@]          strings que contêm o trecho, com contagem
memctx.exe RDR2 <saida.txt> <texto> [janela=1024] [max=40]  strings em volta de cada ocorrência exata
```

`memstr` com `@` deu os dicionários `script@...`, e com `pbl_` deu as playlists. O `memctx` em volta de um dicionário
mostra os nomes que o script usa junto com ele (papel de entidade, playlist). Exemplo: depois de
`script@common@cnv_camp@grief@knockout@knockout` vêm as três playlists `pbl_griefing_ko_*` e `player_one`.
A lista de papéis candidatos da sonda (`NativeProbe-papeis.txt`) saiu dessas janelas.

Resultado (25/10/2026): os 19 dicionários `script@...` do `NativeProbe.ini` criam e carregam no modo história, e
`script@story@fin3@arthurs_grave` tocado com o papel `john` põe o John na cena (câmera de cinema). Papel se confirma
com `_DOES_ENTITY_WITH_ID_EXIST_IN_ANIM_SCENE` sem tocar a cena; o mapa de papéis por cena está no
`TESTES-PENDENTES.md` do handoff.

## Fronteira (categorias com mais natives sem nome)

| Categoria | Total | Sem nome |
|---|---|---|
| VOICE | 24 | 100% |
| POSSE | 31 | 97% |
| COMPANION | 25 | 92% |
| PERSCHAR | 51 | 76% |
| PERSISTENCE | 22 | 73% |
| FLOCK | 46 | 67% |
| MINIGAME | 48 | 62% |
| LAW | 124 | 56% |

## O que o NemesisMod já usa

305 natives, concentrados em PED (81), TASK (43), ENTITY (37), HUD (16). Nenhum de ANIMSCENE, PERSCHAR,
COMPANION, DATABINDING, EVENT, FLOCK, POPULATION, VOLUME, ITEMDATABASE, INVENTORY ou PROPSET.
(Contagem por nome de método no heap de strings do assembly: é inferência, não lista de chamadas.)

## Limites da passada

- Sem Ghidra/IDA instalados: o RDR2.exe não foi desmontado, só os recursos PE foram lidos.
- O parser gerenciado do REA estourou o orçamento de metadados nas duas DLLs .NET; o inventário de membros
  foi feito por reflexão só-leitura (`ReflectionOnlyLoadFrom`, sem executar código).
- Se um native funciona no modo história ou só no Online não dá para saber estaticamente: precisa de teste no jogo.
