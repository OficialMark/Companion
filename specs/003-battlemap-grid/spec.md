# Feature Specification: Battlemap com Grid Interativo

**Feature Branch**: Não criada (nenhum hook before_specify está configurado)

**Created**: 2026-08-28

**Status**: Draft

**Input**: User description: "Crie uma feature de batlemap. O mestre insere a imagem e diz qual a dimensão do grid, cada jogador possui um pino de tabuleiro e pode arrastar ele e selecionar a posição que está no grid. lembre que a feature deve ser facilmente usavel no desktop e no celular. Apesar do grid poder variar de tamanho, a sua janela deve ter um tamanho fixo, para não bagunçar a visualização, o tamanho de cada bloco deve se adaptar a isso, o tamanho médio deve ser 40x40"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Configurar o battlemap (Priority: P1)

Como mestre de uma campanha, quero inserir a imagem de um mapa e informar sua quantidade de colunas e linhas para disponibilizar um tabuleiro quadriculado alinhado à cena do encontro.

**Why this priority**: Sem um mapa configurado e um grid válido não existe tabuleiro no qual os participantes possam se posicionar.

**Independent Test**: Pode ser testada pelo mestre ao selecionar uma imagem, informar dimensões válidas, conferir a prévia e publicar o battlemap; o resultado já entrega um mapa compartilhado e quadriculado, mesmo antes da movimentação dos pinos.

**Acceptance Scenarios**:

1. **Given** um encontro sem battlemap, **When** o mestre seleciona uma imagem compatível, informa 20 colunas e 15 linhas e confirma a configuração, **Then** todos os participantes autorizados visualizam a imagem com um grid de 20 por 15 células.
2. **Given** que o mestre está configurando o battlemap, **When** altera a quantidade de linhas ou colunas, **Then** a prévia é atualizada e mantém o grid alinhado à área da imagem antes da confirmação.
3. **Given** uma dimensão ausente, não inteira ou fora do intervalo aceito, **When** o mestre tenta confirmar, **Then** a publicação é impedida e o campo problemático recebe uma orientação clara para correção.
4. **Given** um battlemap já publicado, **When** um jogador da campanha acessa o encontro, **Then** ele pode visualizar o mapa e o grid, mas não pode alterar a imagem nem as dimensões.

---

### User Story 2 - Posicionar e mover o próprio pino (Priority: P2)

Como jogador, quero identificar meu pino e movê-lo para uma célula do grid para que minha posição no battlemap seja clara para todo o grupo.

**Why this priority**: A movimentação compartilhada é a principal interação de jogo oferecida pelo battlemap após sua configuração.

**Independent Test**: Pode ser testada com um battlemap publicado e dois jogadores: cada um posiciona seu próprio pino, vê o encaixe na célula escolhida e confirma que não consegue mover o pino do outro.

**Acceptance Scenarios**:

1. **Given** um jogador autorizado com pino ainda fora do mapa, **When** ele arrasta o pino e o solta sobre uma célula válida, **Then** o pino se encaixa nessa célula e sua posição confirmada passa a ser vista pelos demais participantes.
2. **Given** um jogador usando uma tela sensível ao toque, **When** ele seleciona o próprio pino e depois seleciona uma célula válida, **Then** o pino é movido para essa célula sem exigir precisão incompatível com o uso no celular.
3. **Given** pinos pertencentes a jogadores diferentes, **When** um jogador tenta mover um pino que não é o seu, **Then** a posição não é alterada e a interface informa que o pino não está sob seu controle.
4. **Given** que o jogador solta o pino fora do grid ou sobre uma área inválida, **When** a interação termina, **Then** o pino retorna à última posição confirmada.
5. **Given** dois ou mais pinos na mesma célula, **When** os participantes visualizam essa célula, **Then** todos os pinos continuam identificáveis e selecionáveis.

---

### User Story 3 - Navegar pelo mapa em desktop e celular (Priority: P3)

Como participante, quero visualizar e navegar por grids de diferentes dimensões sem que o battlemap desorganize a página, tanto no desktop quanto no celular.

**Why this priority**: Uma área de visualização estável e controles acessíveis tornam o recurso utilizável durante a sessão em diferentes dispositivos e tamanhos de mapa.

**Independent Test**: Pode ser testada alternando entre grids pequenos e grandes em viewports de desktop e celular, verificando que a área reservada ao mapa permanece estável, que as células são quadradas e que qualquer região alcançável pode ser selecionada.

**Acceptance Scenarios**:

1. **Given** battlemap com grid pequeno ou grande, **When** ele é aberto, **Then** a área ocupada pelo visualizador permanece constante para a categoria de tela atual e não desloca os controles vizinhos por causa das dimensões do grid.
2. **Given** que o grid cabe com células próximas da escala-alvo de 40 por 40 pixels, **When** o mapa é exibido na escala inicial, **Then** todas as células têm tamanho uniforme, formato quadrado e dimensão próxima da escala-alvo.
3. **Given** um grid que produziria células pequenas demais para interação confiável ao ser exibido por inteiro, **When** o mapa é aberto, **Then** o participante pode navegar pela área do mapa mantendo células e alvos de seleção utilizáveis.
4. **Given** um celular em orientação retrato ou paisagem, **When** o participante arrasta o mapa, amplia, reduz ou seleciona uma célula, **Then** a página não ganha rolagem horizontal acidental e os controles essenciais permanecem acessíveis.
5. **Given** uma perda temporária de conexão, **When** o participante tenta mover um pino, **Then** a interface mantém a última posição confirmada, indica a indisponibilidade e não apresenta o movimento como concluído até receber confirmação.
6. **Given** que a conexão foi restabelecida, **When** o battlemap é sincronizado novamente, **Then** a imagem, as dimensões do grid e as posições atuais confirmadas dos pinos são recuperadas.

### Edge Cases

- A imagem está corrompida, excede o limite aceito ou usa formato não compatível; a configuração não é publicada e o mestre recebe uma mensagem acionável.
- A proporção da imagem difere da proporção formada por colunas e linhas; a prévia deixa explícita a área final do tabuleiro e mantém imagem, grid e coordenadas alinhados.
- O mestre reduz as dimensões de um grid já usado e uma ou mais posições deixam de existir; a confirmação exige aviso prévio e os pinos afetados retornam à reserva, sem posição no mapa.
- O battlemap é substituído durante uma sessão; participantes deixam de interagir com a versão anterior e recebem o estado vigente.
- Dois comandos tentam mover o mesmo pino quase simultaneamente; somente uma posição confirmada prevalece e todos convergem para ela.
- A conexão cai durante o arraste; o pino volta visualmente à última célula confirmada até que o estado atual possa ser recuperado.
- Uma alteração de orientação ou redimensionamento ocorre durante a interação; a posição lógica dos pinos não muda e o visualizador continua dentro de sua área reservada.
- Um grid extremamente largo ou alto não pode ser exibido inteiro com alvos confortáveis; navegação e ajuste de escala permitem alcançar qualquer célula sem reduzir permanentemente a usabilidade.
- Um jogador entra após a publicação do mapa; seu pino aparece na reserva e pode ser colocado pelo próprio jogador em uma célula válida.
- Participantes tentam usar gestos de navegação e movimentação do pino na mesma região; o estado de seleção deixa claro qual ação está ativa e permite cancelá-la sem alterar a posição.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: O sistema MUST permitir que somente o mestre configure ou substitua o battlemap de um encontro ao qual possui acesso.
- **FR-002**: O mestre MUST poder selecionar uma imagem nos formatos PNG, JPEG ou WebP, com tamanho de até 20 MB, e visualizar sua prévia antes da publicação.
- **FR-003**: O mestre MUST informar separadamente a quantidade inteira de colunas e de linhas do grid, cada uma entre 1 e 100.
- **FR-004**: O sistema MUST mostrar uma prévia combinando imagem e grid e MUST refletir alterações de dimensão antes que o mestre confirme a publicação.
- **FR-005**: O sistema MUST manter a imagem, as linhas do grid, as células selecionáveis e as coordenadas dos pinos alinhadas em todas as escalas suportadas.
- **FR-006**: O sistema MUST preservar a proporção visual da imagem e MUST deixar perceptível, na prévia, qualquer ajuste necessário para compatibilizá-la com a proporção do grid.
- **FR-007**: O sistema MUST disponibilizar o battlemap publicado somente aos participantes autorizados da campanha associada ao encontro.
- **FR-008**: O visualizador do battlemap MUST ocupar uma área estável definida para a categoria de tela atual, independentemente da quantidade de linhas, colunas ou da proporção da imagem.
- **FR-009**: A mudança das dimensões do grid MUST alterar o tamanho ou a área navegável das células dentro do visualizador, sem expandir o visualizador a ponto de deslocar o restante da página.
- **FR-010**: Todas as células MUST ser quadradas e uniformes em uma mesma escala; a escala inicial MUST ter como alvo células de aproximadamente 40 por 40 pixels.
- **FR-011**: Quando exibir o grid inteiro tornar as células pequenas demais para seleção confiável, o sistema MUST preservar alvos de interação adequados e oferecer navegação por deslocamento e ajuste de escala dentro do visualizador.
- **FR-012**: O visualizador MUST permitir alcançar e selecionar qualquer célula válida em desktop e em celular, sem depender de rolagem horizontal da página.
- **FR-013**: Cada jogador participante MUST possuir exatamente um pino pessoal identificável no battlemap, inicialmente mantido na reserva até receber uma posição válida.
- **FR-014**: O jogador MUST poder mover somente o próprio pino; o mestre MUST poder reposicionar qualquer pino do encontro para arbitrar a sessão.
- **FR-015**: O sistema MUST oferecer movimentação por arrastar e soltar e uma alternativa por seleção do pino seguida da seleção da célula, adequada a toque, mouse e teclado.
- **FR-016**: Ao concluir um movimento válido, o sistema MUST encaixar o pino em uma única célula e registrar sua posição por coluna e linha.
- **FR-017**: Movimentos para fora do grid, para coordenadas inexistentes ou feitos por participante sem permissão MUST ser rejeitados, preservando a última posição confirmada.
- **FR-018**: O sistema MUST permitir múltiplos pinos na mesma célula e manter cada um distinguível e selecionável.
- **FR-019**: Alterações confirmadas na imagem, no grid ou nas posições dos pinos MUST ser compartilhadas com os participantes ativos e MUST convergir para um único estado vigente.
- **FR-020**: O estado confirmado do battlemap MUST ser recuperável após recarregamento, entrada tardia, desconexão ou reconexão, sem depender de eventos anteriores da sessão.
- **FR-021**: Durante indisponibilidade de conexão, o sistema MUST indicar o estado de conexão, manter visível o último estado seguro e impedir que movimentos sem confirmação pareçam concluídos.
- **FR-022**: Ao reduzir ou substituir um grid com pinos posicionados, o mestre MUST receber um resumo do impacto antes de confirmar; pinos cujas células deixarem de existir MUST retornar à reserva.
- **FR-023**: Os controles essenciais de configuração, movimentação, cancelamento, navegação e ajuste de escala MUST possuir rótulos ou indicações compreensíveis e áreas de interação adequadas para toque e teclado.
- **FR-024**: A posição lógica de cada pino MUST permanecer inalterada quando o visualizador muda de tamanho, escala ou orientação de tela.

### Key Entities

- **Battlemap**: O tabuleiro ativo de um encontro; contém a imagem selecionada, estado de publicação, vínculo com encontro/campanha e configuração vigente do grid.
- **Grid**: A malha sobreposta ao battlemap; contém quantidades de colunas e linhas e define o conjunto de coordenadas válidas.
- **Pino de jogador**: A representação identificável de um participante; pertence a um jogador e possui uma posição opcional no grid ou estado de reserva.
- **Posição no grid**: A coordenada lógica formada por coluna e linha válidas; permanece independente do tamanho visual ou da escala aplicada.
- **Participante do encontro**: Usuário autorizado associado à campanha, com papel de mestre ou jogador que determina suas permissões sobre mapa e pinos.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Em testes de usabilidade, pelo menos 90% dos mestres conseguem selecionar uma imagem, definir o grid e publicar o primeiro battlemap em até 2 minutos, tanto no desktop quanto no celular.
- **SC-002**: Pelo menos 95% dos jogadores conseguem identificar e mover o próprio pino para uma célula indicada na primeira tentativa, usando mouse ou toque.
- **SC-003**: Para grids entre 1 por 1 e 100 por 100, trocar as dimensões não altera a área reservada ao visualizador nem provoca rolagem horizontal da página nos tamanhos de tela suportados.
- **SC-004**: Na escala inicial em que o alvo de 40 por 40 pixels é aplicável, as células permanecem entre 36 e 44 pixels em ambos os eixos; grids maiores continuam completamente navegáveis sem células interativas menores que o limite de uso definido para o dispositivo.
- **SC-005**: Em condições normais de sessão, 95% dos movimentos confirmados ficam visíveis para todos os participantes conectados em até 2 segundos.
- **SC-006**: Após recarregar ou restabelecer a conexão, 95% dos participantes visualizam o estado vigente do mapa, grid e pinos em até 5 segundos e nenhum movimento não confirmado substitui uma posição confirmada.
- **SC-007**: Com até 20 participantes e seus respectivos pinos, todas as posições permanecem identificáveis, inclusive quando mais de um pino ocupa a mesma célula.
- **SC-008**: Todas as jornadas críticas de configuração, movimentação, navegação, desconexão e reconexão podem ser concluídas em viewport de desktop e de celular Android sem perda de funcionalidade.

## Assumptions

- O battlemap pertence a um encontro de uma campanha existente e há somente um battlemap ativo por encontro nesta primeira versão.
- A autenticação, os papéis de mestre e jogador e a participação na campanha já existem e serão reutilizados.
- A escala de 40 por 40 pixels é um alvo visual inicial, não uma exigência de exibir o grid inteiro; quando houver conflito entre enquadrar todo o mapa e manter a interação confortável, prevalecem células utilizáveis com navegação interna.
- A área do visualizador é estável dentro de cada categoria de tela, mas pode assumir dimensões apropriadas diferentes entre desktop, retrato móvel e paisagem móvel.
- A imagem é apresentada sem distorção; ajustes de enquadramento necessários para compatibilizar a proporção da imagem com a do grid são mostrados ao mestre antes da publicação.
- Mais de um pino pode ocupar a mesma célula; esta versão não aplica regras automáticas de colisão, distância, terreno, deslocamento por turno ou bloqueio de movimento.
- Fog of war, desenho sobre o mapa, régua de distância, áreas de efeito, múltiplos andares, rotação de pinos e biblioteca de mapas ficam fora do escopo desta feature.
- O mestre pode arbitrar a sessão reposicionando qualquer pino, enquanto jogadores controlam apenas o próprio pino.
- Formatos de imagem aceitos são PNG, JPEG e WebP, com limite de 20 MB por arquivo.
