# Feature Specification: Tokens do Compêndio e de Itens no Battlemap

**Feature Branch**: `OficialMark/tokens-compendio-item-6fc489fd`\
**Created**: 2026-11-27\
**Status**: Implementada (commit f1a2810)\
**Input**: User description: "Possibilite que os jogadores também possam adicionar no mapa um token do compêndio, além disso também possibilite adicionar um token de um item."

## User Scenarios & Testing

### User Story 1 - Jogador coloca uma criatura do compêndio no mapa (Priority: P1)

Como jogador, quero buscar uma criatura no compêndio e colocá-la no mapa como token, com a imagem e o tamanho corretos, para representar um familiar, uma montaria ou uma invocação sem depender do mestre.

**Why this priority**: É o pedido central. Hoje o jogador só cria tokens de texto livre, sem imagem nem tamanho.

**Independent Test**: Entrar como jogador, abrir "Do compêndio" no mapa, buscar "ogro", adicionar e confirmar que o token aparece na reserva com o nome e a imagem do ogro e ocupa 2x2 ao ser posicionado.

**Acceptance Scenarios**:

1. **Given** um mapa publicado, **When** o jogador abre "Do compêndio", **Then** vê a aba Criaturas com busca, e cada resultado mostra nome, tamanho, tipo e imagem.
2. **Given** a lista de criaturas, **When** o jogador clica em "Adicionar" numa criatura Grande, **Then** um token com o nome da criatura entra na reserva, pertence ao jogador e ocupa 2x2 no mapa.
3. **Given** o token adicionado, **When** o jogador o seleciona e escolhe uma célula, **Then** ele é posicionado e todos os participantes o veem em tempo real.
4. **Given** o token adicionado, **When** o jogador abre o painel do token, **Then** pode mudar tamanho, condições, área de ataque e removê-lo, como em qualquer token próprio.
5. **Given** o token de criatura de um jogador, **When** outro jogador tenta movê-lo, **Then** a ação é recusada.

---

### User Story 2 - Qualquer participante coloca um item no mapa (Priority: P1)

Como jogador ou mestre, quero colocar um item do catálogo no mapa (corda, baú, poção caída) para marcar objetos da cena.

**Why this priority**: Objetos no mapa são parte da narrativa e hoje só existem como texto livre sem distinção visual.

**Independent Test**: Abrir "Do compêndio", trocar para a aba Itens, buscar "corda", adicionar e confirmar que o token aparece como quadrado arredondado.

**Acceptance Scenarios**:

1. **Given** a aba Itens, **When** o participante busca, **Then** cada resultado mostra nome, categoria (com ícone) e raridade.
2. **Given** um item, **When** o participante clica em "Adicionar", **Then** um token com o nome do item entra na reserva, pertence a quem adicionou e ocupa 1x1.
3. **Given** um token de item no mapa, **When** alguém o vê, **Then** o formato é um quadrado arredondado, distinto do círculo usado para seres.

---

### User Story 3 - Mestre escolhe entre token simples e combate (Priority: P2)

Como mestre, quero decidir se a criatura entra só como token (cenário, NPC sem ficha em jogo) ou no combate (com iniciativa e PV), para não poluir a ordem de turnos.

**Why this priority**: O fluxo antigo do mestre (adicionar ao combate) continua necessário; a novidade é poder colocar só o token.

**Independent Test**: Como mestre, abrir "Do compêndio", ver "Só token" e "No combate"; com "No combate" aparece o seletor antigo com quantidade.

**Acceptance Scenarios**:

1. **Given** o mestre na aba Criaturas, **When** escolhe "Só token", **Then** a criatura entra como token sem combatente e sem iniciativa.
2. **Given** o mestre na aba Criaturas, **When** escolhe "No combate", **Then** vê o seletor que adiciona de 1 a 20 combatentes ao encontro.
3. **Given** o mestre, **When** busca criaturas em "Só token", **Then** vê também as criaturas próprias da mesa.

---

### User Story 4 - Segredos do mestre não vazam (Priority: P1)

Como mestre, quero que a busca de criaturas dos jogadores mostre só o compêndio público e nenhuma ficha, para que minhas criaturas próprias e as estatísticas não sejam reveladas.

**Why this priority**: Abrir a busca para jogadores não pode quebrar a privacidade que a biblioteca de criaturas tinha (só mestre).

**Independent Test**: O mestre cria a criatura "Zumbaraque Secreto"; o jogador busca "zumbaraque" e não a encontra; tentar adicioná-la pelo identificador retorna "não encontrada".

**Acceptance Scenarios**:

1. **Given** uma criatura própria do mestre, **When** um jogador busca por ela, **Then** ela não aparece.
2. **Given** o identificador dessa criatura, **When** um jogador tenta criar um token com ele, **Then** a mesa responde "Criatura não encontrada no compêndio".
3. **Given** a busca de um jogador, **When** os resultados chegam, **Then** não contêm pontos de vida, classe de armadura nem outros dados da ficha.

---

### Edge Cases

- Nome da entrada maior que 40 caracteres: o nome do token é cortado na última palavra inteira dentro dos 40 caracteres; sem espaço na segunda metade, corta em 40.
- Participante informa nome próprio junto com a origem: o nome informado prevalece (ex.: "Corda do poço").
- Pedido com criatura e item ao mesmo tempo: recusado com mensagem de validação.
- Criatura ou item inexistente (ou de outra mesa): recusado como "não encontrado".
- Criatura removida ou excluída depois de o token ser colocado: o token permanece com o nome, sem imagem.
- Sem conexão: os botões "Adicionar" ficam desabilitados com aviso "Conexão necessária para adicionar".
- Falha ao adicionar: a mensagem do servidor aparece e a lista continua visível.
- Névoa de guerra: tokens de criatura ou item do mestre seguem a mesma regra dos tokens do mestre (escondidos sob névoa); tokens de jogador sempre visíveis.

## Requirements

### Functional Requirements

- **FR-001**: Todo participante MUST conseguir abrir, a partir do mapa, um seletor "Do compêndio" com abas Criaturas e Itens.
- **FR-002**: A busca de criaturas MUST aceitar texto sem acento e paginar, mostrando nome, tamanho, tipo e imagem.
- **FR-003**: Para jogadores, a busca de criaturas MUST listar apenas o compêndio público ativo; para o mestre, também as criaturas da mesa.
- **FR-004**: A busca de criaturas MUST NOT expor dados da ficha (PV, CA, atributos, ações).
- **FR-005**: Um token criado a partir de criatura MUST herdar o nome, a imagem e o tamanho (Grande 2x2, Enorme 3x3, Imensa 4x4).
- **FR-006**: Um token criado a partir de item MUST herdar o nome e ser desenhado como quadrado arredondado.
- **FR-007**: Tokens do compêndio e de itens MUST entrar na reserva, pertencer a quem os criou e seguir as mesmas permissões dos tokens representativos (dono e mestre controlam).
- **FR-008**: Tokens do compêndio e de itens MUST NOT entrar na ordem de iniciativa.
- **FR-009**: O nome padrão MUST caber em 40 caracteres, cortando em fronteira de palavra quando possível; um nome informado MUST prevalecer.
- **FR-010**: O sistema MUST recusar um pedido com criatura e item ao mesmo tempo e entradas inexistentes ou fora do alcance do participante.
- **FR-011**: O mestre MUST poder escolher entre "Só token" e "No combate" na aba Criaturas.
- **FR-012**: Tokens de texto livre MUST continuar funcionando como antes.
- **FR-013**: Criar ou remover um token MUST atualizar o mapa de todos os participantes em tempo real.

### Key Entities

- **Token representativo**: token colocado por um participante. Atributos: nome, dono, origem (texto livre, criatura do compêndio ou item do catálogo) e a entrada de origem.
- **Opção de criatura para token**: visão reduzida de uma criatura (nome, tamanho, tipo, imagem) usada só para escolher o token.

## Success Criteria

### Measurable Outcomes

- **SC-001**: Um jogador coloca uma criatura do compêndio no mapa em até 4 interações (abrir, buscar, adicionar, posicionar).
- **SC-002**: 100% dos tokens de criatura Grande ou maior aparecem com o tamanho da ficha sem ajuste manual.
- **SC-003**: 0 criaturas próprias do mestre aparecem na busca de jogadores (verificado por teste automatizado).
- **SC-004**: Em telas de 360 px de largura, a lista de resultados não gera rolagem horizontal e todos os botões têm área de toque de 44 px.

## Assumptions

- O compêndio público de criaturas (fonte autorizada já usada na biblioteca) pode ser consultado por jogadores; só as fichas continuam restritas ao mestre.
- Itens do catálogo não têm imagem própria; o token usa as iniciais do nome e o formato quadrado.
- A edição feita pelo mestre numa criatura do compêndio não muda o que o jogador vê na busca: o jogador usa a entrada pública.
- O mestre continua usando "No combate" quando quer PV e iniciativa; "Só token" é para cena.
