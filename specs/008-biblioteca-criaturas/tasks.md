# Tasks: Biblioteca de criaturas, névoa antes de publicar e alinhamento dos ícones

**Input**: `specs/008-biblioteca-criaturas/` (spec, plan, research, data-model, contracts, quickstart)

**Tests**: obrigatórios (constituição IV e CLAUDE.md): unitários, integração, Vitest e `node --test` do importador.

## Phase 1: Setup

- [X] T001 Baseline verde na worktree: `dotnet test` em `backend/`, `pnpm vitest run` + `pnpm nuxt typecheck` em `frontend/`
- [X] T002 [P] Importador do compêndio Pocket DM com parser testado e relatório em `scripts/import-creatures/` gerando `backend/src/Companion.Infrastructure/Persistence/Seed/creatures.pocketdm.json` (agente em segundo plano)

## Phase 2: Foundational (bloqueia US3, US4, US5)

- [X] T003 Domínio: `CreatureSize`, `Ability`, `CreatureAction` e `Creature` (validação, `Modifier`, `FootprintSize`, `ChallengeValue`, `CreateCampaignCopy`, `Update`, `SetNotes`, `SetImage`) em `backend/src/Companion.Domain/Models/Creature*.cs`, e `CreatureImage`, `SeedVersion`
- [X] T004 `Combatant.CreatureId`, `BattlemapTokenAppearance.LastRoll*` + `SetRollPreset`, `DiceRoll.Label` em `backend/src/Companion.Domain/Models/`
- [X] T005 Configurações EF (JSON das listas, índices `SearchKey`/`Slug`/`(CampaignId, BaseCreatureId)`, FKs) em `backend/src/Companion.Infrastructure/Persistence/ModelConfigurations.cs` e `CompanionDbContext.cs`/`Abstractions.cs`
- [X] T006 Migration `AddCreatureLibrary` em `backend/src/Companion.Infrastructure/Persistence/Migrations/` (diff conferido)
- [X] T007 DTOs e requests da spec 008 em `backend/src/Companion.Application/Contracts/Dtos.cs` e `backend/src/Companion.Application/Creatures/CreatureDtos.cs`
- [X] T008 [P] Tipos do frontend em `frontend/app/types/campaign.ts` e `frontend/app/types/creatures.ts`
- [X] T009 [P] Testes unitários do domínio em `backend/tests/Companion.UnitTests/CreatureTests.cs`

---

## Phase 3: US1 - Ícones alinhados (P1) 🎯

- [X] T010 [US1] Reproduzir no navegador em zoom mínimo e confirmar a causa (viewport do `<svg>` aninhado) antes de mudar código
- [X] T011 [US1] Glifo por `<g transform>` com caixa 24x24 fixa em `frontend/app/components/battlemap/BattlemapToken.vue` (selos de condição e indicador de HP oculto) e CSS em `frontend/app/assets/css/battlemap.css`
- [X] T012 [P] [US1] Teste Vitest: o transform do glifo centraliza (translate = -s/2, scale = s/24) para tamanhos 8, 12, 16 em `frontend/tests/battlemap-spec008.spec.ts`

---

## Phase 4: US2 - Névoa antes de publicar (P1)

- [X] T013 [P] [US2] Teste de integração: `PUT .../battlemap` com `fogMask` grava a névoa na criação e na troca; máscara de outro grid 400; jogador recebe já com névoa e sem criatura coberta, em `backend/tests/Companion.IntegrationTests/BattlemapFogApiTests.cs`
- [X] T014 [US2] Ler o campo `fogMask` em `backend/src/Companion.Api/Endpoints/BattlemapEndpoints.cs`, levar em `ConfigureBattlemapRequest` e aplicar `SetFog` em `BattlemapService.ConfigureAsync` antes do `SaveChanges`
- [X] T015 [US2] `useFogOfWar` aceitar fonte local (sem servidor) e `FogPreview.vue` (imagem local + grid + névoa + `FogToolbar`) em `frontend/app/components/battlemap/FogPreview.vue`
- [X] T016 [US2] Integrar a pré-visualização e "Publicar coberto pela névoa" (padrão ligado em mapa novo; começa com a névoa atual ao manter o grid; recomeça e avisa ao trocar o grid) em `frontend/app/components/battlemap/BattlemapSetup.vue` e enviar `fogMask` em `frontend/app/composables/useCompanionApi.ts`
- [X] T017 [P] [US2] Testes Vitest da pré-visualização e do envio em `frontend/tests/battlemap-setup.spec.ts`

---

## Phase 5: US3 - Biblioteca (P1)

- [X] T018 [US3] `CompendiumSeeder` (hash, upsert por slug, marca removidas) em `backend/src/Companion.Application/Creatures/CompendiumSeeder.cs`; JSON como EmbeddedResource; chamado no startup em `backend/src/Companion.Api/Program.cs`
- [X] T019 [US3] `CreatureService` (listar com busca/filtros/paginação e anti-join, obter, criar, salvar com cópia da mesa e concorrência, excluir/restaurar, imagem upload/remoção, tipos) em `backend/src/Companion.Application/Creatures/CreatureService.cs`
- [X] T020 [US3] `ICreatureImageSource` + `HttpCreatureImageSource` (timeout, limite, assinatura, backoff de 24 h) e leitura da imagem com fetch sob demanda
- [X] T021 [US3] Endpoints em `backend/src/Companion.Api/Endpoints/CreatureEndpoints.cs` e registro em DI
- [X] T022 [P] [US3] Testes de integração da biblioteca (mestre-only, busca sem acento, filtros, override e restauração, isolamento entre mesas, 409, upload de imagem, imagem acessível a jogador, seed idempotente) em `backend/tests/Companion.IntegrationTests/CreatureApiTests.cs`
- [X] T023 [US3] Store `frontend/app/stores/creatures.ts` e API em `frontend/app/composables/useCompanionApi.ts`; imagens via blob autenticado (padrão do avatar)
- [X] T024 [US3] Abas Mesa/Criaturas para o mestre em `frontend/app/pages/index.vue`
- [X] T025 [US3] `CreatureLibrary.vue` (busca com debounce, filtros ND/tipo/tamanho/origem, grade de cards, paginação, crédito Pocket DM) e `CreatureCard.vue` em `frontend/app/components/creatures/`
- [X] T026 [US3] `CreatureEditor.vue` (todos os campos, listas de ações editáveis, imagem, salvar/excluir/restaurar) em `frontend/app/components/creatures/`
- [X] T027 [P] [US3] Testes Vitest da biblioteca e do editor em `frontend/tests/creature-library.spec.ts`

---

## Phase 6: US4 - Adicionar ao combate (P2)

- [X] T028 [US4] `AddCreaturesAsync` (numeração, vida, modificador, vínculo, tamanho do token) em `backend/src/Companion.Application/Encounters/EncounterService.cs` + rota em `EncounterEndpoints.cs`
- [X] T029 [US4] `BattlemapTokenDto.ImageUrl/CreatureId/RollPreset` em `BattlemapService.ToSnapshot` e `CombatantDto.CreatureId` em `DtoMapper`
- [X] T030 [P] [US4] Testes unitários da numeração e de integração da adição (Preparing-only, 1-20, imagem no token) em `backend/tests/`
- [X] T031 [US4] Botão "Adicionar ao combate" com quantidade na biblioteca e no modal; tokens de criatura usam a imagem em `BattlemapBoard.vue`/`BattlemapToken.vue` (`photoFor`)

---

## Phase 7: US5 - Ficha e rolagem (P2)

- [X] T032 [US5] `DiceRoll.Label` no `DiceRollService` e no histórico `frontend/app/components/RollHistory.vue`
- [X] T033 [US5] Rota de preset `PUT .../tokens/{id}/roll-preset` em `BattlemapService`/`BattlemapEndpoints`
- [X] T034 [P] [US5] Testes de integração de label e preset (quem controla, validação de faixas) em `backend/tests/Companion.IntegrationTests/`
- [X] T035 [US5] `frontend/app/utils/creatureRoll.ts` (modificador, expressão, parser de ações) com testes em `frontend/tests/creature-roll.spec.ts`
- [X] T036 [US5] `CreatureRollPanel.vue` e `CreatureSheetModal.vue` (ficha, anotações, atalhos de ação, foco preso, Esc, tela cheia no celular) em `frontend/app/components/creatures/`
- [X] T037 [US5] Botão "Ficha" no menu do token de criatura vinculada em `AttackAreaPanel.vue`, abrindo o modal com o preset do token e gravando o preset ao rolar
- [X] T038 [P] [US5] Testes Vitest do modal, rolagem e preset em `frontend/tests/creature-sheet.spec.ts`

---

## Phase 8: Polish

- [X] T039 Integrar o seed real gerado em T002 e conferir o relatório (≥ 98% com PV, CA e atributos)
- [X] T040 Suítes completas: `node --test scripts/import-creatures/`, `dotnet test`, `pnpm vitest run`, `pnpm nuxt typecheck`
- [X] T041 Revisão no navegador (360, 768, 1280 px) seguindo `quickstart.md`, com correções
- [X] T042 Crítico frio (sub-agente sem contexto de construção) sobre o diff e a UI; corrigir os achados
- [X] T043 Atualizar `README.md` (biblioteca, importador, atribuição) e marcar tarefas

## Dependencies

Setup → Foundational → US1 e US2 (independentes da biblioteca) → US3 → US4 → US5 → Polish. T002 roda em paralelo a tudo até T039.

## Parallel Example

```text
T002 (agente) || T003..T009
T012 || T013 || T009
T022 || T027 após T019/T025
```

## Implementation Strategy

MVP: US1 + US2 (bugs e névoa). Depois US3 → US4 → US5, cada uma com seus testes antes de seguir.
