# Feature Specification: Acesso, Administração, Iniciativa e Vida em Combate

**Feature Branch**: `002-campaign-access-initiative`

**Created**: 2026-08-23

**Status**: Draft

**Input**: Adicionar código oculto de mestre, limitar códigos de acesso a quatro dígitos numéricos, disponibilizar um painel administrativo oculto em `/admin`, integrar iniciativa e vida dos jogadores à preparação do combate e permitir ataques que apliquem o resultado de dados à vida de um ou mais alvos em tempo real.

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

### User Story 2 - Preparar iniciativa e vida a partir dos jogadores presentes (Priority: P1)

Como jogador, quero informar meu modificador de iniciativa e minha vida atual na própria mesa para que o mestre possa incluir rapidamente os participantes presentes quando preparar um combate.

**Why this priority**: Remove a redigitação dos jogadores, de seus modificadores e de suas vidas pelo mestre, reduzindo erros e o tempo entre o início do encontro e a primeira rodada.

**Independent Test**: Três jogadores conectados informam modificadores e vidas diferentes; o mestre abre a preparação e rola as iniciativas; os três aparecem uma única vez com seus valores, enquanto um participante desconectado não é incluído automaticamente.

**Acceptance Scenarios**:

1. **Given** um jogador dentro da mesa, **When** informa ou altera seu modificador de iniciativa e sua vida no componente ao final da página, **Then** os valores ficam associados ao próprio participante e são recuperados após atualizar a página.
2. **Given** um participante comum, **When** tenta editar diretamente a iniciativa ou a vida de outro participante fora do fluxo de ataque, **Then** a operação é recusada.
3. **Given** jogadores online com modificadores e vidas informados, **When** o mestre solicita a rolagem de iniciativa, **Then** cada jogador online é adicionado uma única vez à preparação com seu nome atual, seu modificador e sua vida.
4. **Given** um jogador online sem modificador informado, **When** o mestre solicita a rolagem, **Then** ele é adicionado com modificador zero.
5. **Given** combatentes manuais e jogadores já adicionados, **When** o mestre solicita nova rolagem geral, **Then** nenhum combatente é duplicado e os combatentes manuais são preservados.
6. **Given** um combatente de jogador cujo nome, modificador ou vida foi editado pelo mestre naquela preparação, **When** ocorre uma nova rolagem, **Then** a edição do mestre é preservada.
7. **Given** a lista preenchida automaticamente, **When** o mestre edita, remove ou acrescenta combatentes antes de iniciar o combate, **Then** a ordem e as vidas usam a lista revisada pelo mestre.
8. **Given** um combatente sem vida informada, **When** o mestre tenta iniciar o combate, **Then** o sistema identifica o campo pendente e solicita um valor válido antes de prosseguir.

---

### User Story 3 - Atacar um ou mais alvos durante o combate (Priority: P1)

Como personagem participante ou mestre, quero rolar o dano de um ataque e selecionar um ou mais jogadores ou criaturas para que o resultado seja descontado da vida de cada alvo.

**Why this priority**: Transformar a rolagem em alteração efetiva da vida é o fluxo central do combate solicitado.

**Independent Test**: Durante um combate com três alvos, um jogador rola dano, seleciona dois deles e confirma; cada alvo selecionado perde exatamente o total rolado, o terceiro não muda e o mestre acompanha os novos valores.

**Acceptance Scenarios**:

1. **Given** um combate ativo, **When** um jogador aciona `Atacar`, conclui uma rolagem de dano válida, seleciona um ou mais combatentes e confirma, **Then** o total é subtraído uma vez da vida de cada alvo selecionado.
2. **Given** um combate ativo, **When** o mestre aciona `Atacar`, **Then** pode concluir a mesma jornada e selecionar qualquer jogador ou criatura do combate.
3. **Given** um ataque cujo dano excede a vida restante de um alvo, **When** o ataque é confirmado, **Then** a vida desse alvo passa a zero e nunca fica negativa.
4. **Given** uma rolagem concluída, **When** o autor cancela ou não seleciona alvos, **Then** nenhuma vida é alterada.
5. **Given** um participante fora do turno atual, **When** aciona `Atacar`, **Then** a ação continua disponível porque ataques não são restringidos pela ordem de turnos nesta feature.
6. **Given** um alvo cuja vida mudou depois da rolagem e antes da confirmação do ataque, **When** o ataque é confirmado, **Then** o dano é aplicado sobre a vida confirmada mais recente e nenhum ajuste concorrente é perdido.

---

### User Story 4 - Alterar e acompanhar a vida em tempo real (Priority: P1)

Como jogador, quero alterar e acompanhar minha própria vida durante o combate, enquanto o mestre acompanha e ajusta a vida de todos os combatentes, para que o estado compartilhado permaneça correto após dano, cura ou correção.

**Why this priority**: O grupo precisa confiar no mesmo estado de vida sem expor a vida de cada personagem aos demais jogadores.

**Independent Test**: Em três navegadores, um jogador altera a própria vida e depois recebe dano; seu navegador e o do mestre mostram os novos valores imediatamente, enquanto o outro jogador não recebe o valor de vida alheio.

**Acceptance Scenarios**:

1. **Given** um jogador vinculado a um combatente, **When** altera sua própria vida durante o combate, **Then** o novo valor confirmado aparece para ele e para o mestre.
2. **Given** um mestre em um combate ativo, **When** altera a vida de qualquer jogador ou criatura, **Then** o valor confirmado é atualizado para o mestre e, se houver jogador vinculado, para esse jogador.
3. **Given** um jogador comum, **When** acompanha o combate, **Then** vê sua própria vida, mas não os valores de vida dos demais jogadores ou criaturas.
4. **Given** dano, cura ou correção confirmada, **When** os participantes autorizados estão conectados, **Then** recebem o estado atualizado sem recarregar a página.
5. **Given** um participante que ficou desconectado durante mudanças de vida, **When** retorna ao combate, **Then** recupera a vida atual que tem autorização para ver, sem depender das notificações perdidas.

---

### User Story 5 - Administrar mesas existentes (Priority: P2)

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
- Um jogador deixa a vida em branco, informa zero, tenta usar número negativo, decimal ou excessivamente grande.
- Dois ataques ou uma edição de vida e um ataque são confirmados quase simultaneamente sobre o mesmo alvo.
- Um alvo é removido ou deixa de ser elegível entre a rolagem e a confirmação do ataque.
- O autor fecha ou atualiza a página depois de rolar, mas antes de confirmar os alvos.
- Uma rolagem de ataque totaliza zero ou um valor negativo por causa de modificadores.
- O mesmo alvo é selecionado mais de uma vez antes da confirmação.
- Um jogador tenta consultar a vida de outro combatente por uma tela, snapshot, notificação ou mensagem de erro.

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
- **FR-017**: Cada jogador MUST poder informar e atualizar somente o próprio modificador de iniciativa dentro do intervalo permitido para combatentes e a própria vida como número inteiro entre 0 e 99.999.
- **FR-018**: O modificador de iniciativa e a vida do jogador MUST permanecer associados à sua participação na mesa entre atualizações de página e reconexões.
- **FR-019**: A área para editar o próprio modificador e a própria vida MUST aparecer somente para jogadores, ao final da página principal da mesa, e ser utilizável por toque e teclado.
- **FR-020**: Ao rolar a iniciativa geral, o sistema MUST incluir automaticamente na preparação todos os participantes online que ainda não estejam vinculados a um combatente.
- **FR-021**: Um participante importado MUST ser vinculado ao combatente correspondente por identidade estável, sem depender apenas do nome de exibição.
- **FR-022**: Participantes sem modificador informado MUST ser importados com modificador zero.
- **FR-023**: Combatentes manuais e valores de iniciativa ou vida previamente editados pelo mestre MUST ser preservados durante importações e novas rolagens gerais.
- **FR-024**: O mestre MUST poder editar, remover e acrescentar combatentes importados antes ou durante o combate, incluindo modificar a vida de qualquer jogador ou criatura.
- **FR-025**: Importações ou comandos concorrentes MUST produzir no máximo um combatente por participante em cada encontro.
- **FR-026**: Alterações de códigos, modificadores, vidas, mesas e encontros MUST ser confirmadas pelo sistema antes de serem apresentadas como concluídas aos usuários conectados.
- **FR-027**: Ao importar um jogador para a preparação, o sistema MUST copiar sua vida informada para o combatente correspondente sem substituir edições posteriores do mestre naquele encontro.
- **FR-028**: Todo combatente MUST possuir uma vida inteira entre 0 e 99.999 antes que o combate possa ser iniciado e antes que possa ser escolhido como alvo de ataque.
- **FR-029**: Durante um combate ativo, o jogador MUST poder alterar a vida somente do combatente vinculado à sua identidade, e o mestre MUST poder alterar a vida de qualquer combatente.
- **FR-030**: O sistema MUST permitir que jogadores vinculados a combatentes e mestres iniciem a ação `Atacar` a qualquer momento do combate, independentemente do turno atual.
- **FR-031**: Um ataque MUST usar uma rolagem com um único total final; quando o total for menor que zero, o dano aplicado MUST ser zero.
- **FR-032**: Após concluir a rolagem de ataque, o autor MUST poder selecionar um ou mais combatentes válidos, revisar o total e os alvos e confirmar ou cancelar a aplicação do dano.
- **FR-033**: Ao confirmar um ataque, o sistema MUST subtrair uma única vez o total de dano da vida confirmada mais recente de cada alvo distinto e MUST limitar cada vida ao mínimo de zero.
- **FR-034**: A confirmação de um ataque MUST alterar todos os alvos selecionados como uma única ação; se qualquer alvo não for mais válido, nenhuma vida pode ser alterada e o autor MUST revisar a seleção.
- **FR-035**: Reenvios da mesma confirmação de ataque MUST produzir no máximo uma aplicação de dano por alvo.
- **FR-036**: O sistema MUST registrar para cada ataque o autor, a rolagem, o total aplicado, os alvos distintos, a vida anterior e posterior de cada alvo e o horário da confirmação.
- **FR-037**: Cada jogador MUST receber e visualizar somente a própria vida; o mestre MUST receber e visualizar a vida de todos os jogadores e criaturas do combate.
- **FR-038**: Nomes e disponibilidade dos alvos MUST continuar visíveis para seleção de ataque sem revelar aos jogadores a vida dos combatentes que não controlam.
- **FR-039**: Alterações de vida e ataques concorrentes sobre o mesmo alvo MUST ser aplicados sobre o estado confirmado mais recente, sem perda silenciosa de mudanças.
- **FR-040**: Após qualquer alteração de vida, os participantes conectados MUST receber sem recarregar o estado atualizado que têm autorização para ver, e uma reconexão MUST recuperar o mesmo estado por meio do snapshot atual.
- **FR-041**: A apresentação de um ataque aos jogadores MUST mostrar autor, rolagem, dano e nomes dos alvos, mas MUST omitir as vidas anterior e posterior de qualquer combatente que o jogador não controla.
- **FR-042**: O formulário de perfil de combate MUST apresentar iniciativa, vida e ação de salvamento sem sobreposição ou desalinhamento tanto em desktop quanto em telas móveis.
- **FR-043**: Após dano confirmado, todos os participantes conectados MUST visualizar uma animação breve junto a cada alvo mostrando somente a quantidade efetivamente reduzida, sem revelar sua vida.
- **FR-044**: Na visão do jogador, o modificador de iniciativa MUST possuir separação visual suficiente em relação ao nome do personagem.
- **FR-045**: Ao rerrolar todas as iniciativas durante um combate ativo, o turno MUST passar para o combatente com a maior iniciativa da nova ordem; rerrolar somente um combatente MUST preservar o turno atual.
- **FR-046**: A animação de dano MUST ser descartada ao terminar e não pode reaparecer devido a reordenação ou atualização posterior do encontro.
- **FR-047**: Combatentes com zero PV MUST receber tonalidade vermelha visível a todos, expondo somente o estado de PV zero e preservando a privacidade do valor numérico para observadores não autorizados.

### Key Entities

- **Campaign Access Code**: Código de quatro dígitos associado a uma única mesa e a um papel específico de jogador ou mestre; possui finalidade confidencial e deixa de ser válido quando a mesa é excluída.
- **Administrative Credential**: Segredo externo às mesas usado pelo operador da instalação para abrir a área administrativa; não concede participação automática em nenhuma mesa.
- **Campaign**: Mesa existente com nome, criação, códigos de acesso, participantes, presença e dados de jogo relacionados.
- **Participant**: Identidade de uma pessoa dentro da mesa, incluindo papel, nome de exibição, presença, modificador de iniciativa e vida próprios quando tiver papel de jogador.
- **Encounter Combatant**: Entrada da iniciativa que pode ser manual ou vinculada a um participante, com nome, modificador, resultado, vida atual e possíveis edições específicas daquele encontro.
- **Combat Attack**: Ação confirmada durante um encontro, ligada ao autor e à rolagem de dano, com total aplicado, alvos distintos e vidas anterior e posterior de cada alvo.
- **Administrative Audit Event**: Registro de tentativa recusada ou exclusão confirmada sem conteúdo secreto.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Em 100% dos cenários automatizados, códigos válidos possuem exatamente quatro dígitos, os dois códigos de uma mesa são diferentes e não colidem com códigos de outras mesas existentes.
- **SC-002**: Um mestre consegue recuperar acesso privilegiado em um navegador novo usando somente nome de exibição e código reservado em menos de 30 segundos.
- **SC-003**: Nenhum cenário automatizado de jogador comum expõe o código de mestre, dados administrativos ou ações destrutivas.
- **SC-004**: O operador autenticado localiza qualquer mesa e seus dois códigos no painel administrativo em menos de 20 segundos com até 100 mesas existentes.
- **SC-005**: Uma mesa excluída deixa de aceitar 100% das sessões, códigos e comandos anteriores imediatamente após a confirmação da exclusão.
- **SC-006**: Com cinco jogadores online, o mestre prepara todos na lista de iniciativa, com suas vidas informadas, em uma única ação e em menos de 10 segundos, sem entradas duplicadas.
- **SC-007**: Em 100% dos cenários de atualização e reconexão, o modificador e a vida salvos pelo jogador reaparecem corretamente; somente o próprio jogador altera esses valores fora do encontro, enquanto o mestre pode editar a cópia usada no encontro.
- **SC-008**: A importação automática preserva 100% dos combatentes manuais e das edições de iniciativa e vida feitas pelo mestre nos cenários de nova rolagem.
- **SC-009**: As jornadas de entrada, edição de iniciativa e vida, ataque e administração são concluídas nos viewports suportados sem rolagem horizontal e com controles de toque de pelo menos 44 por 44 pixels.
- **SC-010**: Pelo menos 90% de usuários sem treinamento conseguem distinguir qual código compartilhar e qual manter reservado na primeira criação de mesa.
- **SC-011**: Em condições normais de conexão, 95% das alterações de vida confirmadas ficam visíveis aos usuários autorizados em até um segundo.
- **SC-012**: Um jogador ou mestre conclui uma rolagem de ataque, seleciona até cinco alvos e confirma o dano em menos de 30 segundos, sem treinamento prévio.
- **SC-013**: Em 100% dos cenários automatizados, cada ataque confirmado aplica o total exatamente uma vez a cada alvo distinto, nunca produz vida negativa e não altera alvos não selecionados.
- **SC-014**: Em 100% dos cenários de autorização, jogadores não visualizam nem editam diretamente a vida de combatentes alheios, embora possam causar dano pelo fluxo confirmado de ataque; o mestre visualiza e edita todos os combatentes.

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
- A vida informada na página principal representa a vida atual do jogador no momento da preparação, não uma ficha completa nem um valor separado de vida máxima.
- Vida em branco na página principal significa “não informada”; o mestre deve preenchê-la na preparação antes de iniciar o combate. Vida zero é um valor válido.
- A cópia da vida usada no encontro é independente da preferência inicial: mudanças durante o combate não são levadas automaticamente para encontros futuros.
- Jogadores podem selecionar qualquer combatente válido como alvo de ataque, mas continuam sem visualizar a vida dos alvos que não controlam.
- O total integral de uma única rolagem é aplicado igualmente a cada alvo; divisão de dano, resistências, vulnerabilidades, cura automática, acertos críticos e tipos de dano ficam fora desta feature.
- A edição direta da vida cobre dano, cura e correções manuais; o histórico específico exigido nesta feature é o dos ataques confirmados.
- Uma rolagem de ataque ainda não confirmada não altera estado compartilhado e pode ser abandonada sem efeito.
