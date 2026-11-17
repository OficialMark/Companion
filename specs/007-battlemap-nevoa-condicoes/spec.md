# Feature Specification: Névoa de guerra, condições com ícones e HP controlado pelo mestre no Battlemap

**Feature Branch**: `feat/battlemap-condicoes-nevoa`

**Created**: 2026-11-16

**Status**: Draft

**Input**: User description: "Uma feature foi perdida, a de marcar condições nos tokens do mapa (caído, paralisado, envenenado, sangrando, etc), lembre-se que essas condições devem ter ícones agradáveis; o HP dos jogadores visível também desapareceu (o mestre deve poder habilitar ou desabilitar o HP visível dos seus tokens); também quero que adicione uma feature de névoa no battlemap, o mestre deve poder ocultar ou revelar facilmente partes do mapa; problema: no PC, o menu que exibe os projéteis só aparece quando se clica com botão direito, com botão esquerdo só aparece enquanto o botão está pressionado (o que impossibilita usar dessa forma), cuidado para isso não interferir no arraste de tokens no mapa. Sempre revisar a usabilidade e responsividade."

**Relação com a spec 006**: a spec `006-battlemap-combat-ux` (condições, barra de HP, tamanho de token, tokens representativos, destaque de turno) foi implementada num branch que nunca chegou ao branch de trabalho; ela foi recuperada por merge antes desta feature. Esta spec evolui aquele comportamento e substitui explicitamente o FR-009a da 006 (HP sempre visível a todos) pelo controle do mestre descrito abaixo.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Abrir o menu do token com clique esquerdo no PC sem atrapalhar o arraste (Priority: P1)

No computador, o participante clica uma vez com o botão esquerdo num token que controla e o menu do token (área de efeito/projétil, tamanho, condições) abre e permanece aberto depois que o botão é solto. Se em vez de clicar ele arrastar o token, o token é movido e o menu não atrapalha.

**Why this priority**: Hoje o menu é praticamente inutilizável com mouse: ele só aparece enquanto o botão esquerdo está pressionado, obrigando o uso do botão direito, que ninguém descobre sozinho. Sem isso, condições, tamanho e áreas (as demais histórias) ficam inacessíveis no PC.

**Independent Test**: Em viewport de desktop, clicar e soltar o botão esquerdo sobre um token controlável (de tamanho 1x1, 2x2 e um token empilhado com outro na mesma célula) e confirmar que o menu continua aberto; depois arrastar o token para outra célula e confirmar que ele se move.

**Acceptance Scenarios**:

1. **Given** um participante com mouse e um token que controla, **When** ele clica e solta o botão esquerdo sobre o token, **Then** o menu do token abre e continua aberto após soltar o botão.
2. **Given** um token de tamanho 2x2 ou maior, **When** o participante clica em qualquer parte visível do token, **Then** o menu abre e o token não é movido para a célula sob o cursor.
3. **Given** um participante pressiona o botão esquerdo sobre um token e move o ponteiro além de uma pequena tolerância, **When** solta o botão sobre outra célula, **Then** o token é movido para essa célula, como antes.
4. **Given** o menu de um token está aberto, **When** o participante clica de novo no mesmo token, **Then** o menu fecha (alternância), e **When** clica numa célula vazia do mapa, **Then** o token é movido para essa célula, como no fluxo já existente de selecionar e escolher célula.
5. **Given** um participante clica com o botão direito ou do meio num token, **When** move o ponteiro, **Then** nenhum arraste de token começa.
6. **Given** um participante usa toque (celular/tablet), **When** toca uma vez num token, **Then** o mesmo menu abre e permanece aberto; arrastar com o dedo continua movendo o token.

---

### User Story 2 - Marcar condições com ícones reconhecíveis (Priority: P1)

O jogador (nos tokens que controla) ou o mestre (em qualquer token) abre o menu do token e marca condições como caído, paralisado, envenenado, sangrando, atordoado, cego, invisível, amedrontado, enfeitiçado, inconsciente. Cada condição tem um ícone próprio, colorido e reconhecível, mostrado no menu e sobre o token no mapa.

**Why this priority**: É a feature que o usuário percebeu como "perdida"; o merge da 006 trouxe o comportamento de volta, mas com abreviações de três letras ("VEN", "ATD") que são difíceis de ler e pouco agradáveis, e sem condições pedidas explicitamente (paralisado, sangrando).

**Independent Test**: Marcar "Envenenado" e "Sangrando" num token, confirmar que dois ícones distintos aparecem sobre o token para todos os participantes, passar o mouse ou focar para ver o nome da condição, desmarcar e confirmar que somem.

**Acceptance Scenarios**:

1. **Given** o menu de um token controlável aberto, **When** é exibida a seção de condições, **Then** cada condição aparece como um botão com ícone e nome, indicando claramente se está marcada.
2. **Given** um token com condições marcadas, **When** qualquer participante vê o mapa, **Then** cada condição aparece como um ícone num selo circular colorido junto ao token, legível em qualquer nível de zoom, sem cobrir a barra de vida nem o anel de turno.
3. **Given** um token com mais condições do que cabem ao lado dele, **When** é exibido no mapa, **Then** aparecem os primeiros ícones e um selo "+N" indicando quantas condições a mais existem, e o nome de todas as condições fica disponível ao passar o mouse, focar ou ler com leitor de tela.
4. **Given** um token com a condição "Caído" ou "Inconsciente", **When** exibido, **Then** o ícone dessa condição é visualmente distinguível do selo de "derrotado (0 de vida)".
5. **Given** um participante que não controla o token e não é o mestre, **When** interage com o token, **Then** não consegue alterar as condições.

---

### User Story 3 - Mestre controla se o HP dos seus tokens é visível (Priority: P2)

O HP dos personagens dos jogadores aparece sobre os tokens deles e na lista de iniciativa para todos. Para os tokens do mestre (criaturas/NPCs e tokens representativos criados pelo mestre), o mestre escolhe, token a token ou de uma vez, se os jogadores veem a barra e os números de HP. O mestre sempre vê tudo.

**Why this priority**: O usuário relatou que o HP visível "desapareceu" e pediu que o mestre possa decidir a visibilidade. O HP do jogador volta a aparecer com a recuperação da 006 e a correção de sincronização; o controle do mestre é a novidade.

**Independent Test**: Com um combate ativo, um goblin com 7/7 de vida e um jogador com 20/24, o mestre oculta o HP do goblin. Na tela do jogador, a barra e o número do goblin somem (inclusive dos dados recebidos pelo navegador), a barra do jogador continua; o mestre continua vendo ambos. Reexibir e conferir que volta para todos.

**Acceptance Scenarios**:

1. **Given** um personagem de jogador com vida máxima definida, **When** qualquer participante vê o mapa ou a lista de iniciativa, **Then** a vida desse personagem é exibida.
2. **Given** o mestre abre o menu de uma criatura, **When** desliga "HP visível para jogadores", **Then** para os jogadores a barra de vida e os números de vida dessa criatura deixam de ser exibidos e deixam de ser enviados pelo servidor; para o mestre, a barra continua visível com um indicador de que está oculta aos jogadores.
3. **Given** várias criaturas no combate, **When** o mestre usa a ação rápida "Ocultar HP das criaturas" (ou "Mostrar"), **Then** todas as criaturas mudam de uma vez.
4. **Given** uma criatura com HP oculto que chega a 0 de vida, **When** os jogadores veem o mapa, **Then** eles continuam vendo o selo de "derrotado", mas não os números.
5. **Given** um jogador, **When** abre o menu do próprio token, **Then** não existe a opção de ocultar o HP do próprio personagem (o controle é do mestre e só para os tokens do mestre).

---

### User Story 4 - Névoa de guerra pintada pelo mestre (Priority: P2)

O mestre cobre partes do mapa com névoa e revela áreas conforme os aventureiros exploram. Ele pinta e apaga células com um pincel de tamanho ajustável, arrasta um retângulo para cobrir ou revelar uma sala inteira, ou cobre/revela o mapa todo com um botão. Os jogadores veem a névoa opaca e não veem criaturas escondidas sob ela; o mestre vê a névoa semitransparente para continuar enxergando o mapa.

**Why this priority**: É uma feature nova pedida pelo usuário, de grande valor para exploração, mas o combate funciona sem ela.

**Independent Test**: O mestre ativa a ferramenta de névoa, clica em "Cobrir tudo", arrasta um retângulo revelando uma sala e pinta mais três células com pincel 1x1. Na tela do jogador, só a sala e as três células aparecem; uma criatura posicionada na área coberta não aparece; ao revelar a área dela, ela aparece em poucos segundos.

**Acceptance Scenarios**:

1. **Given** o mestre com um battlemap publicado, **When** abre a barra de ferramentas do mapa, **Then** existe um modo "Névoa" com: ocultar/revelar, pincel (1x1, 3x3, 5x5), retângulo, "Cobrir tudo" e "Revelar tudo".
2. **Given** o modo névoa ativo com pincel, **When** o mestre pressiona e arrasta sobre o mapa, **Then** as células sob o pincel são cobertas (ou reveladas) enquanto ele arrasta, com pré-visualização imediata, e a alteração é salva ao soltar.
3. **Given** o modo névoa ativo com retângulo, **When** o mestre arrasta de uma célula a outra, **Then** é exibido o retângulo de seleção e, ao soltar, todas as células dentro dele são cobertas ou reveladas.
4. **Given** células cobertas, **When** um jogador vê o mapa, **Then** essas células aparecem totalmente opacas (sem mostrar a imagem do mapa), e criaturas ou tokens do mestre posicionados nelas não aparecem nem são enviados ao jogador.
5. **Given** células cobertas, **When** o mestre vê o mapa, **Then** elas aparecem semitransparentes, com os tokens visíveis, deixando claro o que os jogadores não veem.
6. **Given** o token de um jogador numa célula coberta, **When** qualquer participante vê o mapa, **Then** o token do jogador continua visível (a névoa não esconde os próprios aventureiros).
7. **Given** o modo névoa ativo, **When** o mestre usa dois dedos no celular ou a roda do mouse, **Then** pan e zoom continuam funcionando; tokens não são arrastados enquanto o modo névoa está ativo; ao sair do modo, tudo volta ao comportamento normal.
8. **Given** a névoa é alterada pelo mestre, **When** a alteração é salva, **Then** todos os participantes conectados veem a nova névoa em até 3 segundos, e quem reconectar vê o estado atual.

---

### Edge Cases

- Clique muito curto com micro movimento do mouse (1 a 4 px) sobre o token: é tratado como clique (abre o menu), não como arraste.
- Clique esquerdo num token que o participante não controla: nada acontece além de foco; não abre o menu e não inicia pan.
- O `click` que o navegador gera após soltar o botão não pode ser interpretado como "escolher célula de destino" do token recém-selecionado, independentemente do tamanho do token, de deslocamentos de empilhamento ou do ponto exato clicado.
- Criatura com HP oculto e sem vida máxima: nenhuma barra para ninguém, como antes.
- O mestre oculta o HP de uma criatura durante a preparação do combate: a preferência vale também quando o combate começa.
- Um token criado pelo mestre via "Inserir token" (representativo) não tem vida; a opção de HP não aparece para ele.
- Névoa num mapa cujo grid é redimensionado ao substituir a imagem: a névoa é descartada (mapa volta todo revelado) e o mestre é avisado no aviso de redução de grid já existente.
- Criatura parcialmente sob a névoa (token 3x3 com algumas células cobertas): fica visível para os jogadores se ao menos uma das células que ocupa está revelada.
- Área de efeito de uma criatura escondida pela névoa: também não é mostrada aos jogadores.
- O mestre arrasta o pincel para fora do mapa: as células fora dos limites são ignoradas.
- Conexão perdida enquanto o mestre pinta: a pintura em andamento é descartada visualmente ao reconectar e o estado do servidor prevalece; o mestre é avisado de que a alteração não foi salva.
- Muitas condições num token 1x1 com zoom baixo: no máximo 3 ícones mais "+N", com tamanho mínimo legível.

## Requirements *(mandatory)*

### Functional Requirements

**Menu do token e arraste**

- **FR-001**: O sistema DEVE abrir o menu do token com um clique do botão esquerdo (pressionar e soltar sem mover além de uma tolerância de poucos pixels) e mantê-lo aberto após soltar o botão.
- **FR-002**: O sistema NÃO DEVE interpretar o clique que encerra a seleção de um token como escolha de célula de destino, para tokens de qualquer tamanho e em qualquer ponto clicado do token.
- **FR-003**: O sistema DEVE mover o token quando o participante pressiona sobre ele e arrasta além da tolerância, soltando sobre outra célula, preservando o fluxo de arraste já existente (inclusive soltar fora do mapa para devolver à reserva).
- **FR-004**: O sistema DEVE iniciar arraste e seleção de token apenas com o botão principal (esquerdo ou toque); botões direito e do meio não arrastam tokens.
- **FR-005**: O sistema DEVE manter o fluxo "selecionar o token e depois clicar numa célula" para mover, e permitir fechar o menu clicando de novo no token, pelo botão "Concluir" ou pela tecla Esc.

**Condições**

- **FR-006**: O sistema DEVE oferecer, no mínimo, as condições: Caído, Atordoado, Paralisado, Envenenado, Sangrando, Imobilizado, Agarrado, Cego, Surdo, Invisível, Amedrontado, Enfeitiçado, Inconsciente, Petrificado, Incapacitado, Exausto, Concentrando e Em chamas.
- **FR-007**: Cada condição DEVE ter um ícone próprio e uma cor própria, iguais no menu e no mapa, e um nome acessível (texto alternativo e dica ao passar o mouse).
- **FR-008**: O sistema DEVE exibir sobre o token no mapa até 3 ícones de condição e um selo "+N" quando houver mais, com todas as condições listadas no nome acessível do token.
- **FR-009**: Os ícones de condição DEVEM manter tamanho mínimo legível em qualquer zoom e não cobrir a barra de vida, o anel de turno, o selo de derrotado ou a foto/iniciais do token além de uma borda.
- **FR-010**: As condições já gravadas pela spec 006 (atordoado, imobilizado, cego, envenenado, invisível, caído) DEVEM continuar válidas após a mudança.

**Visibilidade de HP**

- **FR-011**: A vida dos personagens de jogadores DEVE ser visível a todos os participantes, no mapa e na lista de iniciativa, e DEVE permanecer visível quando qualquer participante conecta ou desconecta.
- **FR-012**: O mestre DEVE poder definir, para cada criatura/NPC do combate, se a vida é visível aos jogadores, pelo menu do token no mapa e pela lista de iniciativa.
- **FR-013**: O mestre DEVE ter uma ação rápida para ocultar ou mostrar a vida de todas as criaturas do combate de uma vez.
- **FR-014**: Quando a vida de uma criatura está oculta, o servidor NÃO DEVE enviar a vida atual e máxima dessa criatura a jogadores (snapshot e eventos), mas DEVE continuar informando se ela está derrotada.
- **FR-015**: O mestre DEVE sempre ver a vida de todos, com um indicador visual nos tokens e itens de iniciativa cuja vida está oculta aos jogadores.
- **FR-016**: A visibilidade padrão da vida de uma criatura nova DEVE ser "visível", preservando o comportamento da spec 006.

**Névoa de guerra**

- **FR-017**: O mestre DEVE poder cobrir e revelar células do battlemap com pincel de 1x1, 3x3 ou 5x5 células, arrastando sobre o mapa, com pré-visualização durante o arraste.
- **FR-018**: O mestre DEVE poder cobrir ou revelar um retângulo de células arrastando de um canto ao outro.
- **FR-019**: O mestre DEVE poder cobrir ou revelar o mapa inteiro com uma ação.
- **FR-020**: A névoa DEVE ser persistida pelo servidor por battlemap e sincronizada em tempo real para todos os participantes, sobrevivendo a recarregamento e reconexão.
- **FR-021**: Para jogadores, células cobertas DEVEM ser totalmente opacas; criaturas e tokens criados pelo mestre cujas células ocupadas estão todas cobertas NÃO DEVEM ser exibidos nem enviados a jogadores, assim como suas áreas de efeito.
- **FR-022**: Tokens de personagens de jogadores e tokens representativos de jogadores DEVEM permanecer visíveis mesmo sob a névoa.
- **FR-023**: Para o mestre, células cobertas DEVEM ser semitransparentes, com tokens visíveis.
- **FR-024**: Enquanto o modo névoa está ativo, arrastar sobre o mapa DEVE pintar em vez de mover tokens ou o mapa; pan por dois dedos, roda do mouse e botões de zoom continuam funcionando.
- **FR-025**: Somente o mestre DEVE poder alterar a névoa; o servidor DEVE rejeitar alterações de jogadores.
- **FR-026**: Substituir a imagem do mapa com outro tamanho de grid DEVE descartar a névoa e avisar o mestre.

**Usabilidade e responsividade**

- **FR-027**: Todos os controles novos (condições, visibilidade de HP, ferramentas de névoa) DEVEM ter alvos de toque de no mínimo 44 px, ser operáveis por teclado e caber sem rolagem horizontal em telas de 360 px de largura.
- **FR-028**: Em celular, o menu do token e a barra de ferramentas da névoa NÃO DEVEM cobrir o mapa a ponto de impedir ver o token selecionado.

### Key Entities

- **Condição de status**: marca aplicada a um token, escolhida de um catálogo fixo; cada uma tem nome, ícone e cor. Um token tem zero ou mais.
- **Visibilidade de HP da criatura**: atributo de uma criatura/NPC do combate que indica se jogadores podem ver sua vida. O mestre sempre vê.
- **Névoa do battlemap**: conjunto de células cobertas de um battlemap, alterado só pelo mestre e consultado por todos; para jogadores, define o que do mapa e quais tokens do mestre ficam ocultos.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Em 100% dos cliques esquerdos curtos sobre um token controlável (1x1, 2x2, 5x5 e empilhado), o menu permanece aberto após soltar o botão, e em 100% dos arrastes o token é movido.
- **SC-002**: Um participante identifica corretamente o nome de uma condição marcada num token, só pelo ícone ou pela dica, em menos de 3 segundos.
- **SC-003**: Em 100% das respostas do servidor para jogadores, a vida de criaturas ocultas está ausente; na tela do mestre ela está presente.
- **SC-004**: A vida dos personagens de jogadores continua visível para todos após qualquer participante conectar ou desconectar (0 ocorrências de vida "sumindo").
- **SC-005**: O mestre cobre o mapa inteiro e revela uma sala retangular em menos de 10 segundos, e os jogadores veem a mudança em até 3 segundos.
- **SC-006**: Nenhuma criatura sob a névoa aparece na tela ou nos dados recebidos por um jogador.
- **SC-007**: Em viewports de 360 px, 768 px e 1280 px, todos os controles novos ficam acessíveis sem rolagem horizontal e com alvos de toque de 44 px ou mais.

## Assumptions

- "Tokens do mestre" são as criaturas/NPCs do combate e os tokens representativos criados pelo mestre. Tokens representativos não têm vida (spec 006), então o controle de visibilidade de HP vale só para criaturas.
- A visibilidade de HP é por criatura (não por battlemap), porque a vida pertence ao combatente e aparece também na lista de iniciativa.
- A névoa é por battlemap, em resolução de célula do grid. Um battlemap novo começa todo revelado.
- O mestre vê a névoa semitransparente; não há modo "ver como jogador" nesta versão.
- O selo de derrotado (0 de vida) e o anel de turno continuam visíveis para todos, mesmo com HP oculto; só o número e a barra são escondidos.
- Os ícones vêm do conjunto de ícones já usado no projeto, para manter a identidade visual.
- A "névoa" desta versão é manual (pintada pelo mestre), sem cálculo automático de linha de visão.
