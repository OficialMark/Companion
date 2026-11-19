# Feature Specification: Ficha pelo botão direito e resultado da rolagem visível por 2 s

**Feature Branch**: `OficialMark/ficha-botao-direito-331fa84b`

**Created**: 2026-11-19

**Status**: Draft

**Input**: User description: "coloque que ao clicar com botão direito no token do battlemap, já abra direto no modal da ficha (futuramente players também terão sua ficha), e ao realizar essa rolagem no modal ficha, o resultado deve permanecer por 2s na tela"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Abrir a ficha com o botão direito (Priority: P1)

Durante o combate, o mestre clica com o botão direito no token de uma criatura no mapa, e a ficha dela abre na hora. Ele não passa pelo menu do token nem pelo botão "Ficha e rolagens".

**Why this priority**: a ficha é o que o mestre mais consulta durante a luta. Hoje são dois cliques e um menu no caminho.

**Independent Test**: no mapa, com uma criatura posicionada, o botão direito no token abre a ficha com o nome do token. O menu do token não abre, e o menu do navegador também não.

**Acceptance Scenarios**:

1. **Given** um token de criatura no mapa, **When** o mestre clica nele com o botão direito, **Then** a ficha daquela criatura abre, identificada com o nome do token, e o menu do token não aparece.
2. **Given** o menu desse token já aberto, **When** o mestre clica no token com o botão direito, **Then** o menu fecha e a ficha abre por cima.
3. **Given** um token que ainda não tem ficha (um jogador ou um marcador), **When** alguém clica nele com o botão direito, **Then** acontece o mesmo de antes: abre o menu do token.
4. **Given** o foco do teclado num token de criatura, **When** o mestre pressiona Shift+F10 ou a tecla Menu, **Then** a ficha abre como no botão direito.
5. **Given** o mapa com pelo menos uma criatura, **When** o mestre lê a dica abaixo do mapa, **Then** ela avisa que o botão direito numa criatura abre a ficha.

---

### User Story 2 - Ver o resultado da rolagem da ficha (Priority: P1)

O mestre rola pela ficha de uma criatura. Os dados e o total ficam visíveis na tela por 2 segundos, sem precisar rolar o modal para ver. Depois disso, o resultado vira a linha "Último resultado".

**Why this priority**: hoje o resultado some em cerca de 0,2 s e aparece abaixo da área visível do modal, então o mestre não chega a ler o número.

**Independent Test**: com a ficha aberta, clicar em "Rolar". Os dados e o total aparecem inteiros dentro da área visível da ficha, ficam por volta de 2 s e dão lugar à linha "Último resultado".

**Acceptance Scenarios**:

1. **Given** a ficha aberta no topo, **When** o mestre rola, **Then** a área dos dados entra na vista e o total fica visível por 2 s.
2. **Given** o resultado em exibição, **When** o mestre rola de novo antes dos 2 s, **Then** a nova rolagem substitui a anterior e os 2 s recomeçam.
3. **Given** uma rolagem, **When** o resultado é revelado, **Then** o histórico de rolagens e a última configuração do token são atualizados na hora, sem esperar os 2 s.
4. **Given** uma rolagem que falha, **When** o erro chega, **Then** a animação some na hora e a mensagem de erro aparece.

### Edge Cases

- Botão direito durante a arrastada de outro token: não começa outra arrastada.
- Botão direito com a ficha de outra criatura aberta: a ficha passa a ser a do token clicado.
- Fechar a ficha durante os 2 s: nada fica pendente e nada reabre.
- Modo névoa ativo: o botão direito no token não abre a ficha, porque o modo névoa controla o mapa.
- Jogador: nunca abre a ficha de criatura. A ficha de jogador é trabalho futuro.
- Sem conexão: a ficha abre se puder ser carregada; caso contrário, aparece o aviso de erro que já existe.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: O botão direito num token com ficha MUST abrir a ficha desse token, sem abrir o menu do token nem o menu do navegador.
- **FR-002**: Um token sem ficha MUST manter o comportamento anterior do botão direito: abrir o menu do token.
- **FR-003**: A decisão de que um token tem ficha MUST ficar num único ponto. Hoje vale para tokens de criatura vistos pelo mestre; as fichas de jogador entram nesse ponto no futuro.
- **FR-004**: O teclado MUST ter um equivalente ao botão direito: Shift+F10 ou a tecla Menu num token com foco.
- **FR-005**: Clique esquerdo, arrastar, clique do meio e modo névoa MUST continuar como antes.
- **FR-006**: Quando há criaturas no mapa, a dica abaixo dele MUST informar o atalho ao mestre.
- **FR-007**: Na rolagem pela ficha, os dados assentados e o total MUST ficar visíveis por 2 segundos dentro da área visível da ficha, e depois a linha "Último resultado" assume.
- **FR-008**: O histórico e a configuração de rolagem do token MUST ser atualizados quando o resultado é revelado, não ao fim dos 2 segundos.
- **FR-009**: Uma nova rolagem MUST poder interromper a exibição. Uma rolagem com erro MUST limpar a exibição na hora.

### Key Entities

Nenhuma entidade nova. Usa a ficha de criatura e a configuração de rolagem por token das specs 008 e 009.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Abrir a ficha de uma criatura no mapa leva 1 ação (antes: clique, menu e botão).
- **SC-002**: O resultado de uma rolagem pela ficha fica na tela entre 1,7 s e 3,5 s, medido automaticamente num navegador real.
- **SC-003**: Pelo menos 95% da área dos dados e do total fica dentro da parte visível da ficha durante a exibição.
- **SC-004**: Nenhuma regressão nos gestos do mapa: clique esquerdo abre o menu, botão direito não arrasta, e tokens sem ficha mantêm o menu. Tudo verificado por testes automáticos.

## Assumptions

- "Modal da ficha" é o modal de criatura da spec 008. Por enquanto só criaturas têm ficha, e só o mestre a abre.
- Os 2 segundos contam a partir do momento em que o total é revelado.
- No toque não existe botão direito. O caminho pelo menu do token ("Ficha e rolagens") continua para celular e tablet.
