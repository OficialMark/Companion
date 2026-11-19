# Tasks: Ficha pelo botão direito e resultado da rolagem visível por 2 s

**Input**: Design documents from `specs/010-ficha-botao-direito/`
**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/ui-interaction.md](contracts/ui-interaction.md), [quickstart.md](quickstart.md)

**Tests**: pedidos pela constituição (IV) e pelos critérios SC-002 a SC-004. São testes de unidade em Vitest e um e2e em Playwright.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Setup

- [X] T001 Criar o branch da tarefa no worktree da sessão, a partir de `origin/main`, e subir a stack isolada (3638) para o e2e

## Phase 2: Foundational

- [X] T002 Criar a constante `RESULT_HOLD_MS = 2000` em `frontend/app/utils/creatureRoll.ts`

## Phase 3: User Story 1 - Abrir a ficha com o botão direito (P1) 🎯 MVP

**Goal**: o botão direito num token com ficha abre a ficha; tokens sem ficha mantêm o menu.
**Independent Test**: com um goblin no mapa, o botão direito abre a ficha "Token · Goblin" sem o menu do token.

- [X] T003 [US1] Criar `sheetFor(token)` (ponto único) e `openTokenContext(token)` em `frontend/app/components/battlemap/BattlemapBoard.vue`, e ligar ao botão 2 em `startTokenDrag` e ao `can-open-sheet` do menu
- [X] T004 [US1] Emitir o evento `context` em Shift+F10 e na tecla Menu em `frontend/app/components/battlemap/BattlemapToken.vue`, e ligá-lo a `openTokenContext` no tabuleiro
- [X] T005 [US1] Mostrar a dica "Botão direito numa criatura abre a ficha." quando `hasSheetTokens`, na barra de status de `BattlemapBoard.vue`
- [X] T006 [P] [US1] Testes de unidade dos gestos em `frontend/tests/token-sheet-shortcut.spec.ts`
- [X] T007 [US1] Crítico 1 (médio): o botão direito fecha **qualquer** menu de token aberto (`if (selectedParticipantId.value) closeAttackAreaPanel()`), não só o do token clicado. Arquivo: `BattlemapBoard.vue`. Teste com o menu de outro token aberto em `tests/token-sheet-shortcut.spec.ts`
- [X] T008 [US1] Crítico 2 (médio): o teste "o botão direito nunca arrasta" precisa poder falhar. Ele deve levar o ponteiro a outra célula, soltar (`pointerup`) e verificar que não houve `move`. Arquivo: `tests/token-sheet-shortcut.spec.ts`. Confirmar por mutação
- [X] T009 [US1] Crítico 3: o caminho do teclado respeita o modo névoa e a falta de conexão, como o botão direito. `openTokenContext` retorna cedo com `fog.active` ou sem `online`. Arquivo: `BattlemapBoard.vue`, com teste
- [X] T010 [P] [US1] Crítico 5: anunciar o atalho no rótulo acessível do token com ficha ("Shift+F10 abre a ficha") e citar a tecla na dica. Arquivos: `BattlemapToken.vue` e `BattlemapBoard.vue`

## Phase 4: User Story 2 - Ver o resultado da rolagem da ficha (P1)

**Goal**: dados e total visíveis por 2 s dentro da ficha; histórico e preset atualizados na hora.
**Independent Test**: rolar com a ficha no topo; a área dos dados fica pelo menos 95% visível e dura entre 1,7 s e 3,5 s.

- [X] T011 [US2] Espera de 2 s com `holdTimer` e `clearAnimation`, botão liberado na revelação, rolagem até `.roll-stage` e `data-roll-showing`, em `frontend/app/components/creatures/CreatureRollPanel.vue` e `frontend/app/assets/css/creatures.css`
- [X] T012 [P] [US2] Testes de unidade com timers falsos (espera, interrupção, erro, fechamento durante a espera) em `frontend/tests/token-sheet-shortcut.spec.ts`
- [X] T013 [US2] E2E no navegador real em `frontend/tests/e2e/token-sheet-shortcut.spec.ts`: medição dentro da área visível da ficha e duração. Mutação: sem a rolagem, falhou 3 de 3; com a rolagem, passou 3 de 3
- [X] T014 [US2] Crítico 4: fechar a ficha durante a animação, antes da revelação, não deixa timer nem rolagem pendente. Uma flag `disposed` em `onBeforeUnmount` impede o novo `holdTimer` e o `scrollIntoView`. Arquivo: `CreatureRollPanel.vue`. Teste que desmonta entre o clique e a revelação e verifica `vi.getTimerCount() === 0`

## Phase 5: Polish & Cross-Cutting

- [X] T015 Rodar `pnpm vitest run`, `pnpm nuxt typecheck` e os e2e `token-sheet-shortcut`, `compendium-sheet` e `creature-catalog` contra a stack da sessão
- [X] T018 Achado durante a T015: o e2e falhava sob carga porque a API limita criar/entrar a 12 por minuto por IP e responde 429 com corpo vazio. O usuário via "Não foi possível concluir a operação.". Agora vê "Muitas tentativas em pouco tempo. Aguarde um minuto e tente de novo.". Arquivos: `frontend/app/composables/useCompanionApi.ts`, teste em `frontend/tests/api-errors.spec.ts`. Os e2e passaram a rodar em lotes que respeitam o limite.
- [X] T016 Crítico frio de novo, sobre as correções, até PASS
- [X] T017 Commit, PR e merge na `main`

## Dependencies & Execution Order

- T001–T006 e T011–T013 já foram feitos.
- T007 e T009 mexem no mesmo trecho de `BattlemapBoard.vue` e vão em sequência. T010 toca `BattlemapToken.vue` e a dica, e pode correr em paralelo a T014 (`CreatureRollPanel.vue`). T008 só mexe no teste.
- T015 vem depois de T007–T010 e de T014. Depois vêm T016 e, por fim, T017.

## Parallel Example

```text
T010 (BattlemapToken.vue) || T014 (CreatureRollPanel.vue)
T007 → T009 (BattlemapBoard.vue, openTokenContext)
```

## Implementation Strategy

US1 e US2 são independentes e as duas já estão entregues. Esta rodada fecha os achados do crítico, roda tudo e só então faz o merge.
