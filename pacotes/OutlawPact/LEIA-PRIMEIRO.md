# Outlaw Pact — Beta 0.4.4 — painel leve

- Painel nativo do RDR2 é o padrão: UseImagePanel=0.
- O painel padrão não cria camadas Windows, não usa UpdateLayeredWindow e não decodifica PNGs ou fontes externas.
- Removido o desenho pixel por pixel dos ícones no painel nativo; opções usam indicadores numéricos leves.
- Textos e quebras de linha ficam em cache. Dados do painel, incluindo saúde dos membros, são atualizados até quatro vezes por segundo; navegação e desenho continuam por quadro.
- Transições nativas usam apenas pequenas mudanças de posição/cor, sem upload de imagens.
- Mantidos todos os menus, 13 idiomas, teclas configuráveis, ordens individuais e salvamento manual.
- A instalação local muda somente UseImagePanel para 0 e preserva os demais valores, perfis e imagem da bandeira, com backup.

O painel tem visual mais simples que o fundo PNG da 0.4.3. A bandeira personalizada permanece como estava e ainda exige janela/sem bordas para a camada visual; o painel nativo não exige esse modo de tela.

Testes: 1.000 mudanças de seleção com o painel nativo não criaram janelas nem inicializaram GDI+, PNGs ou fontes; desenho limitado a até 32 retângulos e 30 rótulos por quadro nas 13 línguas. Compilação, carregamento ASI e regressões de comandos/salvamento/restauração passaram com simulação. O custo real no RDR2 e a resolução do travamento ainda precisam de conferência no jogo.

O caminho antigo com imagens pode ser habilitado explicitamente por UseImagePanel=1, mas não é recomendado enquanto houver travamentos nessa configuração. A otimização anterior do bitmap isolado não provava o custo da apresentação Windows dentro do jogo.


Earlier release notes / Notas das versões anteriores:

# Outlaw Pact — Beta 0.4.3

- Painel com cache do fundo redimensionado, ícones e textos; fontes carregadas uma vez e prévia preparada antes da primeira abertura.
- Navegação do painel limitada a atualizações necessárias, com limite de tamanho de renderização e atualizações de animação controladas.
- Transição curta ao abrir/trocar de página e confirmação visual ao apertar Enter. Animations=0 permite desativá-las.
- Navegar ou apertar Enter no menu não dispara mais um snapshot completo do bando. Salvamento automático segue o intervalo configurado, aguarda o menu fechar; Salvar manual continua imediato.
- Busca de ameaças feita uma vez por ciclo para todos os membros; busca de alvo próximo fora do painel reduzida.
- Bandeira personalizada com PNG, mastro e animação visual guiada pela direção/intensidade do vento do jogo.
- Imagem carregada apenas na criação/reconstrução do acampamento, reduzida e guardada em memória. Distância e teste assíncrono de obstrução limitam o trabalho.
- Tutorial em OutlawPact/assets/FLAG-README-PT.txt. Imagem: OutlawPact/assets/flag.png. Uma atualização preserva a imagem personalizada já instalada.
- Mantidas as correções de restauração da 0.4.2 e de terreno da 0.4.1.

Limites: painel e bandeira usam camada Windows, exigindo janela/sem bordas. A bandeira é uma animação visual projetada, sem física/colisão de tecido nativa; obstruções são aproximadas pelo centro. É oculta no menu/missões e além da distância configurada. Capturas somente do renderizador podem omitir essas imagens.

Medição local: o redesenho de referência de 520x1060 passou de aproximadamente 65 ms para 2 ms em média com cache aquecido. Esse número mede o renderizador isolado, não FPS do RDR2. Carregamento inicial de recursos ainda tem custo e foi antecipado à primeira abertura. Não é garantia de zero travamentos.

Passaram compilação Windows x64, carregamento ASI simulado, 13 idiomas, cache, transições, salvamento com menu aberto, vento/textura, terreno e restauração simulada. A versão ainda precisa ser conferida dentro do RDR2.

## Beta 0.4.2 — correção da restauração

- Restauração automática aguarda 10 segundos e verifica se o jogador está pronto.
- NPCs recriados aguardam o carregamento visual antes de receber roupas, armas e ordens.
- Componentes estruturais de rosto, corpo e olhos não são removidos nem reaplicados. O modelo mantém sua identidade inicializada; apenas as roupas suportadas são recuperadas, inclusive de perfis antigos.
- Armas aguardam seus recursos carregarem antes da entrega.
- Falha ou demora excessiva aborta a restauração, remove atores parciais e preserva o arquivo salvo. No botão Restaurar, o bando anterior é mantido quando a tentativa falha.
- Logs adicionais identificam início, roupa, armas, conclusão e cancelamento de cada tentativa.

O log observado terminava após world_restored, sem apontar a causa exata do crash. Estas alterações corrigem caminhos arriscados; a eliminação do crash ainda precisa ser confirmada dentro do RDR2. Testes simulados e carregamento ASI passaram. A identidade facial exata de um civil recriado não é garantida.

# Outlaw Pact — Beta 0.4.1

Atualização baseada nos pedidos da comunidade: controles configuráveis, 13 idiomas, mais opções de comportamento, perfis persistentes e descrição em inglês.

## Instalação e atualização

Requer RDR2 PC no modo História, ScriptHookRDR2 e carregador ASI. Não exige LML, WERO nem mod de banco.
Com o jogo fechado, coloque OutlawPact.asi, OutlawPact.ini e a pasta OutlawPact ao lado de RDR2.exe.
Se já personalizou OutlawPact.ini, preserve os valores e acrescente as novas seções usando o arquivo do pacote como referência. Preserve OutlawPact/settings.ini, profiles e suas imagens.
As mudanças no INI exigem reiniciar o jogo. O idioma escolhido pelo painel muda imediatamente.

## Teclas e idioma

Padrões: F7 abre o painel; G recruta o civil em foco ou abre ordens para o membro em foco. Setas navegam, Enter confirma, Backspace volta.
Em [Controls], altere MenuKey, RecruitKey e CommandKey. Também existem UpKey, DownKey, ConfirmKey, BackKey e NumpadNavigation.
Exemplo para evitar F7/G: MenuKey=F8, RecruitKey=H, CommandKey=J. Confira também as teclas dos seus outros mods.
Letras, F1..F24, Insert, Home, End, Delete, PageUp, PageDown e códigos 0xNN são aceitos. UP, DOWN, ENTER e BACKSPACE servem para navegação.
NONE desativa o atalho de recrutamento ou comando. O atalho do menu não pode ser desativado. Modificadores e botões do mouse não são aceitos.
Recrutamento e comando podem compartilhar uma tecla porque dependem do alvo. Se colidirem com menu/navegação, o atalho de ação é desativado. Navegação duplicada volta aos padrões. Teclas inválidas usam os padrões.
InteractInVehicles=0 ignora atalhos de interação enquanto o jogador está em veículo. NumpadNavigation=0 desativa os atalhos extras 8/2/5 do teclado numérico.

Abra a engrenagem > Idioma para escolher entre os 13 idiomas do jogo, incluindo russo e chinês simplificado/tradicional. Automático acompanha o jogo.
Todos os rótulos e mensagens expostos têm entradas nos 13 arquivos. Traduções são preliminares e a exibição dos caracteres ainda precisa de teste no jogo.

## Salvamento do bando e do acampamento

Os arquivos ficam em OutlawPact/profiles/<Profile>.world, separados dos saves da Rockstar. O perfil main é o padrão.
A gravação automática ocorre a cada 10 segundos, por padrão, e aguarda o painel fechar para gravar automaticamente. Na engrenagem há Salvar e Restaurar bando e acampamento.
São guardados modelos, componentes de aparência, saúde, armas/ammo, vagas, ordens estáveis e coordenadas do acampamento. Os membros mortos são retirados do perfil.
Após reiniciar ou retornar ao modo História, o perfil é restaurado. O acampamento só é reconstruído quando você chega perto do local salvo e há terreno disponível.
Seguidores reaparecem perto do jogador. Ordens de esperar/guardar muito distantes usam um ponto próximo ao jogador para evitar terreno não carregado. Ordens de acampamento aguardam a reconstrução. Assaltos em andamento não continuam.
O arquivo anterior válido fica em .world.bak. Falha de restauração não transforma o perfil em um bando vazio. Falha ao recriar NPCs pelo botão Restaurar preserva os membros atuais.

IMPORTANTE: Profile não é escolhido automaticamente pelo slot do save do jogo. Use nomes diferentes para campanhas diferentes. Carregar um save antigo usa o último snapshot daquele perfil, e não o bando histórico do save antigo.
Antes de encerrar uma sessão, prefira Salvar no painel. Fechar o jogo abruptamente pode perder mudanças feitas após o último snapshot.
Esta atualização não recupera membros/acampamentos de sessões antigas que nunca tiveram um snapshot salvo.
Componentes de aparência são salvos, mas detalhes faciais exatos, personalidade, vozes, cavalos e acessórios de armas personalizados não estão totalmente serializados.

## Configurações

[Gang]: MaxMembers é a capacidade inicial; MaximumCapacity limita melhorias até 12; MemberHealth define a base; StarterWeapon e CopyClothing controlam arma inicial e cópia de roupas compatíveis.
[Recruitment]: Accept, Refuse e Flee são percentuais; o restante é denúncia. A reputação pode acrescentar chance de aceitação.
[Behaviour]: FollowDistance (1..12), FollowSpeed (0.5..3), CollectWeapons (0/1), LootRadius (1..20).
[Combat]: AutoDefend (0/1) controla a defesa iniciada pelo mod; BaseAccuracy (0..75), Movement (0..3), Range (0..2), DefenceRadius (5..60). A IA natural do jogo ainda pode reagir.
[Camp]: Enabled, IdleActivities, InteractionRadius (5..40), RestoreRadius (30..400). Os testes de solo e espaçamento continuam obrigatórios.
[Reputation]: Enabled, RobberyGain (0..25), RecognitionThreshold (1..100), RecruitmentBonusDivisor (1..100), RecognitionIntervalSeconds (15..600).
[Persistence]: SaveGang, SaveCamp e AutosaveSeconds (5..120). Desativar um campo congela sua parte já salva, em vez de apagar o arquivo.
[Compatibility]: YieldToExternalGroups e ExcludedModels (nomes ou 0xHASH separados por vírgula).
[Features]: EnableRobbery controla assaltos a civis. Negociação de reféns e integração com banco continuam excluídas; consulte as limitações da bandeira visual acima.

## Compatibilidade e validação

O mod recusa NPCs de missão ou já em grupo. Se detectar um grupo/relacionamento externo em um membro, deixa de controlá-lo sem reconfigurar saúde ou tarefas.
Isso não detecta todos os métodos usados por outros mods. Não recrute o mesmo NPC em dois mods. Se excluir o modelo de um membro já salvo ou baixar MaximumCapacity abaixo do bando salvo, a restauração aguardará que você corrija a configuração.
Ultimate Companion System e RedLua não foram testados em conjunto no jogo. Veja COMPATIBILIDADE.md. O preset F8/H/J é apenas um exemplo de teclas diferentes, não certificação de compatibilidade.

Passaram compilação Windows x64, carregamento ASI com ScriptHook simulado, paridade/UTF-8 dos 13 idiomas e testes simulados de teclas, NPCs, acampamento, integridade de snapshots, backups e restauração. Ainda falta validar esta versão no RDR2.
Antes de publicar: siga TESTE-NO-JOGO.md. NEXUS-DESCRIPTION-EN.md está pronto para copiar na descrição da página; este pacote não foi publicado automaticamente.


## Novidades da Beta 0.4.1

Painel com fundo de couro/mapa, 24 ícones PNG suaves e fonte western Rye licenciada. Textos continuam traduzidos e as mesmas teclas e ordens são preservadas. Use modo janela ou sem bordas. [Interface] UseImagePanel=0 retorna ao painel nativo; ausência de imagens também ativa o fallback. A camada é transparente aos cliques. Capturas que incluem apenas o renderizador do jogo podem não mostrar o painel.

Acampamento em inclinações moderadas: cada peça mede seu próprio chão e ajusta altura e inclinação. Barracas procuram espaço próximo em terreno contínuo. Limite padrão de 32 graus; água, precipícios e irregularidades excessivas continuam recusados. Falha em decoração opcional não bloqueia o acampamento, e uma reconstrução malsucedida preserva o anterior. As peças são rígidas, não deformam sua malha.

Novas opções [Camp]: MaxSlopeDegrees, MaxUnevenness, MaxFoundationGap e SiteSearchRadius. Cada membro continua tendo sua própria barraca. A cerveja tem tolerância de apoio mais restrita.

Compilação, carregamento ASI simulado, testes de terreno inclinado/curvo com 12 barracas, PNGs, fontes, 13 idiomas e envio da camada Windows passaram. Falta conferir esta atualização dentro do RDR2.
