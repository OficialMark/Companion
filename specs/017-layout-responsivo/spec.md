# Feature Specification: Layout Responsivo por Princípios de Design

**Feature Branch**: `OficialMark/design-responsivo-6fc489fd`\
**Created**: 2026-11-27\
**Status**: Implementada\
**Input**: User description: "O uso no celular está com muitos bugs visuais, com sobreposições na tela. Rode uma bateria de testes e reviews para avaliar se o design segue hierarquia visual, contraste, proximidade e alinhamento: a tela larga do computador deve ser aproveitada para dispor melhor as funcionalidades, e a tela restrita do celular deve conseguir usar tudo sem bugs visuais. Com base na review, altere a interface."

## User Scenarios & Testing

### User Story 1 - Jogar no celular sem nada sobreposto (Priority: P1)

Como jogador ou mestre no celular, quero usar todas as áreas (Mesa, Ficha, Compêndio, Campanha) sem botões cobertos, texto cortado ou rolagem lateral, com a navegação ao alcance do polegar.

**Why this priority**: Era o problema relatado: o código da mesa cobria a aba "Campanha", o nome da campanha virava "Ruí na...", a página estourava 82 px para o lado com o compêndio aberto.

**Independent Test**: Abrir cada área em 360 e 390 px como mestre e como jogador e verificar que nenhum controle está coberto, nada rola para o lado e todo toque tem pelo menos 44×44 px.

**Acceptance Scenarios**:

1. **Given** o celular, **When** a Mesa abre, **Then** o mapa é o primeiro painel, a rolagem vem logo abaixo, e as áreas ficam numa barra de abas fixa embaixo.
2. **Given** o jogador no celular, **When** está na Mesa, **Then** o resumo da ficha (PV e CA) fica colado à barra de abas, formando um único bloco inferior de no máximo 100 px.
3. **Given** a barra superior no celular, **When** a tela abre, **Then** ela tem 2 linhas (logo e ações; nome da campanha e código) sem sobreposição.
4. **Given** as 5 abas da Ficha, **When** a tela tem 360 px ou mais, **Then** todas aparecem numa linha, sem nenhuma escondida.
5. **Given** qualquer controle focado ou rolado automaticamente, **When** a página rola, **Then** ele para acima da barra inferior e abaixo da superior.

---

### User Story 2 - Aproveitar a tela larga (Priority: P1)

Como mestre ou jogador num monitor largo, quero que o mapa ocupe o centro e que as outras funções fiquem ao lado, não numa faixa estreita com margens vazias.

**Why this priority**: Em 1920 px o conteúdo usava 72% da largura e 54% em 2560, com o mapa abaixo da dobra.

**Independent Test**: Abrir a Mesa em 1280, 1920 e 2560 px e verificar as colunas, o tamanho do mapa e a largura usada.

**Acceptance Scenarios**:

1. **Given** 1600 px ou mais, **When** a Mesa abre, **Then** aparecem três colunas. A rolagem fica à esquerda, o mapa no centro e, à direita, Participantes acima do Histórico, seguidos pelo combate.
2. **Given** 1100 a 1599 px, **When** a Mesa abre, **Then** a rolagem fica à esquerda. A coluna larga tem o mapa e, abaixo dele, Participantes, Histórico e combate. A partir de 1280 px, o jogador tem essas três áreas em uma coluna própria à direita do mapa.
3. **Given** as colunas laterais, **When** a página rola, **Then** elas acompanham e rolam por dentro, com sombra indicando que há mais conteúdo.
4. **Given** um mapa pequeno numa tela grande, **When** ele abre, **Then** preenche o palco (até 80 px por célula) e acompanha o palco enquanto ele cresce, até o usuário tocar no mapa.
5. **Given** a Ficha a partir de 1440 px, **When** abre, **Then** fica em 2 colunas: personagem, combate, atributos e valores-base | perícias, conjuração e anotações.
6. **Given** 1920 ou 2560 px, **When** a Mesa ou a Ficha abre, **Then** o conteúdo usa pelo menos 80% da largura. O Compêndio e a Campanha ficam centralizados e limitados a 1380 px.

---

### User Story 3 - Hierarquia e estados claros (Priority: P2)

Como usuário, quero saber de relance qual é a ação principal, o que está desabilitado e por quê.

**Why this priority**: O primário desabilitado parecia habilitado; depois, tracejado neutro, parecia um botão secundário qualquer.

**Independent Test**: Abrir a rolagem do jogador sem dados escolhidos e verificar o botão "Rolar" e a dica.

**Acceptance Scenarios**:

1. **Given** um botão desabilitado, **When** aparece, **Then** é distinguível por contorno tracejado e falta de preenchimento, não só pela cor; o primário mantém um traço do seu tom.
2. **Given** "Rolar" desabilitado por falta de dados, **When** aparece, **Then** uma dica diz "Escolha os dados ou digite uma expressão para rolar."
3. **Given** a Mesa do mestre, **When** a preparação de combate está aberta, **Then** a ordem de iniciativa aparece acima do catálogo de criaturas.
4. **Given** a barra superior abaixo de 1440 px, **When** os rótulos não cabem, **Then** "Código do mestre", "Modo Jogo" e "Conectado" viram ícones com nome acessível, e o nome da campanha tem pelo menos 100 px.

---

### User Story 4 - Garantia automática contra regressões de layout (Priority: P2)

Como mantenedor, quero um teste automático que falhe quando uma mudança causar sobreposição, rolagem lateral, alvo pequeno ou baixo contraste em qualquer largura.

**Why this priority**: Os bugs relatados passaram por todos os testes existentes.

**Independent Test**: Rodar o auditor de layout contra a instância de validação e ver o relatório e as capturas.

**Acceptance Scenarios**:

1. **Given** o auditor, **When** roda, **Then** percorre 8 telas (mestre e jogador) em 7 larguras, de 360 a 2560 px.
2. **Given** uma violação, **When** o auditor roda, **Then** falha nomeando a tela, a largura e o elemento.

---

### User Story 5 - Ficha que não perde o "Salvo" nem mudanças de outro aparelho (Priority: P3)

Como jogador, quero ver "Salvo" depois de editar e receber mudanças feitas em outro aparelho mesmo se chegarem durante uma gravação.

**Why this priority**: Encontrado pelos testes ponta a ponta durante esta review: o eco da própria gravação apagava o "Salvo", e uma mudança remota durante a gravação era descartada.

**Independent Test**: Editar a ficha e aguardar o eco do próprio salvamento; editar em outro aparelho durante uma gravação.

**Acceptance Scenarios**:

1. **Given** uma edição salva, **When** chega o aviso de mudança da própria gravação, **Then** o indicador continua "Salvo".
2. **Given** uma gravação em andamento, **When** chega uma mudança de outro aparelho, **Then** ela é carregada assim que a gravação termina.

### Edge Cases

- Tela abaixo de 360 px: as abas da Ficha rolam para o lado, com degradê indicando mais conteúdo.
- Telas com menos de 700 px de altura: as colunas laterais não ficam fixas (rolam com a página).
- Combate em andamento: o painel de preparação some; reforços entram pelo switch do seletor do compêndio (spec 016).
- O usuário deu zoom ou arrastou o mapa: redimensionar a janela não reenquadra mais.
- Barra superior em 2 linhas (abaixo de 1280 px): as colunas fixas começam abaixo dela.

## Requirements

### Functional Requirements

- **FR-001**: A Mesa MUST se organizar em três zonas (rolagem, mapa, pessoas), em uma coluna abaixo de 1100 px, duas colunas até 1279 px para o jogador ou até 1599 px para o mestre e três colunas nas larguras maiores. Em duas ou três colunas, a rolagem fica à esquerda e Participantes aparece acima do Histórico na zona da direita.
- **FR-002**: No celular a navegação entre áreas MUST ficar numa barra de abas inferior fixa, com alvos de pelo menos 44 px.
- **FR-003**: Nenhuma tela MUST ter rolagem horizontal de página, controle coberto por outro elemento (fora de modais e barras fixas sobre conteúdo rolável) ou controle fora da tela.
- **FR-004**: Abaixo de 768 px todo controle MUST ter área de toque de pelo menos 44×44 px.
- **FR-005**: Texto MUST ter contraste de pelo menos 4.5:1 (3:1 se grande).
- **FR-006**: Em 1920 e 2560 px a Mesa e a Ficha MUST usar pelo menos 80% da largura. O Compêndio e a Campanha MUST ter margens simétricas e largura máxima de 1380 px.
- **FR-007**: O nome da campanha MUST aparecer na barra superior com pelo menos 100 px em todas as larguras.
- **FR-008**: O mapa MUST abrir ajustado ao palco (células de 16 a 80 px) e acompanhar o palco até a primeira interação do usuário.
- **FR-009**: Foco e rolagem automática MUST parar fora das barras fixas.
- **FR-010**: Botões desabilitados MUST se distinguir por forma, não só por cor; "Rolar" desabilitado MUST explicar o que falta.
- **FR-011**: Um auditor automático MUST verificar FR-003 a FR-007 e abas fora da lista em 8 telas × 7 larguras, falhando com o elemento culpado.
- **FR-012**: O indicador "Salvo" MUST sobreviver ao eco da própria gravação; mudanças remotas recebidas durante uma gravação MUST ser carregadas ao fim dela.

### Key Entities

Nenhuma entidade de dados nova.

## Success Criteria

### Measurable Outcomes

- **SC-001**: 0 controles cobertos, 0 px de rolagem lateral e 0 alvos abaixo de 44 px nas 56 combinações de tela e largura (antes: código sobre "Campanha" em todas as telas de celular, 82 px de rolagem lateral com o compêndio aberto, alvos de 40 px).
- **SC-002**: Na Mesa e na Ficha, largura usada em 1920 px de pelo menos 95% e em 2560 px de pelo menos 90%. No Compêndio e na Campanha, margens simétricas e largura máxima de 1380 px.
- **SC-003**: Num crítico cego comparando antes e depois, a versão nova vence todas as telas avaliadas.
- **SC-004**: O bloco inferior fixo do jogador no celular ocupa no máximo 12% da altura de uma tela de 844 px (antes: 14% em dois blocos soltos).

## Assumptions

- As larguras de referência são 360, 390, 768, 1024, 1280, 1920 e 2560 px.
- O tema escuro e a identidade visual (cores, fontes) se mantêm; a mudança é de layout, hierarquia e estados.
- A ordem do celular (mapa, rolagem, combate, participantes, histórico) prioriza a cena, como pediu a review de hierarquia.
