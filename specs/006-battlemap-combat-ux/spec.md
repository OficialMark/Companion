# Feature Specification: Aprimoramentos de Combate no Battlemap

**Feature Branch**: `006-battlemap-combat-ux`

**Created**: 2026-09-25

**Status**: Draft

**Input**: User description: "durante o combate, deixe destacado de quem é o turno no battlemap, envolta do token, visível a todos os players; em cima dos tokens, deixe uma barra de hp com certa transparência, para isso, na aba inicial o jogador dirá a vida maxima e a vida atual, assim como o mestre pode editar isso ao inserir criaturas; mude um aspecto da abordagem: hoje, pra projetar uma area ou projetil, é necessario clicar com o botao direito, faça com que tambem apareça caso no pc o usuario de um clique unico como botao esquerdo do mouse no token — cada jogador também pode inserir tokens representativos, que podem ser reposicionados ou ter uma area de alcance, etc; no mesmo menu que se escolhe a area/projetil, o jogador tambem pode escolher o tamanho do seu token (1x1 a 5x5); nesse mesmo menu, o player também pode marcar o token com simbolos que representem atordoado, imobilizado, cego, etc; tudo deve ser usavel no celular e no pc, com interface agradavel, intuitiva e polida"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Ver de quem é o turno diretamente no battlemap (Priority: P1)

Durante o combate, qualquer participante olha para o battlemap e imediatamente identifica qual token pertence ao combatente da vez, sem precisar consultar a lista de iniciativa separadamente.

**Why this priority**: É a informação mais consultada durante o combate ("quem joga agora?") e a de menor complexidade de entrega — depende apenas de dados de turno que já existem, sendo puramente uma questão de tornar essa informação visível onde a atenção do jogador já está (o mapa).

**Independent Test**: Com um combate ativo e iniciativas já roladas, avançar o turno e conferir que o destaque visual ao redor do token muda imediatamente para o próximo combatente, em todas as telas conectadas à sessão.

**Acceptance Scenarios**:

1. **Given** um combate está ativo e é a vez de um combatente específico, **When** qualquer participante visualiza o battlemap, **Then** o token desse combatente exibe um destaque visual claro ao seu redor (ex.: contorno ou brilho), distinguindo-o de todos os outros tokens.
2. **Given** o destaque de turno está sendo exibido em um token, **When** o turno avança para o próximo combatente, **Then** o destaque desaparece do token anterior e passa a aparecer no token do novo combatente da vez, para todos os participantes conectados.
3. **Given** o combate ainda não começou (fase de configuração) ou já terminou, **When** um participante visualiza o battlemap, **Then** nenhum token exibe o destaque de turno.
4. **Given** um combatente da vez não possui um token posicionado no battlemap (ex.: ainda na reserva), **When** chega o turno dele, **Then** o sistema não gera erro e simplesmente não há destaque visível até que um token correspondente esteja posicionado no mapa.

---

### User Story 2 - Ver a vida de cada combatente acima do seu token (Priority: P1)

Durante o combate, qualquer participante olha para um token no battlemap e vê, através de uma barra semi-transparente logo acima dele, a proporção de vida atual em relação à vida máxima daquele combatente, sem precisar abrir outro painel.

**Why this priority**: É a segunda informação mais crítica durante o combate (quem está ferido, quem corre risco de cair) e reaproveita dados que já existem hoje; junto com o destaque de turno, forma o par de informações essenciais para acompanhar o combate direto do mapa.

**Independent Test**: Definir vida máxima e atual de um personagem na tela de configuração, publicar o battlemap, e conferir que a barra acima do token reflete a proporção correta; reduzir a vida atual e confirmar que a barra é atualizada para todos os participantes.

**Acceptance Scenarios**:

1. **Given** um jogador está na tela inicial/configuração da sessão, **When** ele informa a vida máxima e a vida atual do seu personagem, **Then** esses valores ficam associados ao combatente e disponíveis para exibição no battlemap.
2. **Given** o mestre está inserindo uma criatura/NPC no encontro, **When** ele define (ou edita) a vida máxima e a vida atual dessa criatura, **Then** esses valores ficam associados ao token da criatura da mesma forma que para um personagem de jogador.
3. **Given** um combatente com vida máxima e atual definidas possui um token posicionado no battlemap, **When** qualquer participante visualiza o mapa, **Then** uma barra de vida com certa transparência aparece acima do token, com preenchimento proporcional a vida atual dividida pela vida máxima.
4. **Given** a vida atual de um combatente muda (por dano ou cura), **When** a mudança é confirmada, **Then** a barra de vida acima do token correspondente é atualizada para refletir a nova proporção, para todos os participantes conectados.
5. **Given** um combatente sem vida máxima definida (valor não informado), **When** seu token é exibido no battlemap, **Then** o sistema não exibe uma barra de vida quebrada ou incorreta para esse token (oculta a barra ou exibe um estado neutro claro, sem erro visual).

---

### User Story 3 - Escolher o tamanho do próprio token no menu de interação (Priority: P2)

Um jogador (ou o mestre, para qualquer token) abre o menu de interação de um token que controla e escolhe entre os tamanhos 1x1, 2x2, 3x3, 4x4 ou 5x5 células do grid, fazendo o token ocupar visualmente essa área no battlemap.

**Why this priority**: Depende do menu de interação do token já existente (usado hoje para configurar área/projétil) apenas ganhar uma nova opção; sem isso, criaturas grandes continuam representadas do mesmo tamanho que uma pequena, o que prejudica a leitura tática do combate, mas não bloqueia as histórias de prioridade mais alta.

**Independent Test**: Selecionar um token controlável, abrir seu menu de interação, escolher o tamanho "3x3" e confirmar que o token passa a ocupar visualmente uma área de 3 por 3 células no grid, centrada ou ancorada de forma consistente à sua posição, e que essa mudança aparece para os demais participantes.

**Acceptance Scenarios**:

1. **Given** um jogador ou o mestre abre o menu de interação de um token que controla, **When** o menu é exibido, **Then** uma opção de tamanho de token aparece com as escolhas 1x1, 2x2, 3x3, 4x4 e 5x5.
2. **Given** o menu de tamanho está aberto, **When** o usuário escolhe um tamanho diferente do atual, **Then** o token passa a ser desenhado ocupando essa quantidade de células no grid, de forma consistente para todos os participantes.
3. **Given** um token de tamanho maior que 1x1 é movido pelo battlemap, **When** o movimento é confirmado, **Then** o token continua ocupando a mesma quantidade de células a partir de sua nova posição, sem deformar ou recortar sua área.
4. **Given** um jogador tenta abrir o menu de tamanho em um token que não controla e não é o mestre, **When** ele interage com o token, **Then** a opção de alterar tamanho não fica disponível para ele.

---

### User Story 4 - Marcar condições de status no token (Priority: P2)

Um jogador ou o mestre, no mesmo menu de interação do token, marca ou desmarca símbolos representando condições comuns de RPG (ex.: atordoado, imobilizado, cego) que se aplicam ao combatente, e esses símbolos passam a aparecer junto ao token no battlemap.

**Why this priority**: Assim como o tamanho do token, é uma extensão do menu de interação já existente; entrega valor tático real (lembrar quem está sob qual condição) mas não é tão crítica quanto ver turno e vida.

**Independent Test**: Abrir o menu de interação de um token controlável, marcar a condição "atordoado", confirmar que um símbolo correspondente aparece junto ao token para todos os participantes, depois desmarcar a condição e confirmar que o símbolo desaparece.

**Acceptance Scenarios**:

1. **Given** um jogador ou o mestre abre o menu de interação de um token que controla, **When** o menu é exibido, **Then** uma lista de condições de status comuns (ex.: atordoado, imobilizado, cego, envenenado, invisível, caído) aparece com controles para marcar/desmarcar cada uma.
2. **Given** o menu de condições está aberto, **When** o usuário marca uma ou mais condições, **Then** um símbolo correspondente a cada condição marcada aparece junto ao token no battlemap, visível a todos os participantes.
3. **Given** um token já possui uma ou mais condições marcadas, **When** o usuário desmarca uma delas no menu, **Then** o símbolo correspondente deixa de aparecer junto ao token, para todos os participantes.
4. **Given** um token possui várias condições marcadas ao mesmo tempo, **When** ele é exibido no battlemap, **Then** todos os símbolos das condições ativas permanecem legíveis e não cobrem informações essenciais do token (como a barra de vida ou o destaque de turno).

---

### User Story 5 - Jogador insere tokens representativos adicionais (Priority: P2)

Um jogador insere no battlemap um ou mais tokens adicionais próprios (por exemplo, para representar um familiar, uma invocação ou um marcador de posição), reposiciona esses tokens livremente e, quando aplicável, configura uma área de alcance para eles, da mesma forma que faria com seu personagem principal.

**Why this priority**: Expande quem pode ter tokens no mapa além do pino principal do jogador e das criaturas do mestre; é um novo tipo de token, então depende conceitualmente das histórias de menu de interação (tamanho, condições, área) para ser consistente, mas não é o núcleo da entrega de visibilidade de combate.

**Independent Test**: Como jogador, inserir um novo token representativo no battlemap, arrastá-lo para uma célula do grid, configurar uma área de alcance para ele pelo menu de interação, e confirmar que outros participantes veem o token, sua posição e sua área normalmente.

**Acceptance Scenarios**:

1. **Given** um jogador está participando de uma sessão com battlemap publicado, **When** ele opta por inserir um novo token representativo próprio, **Then** um novo token sob seu controle aparece no battlemap (inicialmente na reserva ou em uma posição padrão), distinto do seu pino de personagem principal.
2. **Given** um jogador possui um token representativo próprio, **When** ele o arrasta (ou seleciona e escolhe uma célula, no toque) para uma célula válida do grid, **Then** o token se move para essa célula e a nova posição é vista pelos demais participantes.
3. **Given** um jogador possui um token representativo próprio, **When** ele abre o menu de interação desse token, **Then** ele pode configurar formato de área/projétil, tamanho e condições de status para esse token, da mesma forma que para seu personagem principal.
4. **Given** um jogador tenta mover ou configurar um token representativo que pertence a outro jogador, **When** ele interage com esse token, **Then** a ação não é permitida (a menos que o usuário seja o mestre).
5. **Given** um jogador não precisa mais de um token representativo, **When** ele opciona removê-lo, **Then** o token e qualquer área associada a ele desaparecem do battlemap para todos os participantes.

---

### User Story 6 - Abrir o menu de interação do token com um único clique ou toque, em qualquer dispositivo (Priority: P3)

Um participante, seja usando mouse em um computador ou toque em um celular/tablet, abre o menu de interação de um token (área/projétil, tamanho, condições) com um único clique do botão esquerdo (PC) ou um único toque (touch), sem precisar de cliques com botão direito, atalhos de teclado ou gestos compostos.

**Why this priority**: Garante que a expansão do menu (histórias 3 e 4) não introduza, sem querer, a necessidade de um gesto diferente por dispositivo; é uma validação de consistência mais do que uma entrega isolada de valor, por isso a prioridade mais baixa.

**Independent Test**: Em uma tela de combate, em viewport de desktop, clicar uma única vez com o botão esquerdo do mouse sobre um token controlável e confirmar que o menu de interação abre; repetir com um único toque em viewport de celular e confirmar o mesmo resultado.

**Acceptance Scenarios**:

1. **Given** um participante está usando um computador com mouse, **When** ele dá um único clique com o botão esquerdo sobre um token que controla, **Then** o menu de interação do token (área/projétil, tamanho, condições) abre normalmente.
2. **Given** um participante está usando um celular ou tablet, **When** ele dá um único toque sobre um token que controla, **Then** o mesmo menu de interação abre normalmente, com controles utilizáveis por toque.
3. **Given** o menu de interação de um token está aberto, **When** o participante toca ou clica fora do menu, **Then** o menu se fecha mantendo as últimas escolhas confirmadas (área desenhada, tamanho, condições marcadas).

---

### Edge Cases

- O que acontece se dois combatentes tiverem, por engano, a marca de "turno atual" simultaneamente? O sistema deve garantir que apenas um destaque de turno seja exibido por vez em todo o battlemap.
- O que acontece com o destaque de turno e as barras de vida quando o battlemap é trocado ou reiniciado durante o combate? Ambos devem ser recalculados a partir do estado atual do encontro, sem manter destaques ou barras de uma configuração anterior.
- O que acontece se a vida atual de um combatente ultrapassar a vida máxima (ex.: cura acima do teto) ou ficar negativa? A barra de vida deve ser exibida de forma limitada visualmente entre 0% e 100%, sem extrapolar o comprimento da barra.
- O que acontece se um token de tamanho 4x4 ou 5x5 for posicionado próximo à borda do grid? O sistema deve permitir a posição, mesmo que parte do token fique visualmente fora da área jogável, sem travar ou distorcer o token, de forma consistente com o comportamento já existente para áreas de ataque que excedem os limites visíveis.
- O que acontece se dois tokens de tamanhos diferentes se sobrepõem no grid? Ambos continuam visíveis e selecionáveis, mantendo a mesma tolerância a sobreposição já existente para tokens de tamanho padrão.
- O que acontece se um jogador tentar marcar uma condição de status em um token que não controla (e não é o mestre)? A marcação não é permitida.
- O que acontece se um combatente com condições marcadas for removido do encontro ou tiver o token excluído? As condições associadas são removidas junto com o token, sem deixar símbolos órfãos.
- O que acontece se o jogador perder a conexão momentaneamente enquanto reposiciona um token representativo? O token retorna à última posição confirmada, seguindo o mesmo comportamento já existente para os demais pinos do battlemap.
- O que acontece se o número de tokens representativos inseridos por jogadores tornar o battlemap visualmente poluído? Cada token representativo continua individualmente selecionável e identificável, mas o sistema não impõe um limite específico nesta versão (ver Assunções).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: O sistema DEVE exibir, para todo token de um combatente cujo turno esteja em andamento durante um combate ativo, um destaque visual claro ao redor desse token, visível a todos os participantes da sessão.
- **FR-002**: O sistema DEVE atualizar o destaque de turno imediatamente quando o turno avança, removendo-o do token do combatente anterior e aplicando-o ao token do novo combatente da vez, refletido para todos os participantes.
- **FR-003**: O sistema NÃO DEVE exibir o destaque de turno quando o combate não estiver ativo (fase de configuração ou combate encerrado).
- **FR-004**: O sistema DEVE permitir que um jogador informe a vida máxima e a vida atual do seu personagem na tela inicial/de configuração da sessão.
- **FR-005**: O sistema DEVE permitir que o mestre defina e edite a vida máxima e a vida atual de qualquer combatente (personagem de jogador ou criatura/NPC), incluindo no momento em que insere uma criatura no encontro.
- **FR-006**: O sistema DEVE exibir, acima de cada token cujo combatente tenha vida máxima definida, uma barra de vida com transparência parcial, preenchida proporcionalmente à razão entre vida atual e vida máxima.
- **FR-007**: O sistema DEVE atualizar a barra de vida de um token, para todos os participantes, sempre que a vida atual do combatente correspondente for alterada.
- **FR-008**: O sistema DEVE limitar visualmente o preenchimento da barra de vida entre 0% e 100%, independentemente de a vida atual ultrapassar a vida máxima ou ficar negativa.
- **FR-009**: O sistema NÃO DEVE exibir uma barra de vida para tokens cujo combatente não possua vida máxima definida.
- **FR-010**: O sistema DEVE permitir abrir o menu de interação de um token (o mesmo menu hoje usado para configurar área/projétil) por meio de um único clique do botão esquerdo do mouse (em desktop) ou um único toque (em dispositivos touch), sem exigir clique com botão direito, atalhos de teclado ou gestos compostos.
- **FR-011**: O sistema DEVE incluir, no menu de interação do token, uma opção para escolher o tamanho do token entre 1x1, 2x2, 3x3, 4x4 e 5x5 células do grid, disponível para o mesmo público autorizado a configurar área/projétil daquele token (o próprio jogador para tokens que controla, ou o mestre para qualquer token).
- **FR-012**: O sistema DEVE redesenhar o token para ocupar visualmente a quantidade de células correspondente ao tamanho escolhido, mantendo essa proporção ao ser movido pelo battlemap.
- **FR-013**: O sistema DEVE incluir, no mesmo menu de interação do token, uma lista de condições de status comuns (no mínimo: atordoado, imobilizado, cego, envenenado, invisível, caído) que podem ser marcadas ou desmarcadas para aquele token.
- **FR-014**: O sistema DEVE exibir, junto ao token no battlemap, um símbolo distinto para cada condição de status atualmente marcada, visível a todos os participantes, e DEVE remover o símbolo correspondente quando a condição for desmarcada.
- **FR-015**: O sistema DEVE permitir que múltiplas condições de status estejam marcadas simultaneamente no mesmo token, exibindo todos os símbolos ativos de forma legível e sem cobrir a barra de vida ou o destaque de turno desse token.
- **FR-016**: O sistema DEVE permitir que um jogador insira um ou mais tokens representativos adicionais próprios no battlemap, distintos do seu pino de personagem principal.
- **FR-017**: O sistema DEVE permitir que o jogador reposicione livremente cada token representativo que insere, da mesma forma (arrastar, ou selecionar e escolher célula em toque) que já é permitido para o pino de personagem.
- **FR-018**: O sistema DEVE permitir que o menu de interação (área/projétil, tamanho, condições) seja aberto e configurado também para os tokens representativos inseridos por um jogador, com a mesma regra de autorização: o dono do token ou o mestre.
- **FR-019**: O sistema DEVE permitir que um jogador remova um token representativo que inseriu, removendo também qualquer área de alcance associada a ele.
- **FR-020**: O sistema DEVE restringir a movimentação, configuração de menu (área, tamanho, condições) e remoção de um token representativo a seu dono (o jogador que o inseriu) ou ao mestre da sessão.
- **FR-021**: O sistema DEVE garantir que toda a interação descrita nesta especificação (destaque de turno, barra de vida, menu de interação do token, tamanho, condições, tokens representativos) seja utilizável tanto em viewports de celular quanto de desktop, com controles adequados a toque e a mouse/teclado.

### Key Entities

- **Destaque de Turno (Turn Highlight)**: Indicação visual, derivada do combatente cujo turno está em andamento em um combate ativo, aplicada ao token correspondente no battlemap. Não é um dado armazenado à parte — reflete o estado de turno já existente do encontro.
- **Vida do Combatente (Combatant Health)**: Vida máxima e vida atual associadas a um combatente (personagem de jogador ou criatura/NPC). A vida atual já existe hoje; a vida máxima é um atributo novo necessário para calcular a proporção exibida na barra de vida.
- **Barra de Vida (Health Bar)**: Representação visual, semi-transparente, posicionada acima de um token, cujo preenchimento é derivado da razão entre vida atual e vida máxima do combatente associado àquele token.
- **Tamanho do Token (Token Size)**: Atributo de um token indicando quantas células do grid (1x1 a 5x5) ele ocupa visualmente ao ser desenhado e movimentado no battlemap.
- **Condição de Status (Status Condition)**: Marcação aplicável a um token, escolhida entre um conjunto de condições comuns de RPG (atordoado, imobilizado, cego, envenenado, invisível, caído, entre outras), exibida como um símbolo junto ao token enquanto estiver marcada. Um token pode ter zero ou mais condições simultaneamente.
- **Token Representativo (Representative Token)**: Token adicional inserido por um jogador no battlemap, distinto do seu pino de personagem principal, que pode ser reposicionado e configurado (área, tamanho, condições) pelo mesmo jogador ou pelo mestre.
- **Menu de Interação do Token**: Painel único, acionado por um clique/toque no token, que reúne a configuração de área/projétil (já existente), o tamanho do token e as condições de status marcadas, restrito a quem controla aquele token ou ao mestre.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Em 100% das mudanças de turno durante um combate ativo, o destaque visual aparece no token correto e desaparece do anterior em até 3 segundos para todos os participantes conectados.
- **SC-002**: Um participante consegue identificar, olhando apenas para o battlemap (sem abrir outro painel), de quem é o turno atual e a proporção aproximada de vida de qualquer token com vida máxima definida.
- **SC-003**: Uma alteração na vida atual de um combatente é refletida na barra de vida do token correspondente, para os demais participantes, em até 3 segundos.
- **SC-004**: Um jogador ou o mestre consegue abrir o menu de interação de um token controlável com uma única ação (um clique ou um toque) em menos de 2 segundos, tanto em desktop quanto em celular.
- **SC-005**: 100% das tentativas de configurar tamanho, condições, área ou posição de um token por alguém que não é o dono nem o mestre são bloqueadas, sem exceções.
- **SC-006**: Um jogador consegue inserir um token representativo próprio, posicioná-lo no grid e configurar uma condição de status nele em menos de 30 segundos, sem assistência externa.
- **SC-007**: Em telas de celular, o menu de interação do token (área, tamanho, condições) permanece totalmente utilizável por toque, sem exigir zoom manual ou rolagem lateral para alcançar algum controle.

## Assumptions

- "Durante o combate", para efeito do destaque de turno e da restrição de exibição (FR-003), corresponde ao encontro estar no estado ativo de combate (após os participantes começarem oficialmente a lutar, com iniciativa rolada e turnos em andamento) — distinto do modo de visualização "Modo Jogo" já existente na aplicação, que é apenas uma preferência de layout de tela.
- A configuração de área/projétil já é hoje acionada por um único clique/toque no token (não existe atualmente um gatilho por botão direito do mouse a ser substituído). Esta especificação assume que o pedido de "também funcionar com um clique único de botão esquerdo" se refere a preservar e garantir esse comportamento de clique/toque único já existente à medida que o menu de interação do token ganha as novas opções de tamanho e condições — e não à criação de um segundo gatilho paralelo.
- O conjunto mínimo de condições de status desta versão é: atordoado, imobilizado, cego, envenenado, invisível e caído. Outras condições podem ser adicionadas em versões futuras sem impacto nesta especificação.
- Um token pode ter, no máximo, uma configuração de área/projétil ativa por vez (comportamento já existente), independentemente do seu tamanho ou das condições marcadas.
- Tokens representativos inseridos por jogadores não participam automaticamente da lista de iniciativa/combatentes nem recebem vida máxima/atual por padrão; eles são marcadores livres (posição, tamanho, condições, área). Caso um jogador ou o mestre queira que um token representativo tenha vida e participe da iniciativa, isso é tratado inserindo-o como uma criatura/combatente pelo mestre, fora do escopo desta funcionalidade.
- Não há, nesta versão, um limite máximo de tokens representativos por jogador; a organização visual do battlemap com muitos tokens fica a critério do grupo.
- O tamanho do token é sempre ancorado a partir da célula de origem do token (ex.: um token 3x3 ocupa a célula selecionada e as células adjacentes em uma direção consistente), sem suporte, nesta versão, para reancorar manualmente qual célula representa o "centro" do token.
- A barra de vida e o destaque de turno seguem o mesmo modelo de sincronização em tempo real já usado pelas demais informações do battlemap (posições, áreas de ataque): mudanças refletidas para todos os participantes conectados em poucos segundos, sem necessidade de recarregar a página.
