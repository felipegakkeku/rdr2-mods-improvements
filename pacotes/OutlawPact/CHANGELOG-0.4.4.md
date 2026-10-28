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
