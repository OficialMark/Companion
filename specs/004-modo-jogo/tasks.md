---

description: "Task list template for feature implementation"
---

# Tasks: Modo Jogo (Visão Focada de Gameplay)

**Input**: Design documents from `/specs/004-modo-jogo/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/README.md](./contracts/README.md), [quickstart.md](./quickstart.md)

**Tests**: Incluídos — a constituição do projeto exige testes unitários para regras de domínio e teste de regressão para bugs reproduzíveis (Princípio IV), então esta lista inclui tarefas de teste.

**Organization**: Tarefas agrupadas por user story (US1-US4, conforme spec.md) para permitir implementação e teste independentes de cada uma.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Pode rodar em paralelo (arquivos diferentes, sem dependência pendente)
- **[Story]**: A qual user story a tarefa pertence (US1, US2, US3, US4)
- Caminhos de arquivo exatos incluídos em cada descrição

## Path Conventions

Projeto web existente: mudanças confinadas a `frontend/app/` (componentes, composables,
utils) e `frontend/tests/`. Nenhuma mudança em `backend/` (ver plan.md — feature
frontend-only, reaproveitando contratos REST existentes).

---

## Phase 1: Setup

**Purpose**: Confirmar que o ambiente está pronto; nenhuma dependência nova é necessária.

- [X] T001 Rodar `npm run test` e `npm run typecheck` em `frontend/` na branch `004-modo-jogo` para confirmar baseline verde antes de iniciar (nenhuma dependência nova a instalar — feature reaproveita Vue 3/Nuxt 4/Pinia já presentes em `frontend/package.json`)
  > Node.js LTS instalado via `winget` e `pnpm` via `npm install -g pnpm` (projeto usa
  > `pnpm-lock.yaml`). `pnpm run test` e `pnpm run typecheck` confirmados verdes.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Estado compartilhado que as demais fases consomem.

**⚠️ CRITICAL**: Nenhuma user story pode ser finalizada/integrada sem esta fase, pois
US2 e US3 renderizam seus componentes apenas quando o Modo Jogo está ativo.

- [X] T002 Criar `frontend/app/composables/useGameMode.ts` expondo um estado
  compartilhado (`active: Ref<boolean>` + `toggle()`/`enter()`/`exit()`), reiniciado para
  `false` a cada carregamento de página, para ser consumido por `index.vue` e pelos
  componentes que precisam saber se o Modo Jogo está ativo

**Checkpoint**: Fundação pronta — as user stories abaixo podem ser implementadas.

---

## Phase 3: User Story 1 - Entrar no Modo Jogo (Priority: P1) 🎯 MVP

**Goal**: Botão na barra superior que alterna entre a visão completa e uma visão que
mostra apenas Rolagem de Dados, Combate, Mapa e Histórico.

**Independent Test**: Clicar em "Modo Jogo" e verificar que somente os quatro blocos
aparecem; clicar novamente e verificar que a visão completa volta.

### Implementation for User Story 1

- [X] T003 [P] [US1] Criar `frontend/app/components/GameModeToggle.vue`: botão que
  consome `useGameMode()` (de T002), alterna `active` ao ser clicado e troca o próprio
  rótulo ("Modo Jogo" / "Voltar à visão completa") conforme o estado atual
- [X] T004 [US1] Montar `GameModeToggle` dentro do `.topbar` de
  `frontend/app/pages/index.vue` (depende de T003)
- [X] T005 [US1] Atualizar o template do `.dashboard-grid` em
  `frontend/app/pages/index.vue` para, quando `useGameMode().active` for `true`, ocultar
  os painéis de configuração de encontro (`EncounterSetup`), participantes
  (`ParticipantsPanel`) e demais painéis administrativos, mantendo sempre visíveis os
  blocos de Rolagem de Dados, Combate (`InitiativeTracker`/`CombatAttack`), Mapa
  (`BattlemapBoard`/`BattlemapSetup`) e Histórico (`RollHistory`) (depende de T004)
- [X] T006 [P] [US1] Adicionar teste de componente em
  `frontend/tests/game-mode.spec.ts` verificando que, ao alternar `useGameMode().active`,
  os painéis administrativos somem/reaparecem e os quatro blocos do Modo Jogo permanecem
  visíveis em ambos os estados
  > Nota de execução: `frontend/vitest.config.ts` roda em Vite puro (sem o pipeline de
  > build do Nuxt), então os componentes referenciados sem import explícito em
  > `index.vue` (convenção de auto-import do Nuxt) não são resolvíveis via `mount()`
  > fora do Nuxt. O teste implementado cobre `useGameMode()` e `GameModeToggle.vue`
  > isoladamente (estado e alternância de rótulo); a ocultação real dos painéis em
  > `index.vue` deve ser validada manualmente via quickstart.md (Cenário 1).

**Checkpoint**: Neste ponto, a User Story 1 deve estar totalmente funcional e testável
de forma independente.

---

## Phase 4: User Story 2 - Rolagem rápida com modificador persistente (Priority: P1)

**Goal**: Três botões de dado rápido (d20/d8/d6) e um controle de modificador
incrementar/decrementar, aplicado automaticamente a cada rolagem.

**Independent Test**: Ajustar o modificador para um valor diferente de zero, clicar em
cada um dos três botões e conferir que o resultado é "valor do dado + modificador", sem
reconfigurar nada entre rolagens.

### Tests for User Story 2 ⚠️

> **NOTE: Escrever estes testes primeiro e garantir que falham antes da implementação.**

- [X] T007 [P] [US2] Adicionar testes unitários em `frontend/tests/dice.spec.ts` para um
  novo composable `useQuickRollModifier` cobrindo: incrementar, decrementar, valores
  negativos/zero/positivos, e a formatação da expressão enviada para cada dado (ex.
  `1d20+3`, `1d8-2`, `1d6` quando o modificador é 0)

### Implementation for User Story 2

- [X] T008 [P] [US2] Criar `frontend/app/composables/useQuickRollModifier.ts` com
  `value: Ref<number>`, `increment()`, `decrement()` e `expressionFor(faces: 20 | 8 | 6)`
  retornando a expressão com o modificador atual aplicado (faz T007 passar)
- [X] T009 [US2] Criar `frontend/app/components/QuickDiceRoller.vue`: três botões
  (d20/d8/d6) e controles +/- do modificador (usando `useQuickRollModifier` de T008),
  reaproveitando a mesma sequência de `api.rollDice`/`api.revealDiceRoll` e o utilitário
  `diceForAnimation`/`diceWithResults` já usados em `frontend/app/components/DiceRoller.vue`
  para rolar e animar; emite `rolled` para resultados de d8/d6 e `rolled-d20` para
  resultados de d20 (depende de T008)
- [X] T010 [US2] Em `frontend/app/pages/index.vue`, renderizar `QuickDiceRoller` no lugar
  de `DiceRoller` quando `useGameMode().active` for `true` (depende de T005, T009)
- [X] T011 [P] [US2] Adicionar teste de componente em `frontend/tests/dice.spec.ts`
  montando `QuickDiceRoller.vue`, mockando `api.rollDice`, e verificando que (a) a
  expressão enviada inclui o modificador atual e (b) o mesmo modificador é reaplicado em
  duas rolagens consecutivas sem reconfiguração

**Checkpoint**: Neste ponto, as User Stories 1 e 2 devem funcionar de forma independente
(US2 assume que o contêiner do Modo Jogo de US1 já existe para ser montada).

---

## Phase 5: User Story 3 - Fluxo de ataque e dano a partir do d20 (Priority: P2)

**Goal**: Rolar d20 → modal "Acertou?" (expira em 10s) → se sim, modal de rolagem de dano
(mesmas regras já existentes) → escolher alvo → dano aplicado.

**Independent Test**: Rolar um d20 no Modo Jogo, responder "Sim" no modal, rolar o dano,
escolher um alvo e confirmar — verificando que o dano é registrado para o alvo escolhido.

### Tests for User Story 3 ⚠️

> **NOTE: Escrever estes testes primeiro e garantir que falham antes da implementação.**

- [X] T012 [P] [US3] Adicionar teste de integração em `frontend/tests/attack.spec.ts`
  cobrindo o caminho: rolagem de d20 → confirmar acerto → rolar dano → selecionar alvo →
  `api.resolveAttack` chamado com o(s) `targetCombatantIds` escolhido(s); e o caminho de
  expiração automática em 10s (usando timers falsos do Vitest) onde `resolveAttack` nunca
  é chamado

### Implementation for User Story 3

- [X] T013 [P] [US3] Extrair a lógica de seleção de alvo + `resolveAttack` de
  `frontend/app/components/CombatAttack.vue` (computed `canAttack`, `rolled`, `confirm`,
  `cancel`) para um composable `frontend/app/composables/useAttackResolution.ts`,
  atualizando `CombatAttack.vue` para consumi-lo e mantendo
  `frontend/tests/attack.spec.ts` (testes já existentes) passando sem alteração de
  comportamento
- [X] T014 [P] [US3] Criar `frontend/app/components/AttackConfirmModal.vue`: recebe a
  `DiceRoll` do d20 via prop, exibe o resultado e os botões "Sim"/"Não", fecha
  automaticamente após 10s sem resposta (via `setTimeout`, limpo em `onBeforeUnmount`), e
  emite `hit` (com a rolagem) ou `dismissed`
- [X] T015 [US3] Criar `frontend/app/components/DamageRollModal.vue` reaproveitando
  `useAttackResolution` (de T013) e embutindo `DiceRoller` (`mode="attack"`) para rolar o
  dano, seguido da mesma UI de seleção de alvo (checkboxes sobre
  `encounter.combatants`) e confirmação já usada em `CombatAttack.vue` (depende de T013)
- [X] T016 [US3] Em `frontend/app/pages/index.vue` (ou no componente container do Modo
  Jogo), escutar o evento `rolled-d20` de `QuickDiceRoller` (T009) para abrir
  `AttackConfirmModal` (T014), e no evento `hit` abrir `DamageRollModal` (T015) (depende
  de T009, T014, T015)

**Checkpoint**: Neste ponto, as User Stories 1, 2 e 3 devem funcionar de forma
independente.

---

## Phase 6: User Story 4 - Correção da animação de dados não convencionais (Priority: P3)

**Goal**: Dados com faces fora do conjunto padrão (ex. d27) devem exibir uma animação de
rolagem apropriada/genérica, em vez de nenhuma animação (bug atual) ou de reutilizar o
visual de outro valor de faces.

**Independent Test**: Rolar a expressão `1d27` (fora do Modo Jogo, na rolagem por
expressão livre) e conferir que aparece uma animação de dado com visual genérico, e que
`1d20+1d27` anima ambos os termos corretamente.

### Tests for User Story 4 ⚠️

> **NOTE: Escrever estes testes primeiro (devem falhar contra o comportamento atual) e
> garantir que passam após a correção — servem como teste de regressão do bug.**

- [X] T017 [P] [US4] Adicionar testes de regressão em `frontend/tests/dice.spec.ts` para
  `diceForAnimation('1d27')` (deve retornar um `AnimatedDie` com `faces: 27`, hoje retorna
  vazio) e `diceForAnimation('1d20+1d27')` (deve retornar dois dados: um com `faces: 20`
  e um com `faces: 27`, hoje retorna somente o de 20)

### Implementation for User Story 4

- [X] T018 [P] [US4] Em `frontend/app/utils/diceAnimation.ts`: alargar o tipo `faces` de
  `AnimatedDie` para `number` (mantendo `ANIMATED_DICE_FACES` apenas como referência de
  quais faces têm visual dedicado) e remover os filtros `!animatedFaces.has(faces)` em
  `diceForAnimation` e `diceWithResults`, de modo que qualquer face válida aceita pelo
  backend (2–1000) gere um `AnimatedDie` (faz T017 passar)
- [X] T019 [P] [US4] Adicionar uma classe CSS neutra `poly-die-generic` (mesma
  convenção de `.poly-die-dN` já usada em `frontend/app/assets/css/main.css`) para
  representar visualmente qualquer face sem forma dedicada
- [X] T020 [US4] Atualizar `frontend/app/components/DiceRollAnimation.vue` para aplicar
  `poly-die-generic` (além de/no lugar de `poly-die-d{faces}`) quando `faces` não estiver
  no conjunto padrão, mantendo o valor de face exibido baseado no `faces` real do dado
  (depende de T018, T019)
- [X] T021 [P] [US4] Adicionar teste de componente para `DiceRollAnimation.vue`
  verificando que um dado com `faces: 27` recebe a classe `poly-die-generic` e um dado com
  `faces: 20` mantém `poly-die-d20` (depende de T020)

**Checkpoint**: Todas as user stories devem agora estar independentemente funcionais.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Validação final e acabamento que atravessa todas as user stories.

- [X] T022 [P] Rodar `npm run test` e `npm run typecheck` em `frontend/` cobrindo toda a
  feature (todas as suites novas/alteradas de T001-T021)
  > `pnpm run test`: 52/52 testes passando em 12 arquivos (incluindo `game-mode.spec.ts`,
  > casos novos de `dice.spec.ts` e `attack.spec.ts`). `pnpm run typecheck`: sem erros
  > (corrigido um erro de tipo do timer em `AttackConfirmModal.vue` — usar
  > `ReturnType<typeof setTimeout>` em vez de `ReturnType<typeof window.setTimeout>`,
  > consistente com o padrão já usado em `DiceRollAnimation.vue`).
- [ ] T023 Executar manualmente os 4 cenários de
  [quickstart.md](./quickstart.md) (Modo Jogo, rolagem rápida, ataque/dano/alvo, dado não
  convencional) de ponta a ponta
  > **Bloqueado nesta execução**: exige app rodando (frontend + backend) em um
  > navegador; não realizável neste ambiente. Pendente de validação manual.
- [X] T024 [P] Revisar acessibilidade dos novos modais (`AttackConfirmModal.vue`,
  `DamageRollModal.vue`): foco inicial, `aria-live`/`role="alertdialog"` e fechamento por
  tecla Esc, consistente com os demais painéis do app

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Sem dependências — pode começar imediatamente.
- **Foundational (Phase 2)**: Depende do Setup — T002 bloqueia a integração de US2/US3
  ao Modo Jogo (mas não bloqueia escrever seus testes/composables isoladamente).
- **User Stories (Phase 3-6)**: Todas dependem da Foundational completa para a
  *integração* final ao Modo Jogo; a lógica interna de cada história (composables,
  componentes isolados, testes unitários) pode ser desenvolvida em paralelo.
- **Polish (Phase 7)**: Depende de todas as user stories desejadas estarem completas.

### User Story Dependencies

- **US1 (P1)**: Depende apenas de T002 (Foundational). Sem dependência de outras
  histórias.
- **US2 (P1)**: Composable/testes (T007-T008) são independentes; a integração visual
  (T010) depende de T005 (US1) já ter definido onde o Modo Jogo renderiza o painel de
  dados.
- **US3 (P2)**: Composable/modais (T012-T015) são independentes; a integração (T016)
  depende de T009 (US2, evento `rolled-d20`).
- **US4 (P3)**: Totalmente independente — não depende de US1/US2/US3 nem é consumida por
  elas (o bug e a correção vivem no utilitário de animação e na rolagem por expressão
  livre, usada fora do Modo Jogo).

### Within Each User Story

- Testes escritos e falhando antes da implementação (quando presentes).
- Composables antes de componentes que os consomem.
- Componentes isolados antes da integração em `index.vue`.
- História completa e com checkpoint validado antes de prosseguir para a próxima
  prioridade.

### Parallel Opportunities

- Dentro de cada fase, tarefas marcadas `[P]` tocam arquivos diferentes e podem rodar em
  paralelo.
- US1, US2 (parte de composable/testes) e US4 podem ser desenvolvidas em paralelo por
  pessoas diferentes assim que T002 estiver pronto; a integração final de US2 e US3 deve
  aguardar T005/T009 respectivamente.
- US4 pode ser feita a qualquer momento, em paralelo com todas as outras, por não ter
  dependências cruzadas.

---

## Parallel Example: User Story 2

```bash
# Testes e composable de US2 podem ser feitos em paralelo com US1/US4:
Task: "Testes unitários de useQuickRollModifier em frontend/tests/dice.spec.ts (T007)"
Task: "Criar useQuickRollModifier.ts em frontend/app/composables/useQuickRollModifier.ts (T008)"
```

## Parallel Example: User Story 4 (independente do restante)

```bash
Task: "Testes de regressão diceForAnimation('1d27') em frontend/tests/dice.spec.ts (T017)"
Task: "Corrigir diceForAnimation/diceWithResults em frontend/app/utils/diceAnimation.ts (T018)"
Task: "Adicionar classe CSS poly-die-generic (T019)"
```

---

## Implementation Strategy

### MVP First (User Story 1 + 2)

1. Completar Phase 1: Setup
2. Completar Phase 2: Foundational (`useGameMode`)
3. Completar Phase 3: US1 (entrar/sair do Modo Jogo)
4. Completar Phase 4: US2 (rolagem rápida + modificador)
5. **PARAR e VALIDAR**: testar US1+US2 de forma independente (cenários 1 e 2 do
   quickstart.md)
6. Demonstrar/entregar se pronto — já é um MVP útil de "Modo Jogo com rolagem rápida"

### Incremental Delivery

1. Setup + Foundational → base pronta
2. US1 → testar independentemente → demo (visão focada funcionando)
3. US2 → testar independentemente → demo (rolagem rápida com modificador)
4. US3 → testar independentemente → demo (fluxo completo de ataque/dano/alvo)
5. US4 → testar independentemente → demo (bug de dado não convencional corrigido)
6. Cada história agrega valor sem quebrar as anteriores

### Parallel Team Strategy

Com múltiplos desenvolvedores, após Setup + Foundational:
- Dev A: US1 → depois integra com US2 (T010)
- Dev B: US2 (composable/testes) em paralelo, depois integra (T010) quando US1 estiver
  pronta
- Dev C: US3 (modais/composable) em paralelo, integra (T016) quando US2 estiver pronta
- Dev D: US4, totalmente em paralelo, sem esperar ninguém

---

## Notes

- `[P]` = arquivos diferentes, sem dependência pendente.
- O rótulo `[Story]` mapeia cada tarefa à sua user story para rastreabilidade.
- Nenhum novo endpoint de backend é criado; todas as tarefas de implementação reaproveitam
  `api.rollDice`, `api.revealDiceRoll` e `api.resolveAttack` já existentes (ver
  [contracts/README.md](./contracts/README.md)).
- Confirmar que os testes falham antes de implementar (T007, T012, T017 antes de suas
  respectivas implementações).
- Commitar após cada tarefa ou grupo lógico de tarefas.
- Parar em cada checkpoint para validar a história independentemente antes de seguir.
