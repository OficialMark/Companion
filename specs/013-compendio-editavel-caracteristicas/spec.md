# Feature Specification: Compêndio editável pelo mestre e características com contador de usos

**Feature Branch**: `OficialMark/compendio-editavel-habilidades-e778e391`
**Created**: 2026-11-20
**Status**: Draft
**Input**: Pedido do Marcos: "na tela de compêndio do mestre, ele também deve ser capaz de criar ou editar magias e itens" e "cadastre como características de classe o smite, cura pelas mãos e sentido divino, com um contador de usos; meu paladino é o da conquista, então cadastre suas habilidades de canalizar divindade". O app está em fase de testes; a ficha é global por nome (spec 011) e o compêndio de criaturas já é editável por campanha (spec 008).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Mestre cria e edita magias e itens no Compêndio (Priority: P1)

Nas abas Magias e Itens do Compêndio, o mestre cria magias e itens próprios da mesa e edita os do catálogo. Funciona como nas criaturas: o catálogo global nunca muda; editar uma entrada do catálogo cria a versão da mesa, que passa a aparecer no lugar da original só naquela campanha. Os jogadores da campanha veem e adicionam à ficha a versão da mesa.

**Why this priority**: o mestre precisa de itens mágicos e magias de mesa (homebrew, ajustes de balanceamento, tradução corrigida) sem depender de alguém mexer no banco. Hoje o Compêndio é só leitura.

**Independent Test**: o mestre cria o item "Corpo Fênix Armadura de Placas +1" no Compêndio, um jogador da mesma campanha busca "fênix" no catálogo da ficha e adiciona o item. O mestre edita "Bola de Fogo" para 10d6; o jogador vê 10d6 no catálogo. Uma segunda campanha continua vendo 8d6.

**Acceptance Scenarios**:

1. **Given** o mestre na aba Itens (ou Magias), **When** clica em "Novo item" ("Nova magia"), preenche o formulário e salva, **Then** a entrada aparece na lista com o selo "Personalizada" e pode ser buscada por nome, nome em inglês e descrição.
2. **Given** uma entrada do catálogo global, **When** o mestre a edita e salva, **Then** a lista mostra a versão da mesa com o selo "Editada" no lugar da original (sem duplicar), e o detalhe mostra os novos valores.
3. **Given** uma entrada "Editada", **When** o mestre a exclui, **Then** a original do catálogo volta a aparecer, sem selo.
4. **Given** uma entrada "Personalizada", **When** o mestre a exclui (confirmando), **Then** ela some da lista e do catálogo dos jogadores daquela campanha.
5. **Given** uma entrada do catálogo global sem edição, **When** o mestre abre o detalhe, **Then** existe "Editar" e não existe "Excluir".
6. **Given** um jogador da campanha, **When** abre o catálogo da ficha, **Then** vê as versões da mesa (editadas e personalizadas) e não vê a original de uma entrada editada; ao adicionar, a ficha recebe uma cópia da versão da mesa.
7. **Given** um jogador que já tinha copiado um item para a ficha, **When** o mestre edita esse item, **Then** a cópia na ficha não muda.
8. **Given** o filtro de origem (Todas, Compêndio, Editadas, Personalizadas), **When** o mestre escolhe uma origem, **Then** a lista mostra só aquela origem, combinada com busca e demais filtros.
9. **Given** um jogador (não mestre), **When** tenta criar, editar ou excluir entradas do catálogo, **Then** o sistema recusa.
10. **Given** a carga do catálogo global roda de novo (seed atualizado), **When** termina, **Then** nenhuma entrada de campanha foi alterada nem removida.

---

### User Story 2 - Características de classe com contador de usos na ficha (Priority: P1)

A ficha do jogador ganha a seção "Características": características de classe, de raça e talentos, cada uma com nome, origem (ex.: "Paladino 5"), descrição e, se tiver, um contador de usos com recuperação em descanso curto ou longo. O jogador gasta e recupera usos com um toque, e os botões "Descanso curto" e "Descanso longo" restauram tudo o que aquela pausa recupera.

**Why this priority**: recursos como Cura pelas Mãos, Sentido Divino e Canalizar Divindade são consultados e gastos várias vezes por sessão. Hoje ficam soltos em Traços como texto, sem contagem.

**Independent Test**: o jogador cria "Sentido Divino" com 5 usos e recuperação em descanso longo, gasta 2 (fica 3/5), clica "Descanso curto" (continua 3/5) e "Descanso longo" (volta a 5/5). Recarrega a página e os valores persistem.

**Acceptance Scenarios**:

1. **Given** a ficha aberta, **When** o jogador adiciona uma característica com nome, origem, descrição, usos máximos 5 e recuperação "Descanso longo", **Then** ela aparece na seção Características com o contador 5/5.
2. **Given** uma característica com contador, **When** o jogador usa -1 ou +1, **Then** o atual muda e fica sempre entre 0 e o máximo; os botões ficam desabilitados nos limites.
3. **Given** características com recuperação em descanso curto e em descanso longo gastas, **When** o jogador clica "Descanso curto", **Then** só as de descanso curto voltam ao máximo; **When** clica "Descanso longo", **Then** as de descanso curto e as de descanso longo voltam ao máximo.
4. **Given** uma característica sem contador, **When** exibida, **Then** mostra nome, origem e descrição, sem botões de uso.
5. **Given** uma característica, **When** o jogador a edita (inclusive reduzindo o máximo abaixo do atual), **Then** o atual é ajustado para no máximo o novo máximo.
6. **Given** uma característica, **When** o jogador a move para cima/baixo ou a exclui, **Then** a ordem e a lista persistem.
7. **Given** o mesmo jogador em outra campanha, **When** abre a ficha, **Then** vê as mesmas características e contadores (a ficha é global por nome).

---

### User Story 3 - Habilidades do paladino Vashkar cadastradas (Priority: P2)

Depois do deploy, a ficha do Vashkar (Paladino 5, Juramento da Conquista) tem Destruição Divina, Cura pelas Mãos, Sentido Divino e Canalizar Divindade com as opções da Conquista: Presença Conquistadora e Golpe Guiado.

**Why this priority**: é o pedido direto do Marcos para a mesa atual; depende da US2.

**Independent Test**: entrar como "Vashkar" e ver as 6 características com os contadores: Cura pelas Mãos com reserva de 25 (descanso longo; o atual vem da ficha, hoje 0/25), Sentido Divino 5/5 (descanso longo), Canalizar Divindade 1/1 (descanso curto).

**Acceptance Scenarios**:

1. **Given** a ficha do Vashkar, **When** aberta, **Then** mostra Destruição Divina, Cura pelas Mãos (25), Sentido Divino (5), Canalizar Divindade (1, descanso curto), Presença Conquistadora e Golpe Guiado, com origem "Paladino" ou "Juramento da Conquista".

### Edge Cases

- Duas entradas da mesa com o mesmo nome: permitido (como criaturas personalizadas); a busca traz as duas.
- O mestre edita uma entrada "Editada": atualiza a própria cópia, não cria outra.
- Uma entrada global é removida do seed depois de ter sido editada na mesa: a versão da mesa continua existindo e aparecendo.
- Item genérico do catálogo que pede item base ("Arma +1"): pode ser editado; a cópia continua pedindo o item base ao ser adicionada.
- Formulário inválido (dado de dano inválido, nome vazio, campos longos demais): o sistema recusa com mensagem em português e nada é salvo.
- Mestre e jogador da mesma campanha: o jogador nunca vê entradas de outra campanha.
- Uma característica com contador atual 0: -1 desabilitado; descanso adequado restaura.
- Limite de 100 características por ficha: a 101ª é recusada com mensagem.
- Escrita simultânea na mesma ficha por dois aparelhos: como em toda escrita da ficha (spec 011), a segunda recebe conflito e o jogador recarrega; no mesmo aparelho as escritas entram em fila, então dois toques rápidos são dois gastos. O contador nunca sai dos limites, e editar o texto nunca desfaz um gasto (o servidor ignora o "atual" do formulário).
- Uma magia já na ficha em qualquer versão (original do compêndio, versão de outra mesa ou desta) não é adicionada de novo, mesmo depois que a versão da mesa for excluída.

## Requirements *(mandatory)*

### Functional Requirements

**Compêndio editável (US1)**

- **FR-001**: O mestre MUST poder criar magias e itens na campanha a partir das abas Magias e Itens do Compêndio.
- **FR-002**: O mestre MUST poder editar qualquer magia ou item visível no Compêndio. Editar uma entrada do catálogo global MUST criar uma versão da campanha ligada à original; editar uma versão da campanha MUST atualizá-la.
- **FR-003**: Dentro de uma campanha, a versão editada MUST substituir a original em listas, buscas, detalhe e na adição à ficha; outras campanhas MUST continuar vendo a original.
- **FR-004**: O mestre MUST poder excluir entradas da campanha: excluir uma personalizada a remove; excluir uma editada restaura a original. Entradas globais MUST NOT ser excluíveis.
- **FR-005**: A lista MUST mostrar a origem de cada entrada (Compêndio, Editada, Personalizada) e MUST permitir filtrar por origem, combinando com busca e filtros existentes.
- **FR-006**: Criar, editar e excluir MUST ser exclusivos do mestre da campanha; jogadores MUST continuar só consultando e adicionando à ficha.
- **FR-007**: O formulário de item MUST cobrir os campos do item da ficha (nome, categoria, peso, descrição, dados de arma, armadura ou escudo, bônus mágico, efeitos) mais nome em inglês, subcategoria, raridade, sintonização e custo. O formulário de magia MUST cobrir os campos da magia da ficha mais nome em inglês, classes e "em níveis superiores".
- **FR-008**: As mesmas validações da ficha MUST valer para entradas da campanha, com mensagens em português.
- **FR-009**: A carga do catálogo global MUST NOT alterar nem remover entradas de campanha.
- **FR-010**: Itens e magias já copiados para fichas MUST NOT mudar quando a entrada do catálogo for editada ou excluída.

**Características (US2)**

- **FR-011**: A ficha MUST ter uma lista ordenada de características, cada uma com nome (obrigatório, até 80 caracteres), origem (opcional, até 60), descrição (opcional, até 8000), usos máximos (opcional, 1 a 999), usos atuais (0 a máximo) e recuperação (nenhuma, descanso curto, descanso longo).
- **FR-012**: O jogador MUST poder criar, editar, excluir e reordenar características da própria ficha; no máximo 100 por ficha.
- **FR-013**: O jogador MUST poder gastar (-1) e recuperar (+1) um uso, e restaurar uma característica ao máximo; o atual MUST ficar sempre entre 0 e o máximo.
- **FR-014**: "Descanso curto" MUST restaurar as características com recuperação em descanso curto; "Descanso longo" MUST restaurar as de descanso curto e as de descanso longo.
- **FR-015**: Uma característica nova com contador MUST começar com o atual igual ao máximo; reduzir o máximo MUST ajustar o atual.
- **FR-016**: Toda mudança MUST ser persistida e refletida nas outras telas abertas da mesma ficha, como as demais partes da ficha.

### Key Entities

- **Entrada de catálogo da campanha**: magia ou item que pertence a uma campanha; se for editada, aponta para a entrada global que substitui; se for personalizada, não aponta para nenhuma.
- **Característica**: pertence a uma ficha; nome, origem, descrição, usos máximos e atuais, recuperação, posição na lista.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: O mestre cria uma magia ou item novo em menos de 2 minutos e ele já aparece para os jogadores da campanha na próxima busca.
- **SC-002**: 100% das entradas editadas aparecem uma única vez na lista da campanha (sem original duplicada) e 0 entradas de campanha mudam após uma nova carga do catálogo global (verificado em teste automatizado).
- **SC-003**: Gastar ou recuperar um uso de característica leva 1 toque; um descanso restaura todas as características aplicáveis em 1 toque.
- **SC-004**: Após recarregar a página ou trocar de campanha, 100% dos contadores mostram o último valor salvo.
- **SC-005**: A ficha do Vashkar mostra as 6 características pedidas com os contadores corretos após o deploy.

## Assumptions

- Mesmo modelo de origem das criaturas (spec 008): o catálogo global é só leitura; o que muda é da campanha.
- Itens e magias da campanha seguem o escopo da campanha, não da conta do mestre: um mestre com duas campanhas edita cada uma separadamente.
- O catálogo é visível para todos os participantes da campanha (como hoje); só a escrita é do mestre.
- Características não fazem cálculos automáticos (não somam bônus); são texto com contador. Efeitos mecânicos continuam nos itens.
- Destruição Divina segue o Livro do Jogador 2024: pode ser conjurada sem gastar espaço de magia 1 vez por descanso longo (contador 1/1), e também gastando espaços de magia normalmente. Sentido Divino, Cura pelas Mãos e Canalizar Divindade seguem os números já registrados na ficha do Vashkar (5 usos, reserva de 25 PV, 1 uso por descanso curto).
- Sem histórico de uso nem log de quem gastou: o contador é o estado atual.
- Fora de escopo: o mestre ver a ficha de um jogador. Hoje não existe essa tela para nenhuma parte da ficha; as características ficam na ficha do próprio jogador.
- As características ganham uma aba própria na ficha ("Características"), ao lado de Mochila e Magias.
