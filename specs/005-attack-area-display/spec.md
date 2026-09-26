# Feature Specification: Exibição de Área de Ataque no Battlemap

**Feature Branch**: `005-attack-area-display`

**Created**: 2026-09-12

**Status**: Draft

**Input**: User description: "mostrar ataques no mapa" — cada jogador pode selecionar um formato de área (cilindro, cone, círculo, quadrado) para representar o alcance de um ataque/efeito, configurar distância/raio e largura via sliders compactos e intuitivos (mobile e desktop), desenhar a área sobre o battlemap, reconfigurar ao clicar na área, com áreas visíveis para todos os participantes da sessão. Jogadores configuram apenas para seus próprios personagens; o mestre configura para qualquer personagem/criatura. Além disso, corrigir bug: o editor de vida (HP) e iniciativa continua aparecendo durante o modo de combate, quando deveria aparecer apenas na tela inicial (setup).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Configurar área de ataque no próprio personagem (Priority: P1)

Um jogador, durante o combate, clica no token do seu personagem no battlemap e escolhe um formato de área (círculo, quadrado, cone ou cilindro) para visualizar o alcance de um ataque ou habilidade. Ele ajusta a distância/raio (e a largura, se for cone/cilindro) usando sliders, e a área é desenhada sobre o mapa na posição do personagem.

**Why this priority**: É o núcleo da funcionalidade — sem isso não há valor entregue. Um único jogador configurando sua própria área já é útil de forma independente.

**Independent Test**: Em uma sessão de combate, selecionar o próprio token, escolher "círculo", ajustar o raio para 2 blocos e confirmar que um círculo semi-transparente de raio 2 blocos aparece centrado no token, sem exigir nenhuma outra funcionalidade da spec.

**Acceptance Scenarios**:

1. **Given** um jogador está na tela de combate e controla um personagem, **When** ele clica no token do seu personagem, **Then** um seletor compacto de formato de área (círculo, quadrado, cone, cilindro) aparece próximo ao token.
2. **Given** o seletor de formato está aberto, **When** o jogador escolhe "círculo" ou "quadrado", **Then** aparecem sliders para ajustar o raio/lado (em blocos do grid) e a área é desenhada ao redor do personagem em tempo real conforme o slider é movido.
3. **Given** o seletor de formato está aberto, **When** o jogador escolhe "cone" ou "cilindro", **Then** aparecem sliders para distância e largura, e um controle de direção/ângulo, e a área é desenhada a partir do personagem na direção escolhida.
4. **Given** o jogador ajustou a área e está satisfeito, **When** ele confirma/fecha a configuração (ex: toca fora do painel ou em um botão de concluir), **Then** os controles de configuração desaparecem da tela, mas o desenho da área permanece visível sobre o battlemap.

---

### User Story 2 - Reconfigurar uma área já desenhada (Priority: P2)

Um jogador ou o mestre clica em uma área de ataque já desenhada no mapa para reabrir os controles de configuração (formato, distância, raio, largura, direção) e ajustá-la novamente, ou removê-la.

**Why this priority**: Sem isso, uma área mal configurada exigiria apagar e recriar do zero, prejudicando a fluidez de uso durante o combate. É a segunda peça de valor mais crítica após criar a área.

**Independent Test**: Com uma área já desenhada no mapa (criada via User Story 1), clicar sobre o desenho da área e confirmar que os controles de configuração reabrem com os valores atuais pré-carregados, permitindo ajuste ou remoção.

**Acceptance Scenarios**:

1. **Given** uma área de ataque está desenhada no battlemap, **When** o usuário autorizado clica sobre o desenho da área, **Then** o painel de configuração reabre com o formato, distância/raio e largura atuais já preenchidos.
2. **Given** o painel de reconfiguração está aberto, **When** o usuário altera os sliders, **Then** o desenho da área é atualizado em tempo real no mapa.
3. **Given** o painel de reconfiguração está aberto, **When** o usuário aciona a opção de remover, **Then** a área desaparece do mapa para todos os participantes.

---

### User Story 3 - Mestre configura áreas para qualquer personagem ou criatura (Priority: P2)

O mestre (DM) da sessão pode clicar em qualquer token no battlemap — seu próprio, de um jogador, ou de uma criatura/monstro — e configurar uma área de ataque para ele, da mesma forma que um jogador faz para seu próprio personagem.

**Why this priority**: Essencial para que o mestre demonstre ataques de monstros/NPCs, mas depende da mecânica básica de configuração já existir (User Story 1).

**Independent Test**: Logado como mestre, clicar no token de uma criatura controlada pelo mestre (ou de um personagem de jogador) e confirmar que o seletor de formato de área abre normalmente, igual ao fluxo do jogador.

**Acceptance Scenarios**:

1. **Given** o usuário atual é o mestre da sessão, **When** ele clica em qualquer token no battlemap (personagem de jogador, seu próprio personagem, ou criatura/NPC), **Then** o seletor de formato de área abre normalmente.
2. **Given** o usuário atual é um jogador (não mestre), **When** ele clica em um token que não é seu, **Then** o seletor de configuração de área NÃO abre (o jogador só configura para si mesmo).

---

### User Story 4 - Áreas visíveis para todos os participantes em tempo real (Priority: P1)

Quando um jogador ou o mestre cria, ajusta ou remove uma área de ataque, todos os outros participantes conectados à mesma sessão veem a mudança refletida no battlemap quase instantaneamente, sem precisar recarregar a tela.

**Why this priority**: Sem sincronização, a funcionalidade perde a maior parte do seu valor colaborativo (ver o alcance de ataques dos outros jogadores durante o combate). Deve ser entregue junto com a P1 para o MVP fazer sentido em grupo.

**Independent Test**: Com duas sessões de navegador abertas na mesma campanha (participantes diferentes), criar uma área em uma sessão e verificar que ela aparece automaticamente na outra sessão em poucos segundos.

**Acceptance Scenarios**:

1. **Given** dois participantes estão conectados à mesma sessão de campanha, **When** um deles cria uma nova área de ataque, **Then** a área aparece no battlemap do outro participante sem necessidade de atualizar a página.
2. **Given** uma área existente é ajustada (raio, direção, formato) ou removida por seu autor ou pelo mestre, **When** a mudança é salva, **Then** todos os participantes veem o estado atualizado (ou a remoção) refletido.

---

### User Story 5 - Corrigir exibição indevida do editor de vida/iniciativa em combate (Priority: P3)

Ao entrar no modo de combate, os campos de edição de vida (HP) e iniciativa dos personagens não devem mais aparecer editáveis na tela; esse editor deve ficar restrito à tela inicial (modo de configuração/setup).

**Why this priority**: É uma correção de bug isolada, sem dependência das demais histórias, mas de menor prioridade por ser um ajuste pontual de UI já identificado.

**Independent Test**: Entrar no modo de combate e confirmar que os campos de edição de HP/iniciativa (nome, modificador, HP, iniciativa) não são mais exibidos como editáveis; voltar ao modo de configuração e confirmar que eles voltam a aparecer normalmente.

**Acceptance Scenarios**:

1. **Given** a sessão está no modo de configuração (setup), **When** o mestre ou jogador visualiza a lista de combatentes, **Then** os campos de edição de HP e iniciativa aparecem editáveis normalmente (comportamento atual preservado).
2. **Given** a sessão está no modo de combate, **When** qualquer participante visualiza a lista de combatentes, **Then** os campos de edição de HP e iniciativa NÃO são exibidos como editáveis (apenas leitura, ou ocultos, conforme o restante da UI de combate).

---

### Edge Cases

- O que acontece se um jogador clicar em um token que ele não controla (e não é o mestre)? O seletor de configuração de área não deve abrir para ele.
- O que acontece se dois participantes tentarem configurar a mesma área/token ao mesmo tempo? A última alteração salva prevalece (last-write-wins); não é necessário bloqueio de edição concorrente nesta versão.
- O que acontece se o token do personagem for movido no mapa depois que uma área foi criada para ele? A área permanece ancorada à sua posição/direção original de criação (não segue automaticamente o token), até ser reconfigurada manualmente.
- O que acontece com as áreas desenhadas quando o combate termina ou a cena/battlemap é reiniciada? Todas as áreas de ataque são removidas junto com o encerramento do combate/reset do battlemap.
- O que acontece se a distância configurada exceder os limites visíveis do grid? A área é desenhada normalmente, apenas visualmente cortada pelos limites da área de visualização/rolagem do mapa, sem erro.
- O que acontece se o mestre remover um personagem/token que possui uma área ativa? A área associada é removida automaticamente junto com o token.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: O sistema DEVE permitir que, ao clicar em um token de personagem/criatura no battlemap durante uma sessão, um seletor compacto de formato de área apareça, com as opções: círculo, quadrado, cone e cilindro.
- **FR-002**: O sistema DEVE restringir quem pode abrir o seletor de configuração de área para um token: jogadores comuns só podem configurar para o(s) personagem(ns) que controlam; o mestre pode configurar para qualquer token (personagens de jogadores, seu próprio personagem, ou criaturas/NPCs).
- **FR-003**: Para os formatos "círculo" e "quadrado" (zona centrada no próprio personagem), o sistema DEVE oferecer um controle deslizante (slider) para ajustar o raio/lado da área, medido em quantidade de blocos do grid do battlemap.
- **FR-004**: Para os formatos "cone" e "cilindro" (projéteis/direcionais), o sistema DEVE oferecer sliders para a distância/alcance e para a largura da área, ambos medidos em quantidade de blocos do grid, além de um controle para definir a direção/ângulo a partir do personagem.
- **FR-005**: O sistema DEVE atualizar o desenho da área sobre o battlemap em tempo real conforme o usuário move os sliders ou o controle de direção, antes mesmo de confirmar/fechar a configuração.
- **FR-006**: A interface de seleção de formato e os sliders de configuração DEVEM ocupar uma área compacta da tela (painel/popover flutuante próximo ao token), sem cobrir permanentemente o battlemap, e DEVEM ser utilizáveis tanto por toque (mobile) quanto por mouse (desktop).
- **FR-007**: Ao finalizar a configuração (ex.: tocar fora do painel, ou em um botão de confirmar), o sistema DEVE ocultar os controles de seleção/configuração, mantendo apenas o desenho da área visível sobre o battlemap.
- **FR-008**: O sistema DEVE permitir que qualquer participante autorizado (dono do personagem, ou o mestre) clique sobre uma área já desenhada no battlemap para reabrir o painel de configuração com os valores atuais (formato, distância/raio, largura, direção) pré-carregados.
- **FR-009**: O sistema DEVE permitir remover uma área de ataque configurada, a partir do painel de reconfiguração reaberto (FR-008).
- **FR-010**: O sistema DEVE sincronizar em tempo real, para todos os participantes conectados à mesma sessão, a criação, atualização e remoção de áreas de ataque, refletindo o estado atual do battlemap.
- **FR-011**: O sistema DEVE remover automaticamente as áreas de ataque associadas a um token quando esse token/personagem é removido do battlemap.
- **FR-012**: O sistema DEVE remover todas as áreas de ataque ativas quando o combate é encerrado ou o battlemap é reiniciado/resetado.
- **FR-013**: O sistema DEVE ocultar os campos de edição de vida (HP) e iniciativa dos combatentes (nome, modificador, HP, iniciativa) sempre que a sessão estiver no modo de combate, exibindo-os apenas no modo de configuração (setup).
- **FR-014**: No modo de combate, as informações de vida e iniciativa DEVEM continuar visíveis em modo somente leitura (sem os campos editáveis), preservando a visibilidade dessas informações durante o combate.

### Key Entities

- **Área de Ataque (Attack Area)**: Representa uma forma geométrica desenhada sobre o battlemap, associada a um token/personagem de origem. Atributos: formato (círculo, quadrado, cone, cilindro), distância/raio, largura (quando aplicável), direção/ângulo (quando aplicável), posição de origem (derivada do token no momento da criação), autor (participante ou mestre que a configurou), e a sessão/battlemap ao qual pertence.
- **Token/Personagem (Battlemap Token)**: Entidade existente do battlemap (personagem de jogador, personagem do mestre ou criatura/NPC), à qual uma ou mais Áreas de Ataque podem estar associadas.
- **Participante/Sessão (Participant/Session)**: Entidade existente que define quem é o mestre e quem controla qual personagem, usada para autorizar quem pode criar/editar/remover uma Área de Ataque.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Um jogador consegue configurar e visualizar uma área de ataque sobre seu personagem em menos de 15 segundos a partir do clique inicial no token.
- **SC-002**: Uma alteração feita em uma área de ataque (criação, ajuste ou remoção) é refletida na tela dos demais participantes da sessão em até 3 segundos.
- **SC-003**: Em telas de celular (largura de tela pequena), o painel de seleção/configuração de área ocupa no máximo um terço da altura visível da tela e não impede a visualização do restante do battlemap.
- **SC-004**: 100% das tentativas de um jogador de configurar uma área para um token que não é seu (e ele não é o mestre) são bloqueadas, sem exceções.
- **SC-005**: Após corrigir o bug, 0% das visualizações da lista de combatentes durante o modo de combate exibem campos de edição de HP/iniciativa; 100% das visualizações no modo de configuração continuam exibindo esses campos normalmente.

## Assumptions

- "Zona ao redor do personagem" (círculo/quadrado) é sempre centrada no próprio token de origem; não há suporte, nesta versão, para centralizar essas formas em um ponto arbitrário do mapa.
- Os formatos direcionais (cone, cilindro) partem da posição do token de origem e se estendem na direção configurada pelo usuário; o cilindro desta funcionalidade representa uma área de efeito direcional em forma de feixe/retângulo com largura configurável (linha reta com largura), não uma área circular ao redor de um ponto distante — conforme uso comum em jogos de mesa para representar "projéteis com largura".
- Cada personagem/criatura pode ter, no máximo, uma área de ataque ativa por vez; configurar uma nova área para o mesmo token substitui a anterior. Múltiplas áreas simultâneas para o mesmo personagem estão fora do escopo desta versão.
- A unidade de medida (blocos do grid) segue o tamanho de célula já configurado no battlemap pelo mestre; não há conversão para unidades de distância "reais" (metros/pés) nesta versão.
- O bloqueio de edição concorrente de uma mesma área por dois usuários ao mesmo tempo não é necessário; a última alteração salva prevalece.
- A correção do bug de HP/iniciativa assume que "modo de combate" corresponde ao estado já existente de alternância entre tela inicial (setup) e tela de combate na aplicação.
