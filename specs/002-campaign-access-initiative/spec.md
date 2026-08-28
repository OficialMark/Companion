# Feature Specification: Acesso, Administração e Iniciativa da Campanha

**Feature Branch**: `002-campaign-access-initiative`

**Created**: 2026-08-23

**Status**: Draft

**Input**: Adicionar código oculto de mestre, limitar códigos de acesso a quatro dígitos numéricos, disponibilizar um painel administrativo oculto em `/admin` e integrar os modificadores de iniciativa dos jogadores à preparação do combate.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Entrar como jogador ou mestre pelo código correto (Priority: P1)

Como responsável por uma mesa, quero receber um código reservado de mestre separado do código compartilhado com jogadores para poder recuperar meu papel de mestre em outro navegador sem conceder esse papel aos demais participantes.

**Why this priority**: A distinção segura entre jogador e mestre protege todas as operações privilegiadas e remove a dependência da sessão original usada para criar a mesa.

**Independent Test**: Criar uma mesa, anotar os dois códigos, entrar em navegadores limpos com cada código e verificar que ambos chegam à mesma mesa, mas somente o código de mestre concede as ações exclusivas desse papel.

**Acceptance Scenarios**:

1. **Given** uma mesa recém-criada, **When** a criação é concluída, **Then** o criador recebe um código de jogador e um código de mestre distintos, ambos formados por exatamente quatro dígitos.
2. **Given** um visitante com o código de jogador, **When** informa o código e um nome de exibição válido, **Then** entra na mesa com papel de jogador.
3. **Given** um visitante com o código reservado de mestre, **When** informa o código e um nome de exibição válido, **Then** entra na mesma mesa com papel de mestre.
4. **Given** um participante que entrou pelo código de jogador, **When** consulta qualquer área normal da mesa, **Then** o código de mestre não é exibido nem incluído nos dados disponíveis a esse participante.
5. **Given** um código com letras, menos ou mais de quatro dígitos ou inexistente, **When** alguém tenta entrar, **Then** nenhuma sessão é criada e a mensagem não revela se a tentativa se aproximou de um código de jogador, mestre ou administrador.
6. **Given** várias mesas existentes, **When** uma nova mesa é criada, **Then** seus dois códigos não coincidem entre si nem com qualquer código de acesso de outra mesa existente.

---

### User Story 2 - Preparar iniciativa a partir dos jogadores presentes (Priority: P1)

Como jogador, quero informar meu modificador de iniciativa na própria mesa para que o mestre possa incluir rapidamente os participantes presentes quando preparar um combate.

**Why this priority**: Remove a redigitação dos jogadores e seus modificadores pelo mestre, reduzindo erros e o tempo entre o início do encontro e a primeira rodada.

**Independent Test**: Três jogadores conectados informam modificadores diferentes; o mestre abre a preparação e rola as iniciativas; os três aparecem uma única vez com seus modificadores, enquanto um participante desconectado não é incluído automaticamente.

**Acceptance Scenarios**:

1. **Given** um jogador dentro da mesa, **When** informa ou altera seu modificador no componente ao final da página, **Then** o valor fica associado ao próprio participante e é recuperado após atualizar a página.
2. **Given** um participante comum, **When** tenta alterar o modificador de outro participante, **Then** a operação é recusada.
3. **Given** jogadores online com modificadores informados, **When** o mestre solicita a rolagem de iniciativa, **Then** cada jogador online é adicionado uma única vez à preparação com seu nome atual e seu modificador.
4. **Given** um jogador online sem modificador informado, **When** o mestre solicita a rolagem, **Then** ele é adicionado com modificador zero.
5. **Given** combatentes manuais e jogadores já adicionados, **When** o mestre solicita nova rolagem geral, **Then** nenhum combatente é duplicado e os combatentes manuais são preservados.
6. **Given** um combatente de jogador cujo nome ou modificador foi editado pelo mestre naquela preparação, **When** ocorre uma nova rolagem, **Then** a edição do mestre é preservada.
7. **Given** a lista preenchida automaticamente, **When** o mestre edita, remove ou acrescenta combatentes antes de iniciar o combate, **Then** a ordem usa a lista revisada pelo mestre.

---

### User Story 3 - Administrar mesas existentes (Priority: P2)

Como operador da instalação, quero acessar uma área administrativa reservada para localizar mesas, recuperar seus códigos e excluir mesas que não devem mais permanecer armazenadas.

**Why this priority**: Permite suporte e limpeza operacional sem expor controles destrutivos aos participantes normais.

**Independent Test**: Com duas mesas existentes, autenticar-se em `/admin`, visualizar ambas e seus códigos, excluir uma mediante confirmação e verificar que ela desaparece da administração e que suas sessões e códigos deixam de funcionar.

**Acceptance Scenarios**:

1. **Given** um visitante sem credencial administrativa válida, **When** abre `/admin` ou tenta consultar mesas, **Then** nenhuma informação de mesa ou código é revelada.
2. **Given** o operador com credencial administrativa válida, **When** acessa `/admin`, **Then** vê todas as mesas existentes com nome, criação, quantidade de participantes, situação de presença, código de jogador e código de mestre.
3. **Given** uma lista com várias mesas, **When** o operador a consulta, **Then** consegue distinguir rapidamente mesas com participantes online das mesas sem presença atual.
4. **Given** uma mesa selecionada para exclusão, **When** o operador inicia a ação, **Then** o sistema exige confirmação explícita identificando a mesa afetada.
5. **Given** uma exclusão confirmada, **When** ela é concluída, **Then** a mesa, seus dados relacionados, suas sessões e seus códigos deixam de estar disponíveis, e participantes conectados são informados de que a mesa foi encerrada.
6. **Given** uma exclusão cancelada ou malsucedida, **When** o operador retorna à lista, **Then** a mesa e seus dados permanecem inalterados e uma mensagem adequada é apresentada.

### Edge Cases

- O espaço de códigos de quatro dígitos está próximo do esgotamento e não há dois códigos livres para criar uma nova mesa.
- Códigos contendo zeros à esquerda são digitados, copiados ou colados e devem preservar os quatro dígitos.
- O mesmo nome de exibição é usado por mais de um participante da mesa.
- O mestre entra novamente pelo código reservado enquanto outra sessão de mestre continua conectada.
- Muitas tentativas inválidas de código ou credencial administrativa são realizadas em sequência.
- A credencial administrativa está ausente ou inválida na configuração da instalação.
- A mesa é excluída enquanto há participantes conectados, rolagem em andamento ou combate ativo.
- Um jogador altera o modificador exatamente quando o mestre solicita a rolagem de iniciativa.
- Um jogador fica offline ou online durante a preparação da iniciativa.
- Dois jogadores têm o mesmo nome, mas modificadores e identidades diferentes.
- Um combatente manual tem o mesmo nome de um jogador importado.
- O mestre remove um jogador da preparação e solicita uma nova rolagem geral.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: O sistema MUST gerar para cada mesa um código de jogador e um código de mestre distintos.
- **FR-002**: Cada código de acesso MUST conter exatamente quatro caracteres numéricos, preservando zeros à esquerda.
- **FR-003**: Os códigos de jogador e mestre MUST ser globalmente únicos entre todas as mesas existentes e MUST identificar simultaneamente a mesa e o papel concedido.
- **FR-004**: O sistema MUST atribuir papel de jogador a quem entrar pelo código de jogador e papel de mestre a quem entrar pelo código de mestre.
- **FR-005**: O sistema MUST apresentar os dois códigos ao criador da mesa, destacando que o código de mestre é reservado e não deve ser compartilhado com jogadores.
- **FR-006**: O sistema MUST impedir que jogadores obtenham o código de mestre por telas, snapshots, notificações ou mensagens de erro da área comum.
- **FR-007**: O sistema MUST recusar formatos inválidos e códigos inexistentes sem revelar a finalidade ou a existência parcial do código tentado.
- **FR-008**: O sistema MUST limitar tentativas repetidas de entrada e de autenticação administrativa para reduzir descoberta automatizada de códigos curtos.
- **FR-009**: O sistema MUST recusar a criação de uma nova mesa com mensagem compreensível quando não houver dois códigos livres, sem invalidar códigos existentes.
- **FR-010**: O sistema MUST disponibilizar em `/admin` uma área não anunciada na navegação normal e protegida por credencial administrativa própria da instalação.
- **FR-011**: A ausência ou invalidade da credencial administrativa MUST impedir acesso a qualquer dado ou ação administrativa.
- **FR-012**: O painel administrativo MUST listar todas as mesas existentes e indicar nome, data de criação, quantidade de participantes, presença atual, código de jogador e código de mestre.
- **FR-013**: O painel administrativo MUST permitir excluir permanentemente uma mesa somente após confirmação explícita que identifique a mesa.
- **FR-014**: A exclusão de uma mesa MUST remover seus participantes, rolagens, encontros e demais dados dependentes, invalidar suas sessões e códigos e impedir qualquer novo acesso.
- **FR-015**: Participantes conectados a uma mesa excluída MUST receber indicação de encerramento e não podem continuar emitindo comandos para ela.
- **FR-016**: O sistema MUST registrar tentativas administrativas recusadas e exclusões confirmadas sem registrar códigos, credenciais ou outros segredos em texto legível.
- **FR-017**: Cada participante MUST poder informar e atualizar somente o próprio modificador de iniciativa dentro do intervalo permitido para combatentes.
- **FR-018**: O modificador de iniciativa do participante MUST permanecer associado à sua participação na mesa entre atualizações de página e reconexões.
- **FR-019**: A área para editar o próprio modificador MUST aparecer ao final da página principal da mesa e ser utilizável por toque e teclado.
- **FR-020**: Ao rolar a iniciativa geral, o sistema MUST incluir automaticamente na preparação todos os participantes online que ainda não estejam vinculados a um combatente.
- **FR-021**: Um participante importado MUST ser vinculado ao combatente correspondente por identidade estável, sem depender apenas do nome de exibição.
- **FR-022**: Participantes sem modificador informado MUST ser importados com modificador zero.
- **FR-023**: Combatentes manuais e combatentes previamente editados pelo mestre MUST ser preservados durante importações e novas rolagens gerais.
- **FR-024**: O mestre MUST poder editar, remover e acrescentar combatentes importados antes ou durante o combate conforme as regras existentes de edição e nova rolagem.
- **FR-025**: Importações ou comandos concorrentes MUST produzir no máximo um combatente por participante em cada encontro.
- **FR-026**: Alterações de códigos, modificadores, mesas e encontros MUST ser confirmadas pelo sistema antes de serem apresentadas como concluídas aos usuários conectados.

### Key Entities

- **Campaign Access Code**: Código de quatro dígitos associado a uma única mesa e a um papel específico de jogador ou mestre; possui finalidade confidencial e deixa de ser válido quando a mesa é excluída.
- **Administrative Credential**: Segredo externo às mesas usado pelo operador da instalação para abrir a área administrativa; não concede participação automática em nenhuma mesa.
- **Campaign**: Mesa existente com nome, criação, códigos de acesso, participantes, presença e dados de jogo relacionados.
- **Participant**: Identidade de uma pessoa dentro da mesa, incluindo papel, nome de exibição, presença e modificador de iniciativa próprio.
- **Encounter Combatant**: Entrada da iniciativa que pode ser manual ou vinculada a um participante, com nome, modificador, resultado e possíveis edições específicas daquele encontro.
- **Administrative Audit Event**: Registro de tentativa recusada ou exclusão confirmada sem conteúdo secreto.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Em 100% dos cenários automatizados, códigos válidos possuem exatamente quatro dígitos, os dois códigos de uma mesa são diferentes e não colidem com códigos de outras mesas existentes.
- **SC-002**: Um mestre consegue recuperar acesso privilegiado em um navegador novo usando somente nome de exibição e código reservado em menos de 30 segundos.
- **SC-003**: Nenhum cenário automatizado de jogador comum expõe o código de mestre, dados administrativos ou ações destrutivas.
- **SC-004**: O operador autenticado localiza qualquer mesa e seus dois códigos no painel administrativo em menos de 20 segundos com até 100 mesas existentes.
- **SC-005**: Uma mesa excluída deixa de aceitar 100% das sessões, códigos e comandos anteriores imediatamente após a confirmação da exclusão.
- **SC-006**: Com cinco jogadores online, o mestre prepara todos na lista de iniciativa em uma única ação e em menos de 10 segundos, sem entradas duplicadas.
- **SC-007**: Em 100% dos cenários de atualização e reconexão, o modificador salvo pelo jogador reaparece corretamente; somente o próprio participante altera essa preferência, enquanto o mestre pode editar apenas a cópia usada no encontro.
- **SC-008**: A importação automática preserva 100% dos combatentes manuais e das edições feitas pelo mestre nos cenários de nova rolagem.
- **SC-009**: As jornadas de entrada, edição do modificador e administração são concluídas nos viewports suportados sem rolagem horizontal e com controles de toque de pelo menos 44 por 44 pixels.
- **SC-010**: Pelo menos 90% de usuários sem treinamento conseguem distinguir qual código compartilhar e qual manter reservado na primeira criação de mesa.

## Assumptions

- Uma mesa existente é considerada ativa para fins administrativos enquanto não tiver sido excluída; a presença online é mostrada separadamente.
- Códigos podem começar com zero e códigos de mesas excluídas podem ser reutilizados após a exclusão ser concluída.
- O criador continua entrando automaticamente como mestre ao criar a mesa, além de receber o código reservado para acessos futuros.
- Mais de uma sessão de mestre pode existir simultaneamente para a mesma mesa quando todas usam o código reservado correto.
- A credencial administrativa é um segredo único da instalação, configurado pelo operador fora da interface comum e diferente dos códigos de mesa.
- A rota `/admin` ser oculta da navegação reduz descoberta casual, mas a proteção real depende da credencial administrativa e do limite de tentativas.
- Exclusão administrativa é permanente e não inclui lixeira ou restauração nesta feature.
- “Jogadores na sessão” significa participantes online no momento em que o mestre solicita a rolagem geral de iniciativa.
- O valor informado pelo jogador é uma preferência da participação; depois de importado, o combatente torna-se uma cópia específica do encontro e edições do mestre têm precedência.
- Jogadores que ficarem online depois da importação não entram automaticamente até uma nova ação de rolagem geral do mestre.
- Monstros, NPCs e participantes offline continuam podendo ser adicionados manualmente pelo mestre.
