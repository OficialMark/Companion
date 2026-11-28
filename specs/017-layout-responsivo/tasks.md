# Tasks: Layout Responsivo por Princípios de Design

**Input**: spec.md, plan.md, research.md, quickstart.md
**Status**: implementada; todas as tarefas concluídas.

## Phase 1: Setup

- [X] T001 Rubrica congelada (gates G1–G7 e princípios P1–P6) antes de qualquer CSS, fora do repositório (scratchpad/critique/rubric.md)
- [X] T002 Auditor de layout com medições no navegador e capturas em frontend/tests/e2e/layout-audit.spec.ts
- [X] T003 Linha de base "antes" (48 telas) e review fria de design por crítico sem contexto

## Phase 2: Foundational

- [X] T004 Zonas da Mesa (pessoas, mapa, dados) e ordem do celular em frontend/app/pages/index.vue
- [X] T005 Grid de 1/2/3 colunas, colunas fixas com rolagem própria e sombra em frontend/app/assets/css/main.css
- [X] T006 Limite do dashboard de 1380 para 2400 px em frontend/app/assets/css/main.css

## Phase 3: User Story 1 - Celular sem sobreposição (P1) 🎯 MVP

- [X] T007 [US1] Barra de abas inferior e barra superior em 2 linhas em frontend/app/assets/css/main.css
- [X] T008 [US1] Resumo da ficha acoplado à barra de abas (bloco de 100 px) em frontend/app/assets/css/main.css
- [X] T009 [US1] Barra do mapa em uma linha de ícones de 44 px em frontend/app/assets/css/battlemap.css
- [X] T010 [US1] Abas da Ficha em uma linha a partir de 360 px em frontend/app/assets/css/character.css
- [X] T011 [P] [US1] Alvos de 44 px: abas do compêndio, expressão de rolagem, botões compactos, controle do código do mestre
- [X] T012 [US1] `scroll-padding` acima e abaixo das barras fixas em frontend/app/assets/css/main.css

## Phase 4: User Story 2 - Tela larga (P1)

- [X] T013 [US2] Mapa ajusta ao palco até 80 px e para ao primeiro toque (`hold`) em frontend/app/composables/useBattlemapViewport.ts e frontend/app/components/battlemap/BattlemapBoard.vue
- [X] T014 [US2] Ficha em 2 colunas a partir de 1440 px em frontend/app/components/character/SheetCoreTab.vue e frontend/app/assets/css/character.css
- [X] T015 [P] [US2] Seletor de criaturas e grade de dados por container query em frontend/app/assets/css/creatures.css e frontend/app/assets/css/roll-panel.css
- [X] T016 [P] [US2] Campanha na mesma largura das outras áreas em frontend/app/assets/css/campaign-knowledge.css

## Phase 5: User Story 3 - Hierarquia e estados (P2)

- [X] T017 [US3] Desabilitado por forma (tracejado), primário mantém o tom em frontend/app/assets/css/main.css
- [X] T018 [US3] Dica do "Rolar" desabilitado com `aria-describedby` em frontend/app/components/character/PlayerRollPanel.vue
- [X] T019 [US3] "Limpar dados" como 8ª célula da grade em frontend/app/components/character/DicePad.vue
- [X] T020 [US3] Iniciativa acima do catálogo de combatentes em frontend/app/pages/index.vue
- [X] T021 [US3] Rótulos da barra superior viram ícones abaixo de 1440 px em frontend/app/assets/css/main.css
- [X] T022 [US3] Um catálogo por propósito: seletor do compêndio sem modo de combate na preparação, switch de reforço no combate em frontend/app/components/battlemap/TokenSourcePicker.vue

## Phase 6: User Story 4 - Gate automático (P2)

- [X] T023 [US4] Checagens extras do auditor: nome da campanha ≥ 100 px, abas fora da lista, colunas com rolagem, conteúdo de `<details>` fechado
- [X] T024 [US4] Roteiro da instância de validação em docs/testes-e2e.md e link no README.md

## Phase 7: User Story 5 - Ficha estável (P3)

- [X] T025 [US5] `remoteChangePending` e `reconcile()` preservando "Salvo" em frontend/app/stores/character.ts
- [X] T026 [US5] Testes que falham sem a correção em frontend/tests/character-store.spec.ts

## Phase 8: Polish

- [X] T027 E2E ajustados ao fluxo novo e sem corrida de busca (`Ver ficha de Goblin` exato) em frontend/tests/e2e/
- [X] T028 Testes de componente: dice-pad, player-roll-panel, token-source-picker, battlemap-viewport, battlemap-markers
- [X] T029 Rodadas cegas A/B até PASS; vitest, typecheck, e2e desktop + Android e gate de layout verdes

## Phase 9: Acabamento responsivo

- [X] T030 Histórico do jogador na coluna de pessoas no desktop, com teste de visibilidade e de redimensionamento em frontend/tests/e2e/sticky-columns.spec.ts
- [X] T031 Fundo do palco do battlemap na cor do painel
- [X] T032 Contraste do primário desabilitado com borda tracejada
- [X] T033 Rodapés fixos com cantos inferiores arredondados e borda
- [X] T034 Espaçamento e rótulos acessíveis nos controles de vantagem
- [X] T035 Ação única de criação e estado vazio centralizado na Campanha
- [X] T036 Colunas laterais proporcionais e combatentes em uma linha nas telas largas
- [X] T037 Rótulo completo de salvaguarda nos cartões estreitos
- [X] T038 Ações e paginação em uma linha no compêndio, com iniciais quando a imagem não carrega
- [X] T039 Rótulo Pôr e rolagem da página no seletor de tokens móvel
- [X] T040 Fluxo das perícias por coluna, na mesma ordem do teclado

## Phase 10: Disposição das colunas

- [X] T041 Rolagem à esquerda e Participantes acima do Histórico à direita em desktop para mestre e jogador
- [X] T042 Compêndio e Campanha centralizados, com limite de 1380 px
- [X] T043 Testes de posição das colunas e auditoria de largura e centralização
- [X] T044 Preservar edições da ficha durante a atualização disparada pelo SignalR, com teste de regressão dos PV

## Dependencies

Setup → Foundational → US1 e US2 (paralelos) → US3 → US4 → US5 → Polish.
