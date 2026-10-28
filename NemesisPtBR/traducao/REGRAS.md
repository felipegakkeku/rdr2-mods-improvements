# Tradução dos textos do NemesisMod.dll (enRAGE) para pt-BR

O mod é um "sistema Nemesis" para Red Dead Redemption 2: inimigos que lembram de você, gangues, guerras,
interrogatórios, telegramas, histórias de vida. Os textos abaixo foram tirados do DLL e vão ser regravados
nele, instrução por instrução. Os textos de conteúdo (JSON) já estão em pt-BR; use o `glossario.tsv` desta
pasta para nomes de facção, postos e afins, para bater com o que o jogador já vê.

## Entrada e saída

- Entrada: `loteN.tsv` com colunas `id, usos, contexto, prefixo_fixo, texto`.
  - `contexto` = até 2 pontos do código onde o texto aparece, no formato `Tipo::Metodo<consumidor>`.
    Ajuda a saber se é rótulo de menu, fala de NPC, aviso, fragmento de frase concatenada etc.
  - `texto` vem com escapes: `\n`, `\t`, `\\`. Mantenha os escapes do mesmo jeito na tradução.
- Saída: `saidaN.tsv`, UTF-8 sem BOM, **uma linha para cada id do lote, na mesma ordem**, no formato
  `id<TAB>tradução`. Sem cabeçalho. Para manter o original, escreva `=` no lugar da tradução.
  Nunca use TAB nem quebra de linha real dentro da tradução (use `\n` se o original tiver `\n`).

## O que NÃO traduzir (escreva `=`)

- Nomes próprios de gente (Hampton Lassiter, Colm, Angelo), apelidos que são nome ("Kilkenny Cat"),
  lugares (Saint Denis, Valentine, Rhodes, Strawberry, Blackwater, Annesburg, Van Horn, Armadillo,
  Tumbleweed, Lemoyne, New Hanover, Ambarino, West Elizabeth, New Austin, Fort Brennand, County Cork...)
  e modelos de arma (Cattleman, LeMat, Springfield, Schofield, Volcanic...). Se o texto mistura nome e
  frase, traduza a frase e mantenha o nome.
- Identificadores e chaves de código: palavras sem espaço que parecem chave (`warband`, `lemoyneraiders`,
  `refuse_scared`, `MaxDays`, `FistCap`, nomes de setting em PascalCase), nomes de modelo/anim, hashes.
  Palavra minúscula sozinha com contexto `Dictionary.set_Item`, `.cctor<>` de tabela de chaves ou
  `ValueTuple` como primeira parte de um par costuma ser chave: na dúvida entre chave e texto, `=`.
- Texto de depuração/log: coisas com `ped={0}`, `dmg=`, `->`, nomes de função, "EPILOGUE eight years
  later (" e afins, linhas de diagnóstico de desenvolvedor.
- Linhas com `prefixo_fixo` diferente de `the `, `The `, `THE `, `You `, `you `, `near `, `in `, `at `,
  `on `: o código classifica o texto pelo começo dele (ex.: `held`, `trade:`, `INTEL`, `Left`, `saw`).
  Para essas, escreva `=`. Com os prefixos listados aqui (artigos e preposições), traduza normalmente.

## Como traduzir

- Português do Brasil, tom de faroeste. Falas de NPC (provocações, interrogatório, saudações, telegramas)
  em registro falado e rústico quando o original é rústico ("I don't know nothing" -> "Num sei de nada,
  não"). Rótulos de menu e avisos em português claro e curto.
- Nunca use travessão (" — "). Onde o original tem " - " (hífen com espaços), mantenha " - ".
- **Placeholders intactos**: `{0}`, `{1:0.#}`, `{0:0}`, `{t}`, `{p}`, `{a}`, `{b}`, `{n}`, `{f}`, `{name}`,
  `{KIN}`, `{home}`, `{town}`, `{aw}`, `{bw}`, `${dollars}`, `{{`, `}}` etc. Mesmos nomes, mesma quantidade.
  Pode mudar a ordem na frase quando o português pedir.
- **Espaços nas pontas**: se o original começa ou termina com espaço (fragmento que vai ser colado em outro),
  a tradução começa/termina igual. Fragmentos devem continuar encaixando: "settling after " é colado antes
  de um motivo, então "se acalmando depois de ". Não junte nem complete a frase.
- Maiúsculas: se o original é TODO EM CAIXA ALTA (cabeçalho de menu, telegrama), a tradução também.
  Rótulos curtos de menu (HIERARCHY, WORLD, Close, Navigate, Details, Settings, Zoom, Fit) devem ficar
  curtos: HIERARQUIA, MUNDO, Fechar, Navegar, Detalhes, Opções, Zoom, Ajustar.
- Telegramas: mantenha `STOP` como separador.
- **Pronome masculino de terceira pessoa** (o nêmesis): use `ele`, `dele`, `nele`, `ele mesmo`. O mod troca
  essas quatro formas por `ela/dela/nela/ela mesma` quando a personagem é mulher. Evite `o`, `-lo`, `lhe`
  para "him" (prefira "pegar ele", "atrás dele", "falar com ele", que soa natural no faroeste) e evite
  adjetivo que marque gênero quando der para reescrever sem perder sentido.
- Frases com o controle precisam usar exatamente estas expressões (o mod substitui por elas):
  - "double-tap D-pad Right" / "double-tap Right on the D-pad" -> `toque duas vezes no direcional para a direita`
  - "Double-tap D-pad Right" -> `Toque duas vezes no direcional para a direita`
  - "double-tap D-pad Left" -> `toque duas vezes no direcional para a esquerda`
  - "double-tap D-pad Down" -> `toque duas vezes no direcional para baixo`
  - "hold D-pad Right" -> `segure o direcional para a direita`
  - "the keyboard key only" -> `só a tecla do teclado`
- Termos do mod: nemesis -> nêmesis (plural nêmesis); grudge -> rixa; hate -> ódio; rank -> posto;
  camp -> acampamento; hideout -> esconderijo; bounty -> recompensa; lawman -> homem da lei;
  interrogate -> interrogar; telegram -> telegrama; X-ray -> raio-X; wanted -> procurado.
  Nome de gangue e de posto: siga o glossário.
- Números por extenso ("seven", "eight") se traduzem ("sete", "oito"); moeda `$` fica.

## Conferência antes de entregar

Rode no PowerShell (troque N):
```
$e=[IO.File]::ReadAllLines("loteN.tsv",[Text.Encoding]::UTF8)|select -skip 1|%{($_ -split "`t")[0]}
$s=[IO.File]::ReadAllLines("saidaN.tsv",[Text.Encoding]::UTF8)|%{($_ -split "`t")[0]}
"faltando: " + (@($e|?{$_ -notin $s}).Count) + " | linhas: $($s.Count) de $($e.Count)"
```
E confira que cada tradução tem os mesmos placeholders `{...}` do original.
