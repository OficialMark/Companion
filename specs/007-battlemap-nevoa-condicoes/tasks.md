# Tasks: Névoa de guerra, condições com ícones e HP controlado pelo mestre

**Input**: Design documents from `specs/007-battlemap-nevoa-condicoes/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/battlemap-api.md, quickstart.md

**Tests**: obrigatórios pela constituição (princípio IV): unitários de domínio, integração de API, componentes Vitest e regressão do bug do menu.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Setup

- [ ] T001 Confirmar baseline verde após o merge da 006: `dotnet test` em `backend/` e `pnpm vitest run` + `pnpm nuxt typecheck` em `frontend/`

---

## Phase 2: Foundational (bloqueia US2, US3, US4)

- [ ] T002 Anexar `Paralyzed, Bleeding, Grappled, Deafened, Frightened, Charmed, Unconscious, Petrified, Incapacitated, Exhausted, Concentrating, Burning` ao enum em `backend/src/Companion.Domain/Models/StatusCondition.cs`
- [ ] T003 Adicionar `IsHitPointsHidden` + `SetHitPointsHidden(bool)` (lança `InvalidOperationException` para personagem) em `backend/src/Companion.Domain/Models/Combatant.cs`
- [ ] T004 Adicionar `FogMask`, `SetFog`, `IsCellFogged`, `IsAreaFogged`, `FogMaskLength` e reset de névoa em `Replace` quando o grid muda em `backend/src/Companion.Domain/Models/Battlemap.cs`
- [ ] T005 Configurar `FogMask` (`varbinary(1250)`), `IsHitPointsHidden` (default false) e `StatusConditions` com 512 em `backend/src/Companion.Infrastructure/Persistence/ModelConfigurations.cs`
- [ ] T006 Gerar migration `AddFogAndHitPointsVisibility` com `dotnet ef migrations add` em `backend/src/Companion.Infrastructure/Persistence/Migrations/` e conferir que o diff só contém as três colunas
- [ ] T007 Adicionar `IsHitPointsHidden` em `CombatantDto`, `FogMask` em `BattlemapSnapshotDto`, `SetFogRequest` e `SetHitPointsVisibilityRequest` em `backend/src/Companion.Application/Contracts/Dtos.cs`
- [ ] T008 [P] Espelhar os tipos (`StatusCondition` com 18 valores, `isHitPointsHidden`, `fogMask`) em `frontend/app/types/campaign.ts`

**Checkpoint**: backend compila, testes antigos passam.

---

## Phase 3: User Story 1 - Menu do token com clique esquerdo sem atrapalhar o arraste (P1) 🎯 MVP

**Goal**: clique esquerdo abre e mantém o menu; arraste move; botão direito não arrasta.

**Independent Test**: quickstart cenário 1.

- [ ] T009 [P] [US1] Testes de regressão em `frontend/tests/battlemap-token-menu-gesture.spec.ts`: clique esquerdo em token 1x1, 2x2 (click sintetizado sobre outra célula) e empilhado mantém o painel e não emite `move`; arraste emite `move`; botão direito abre sem arraste; clique em célula vazia após selecionar move; novo pointerdown zera a supressão
- [ ] T010 [US1] Em `frontend/app/components/battlemap/BattlemapBoard.vue`: flag `suppressSurfaceClick` armado no fim de sessão de token e consumido em `clickSurface`; zerado em `pointerDown` da superfície e no início de `startTokenDrag`; arraste só com botão principal
- [ ] T011 [US1] Em `frontend/app/components/battlemap/BattlemapBoard.vue`: botão direito no token seleciona sem arraste; `@contextmenu.prevent` no board; pan com botão direito/meio no fundo
- [ ] T012 [US1] Em `frontend/app/components/battlemap/BattlemapToken.vue`: repassar o botão do evento e não iniciar arraste para botão do meio

**Checkpoint**: US1 testável sozinha.

---

## Phase 4: User Story 2 - Condições com ícones (P1)

**Goal**: 18 condições com ícone/cor no menu e no token, máximo 3 selos + "+N".

**Independent Test**: quickstart cenário 2.

- [ ] T013 [P] [US2] Teste unitário de aparência com condições novas em `backend/tests/Companion.UnitTests/BattlemapTokenAppearanceTests.cs`
- [ ] T014 [P] [US2] Teste de integração: PUT de aparência com `Bleeding`, `Paralyzed`, `Burning` persiste e volta no snapshot, nome inválido 400, em `backend/tests/Companion.IntegrationTests/BattlemapApiTests.cs`
- [ ] T015 [P] [US2] Criar catálogo `STATUS_CONDITIONS` (valor, rótulo, ícone Lucide literal, cor) e helpers em `frontend/app/utils/statusConditions.ts`; `useTokenAppearance.ts` passa a reexportar a partir dele
- [ ] T016 [US2] Redesenhar a seção de condições em `frontend/app/components/battlemap/AttackAreaPanel.vue`: grade de chips com ícone colorido e rótulo, `aria-pressed`, contador de ativas, 44 px
- [ ] T017 [US2] Selos de condição com `<Icon>` em SVG aninhado, até 3 + "+N", nome acessível com todas as condições em `frontend/app/components/battlemap/BattlemapToken.vue`
- [ ] T018 [US2] Estilos dos chips e selos (cor por `--condition-color`, contraste, responsivo) em `frontend/app/assets/css/battlemap.css`
- [ ] T019 [P] [US2] Testes Vitest de ícones e "+N" em `frontend/tests/battlemap.spec.ts` e do menu em `frontend/tests/attack-area.spec.ts`

**Checkpoint**: US2 testável sozinha.

---

## Phase 5: User Story 3 - Mestre controla visibilidade do HP (P2)

**Goal**: HP de personagens sempre visível; HP de criaturas ocultável por criatura e em lote, filtrado no servidor.

**Independent Test**: quickstart cenário 3.

- [ ] T020 [P] [US3] Testes unitários de `Combatant.SetHitPointsHidden` e do filtro em `DtoMapper` em `backend/tests/Companion.UnitTests/EncounterTests.cs` e `backend/tests/Companion.UnitTests/DtoMapperTests.cs`
- [ ] T021 [P] [US3] Testes de integração: mestre oculta criatura, jogador recebe HP nulo e `isDefeated` correto, mestre vê; lote; 400 para personagem; 403 para jogador; presença não apaga HP de participantes, em `backend/tests/Companion.IntegrationTests/EncounterApiTests.cs`
- [ ] T022 [US3] Filtrar HP de criaturas ocultas para não mestres e preencher `IsHitPointsHidden` em `backend/src/Companion.Application/Contracts/DtoMapper.cs`
- [ ] T023 [US3] `SetHitPointsVisibilityAsync` (uma criatura) e `SetAllCreatureHitPointsVisibilityAsync` em `backend/src/Companion.Application/Encounters/EncounterService.cs`
- [ ] T024 [US3] Rotas `PUT .../combatants/{id}/hit-points-visibility` e `PUT .../hit-points-visibility` em `backend/src/Companion.Api/Endpoints/EncounterEndpoints.cs`
- [ ] T025 [US3] Alinhar `canSee` do resultado de ataque à regra de visibilidade em `backend/src/Companion.Application/Encounters/AttackService.cs`
- [ ] T026 [US3] Métodos `setHitPointsVisibility` e `setAllHitPointsVisibility` em `frontend/app/composables/useCompanionApi.ts`
- [ ] T027 [US3] Toggle "HP visível para jogadores" no painel do token (só mestre, só criaturas) em `frontend/app/components/battlemap/AttackAreaPanel.vue` e handler em `frontend/app/pages/index.vue`
- [ ] T028 [US3] Botão de olho por criatura e ação "Ocultar/Mostrar HP das criaturas" na iniciativa em `frontend/app/components/InitiativeTracker.vue`
- [ ] T029 [US3] Indicador de HP oculto (barra tracejada + ícone) para o mestre em `frontend/app/components/battlemap/BattlemapToken.vue` e `frontend/app/components/battlemap/BattlemapBoard.vue`
- [ ] T030 [P] [US3] Testes Vitest do toggle e do indicador em `frontend/tests/battlemap.spec.ts`

**Checkpoint**: US3 testável sozinha.

---

## Phase 6: User Story 4 - Névoa de guerra (P2)

**Goal**: mestre pinta/apaga névoa; jogadores veem opaco e não recebem criaturas cobertas.

**Independent Test**: quickstart cenário 4.

- [ ] T031 [P] [US4] Testes unitários de `Battlemap.SetFog/IsCellFogged/IsAreaFogged/Replace` em `backend/tests/Companion.UnitTests/BattlemapFogTests.cs`
- [ ] T032 [P] [US4] Testes de integração da rota de névoa (mestre 200, jogador 403, tamanho inválido 400, `null` revela), snapshot do jogador sem criatura/marcador do mestre coberto, token de jogador coberto continua, criatura parcialmente coberta continua, em `backend/tests/Companion.IntegrationTests/BattlemapFogApiTests.cs`
- [ ] T033 [US4] `SetFogAsync` e `ToSnapshot(campaignId, battlemap, observer)` filtrando tokens cobertos para jogadores, em `backend/src/Companion.Application/Battlemaps/BattlemapService.cs`
- [ ] T034 [US4] Rota `PUT .../battlemap/fog` em `backend/src/Companion.Api/Endpoints/BattlemapEndpoints.cs`
- [ ] T035 [P] [US4] Utilitários puros (decode/encode base64, set de células, pincel, retângulo, path de runs) em `frontend/app/utils/fogMask.ts` com testes em `frontend/tests/fog-mask.spec.ts`
- [ ] T036 [US4] `setFog` em `frontend/app/composables/useCompanionApi.ts` e ação `commitFog` com reversão em erro em `frontend/app/stores/campaign.ts`
- [ ] T037 [US4] Composable `frontend/app/composables/useFogOfWar.ts`: máscara local sincronizada com o snapshot, traço com pré-visualização, retângulo, cancelamento ao entrar segundo ponteiro
- [ ] T038 [US4] Componente `frontend/app/components/battlemap/FogToolbar.vue`: modo ocultar/revelar, pincel 1/3/5, retângulo, cobrir tudo, revelar tudo, sair; 44 px; quebra em 360 px
- [ ] T039 [US4] Integrar camada de névoa (path entre grid e tokens; opaca para jogador, translúcida + hachura para mestre), modo névoa (tokens sem pointer-events, pintura, Esc sai) e esmaecimento de criaturas cobertas para o mestre em `frontend/app/components/battlemap/BattlemapBoard.vue`
- [ ] T040 [US4] Emitir `fog-commit` do board e tratar em `frontend/app/pages/index.vue`
- [ ] T041 [US4] Aviso de descarte da névoa ao mudar o grid em `frontend/app/components/battlemap/BattlemapSetup.vue`
- [ ] T042 [US4] Estilos da névoa e da barra de ferramentas em `frontend/app/assets/css/battlemap.css`
- [ ] T043 [P] [US4] Testes Vitest do modo névoa (pintar emite commit, tokens não arrastam, jogador vê opaco) em `frontend/tests/battlemap-fog.spec.ts`

**Checkpoint**: todas as histórias funcionais.

---

## Phase 7: Polish & Cross-Cutting

- [ ] T044 Rodar `dotnet test`, `pnpm vitest run`, `pnpm nuxt typecheck`
- [ ] T045 Revisão de usabilidade e responsividade no navegador (360, 768, 1280 px) seguindo `specs/007-battlemap-nevoa-condicoes/quickstart.md`, com correções
- [ ] T046 Atualizar `specs/006-battlemap-combat-ux/spec.md` (nota de que FR-009a foi substituído pela 007) e marcar tarefas concluídas

---

## Dependencies & Execution Order

- Setup → Foundational → (US1 independe da Foundational e pode ir primeiro) → US2, US3, US4 em qualquer ordem → Polish.
- US1 toca só `BattlemapBoard.vue`/`BattlemapToken.vue`; US2/US3/US4 também tocam esses arquivos, então rodam em sequência no frontend.
- No backend, US2/US3/US4 tocam arquivos distintos após a Foundational (exceto `Dtos.cs`, já feito em T007).

## Parallel Example

```text
T013, T014, T015 (US2: testes backend + catálogo frontend)
T020, T021 (US3: testes)
T031, T032, T035 (US4: testes + utilitários puros)
```

## Implementation Strategy

1. MVP = US1 (bug que bloqueia o uso do menu no PC).
2. US2 entrega o que o usuário sentiu como perdido.
3. US3 e US4 completam a feature.
4. Cada checkpoint roda os testes da história antes de seguir.
