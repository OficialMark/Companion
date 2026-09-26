# Feature Specification: Modo Jogo (Visão Focada de Gameplay)

**Feature Branch**: `004-modo-jogo`

**Created**: 2026-09-06

**Status**: Draft

**Input**: User description: "Adicionar uma visualização de 'Modo Jogo' focada em gameplay, acessível por um botão na barra superior. Nessa visualização só ficam disponíveis: Rolagem de Dados, Combate, Mapa e Histórico. O componente de rolagem de dados é reformulado com uma nova mecânica: três botões de dado rápido (d20, d8, d6) e um controle de modificador numérico com incrementar/decrementar, aplicado automaticamente a cada rolagem. Rolar um d20 (incluindo variações como 2d20) deve abrir um modal perguntando se acertou (expira em 10s); se sim, abre modal de rolagem de dano (mesmas regras de dano já existentes) e depois permite escolher o alvo que recebe o dano. Corrigir bug: dado não convencional (ex. d27) deve ter animação apropriada em vez de comportamento incorreto."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Entrar no Modo Jogo (Priority: P1)

Durante uma sessão em andamento, um jogador ou mestre quer uma tela sem distrações, mostrando apenas o que é necessário para jogar: rolar dados, atacar, ver o mapa e consultar o histórico de rolagens. Ele clica em um botão "Modo Jogo" na barra superior e a tela é reorganizada para mostrar somente esses quatro blocos, ocultando configuração de encontro, participantes e demais painéis administrativos.

**Why this priority**: É a mudança estrutural que habilita todo o resto da feature — sem essa visão focada, a nova rolagem rápida e o fluxo de ataque/dano não têm onde "viver" de forma isolada.

**Independent Test**: Pode ser testado sozinho abrindo a sessão, clicando em "Modo Jogo" e verificando que somente os quatro blocos (Dados, Combate, Mapa, Histórico) aparecem, com um botão visível para voltar à visão completa.

**Acceptance Scenarios**:

1. **Given** o usuário está na visão completa da sessão, **When** ele clica no botão "Modo Jogo" na barra superior, **Then** a tela passa a exibir apenas os blocos de Rolagem de Dados, Combate, Mapa e Histórico, e os demais painéis (configuração de encontro, participantes, etc.) ficam ocultos.
2. **Given** o usuário está no Modo Jogo, **When** ele clica no botão para sair do Modo Jogo, **Then** a visão completa (todos os painéis) volta a ser exibida.
3. **Given** o usuário está no Modo Jogo, **When** ele recarrega a página, **Then** o sistema exibe uma visão válida (completa ou Modo Jogo, conforme preferência lembrada) sem erros.

---

### User Story 2 - Rolagem rápida com modificador persistente (Priority: P1)

Durante o jogo, o usuário quer rolar rapidamente d20, d8 ou d6 sem digitar expressões. Ele ajusta um modificador uma vez (ex. +5) usando os botões de incrementar/decrementar, e esse modificador é aplicado automaticamente a todas as rolagens seguintes até que ele o altere de novo.

**Why this priority**: É o novo mecanismo central de rolagem do Modo Jogo e pré-requisito direto do fluxo de ataque/dano (User Story 3).

**Independent Test**: Pode ser testado sozinho ajustando o modificador para um valor diferente de zero, clicando em cada um dos três botões de dado (d20, d8, d6) e conferindo que o resultado exibido é "valor do dado + modificador", sem precisar reconfigurar nada entre rolagens.

**Acceptance Scenarios**:

1. **Given** o modificador está em 0, **When** o usuário clica em "+" três vezes, **Then** o modificador exibido passa a ser +3.
2. **Given** o modificador está em +3, **When** o usuário clica no botão d8, **Then** o sistema rola 1d8, soma +3 ao resultado do dado, e exibe o resultado final e o valor bruto do dado.
3. **Given** o modificador foi ajustado para +3, **When** o usuário rola d20 e depois d6 em sequência, **Then** ambas as rolagens usam o mesmo modificador +3 sem que o usuário precise reinserir o valor.
4. **Given** o modificador está em um valor negativo (ex. -2), **When** o usuário clica em "-" repetidamente, **Then** o modificador continua decrescendo (valores negativos são permitidos).

---

### User Story 3 - Fluxo de ataque e dano a partir do d20 (Priority: P2)

Ao rolar um d20 (ou uma rolagem múltipla como 2d20), o usuário quer imediatamente decidir se aquele ataque acertou e, em caso positivo, rolar o dano e aplicá-lo a um alvo, tudo sem sair da tela de jogo.

**Why this priority**: Depende da rolagem rápida (US2) já existir; entrega o valor central de "jogar combate mais rápido", mas é uma camada acima da rolagem básica.

**Independent Test**: Pode ser testado isoladamente rolando um d20 no Modo Jogo, respondendo "Acertou" no modal de confirmação, rolando o dano no modal seguinte e escolhendo um alvo da lista de combatentes — e verificando que o dano é registrado para o alvo escolhido.

**Acceptance Scenarios**:

1. **Given** o usuário está no Modo Jogo, **When** ele rola um d20 usando o botão rápido, **Then** um modal aparece perguntando "Acertou?" junto ao resultado da rolagem.
2. **Given** o modal "Acertou?" está aberto, **When** o usuário não responde por 10 segundos, **Then** o modal se fecha automaticamente e nenhuma rolagem de dano é iniciada.
3. **Given** o modal "Acertou?" está aberto, **When** o usuário clica em "Sim", **Then** um modal de rolagem de dano é aberto, seguindo as mesmas regras de rolagem de dano já usadas no fluxo de combate existente.
4. **Given** o modal "Acertou?" está aberto, **When** o usuário clica em "Não", **Then** o modal se fecha imediatamente e nenhuma rolagem de dano é iniciada.
5. **Given** o modal de dano exibiu um resultado, **When** o usuário escolhe um combatente da lista de alvos, **Then** o dano é aplicado a esse combatente e refletido no estado do encontro/histórico.
6. **Given** o usuário rola uma expressão múltipla de d20 (ex. "2d20" ou "2#d20"), **When** a rolagem é concluída, **Then** o mesmo modal "Acertou?" aparece considerando o resultado dessa rolagem múltipla.

---

### User Story 4 - Correção da animação de dados não convencionais (Priority: P3)

Quando alguém rola um dado com número de faces fora do conjunto padrão (ex. d27), o usuário quer ver uma animação de rolagem coerente com aquele dado (ou ao menos uma animação genérica plausível), em vez de a rolagem passar sem nenhuma animação visual ou de o sistema usar informações erradas de outro dado.

**Why this priority**: É uma correção de bug isolada, sem dependência com as outras histórias, incluída no mesmo esforço por ter sido levantada junto.

**Independent Test**: Pode ser testado digitando uma expressão com um dado fora do conjunto padrão (ex. "1d27") na rolagem por expressão e conferindo que aparece uma animação de dado plausível (não a de outro valor de faces, e não a ausência de animação) antes do resultado.

**Acceptance Scenarios**:

1. **Given** o usuário insere uma expressão com um dado de faces não padrão (ex. "1d27"), **When** ele executa a rolagem, **Then** o sistema exibe uma animação de rolagem para esse termo (usando um visual genérico apropriado), em vez de omitir a animação ou reutilizar incorretamente o visual de outro dado.
2. **Given** uma expressão combina um dado padrão e um não padrão (ex. "1d20+1d27"), **When** a rolagem é executada, **Then** ambos os termos exibem alguma animação de dado (o padrão com seu visual específico, o não padrão com o visual genérico), e o resultado final soma corretamente as duas partes.

---

### Edge Cases

- O que acontece se o usuário clicar em um botão de dado rápido enquanto uma rolagem anterior ainda está animando? A rolagem anterior deve concluir (ou ser cancelada de forma limpa) antes de iniciar a próxima, sem sobrepor animações ou resultados.
- O que acontece se o modal "Acertou?" for aberto e o usuário navegar para fora do Modo Jogo (ou sair da sessão) antes de responder? O modal deve ser descartado sem aplicar dano.
- O que acontece se não houver nenhum combatente disponível como alvo quando o modal de dano pedir para escolher o alvo? O sistema deve indicar claramente que não há alvos e permitir fechar o modal sem aplicar dano.
- O que acontece se o usuário não tiver permissão para atacar/aplicar dano no encontro atual (ex. encontro não está ativo)? O fluxo de "Acertou?"/dano não deve ser iniciado, ou deve informar que a ação não é permitida.
- O que acontece com o modificador ao alternar entre Modo Jogo e a visão completa, ou ao recarregar a página? O valor do modificador é uma preferência local de sessão de jogo e pode ser reiniciado para 0 ao recarregar, desde que isso seja consistente e não cause confusão.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: O sistema DEVE exibir um botão "Modo Jogo" na barra superior, visível durante uma sessão em andamento.
- **FR-002**: Ao ativar o Modo Jogo, o sistema DEVE exibir apenas os blocos de Rolagem de Dados, Combate, Mapa e Histórico, ocultando os demais painéis (configuração de encontro, participantes, etc.).
- **FR-003**: O sistema DEVE fornecer um botão visível para sair do Modo Jogo e retornar à visão completa a qualquer momento.
- **FR-004**: No Modo Jogo, o sistema DEVE substituir a rolagem de dados por três botões de rolagem rápida: d20, d8 e d6.
- **FR-005**: O sistema DEVE fornecer um controle de modificador numérico com botões de incrementar e decrementar, exibindo o valor atual do modificador.
- **FR-006**: O sistema DEVE aplicar automaticamente o valor atual do modificador como bônus somado ao resultado sempre que um dos três botões de dado for clicado, sem exigir reconfiguração a cada rolagem.
- **FR-007**: O modificador DEVE poder assumir valores positivos, negativos ou zero.
- **FR-008**: Ao concluir uma rolagem de d20 (incluindo variações de múltiplas rolagens do mesmo dado, ex. "2d20"/"2#d20") feita pelos botões rápidos, o sistema DEVE exibir um modal perguntando se o ataque acertou, junto ao resultado da rolagem.
- **FR-009**: O modal de confirmação de acerto DEVE se fechar automaticamente após 10 segundos sem resposta do usuário, sem iniciar rolagem de dano.
- **FR-010**: Se o usuário confirmar que o ataque acertou, o sistema DEVE abrir um modal de rolagem de dano que segue as mesmas regras de rolagem de dano já usadas no fluxo de combate existente da aplicação.
- **FR-011**: Se o usuário indicar que o ataque não acertou (ou o modal expirar), o sistema NÃO DEVE iniciar a rolagem de dano.
- **FR-012**: Após exibir o resultado da rolagem de dano, o sistema DEVE permitir que o usuário escolha, entre os combatentes/participantes disponíveis no encontro atual, quem recebe esse dano.
- **FR-013**: Ao selecionar o alvo, o sistema DEVE aplicar o dano ao combatente escolhido, refletindo essa aplicação no estado do encontro e no histórico de rolagens, de forma consistente com o fluxo de dano já existente.
- **FR-014**: O sistema DEVE corrigir a animação de rolagem para dados com número de faces fora do conjunto padrão atualmente animado (ex. d27): esses dados DEVEM exibir alguma animação de rolagem apropriada/genérica, e NÃO DEVEM ser exibidos sem nenhuma animação nem usar incorretamente o visual de outro valor de faces (como o d100).
- **FR-015**: A correção do comportamento de dados não convencionais DEVE se aplicar tanto às rolagens feitas pelos botões rápidos do Modo Jogo quanto às rolagens por expressão livre já existentes na aplicação.

### Key Entities

- **Modificador de Rolagem Rápida**: valor numérico corrente (pode ser positivo, negativo ou zero) mantido enquanto o usuário está no Modo Jogo, aplicado a toda rolagem feita pelos botões rápidos.
- **Rolagem de Ataque (d20)**: resultado de uma rolagem de d20 (simples ou múltipla) feita pelos botões rápidos, que dispara o fluxo de confirmação de acerto.
- **Rolagem de Dano**: resultado de uma rolagem de dano associada a uma rolagem de ataque confirmada como acerto, seguindo as regras de dano já existentes.
- **Alvo do Dano**: combatente/participante do encontro atual escolhido para receber o dano de uma rolagem de dano confirmada.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Um jogador consegue entrar no Modo Jogo e realizar uma rolagem de d20 com modificador em menos de 5 segundos, sem precisar digitar nenhuma expressão de dado.
- **SC-002**: Em 100% das rolagens de d20 feitas pelos botões rápidos, o modal de confirmação de acerto aparece, e em 100% dos casos em que não há resposta em 10 segundos, o modal se fecha sozinho sem iniciar rolagem de dano.
- **SC-003**: Um jogador consegue ir do "resultado do ataque" até "dano aplicado a um alvo escolhido" em no máximo 3 interações (confirmar acerto, ver dano, escolher alvo).
- **SC-004**: 100% das rolagens de dados com faces fora do conjunto padrão (ex. d27) exibem alguma animação de rolagem visível, eliminando os casos anteriores de rolagem sem nenhum feedback visual.
- **SC-005**: O modificador configurado permanece aplicado corretamente em pelo menos 10 rolagens consecutivas sem que o usuário precise reconfigurá-lo.

## Assumptions

- O Modo Jogo é um modo de exibição alternativo dentro da mesma tela de sessão (mostra/oculta blocos), e não uma rota/página separada, já que a aplicação hoje não possui um sistema de abas de navegação.
- "Combate" no Modo Jogo corresponde ao fluxo de ataque/turno já existente (ex. rastreador de iniciativa e ataque), reaproveitado como está, exceto pela nova rolagem rápida de d20 que dispara o fluxo de acerto/dano.
- O botão "Modo Jogo" fica disponível tanto para o mestre quanto para jogadores participantes da sessão; permissões de quem pode atacar/aplicar dano continuam sendo as mesmas já aplicadas ao fluxo de combate existente.
- O bug relatado como "animação incorreta de d100 para dados não convencionais" corresponde, no comportamento atual do sistema, à ausência total de animação para dados fora do conjunto suportado (o termo é descartado do conjunto de dados animados); a correção deste bug consiste em garantir que esses dados sempre recebam alguma animação de rolagem, sem que fiquem sem feedback visual nem reutilizem incorretamente o visual fixo de outro valor de faces.
- Os três botões rápidos (d20, d8, d6) substituem, no Modo Jogo, a rolagem por expressão livre; a rolagem por expressão livre continua existindo fora do Modo Jogo (na visão completa) sem alterações no seu comportamento, além da correção do bug de animação.
- O valor do modificador é reiniciado para 0 sempre que a página é recarregada ou a sessão é reaberta, não sendo persistido entre sessões do navegador.
- "Escolher quem recebe o dano" reaproveita a mesma lista de combatentes/alvos e o mesmo mecanismo de aplicação de dano já existentes no fluxo de combate atual (seleção de alvo(s) entre os combatentes do encontro ativo).
