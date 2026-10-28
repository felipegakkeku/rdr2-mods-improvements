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

# Conferência da Beta 0.4.1 no RDR2

1. Use modo História e confira BETA 0.4.1 no painel. Recrute dois civis e construa um acampamento em área plana e depois em encosta moderada. Cada um precisa de uma barraca.
2. Dê ordem individual para um guardar o acampamento e para outro seguir. Confira que apenas o escolhido recebe a ordem.
3. Selecione russo e chinês na engrenagem. Confira textos e caracteres; volte a português. Reinicie e confira o idioma escolhido.
4. Use Salvar bando e acampamento, saia normalmente e inicie o jogo novamente. Confira quantidade, modelos, roupas, armas e saúde. Aproxime-se do local salvo e confira as barracas, chão e ausência de duplicatas.
5. Dê uma ordem de esperar, salve e carregue outro save no mesmo perfil. Confira a restauração. O esperado é o snapshot mais recente do perfil, não a data daquele save.
6. Use Restaurar duas vezes e confira que NPCs/barracas não se multiplicam. Mate um membro, salve e reinicie: ele não deve ser restaurado.
7. Feche o jogo, mude MenuKey=F8, RecruitKey=H e CommandKey=J no INI. Reinicie. F8 deve abrir; H deve recrutar sem comandar um membro; J deve comandar sem recrutar um civil. H/J devem aparecer nos avisos.
8. Restaure as suas teclas preferidas. Teste os ajustes de comportamento um por vez.
9. Para Ultimate Companion System/RedLua, faça uma rodada específica com a combinação desejada, teclas distintas e NPCs distintos. Compatibilidade só deve ser anunciada após isso passar. Esses mods não estão instalados neste ambiente.

Registre versão do jogo, ScriptHook, outros mods, passos e OutlawPact.log para qualquer falha. Não publique como versão validada no jogo antes de concluir esta conferência.


## Novidades da Beta 0.4.1

Painel com fundo de couro/mapa, 24 ícones PNG suaves e fonte western Rye licenciada. Textos continuam traduzidos e as mesmas teclas e ordens são preservadas. Use modo janela ou sem bordas. [Interface] UseImagePanel=0 retorna ao painel nativo; ausência de imagens também ativa o fallback. A camada é transparente aos cliques. Capturas que incluem apenas o renderizador do jogo podem não mostrar o painel.

Acampamento em inclinações moderadas: cada peça mede seu próprio chão e ajusta altura e inclinação. Barracas procuram espaço próximo em terreno contínuo. Limite padrão de 32 graus; água, precipícios e irregularidades excessivas continuam recusados. Falha em decoração opcional não bloqueia o acampamento, e uma reconstrução malsucedida preserva o anterior. As peças são rígidas, não deformam sua malha.

Novas opções [Camp]: MaxSlopeDegrees, MaxUnevenness, MaxFoundationGap e SiteSearchRadius. Cada membro continua tendo sua própria barraca. A cerveja tem tolerância de apoio mais restrita.

Compilação, carregamento ASI simulado, testes de terreno inclinado/curvo com 12 barracas, PNGs, fontes, 13 idiomas e envio da camada Windows passaram. Falta conferir esta atualização dentro do RDR2.

Confira o painel em modo janela/sem bordas, F7, engrenagem, seleção, ordens individuais e os idiomas. Tente terreno plano e uma encosta moderada; confira barracas, cerveja e que uma tentativa inválida preserve o acampamento existente.
