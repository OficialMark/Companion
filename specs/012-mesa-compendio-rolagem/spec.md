# Feature Specification: Mesa com compêndio em abas, busca ampla, rolagem completa e overview da ficha

**Feature Branch**: `OficialMark/mesa-compendio-rolagem-c6d85eea`
**Created**: 2026-11-20
**Status**: Draft
**Input**: Pedido do Marcos (7 mudanças na Mesa, no Compêndio e na rolagem). O app está em fase de testes; a ficha é global por nome (spec 011).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Rolagem completa ligada à ficha, na visão completa e no Modo Jogo (Priority: P1)

O jogador rola a partir da própria ficha também na visão completa, não só no Modo Jogo. O mestre escolhe uma criatura da mesa e rola com os números dela. Isso vale na visão completa e no Modo Jogo. Depois de rolar, a tela desce até o resultado.

**Why this priority**: rolar é a ação mais frequente na mesa. Hoje a visão completa só tem um rolador genérico, e o mestre não tem como rolar pela ficha de uma criatura sem abrir a ficha dela.

**Independent Test**: jogador na visão completa rola "Ataque com Espada Longa" e o histórico mostra a expressão com o bônus da ficha. Mestre escolhe "Goblin 2" e rola Destreza com o modificador do goblin.

**Acceptance Scenarios**:

1. **Given** um jogador na visão completa, **When** a Mesa abre, **Then** ele vê o painel de rolagem da ficha (fonte, tipo, rolagens salvas, Mochila), o mesmo do Modo Jogo.
2. **Given** o mestre com um encontro ativo que tem criaturas da biblioteca, **When** ele abre o seletor de ficha no painel de rolagem, **Then** vê cada criatura da mesa pelo nome do combatente (ex.: "Goblin 2") e a opção "Rolagem livre".
3. **Given** o mestre escolheu "Goblin 2", **When** rola com atributo Destreza, **Then** a rolagem usa o modificador de Destreza do goblin e o histórico mostra "Goblin 2" no rótulo.
4. **Given** qualquer rolagem disparada na visão completa ou no Modo Jogo, **When** o resultado aparece, **Then** a página rola suavemente até o resultado ficar visível e o resultado recebe o foco de leitura (anunciado).
5. **Given** o mestre sem encontro ativo ou sem criaturas na mesa, **When** abre o painel, **Then** só existe "Rolagem livre", que mantém o rolador de dados livre de hoje.

---

### User Story 2 - Compêndio do mestre em abas, com busca por nome, descrição e inglês (Priority: P1)

O mestre consulta criaturas, magias e itens no mesmo Compêndio, cada um na sua aba. Na busca de magias e itens, o termo é procurado no nome em português, no nome em inglês e na descrição.

**Why this priority**: o mestre precisa consultar uma magia ou um item no meio do jogo sem pedir a ficha de um jogador. A busca só por nome não encontra "bola de fogo" quando o mestre lembra de "fireball" ou de "esfera de chamas".

**Independent Test**: o mestre abre Compêndio > Magias, busca "fireball" e acha "Bola de Fogo". Busca "invisível" e acha magias que citam invisibilidade na descrição.

**Acceptance Scenarios**:

1. **Given** o mestre no Compêndio, **When** a tela abre, **Then** vê as abas Criaturas, Magias e Itens; Criaturas é a aba inicial, e a aba escolhida fica guardada no endereço.
2. **Given** a aba Magias, **When** o mestre busca um termo, **Then** a lista traz magias cujo nome (PT ou EN) ou descrição contém o termo, sem diferenciar maiúsculas nem acentos. Acertos no nome vêm antes de acertos só na descrição.
3. **Given** a aba Itens, **When** o mestre busca, **Then** vale a mesma regra para itens: nome, nome em inglês e descrição.
4. **Given** um item ou uma magia na lista, **When** o mestre abre, **Then** vê os detalhes completos, só para leitura.
5. **Given** o jogador adicionando item ou magia à ficha (catálogo da spec 011), **When** busca, **Then** a busca ampla também vale ali.

---

### User Story 3 - Overview da ficha para o jogador, com ajuste rápido de vida (Priority: P2)

No lugar do painel de rodapé "Perfil de combate", o jogador tem um resumo da ficha no rodapé. Ao tocar nele, abre o overview em modal. Ali o jogador vê os números importantes e soma ou subtrai vida.

**Why this priority**: o painel de rodapé repetia dois números que a ficha já calcula. No jogo, o jogador precisa ver CA, PV, atributos e salvaguardas de relance, e registrar dano ou cura rápido.

**Independent Test**: o jogador abre o overview, digita 7 e toca em "Dano", e a vida cai 7 na ficha e na barra de vida da mesa. Depois digita 5 e toca em "Cura", e a vida sobe 5 sem passar do máximo.

**Acceptance Scenarios**:

1. **Given** um jogador na Mesa, **When** a tela abre, **Then** o rodapé mostra um resumo compacto (nome do personagem, PV atual/máximo, CA, iniciativa), sem campos editáveis de iniciativa nem de vida.
2. **Given** o resumo, **When** o jogador toca nele, **Then** abre um modal com: nome, classe e nível; PV atual, máximo e temporário; CA, iniciativa, deslocamento, bônus de proficiência e percepção passiva; os seis atributos com modificador; salvaguardas; e um botão "Abrir ficha completa".
3. **Given** o modal aberto, **When** o jogador informa um valor e toca em "Dano" ou "Cura", **Then** a vida atual muda por esse valor, entre 0 e o máximo, salva na ficha e reflete na mesa. Botões rápidos −1 e +1 também existem.
4. **Given** o modal, **When** o jogador aperta Esc ou toca fora, **Then** fecha e o foco volta ao resumo.

---

### User Story 4 - Ajustes de interface: código do mestre e legibilidade do seletor (Priority: P3)

O código reservado do mestre sai do corpo da Mesa e vai para a barra superior, oculto por padrão, com um ícone de olho que mostra e esconde o código. Os títulos de grupo do seletor de fonte da rolagem (Armas equipadas, Magias, Atributos) passam a ter um fundo escuro legível.

**Independent Test**: o mestre vê na barra superior "Código do mestre ••••••" e o olho. Ao tocar no olho, o código aparece; ao tocar de novo, some. O seletor de fonte mostra os títulos de grupo legíveis em tema escuro.

**Acceptance Scenarios**:

1. **Given** o mestre na Mesa, **When** a tela abre, **Then** o painel "Código reservado do mestre" não aparece mais no corpo, e a barra superior tem o código mascarado com um botão de olho ("Mostrar código do mestre" / "Ocultar código do mestre").
2. **Given** um jogador, **When** a tela abre, **Then** nada do código do mestre aparece.
3. **Given** o seletor de fonte aberto, **When** o jogador lê os grupos, **Then** os títulos de grupo têm fundo um tom mais escuro que o das opções e texto claro, com contraste de pelo menos 4,5:1.

### Edge Cases

- O mestre escolheu uma criatura que saiu da mesa (encontro encerrado ou combatente removido): o seletor volta para "Rolagem livre" e avisa.
- Combatentes manuais (sem criatura da biblioteca) não aparecem no seletor do mestre: não têm ficha.
- Busca com 1 caractere: continua aceita, mas a busca na descrição só roda a partir de 3 caracteres, para não retornar o catálogo inteiro.
- Magia sem nome em inglês conhecido: a busca continua por nome PT e descrição.
- Overview com a ficha ainda carregando ou offline: mostra estado de carregando e desativa Dano e Cura, com o aviso de conexão.
- Dano maior que a vida atual: a vida fica em 0. Cura acima do máximo: a vida fica no máximo.
- O usuário pediu "reduzir menos movimento": a rolagem até o resultado não anima, só salta.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: O Compêndio do mestre MUST ter três abas (Criaturas, Magias, Itens), acessíveis por teclado, com a aba ativa no endereço da página.
- **FR-002**: As abas Magias e Itens MUST listar o catálogo com os filtros que já existem (nível, escola, categoria, raridade) e abrir os detalhes só para leitura.
- **FR-003**: A busca de magias e itens MUST casar o termo, sem diferenciar acento nem maiúscula, com o nome em português, o nome em inglês e a descrição. Acertos no nome MUST vir antes dos acertos só na descrição.
- **FR-004**: Cada magia do catálogo MUST ter o nome em inglês quando ele existir, vindo de uma tabela fixa, versionada e validada.
- **FR-005**: Na visão completa, o jogador MUST usar o mesmo painel de rolagem da ficha do Modo Jogo.
- **FR-006**: Na visão completa e no Modo Jogo, o mestre MUST ter um seletor de ficha com as criaturas da biblioteca presentes no encontro ativo, mais "Rolagem livre". Escolhida uma criatura, a rolagem usa os atributos dela e é rotulada com o nome do combatente.
- **FR-007**: Toda rolagem feita pelos painéis da Mesa MUST, ao revelar o resultado, rolar a página até o resultado e anunciá-lo para leitores de tela, respeitando "reduzir movimento".
- **FR-008**: Os títulos de grupo do seletor de fonte MUST ter fundo um tom mais escuro que as opções, com contraste de texto de no mínimo 4,5:1.
- **FR-009**: O código do mestre MUST sair do corpo da Mesa e ficar na barra superior, mascarado por padrão, com um botão de olho que alterna mostrar e ocultar. Só o mestre vê esse controle.
- **FR-010**: O painel de rodapé "Perfil de combate" do jogador MUST ser substituído por um resumo compacto que abre o modal de overview da ficha.
- **FR-011**: O overview MUST mostrar os números importantes da ficha (ver US3) e permitir dano, cura, −1 e +1 na vida atual, limitada entre 0 e o máximo, salva pela mesma via de escrita da ficha.
- **FR-012**: O overview MUST ser um modal acessível: foco entra ao abrir, Tab fica dentro, Esc fecha e o foco volta ao gatilho.

### Key Entities

- **Magia do catálogo**: ganha o nome em inglês (opcional) e um texto de busca que junta nome PT, nome EN e descrição.
- **Item do catálogo**: ganha o texto de busca que junta nome PT, nome EN e descrição.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Buscar o nome em inglês de qualquer magia do SRD encontra a magia certa entre os 5 primeiros resultados, em pelo menos 95% das magias do SRD que estão no catálogo.
- **SC-002**: Uma busca por termo da descrição (ex.: "invisível") retorna resultados que nenhuma busca só por nome retornava.
- **SC-003**: O jogador registra dano ou cura em até 3 toques a partir da Mesa.
- **SC-004**: Depois de uma rolagem, o resultado está visível na tela sem rolagem manual em 100% dos casos testados, no desktop e no celular.
- **SC-005**: O código do mestre fica oculto ao abrir a Mesa e aparece em 1 toque.

## Assumptions

- "Criaturas na mesa" são os combatentes do encontro ativo ligados a uma criatura da biblioteca. Combatentes manuais e jogadores não entram no seletor do mestre.
- Sem encontro ativo, o mestre usa "Rolagem livre" (o rolador de dados atual).
- O Perfil de combate manual (sem ficha) deixa de existir para o jogador: com a spec 011 todo jogador tem ficha, criada ao abrir o overview.
- A rolagem da criatura reaproveita o painel de rolagem de criatura da spec 008/010.
- O nome em inglês das magias vem de uma tabela PT→EN fixa no importador. Os nomes do SRD 5.2.1 são conferidos contra a lista oficial; os de fora do SRD ficam marcados como não verificados.
- O app está em fase de testes. Mudanças de esquema podem migrar sem preservar dados de sessão, mas o compêndio é sempre preservado.
