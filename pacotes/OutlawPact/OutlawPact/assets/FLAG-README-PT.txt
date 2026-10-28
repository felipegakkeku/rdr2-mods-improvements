BANDEIRA PERSONALIZADA — OUTLAW PACT BETA 0.4.3

1. Feche o jogo.
2. Abra a pasta do RDR2, a mesma que contém RDR2.exe.
3. Abra OutlawPact > assets.
4. Substitua flag.png pela sua imagem PNG. O nome precisa ser exatamente flag.png.
   Ative a exibição de extensões no Explorer para evitar flag.png.png.
5. Recomendado: 768 x 512 pixels (proporção 3:2), conteúdo centralizado.
   Limites: até 4096 x 4096 pixels e 8 MB. Internamente é reduzida para 384 x 256.
   Outros formatos devem ser exportados como PNG; renomear .jpg para .png não converte a imagem.
6. Inicie o modo História em modo janela ou sem bordas.
7. Crie ou reconstrua o acampamento. Um acampamento restaurado também carrega a imagem.

Na instalação atual a imagem fica em:
D:\Program Files (x86)\Steam\steamapps\common\Red Dead Redemption 2\OutlawPact\assets\flag.png

O PNG é carregado uma vez por criação/reconstrução, não em cada quadro.
A posição acompanha o mastro e a animação usa direção/intensidade do vento do jogo.
É uma animação visual projetada pela camada Windows, não o sistema de tecido/colisão nativo.
A visibilidade usa um teste de obstrução no centro da bandeira; ocultação parcial pode ser aproximada.
A camada pode não aparecer em capturas que incluem apenas o renderizador do jogo.
A bandeira é oculta enquanto o painel está aberto, em missões e fora da distância de desenho.
Se o modelo de mastro não carregar ou não encontrar espaço, o acampamento ainda pode ser criado sem ele.

OutlawPact.ini:
[Camp] CustomFlag=0 desativa a bandeira; FlagDrawDistance ajusta a distância (10 a 80 metros).
[Interface] Animations=0 desativa as transições do painel.

Evite apagar profiles ou substituir settings.ini ao atualizar.
Falta conferir o mastro, a projeção, as obstruções e o desempenho desta versão dentro do RDR2.
