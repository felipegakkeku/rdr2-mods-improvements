# Separa os ldstr do strings.tsv em texto de tela (candidato à tradução) e texto de lógica.
# Saídas: instancias.tsv (cada ldstr com situação) e unicos.tsv (texto candidato único, com contexto, para traduzir).
param([string]$Lab = $PSScriptRoot)
$utf8 = New-Object Text.UTF8Encoding($false)
$linhas = [IO.File]::ReadAllLines("$Lab\strings.tsv", $utf8) | Select-Object -Skip 1

$metodoDebug = 'Crumbs|StartupTrace|GrudgeLog|::Log|Debug|::Trace|NameBank|::Dump|DrawDebug|FeedLines'
# -match não diferencia caixa: a regra de namespace de native (PED., TASK.) vai à parte, com -cmatch.
$consFora = '^(Path|File|Directory|Regex|Crumbs|Enum|Convert|Int32|Single|Double|Model|Function|StreamWriter|StreamReader|JsonConvert|Debug|Trace)\.|GrudgeLog|UiSound|Setting\.Line|\.Parse$|\.TryParse$|\.Split$|\.Trim|ToLowerInvariant|Exception\.get_Message|\.Log$|\.LogLine$|Hash'
$consCompara = 'op_Equality|op_Inequality|\.Equals$|StartsWith|EndsWith|\.Contains$|IndexOf|ContainsKey|TryGetValue|get_Item|\.Remove$|String\.Replace|ContainsValue|StringComparer|CompareTo'
$ident = '^[A-Za-z0-9_\.\-:/\\#@\+\$%]+$'

$inst = New-Object Collections.Generic.List[object]
$protegido = New-Object 'Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
$inicio = New-Object 'Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
foreach ($l in $linhas) {
    $p = $l -split "`t"
    $o = [pscustomobject]@{ m = $p[0]; o = $p[1]; c = $p[2]; d = $p[4]; t = $p[5]; s = '' }
    if ($o.t -cnotmatch '[A-Za-z]') { $o.s = 'sem-letra' }
    elseif ($o.c -match $consCompara) { $o.s = 'compara'; [void]$protegido.Add($o.t); if ($o.c -match 'StartsWith') { [void]$inicio.Add($o.t) } }
    elseif ($o.m -match $metodoDebug) { $o.s = 'debug' }
    elseif ($o.c -cmatch '^[A-Z][A-Z0-9_]+\.' -or $o.c -match $consFora) { $o.s = 'logica' }
    elseif ($o.t -match '^\[.*\]$' -or $o.t -match '^;') { $o.s = 'ini' }
    elseif (($o.t -cmatch $ident) -and (($o.t -match '[_\.:/\\@#\$%]') -or ($o.t -cmatch '[a-z][A-Z]') -or (($o.t -cmatch '^[a-z0-9]+$') -and $o.t.Length -gt 14))) { $o.s = 'identificador' }
    else { $o.s = 'candidato' }
    $inst.Add($o)
}
foreach ($o in $inst) { if ($o.s -eq 'candidato' -and $protegido.Contains($o.t)) { $o.s = 'protegido' } }

# Texto candidato que começa com um literal comparado (ex.: "INTEL: ..." com StartsWith("INTEL")): o prefixo fica.
$prefixos = @($inicio | Where-Object { $_.Length -ge 3 -and $_ -cmatch '[A-Za-z]' })
$porTamanho = @{}
foreach ($p in $prefixos) { if (-not $porTamanho[$p.Length]) { $porTamanho[$p.Length] = New-Object 'Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal) }; [void]$porTamanho[$p.Length].Add($p) }

$unicos = [ordered]@{}
foreach ($o in $inst) {
    if ($o.s -ne 'candidato') { continue }
    if (-not $unicos.Contains($o.t)) { $unicos[$o.t] = [pscustomobject]@{ t = $o.t; n = 0; ctx = New-Object Collections.Generic.List[string]; pre = '' } }
    $u = $unicos[$o.t]; $u.n++
    if ($u.ctx.Count -lt 2) { $tipo = ($o.m -replace '^NemesisSystem\.', '' -replace '/<>c__DisplayClass\d+_\d+', '' -replace '/<>c', ''); $u.ctx.Add("$tipo<$($o.c)>") }
}
foreach ($u in $unicos.Values) {
    foreach ($len in $porTamanho.Keys) { if ($u.t.Length -gt $len -and $porTamanho[$len].Contains($u.t.Substring(0, $len))) { $u.pre = $u.t.Substring(0, $len) } }
}

$w = New-Object IO.StreamWriter("$Lab\instancias.tsv", $false, $utf8)
$w.WriteLine("metodo`toffset`tsituacao`ttexto")
foreach ($o in $inst) { $w.WriteLine("$($o.m)`t$($o.o)`t$($o.s)`t$($o.t)") }
$w.Close()
$w = New-Object IO.StreamWriter("$Lab\unicos.tsv", $false, $utf8)
$w.WriteLine("id`tusos`tcontexto`tprefixo_fixo`ttexto")
$i = 0
foreach ($u in $unicos.Values) { $i++; $w.WriteLine("$i`t$($u.n)`t$($u.ctx -join ' ; ')`t$($u.pre)`t$($u.t)") }
$w.Close()
$inst | Group-Object s | Sort-Object Count -Descending | ForEach-Object { "{0,6} {1}" -f $_.Count, $_.Name }
"unicos candidatos: $i"
