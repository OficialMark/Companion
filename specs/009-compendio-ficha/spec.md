# Feature Specification: Compêndio na barra superior e ficha de criatura legível

**Feature Branch**: `OficialMark/compendium-ficha-331fa84b`

**Created**: 2026-11-19

**Status**: Draft

**Input**: User description: "quero que revise a interface e responsividade do modal de editar ficha, pois o espaçamento esta feio, e dificulta a leitura, quero que na aba superior da tela do mestre tenha uma tela para o compedium, onde ele pode consultar e filtrar na lista de criaturas e adicionar suas proprias, registre no speckit"

## Contexto

A spec 008 entregou a biblioteca de criaturas e a ficha em modal. Dois problemas apareceram no uso:

- **Leitura da ficha.** O modal (ver e editar) tem rótulos colados aos campos, uma grade de 3 a 4 colunas com buracos, títulos de seção sobre a borda e blocos densos. No celular, os campos espremem contra a barra de ações. O mestre perde tempo procurando valores no meio do combate.
- **Onde fica o compêndio.** As abas "Mesa" e "Criaturas" ficam no meio da página, abaixo do aviso do código do mestre. Elas não são percebidas como navegação, e a biblioteca divide a tela com o resto da mesa.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Ler a ficha de uma criatura de relance (Priority: P1)

Durante o combate, o mestre abre a ficha de uma criatura (pelo compêndio, pelo token ou pelo catálogo da preparação). Ele encontra CA, PV, deslocamento, atributos, ações e anotações sem esforço, num layout de bloco de estatísticas: valores grandes, rótulos discretos e seções bem separadas.

**Why this priority**: a ficha é aberta várias vezes por sessão, no meio do jogo. Ler mal custa tempo de mesa a cada turno.

**Independent Test**: abrir a ficha de uma criatura longa (Dragão Vermelho Adulto) em 1280, 768 e 375 px. Todos os blocos ficam legíveis, sem sobreposição de texto, com espaçamento consistente e sem rolagem horizontal.

**Acceptance Scenarios**:

1. **Given** a ficha de uma criatura com muitas perícias, imunidades e sentidos, **When** o mestre a abre em qualquer largura, **Then** cada rótulo fica separado do seu valor, nenhuma linha se sobrepõe a outra e textos longos quebram dentro da própria coluna.
2. **Given** a ficha aberta num celular de 375 px, **When** o mestre rola até o fim, **Then** a barra de ações fixa não cobre conteúdo e todos os controles têm pelo menos 44 px.
3. **Given** a ficha aberta, **When** o mestre procura um valor (CA, PV, um atributo), **Then** o valor aparece em destaque tipográfico e o rótulo em tom secundário, com a mesma hierarquia em todos os blocos.

---

### User Story 2 - Editar a ficha sem se perder nos campos (Priority: P1)

O mestre edita uma criatura (ou cria uma nova) e preenche os campos num formulário arejado. Cada seção tem título claro, os campos se alinham numa grade uniforme, o rótulo fica acima do campo com espaço, e no celular há um campo por linha (exceto os pares curtos, como CA e PV).

**Why this priority**: criar e ajustar criaturas próprias é metade do valor da biblioteca, e o formulário atual é o principal motivo da reclamação.

**Independent Test**: entrar em "Editar ficha" do Dragão Vermelho Adulto e em "Nova criatura" em 1280, 768 e 375 px. A grade não tem buracos, os campos se alinham e o celular mostra um campo por linha nas seções de texto.

**Acceptance Scenarios**:

1. **Given** o editor aberto em desktop, **When** o mestre olha qualquer seção, **Then** os campos ocupam uma grade de colunas iguais, sem célula vazia no fim da linha, com o mesmo espaço vertical entre rótulo e campo e entre linhas.
2. **Given** o editor aberto em 375 px, **When** o mestre rola o formulário, **Then** campos de texto ocupam a largura toda, números curtos aparecem em pares e os botões Cancelar e Salvar ficam sempre visíveis, sem cobrir o último campo.
3. **Given** uma ação ou característica no editor, **When** o mestre edita nome e descrição, **Then** o nome fica numa linha, a descrição embaixo com largura total e o botão de remover alinhado ao nome.

---

### User Story 3 - Compêndio como tela própria na barra superior (Priority: P2)

O mestre alterna entre "Mesa" e "Compêndio" pela barra superior do app, num clique, de qualquer ponto da página. O Compêndio ocupa a tela toda com busca, filtros (tamanho, tipo, ND, origem), o atalho "Minhas criaturas" e o botão "Nova criatura". Ao recarregar a página, o mestre continua na tela em que estava. Jogadores não veem essa navegação.

**Why this priority**: a biblioteca já existe; o que muda é a facilidade de encontrá-la e o espaço que ela recebe.

**Independent Test**: como mestre, clicar em "Compêndio" na barra superior, filtrar por "Minhas criaturas", criar uma criatura, recarregar a página e continuar no Compêndio. Como jogador, não ver a opção.

**Acceptance Scenarios**:

1. **Given** o mestre na Mesa com a página rolada até o battlemap, **When** clica em "Compêndio" na barra superior, **Then** a tela do compêndio abre do topo, sem o aviso do código do mestre nem os painéis da mesa.
2. **Given** o mestre no Compêndio, **When** recarrega a página, **Then** continua no Compêndio, com a busca vazia e a primeira página.
3. **Given** o mestre no Compêndio, **When** ativa "Minhas criaturas", **Then** a lista mostra só as criaturas criadas nesta mesa, e a contagem e o estado vazio refletem o filtro.
4. **Given** um jogador na mesa, **When** a página carrega, **Then** não existe "Compêndio" na barra superior, mesmo se o endereço pedir essa tela.
5. **Given** o mestre no celular (375 px), **When** olha a barra superior, **Then** Mesa e Compêndio cabem sem empurrar os outros controles para fora da tela, com alvos de 44 px.

### Edge Cases

- Criatura com nome muito longo: o título da ficha quebra em várias linhas sem empurrar o botão fechar.
- Criatura sem nenhuma ação, característica ou detalhe: as seções vazias não aparecem.
- Valores vazios (sem nota de CA, sem dados de vida): nenhum traço solto nem rótulo órfão.
- Modo Jogo ativo: a navegação Mesa/Compêndio continua acessível ao mestre, ou some junto com o resto da interface do mestre, mas nunca deixa o Compêndio preso sem saída.
- Endereço pede o Compêndio mas a sessão é de jogador: abre a Mesa.
- O mestre abre uma ficha pelo token da Mesa: a ficha abre por cima da Mesa, sem trocar de tela.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: A ficha (modo de leitura) MUST usar uma escala de espaçamento única entre blocos, entre linhas e entre rótulo e valor, igual em todos os blocos.
- **FR-002**: Na ficha, rótulos MUST aparecer em tom secundário e valores em destaque. Detalhes como salvaguardas, perícias e sentidos MUST ficar em linhas próprias, com o texto longo quebrando dentro da coluna do valor e nunca sobrepondo a linha seguinte.
- **FR-003**: O editor MUST organizar os campos em seções com título visível acima do conteúdo (não sobre a borda), numa grade de colunas iguais sem células vazias, com o rótulo acima do campo.
- **FR-004**: Em larguras de celular, o editor MUST mostrar campos de texto em largura total e pares só para números curtos (CA/PV, ND/XP, Dados de vida/Iniciativa, atributos em 3 colunas).
- **FR-005**: As barras de ação da ficha e do editor MUST ficar fixas no rodapé do modal sem cobrir o último conteúdo, também com a área segura de celulares.
- **FR-006**: Todos os controles da ficha, do editor e do compêndio MUST ter pelo menos 44 px de alvo, e nenhuma tela desta feature pode gerar rolagem horizontal em 375 px.
- **FR-007**: A navegação do mestre entre "Mesa" e "Compêndio" MUST ficar na barra superior do app, visível só para o mestre, e substituir as abas que hoje ficam no meio da página.
- **FR-008**: O Compêndio MUST abrir como tela própria de largura total, sem o aviso do código do mestre e sem os painéis da mesa, com busca, filtros de tamanho, tipo, ND mínimo e máximo, origem, atalho "Minhas criaturas", contagem, paginação, "Nova criatura" e o crédito do Pocket DM.
- **FR-009**: A tela escolhida (Mesa ou Compêndio) MUST ser lembrada no endereço da página, de modo que recarregar ou compartilhar o endereço com o próprio mestre reabra a mesma tela. Para um jogador, o endereço do Compêndio MUST abrir a Mesa.
- **FR-010**: Abrir uma ficha a partir de um token da Mesa MUST manter o mestre na Mesa.
- **FR-011**: Nenhuma funcionalidade existente da ficha (rolagem, atalhos de acerto e dano, anotações, adicionar ao combate, editar, restaurar original, imagem) pode mudar de comportamento.

### Key Entities

Nenhuma entidade nova. A tela usa a biblioteca de criaturas da spec 008, incluindo o filtro de origem já existente ("Minhas criaturas" corresponde às criaturas criadas na mesa).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Em 1280, 768 e 375 px, a ficha do Dragão Vermelho Adulto (uma das mais longas do compêndio) não tem nenhuma sobreposição de texto nem rolagem horizontal, verificado automaticamente.
- **SC-002**: O mestre chega ao Compêndio em 1 clique a partir de qualquer ponto da Mesa, e volta à Mesa em 1 clique.
- **SC-003**: Recarregar a página no Compêndio reabre o Compêndio em 100% das vezes para o mestre e nunca o mostra para um jogador.
- **SC-004**: Todo controle interativo das telas desta feature mede pelo menos 44 × 44 px em 375 px, verificado automaticamente.
- **SC-005**: Um revisor que não participou da construção compara capturas de antes e depois em três larguras e escolhe o depois como mais legível em todas.

## Assumptions

- "Aba superior" é a barra fixa do topo do app (onde ficam Modo Jogo, conexão e menu do usuário).
- "Minhas criaturas" significa as criaturas criadas pelo mestre nesta mesa. As editadas a partir do compêndio continuam no filtro de origem ("Editadas").
- A tela é lembrada pelo endereço da página, sem preferência guardada no servidor.
- O conteúdo e as regras da ficha, a API e os dados não mudam: a feature é de interface.
- O Modo Jogo segue escondendo a interface de preparação. A navegação Mesa/Compêndio permanece na barra superior, que o Modo Jogo não esconde.
