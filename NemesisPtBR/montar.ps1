# Junta as traduções (traducao\loteN.tsv + saidaN.tsv) e monta o NemesisMod.dll em pt-BR.
#   montar.ps1 [-Dll NemesisMod-original-0.9.5.2.dll] [-Saida NemesisMod-ptbr.dll]
# A tradução vale por texto: rodar de novo sobre outra versão do mod (dump + classificar.ps1) reaproveita tudo.
param([string]$Dll = "NemesisMod-original-0.9.5.2.dll", [string]$Saida = "NemesisMod-ptbr.dll")
$lab = $PSScriptRoot
$utf8 = New-Object Text.UTF8Encoding($false)
function Unesc([string]$s) { $s -replace '\\t', "`t" -replace '\\n', "`n" -replace '\\r', "`r" -replace '\\\\', '\' }
function Marcas([string]$s) { (([regex]::Matches($s, '\{[^{}]*\}|\$\{[^{}]*\}') | ForEach-Object { $_.Value }) | Sort-Object) -join ' ' }

# 1. Texto original -> tradução, com conferência de placeholder, espaço nas pontas e TAB.
$trad = New-Object 'Collections.Generic.Dictionary[string,string]' ([StringComparer]::Ordinal)
$recusas = New-Object Collections.Generic.List[string]
foreach ($lote in Get-ChildItem "$lab\traducao" -Filter 'lote*.tsv') {
    $n = $lote.BaseName.Substring(4)
    # Nome diferente do parâmetro $Saida: variável no PowerShell não diferencia caixa.
    $arqSaida = "$lab\traducao\saida$n.tsv"
    if (-not (Test-Path $arqSaida)) { Write-Warning "sem saida$n.tsv"; continue }
    $porId = @{}
    foreach ($l in [IO.File]::ReadAllLines($lote.FullName, $utf8) | Select-Object -Skip 1) { $p = $l -split "`t"; $porId[$p[0]] = $p[4] }
    foreach ($l in [IO.File]::ReadAllLines($arqSaida, $utf8)) {
        $i = $l.IndexOf("`t"); if ($i -lt 0) { continue }
        $id = $l.Substring(0, $i).Trim([char]0xFEFF, ' '); $t = $l.Substring($i + 1)
        $o = $porId[$id]
        if ($null -eq $o -or $t -eq '=' -or $t -ceq $o -or $t.Length -eq 0) { continue }
        # O editor dos tradutores come o espaço do fim da linha: devolve o mesmo espaço final do original.
        if ($o -match '(\s+)$' -and $t -notmatch '\s$') { $t += $Matches[1] }
        $uo = Unesc $o; $ut = Unesc $t
        # Espaço no começo pode mudar de propósito: fragmento colado depois de "Nome's" ou frase refeita com pontuação.
        $inicioLivre = ($uo -match "^'") -or ($ut -match '^[\.,:;]')
        $motivo = $null
        if ((Marcas $uo) -cne (Marcas $ut)) { $motivo = 'placeholder' }
        elseif ((-not $inicioLivre -and ($uo -match '^\s') -ne ($ut -match '^\s')) -or ($uo -match '\s$') -ne ($ut -match '\s$')) { $motivo = 'espaco' }
        elseif ($t.Contains("`t")) { $motivo = 'tab' }
        elseif ($ut.Contains([char]0x2014)) { $motivo = 'travessao' }
        if ($motivo) { $recusas.Add("$motivo`tlote$n#$id`t$o`t$t"); continue }
        $trad[$o] = $t
    }
}

# Plural colado ({1} = "s") que não funciona em português: aqui o placeholder pode sumir (string.Format ignora o que sobra).
$trad['{0} favour{1}: {2}'] = 'Favores ({0}): {2}'
$trad['seen {0} time{1}{2}'] = 'cruzou com você {0}x{2}'
$trad['{0} fight{1}, {2} named m{3}n dead{4}'] = '{0} combate{1}, mortos de nome: {2}{4}'

# 2. Mapa exato (método + offset) para cada ldstr de tela cujo texto tem tradução.
$mapa = New-Object Collections.Generic.List[string]
$inst = [IO.File]::ReadAllLines("$lab\instancias.tsv", $utf8) | Select-Object -Skip 1 | ForEach-Object { $p = $_ -split "`t"; [pscustomobject]@{ m = $p[0]; o = $p[1]; s = $p[2]; t = $p[3] } }

# Os lotes juntaram textos que só mudam de caixa ("FAITH" e "Faith"): a variante sem lote herda a tradução da irmã, na caixa dela.
$ptbr = [Globalization.CultureInfo]::GetCultureInfo('pt-BR')
$porCaixa = New-Object 'Collections.Generic.Dictionary[string,string]' ([StringComparer]::OrdinalIgnoreCase)
foreach ($k in @($trad.Keys)) { if (-not $porCaixa.ContainsKey($k)) { $porCaixa[$k] = $k } }
function Maiuscula([string]$s) { [regex]::Replace($s, '\{[^{}]*\}|\\.|[^{}\\]+', { param($m) if ($m.Value -match '^[{\\]') { $m.Value } else { $m.Value.ToUpper($ptbr) } }) }
function PrimeiraLetra([string]$s, [bool]$alta) {
    $m = [regex]::Match($s, '\p{L}'); if (-not $m.Success) { return $s }
    $c = if ($alta) { $m.Value.ToUpper($ptbr) } else { $m.Value.ToLower($ptbr) }
    $s.Substring(0, $m.Index) + $c + $s.Substring($m.Index + 1)
}
$herdados = 0
foreach ($t in @($inst | Where-Object { $_.s -eq 'candidato' } | ForEach-Object { $_.t } | Sort-Object -Unique -CaseSensitive)) {
    if ($trad.ContainsKey($t) -or -not $porCaixa.ContainsKey($t)) { continue }
    $irma = $porCaixa[$t]; $novo = $trad[$irma]; $u = Unesc $t
    if ($u -cmatch '\p{Lu}' -and $u -cnotmatch '\p{Ll}') { $novo = Maiuscula $novo }
    else {
        # Irmã toda em maiúscula ("BOSS") e esta não ("Boss"): parte da tradução em minúscula e acerta a primeira letra.
        $ui = Unesc $irma
        if ($ui -cmatch '\p{Lu}' -and $ui -cnotmatch '\p{Ll}') { $novo = $novo.ToLower($ptbr) -replace '\\N', '\n' -replace '\\T', '\t' -replace '\\R', '\r'; $irma = $irma.ToLower() }
        $a = [regex]::Match($u, '\p{L}').Value; $b = [regex]::Match((Unesc $irma), '\p{L}').Value
        if ($a -cne $b) { $novo = PrimeiraLetra $novo ($a -cmatch '\p{Lu}') }
    }
    if ((Marcas $u) -cne (Marcas (Unesc $novo))) { continue }
    $trad[$t] = $novo; $herdados++
}
"herdados pela caixa: $herdados"
$usados = 0
foreach ($x in $inst) {
    if ($x.s -ne 'candidato') { continue }
    if ($trad.ContainsKey($x.t)) { $mapa.Add("$($x.m)`t$($x.o)`t$($x.t)`t$($trad[$x.t])"); $usados++ }
}

# 3. Ajustes de lógica feitos à mão.
function Exato($metodo, $texto, $novo, [int]$ordem = 0) {
    $hits = @($inst | Where-Object { $_.m -eq $metodo -and $_.t -ceq $texto })
    if ($hits.Count -le $ordem) { throw "não achei '$texto' em $metodo" }
    $script:mapa.Add("$metodo`t$($hits[$ordem].o)`t$texto`t$novo")
}
# Troca de pronome para personagem mulher: agora casa ele/dele/nele/ele mesmo (as traduções usam essas formas).
$pron = 'NemesisSystem.Pron/<>c::<Her>b__1_0'
Exato 'NemesisSystem.Pron::.cctor' '\\b(he|him|his|himself|he''s|he''ll|he''d)\\b' '\\b(ele mesmo|ele|dele|nele)\\b'
Exato $pron 'he' 'ele'; Exato $pron 'him' 'dele'; Exato $pron 'his' 'nele'; Exato $pron 'himself' 'ele mesmo'
Exato $pron 'he''s' '\u0001'; Exato $pron 'he''ll' '\u0002'
Exato $pron 'she' 'ela'; Exato $pron 'herself' 'ela mesma'
Exato $pron 'her' 'nela' 0   # ramo do "his" (primeiro no IL)
Exato $pron 'her' 'dela' 1   # ramo do "him"
# "a" -> "an" antes de vogal estragaria o português ("a arma" -> "an arma"): padrão que nunca casa.
Exato 'NemesisSystem.NemesisMod::Incident' '(^|\\s)([aA]) (?=[AEIOaeio])' '(?!)(^|\\s)([aA]) '
# Frases do controle que o KeyCombo.Keyed troca pela tecla escolhida: mesmas expressões das traduções.
$kc = 'NemesisSystem.KeyCombo::Keyed'
Exato $kc 'the keyboard key only' 'só a tecla do teclado'
Exato $kc 'hold D-pad Right' 'segure o direcional para a direita'
Exato $kc 'double-tap D-pad Down' 'toque duas vezes no direcional para baixo'
Exato $kc 'double-tap D-pad Left' 'toque duas vezes no direcional para a esquerda'
Exato $kc 'double-tap Right on the D-pad' 'toque duas vezes no direcional para a direita'
Exato $kc 'double-tap D-pad Right' 'toque duas vezes no direcional para a direita'
Exato $kc 'Double-tap D-pad Right' 'Toque duas vezes no direcional para a direita'

# Rótulos de tela cuja irmã de outra caixa ficou em inglês por ser chave em outro lugar: aqui só o desenho, método por método.
function Rotulo($metodo, $texto, $novo) {
    $hits = @($inst | Where-Object { $_.m -eq "NemesisSystem.$metodo" -and $_.t -ceq $texto -and $_.s -eq 'candidato' })
    if (-not $hits) { throw "não achei '$texto' em $metodo" }
    foreach ($h in $hits) { $script:mapa.Add("$($h.m)`t$($h.o)`t$texto`t$novo") }
}
$det = 'RosterMenu::DrawDetail'
Rotulo $det 'TOWN' 'CIDADE'; Rotulo $det 'KNOWN' 'CONHECIDO'; Rotulo $det 'NO' 'NÃO'; Rotulo $det 'WARM' 'BASTANTE'
Rotulo $det 'Rank {0}' 'Posto {0}'; Rotulo $det 'INTENT' 'INTENÇÃO'; Rotulo $det 'BORN' 'NASCEU'
Rotulo $det 'FAITH' 'FÉ'; Rotulo $det 'FAMILY' 'FAMÍLIA'; Rotulo $det 'WHY' 'POR QUÊ'
Rotulo 'RosterMenu::RankText' 'Rank {0}' 'Posto {0}'
Rotulo 'RosterMenu::DrawUnaffiliatedList' 'KNOWN' 'CONHECIDO'
Rotulo 'RosterMenu::DrawCrimeSheet' 'FACTIONS' 'FACÇÕES'
Rotulo 'RosterMenu::DrawWorld' 'PEOPLE' 'PESSOAS'
Rotulo 'RosterMenu::DrawWarsPage' 'LEADER' 'LÍDER'
Rotulo 'WelcomePics::Grudge' 'RESENTFUL' 'RESSENTIDO'; Rotulo 'WelcomePics::Grudge' 'VENGEFUL' 'VINGATIVO'; Rotulo 'WelcomePics::Grudge' 'MURDEROUS' 'ASSASSINO'
Rotulo 'BulletImpact.XRayViewer::DrawFacts' 'WHEN' 'QUANDO'

# O mapa pode ter a mesma instrução duas vezes (ajuste manual por cima da tradução): vale a última.
$final = [ordered]@{}
foreach ($l in $mapa) { $p = $l -split "`t"; $final["$($p[0])`t$($p[1])"] = $l }
[IO.File]::WriteAllLines("$lab\mapa-ptbr.tsv", [string[]]$final.Values, $utf8)
[IO.File]::WriteAllLines("$lab\recusas.tsv", [string[]]$recusas, $utf8)
"traduções: $($trad.Count) textos | instruções no mapa: $($final.Count) | recusadas: $($recusas.Count)"
$recusas | Group-Object { ($_ -split "`t")[0] } | ForEach-Object { "  recusa $($_.Name): $($_.Count)" }

& "$lab\CecilStr.exe" apply "$lab\$Dll" "$lab\mapa-ptbr.tsv" "$lab\$Saida" --avisos "$lab\NemesisAvisos.dll"
"saida: $LASTEXITCODE"
