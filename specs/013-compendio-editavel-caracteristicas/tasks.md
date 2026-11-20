# Tasks: Compêndio editável pelo mestre e características com contador de usos

**Input**: spec.md, plan.md, research.md, data-model.md, contracts/api.md
**Tests**: obrigatórios (CLAUDE.md: toda mudança de comportamento leva teste).

## Phase 1: Setup

- [x] T001 Nenhum setup novo: projetos, Testcontainers e Playwright já existem (conferir que `dotnet build backend/Companion.slnx` passa na worktree)

## Phase 2: Foundational

- [x] T002 Migration `CampaignCatalogAndFeatures` em backend/src/Companion.Infrastructure/Persistence/Migrations (gerada depois de T003, T004, T012, T013)

## Phase 3: User Story 1 - Mestre cria e edita magias e itens (P1)

**Goal**: catálogo por campanha com origem Compendium/Edited/Custom, escrita só do mestre.
**Independent Test**: quickstart.md passos 1-4.

- [x] T003 [US1] `CampaignId`, `BaseId`, `Origin`, `ForCampaign(...)` e `IsCampaignEntry` em `CatalogItem` e `CatalogSpell` em backend/src/Companion.Domain/Catalog/CatalogEntities.cs
- [x] T004 [US1] Configs EF (FK campanha cascata, FK base NoAction, slug único filtrado, `(CampaignId, BaseId)` único filtrado) em backend/src/Companion.Infrastructure/Persistence/CharacterConfigurations.cs
- [x] T005 [US1] Seeder lê/remove só globais em backend/src/Companion.Application/Catalog/CatalogSeeder.cs
- [x] T006 [US1] Visão por campanha, filtro `origin`, `origin`/`baseId` nos DTOs, `ResolveForCampaign`, `Create/Save/Delete` de itens e magias (mestre) em backend/src/Companion.Application/Catalog/CatalogService.cs
- [x] T007 [US1] Adição à ficha (`AddItemFromCatalogAsync`, `AddSpellFromCatalogAsync`, `WithBaseItem`) resolve pela visão da campanha em backend/src/Companion.Application/Characters/CharacterService.cs
- [x] T008 [US1] Rotas POST/PUT/DELETE de catálogo e `origin` na listagem em backend/src/Companion.Api/Endpoints/CharacterEndpoints.cs
- [x] T009 [US1] Exclusão de campanha apaga catálogo da campanha em backend/src/Companion.Infrastructure/Persistence/CompanionDbContext.cs
- [x] T010 [P] [US1] Testes unitários de origem e `ForCampaign` em backend/tests/Companion.UnitTests/CatalogCampaignTests.cs
- [x] T011 [US1] Testes de integração (criar, editar global sem duplicar, outra campanha intacta, excluir restaura, global não exclui, jogador 403, jogador adiciona versão da mesa, seed não toca, campanha apagada limpa) em backend/tests/Companion.IntegrationTests/CatalogCampaignApiTests.cs
- [x] T014 [US1] Tipos e chamadas (origin, save/delete) em frontend/app/types/character.ts e frontend/app/composables/useCharacterApi.ts
- [x] T015 [US1] Modo catálogo (prop `catalog`) em frontend/app/components/character/ItemEditorModal.vue e SpellEditorModal.vue
- [x] T016 [US1] Botão Novo, filtro de origem, selo, Editar/Excluir em frontend/app/components/character/CatalogPicker.vue e CatalogDetail.vue
- [x] T017 [P] [US1] Vitest do CatalogPicker em modo mestre em frontend/tests/catalog-picker.spec.ts
- [x] T018 [US1] E2E compêndio editável em frontend/tests/compendium-edit.spec.ts

## Phase 4: User Story 2 - Características com contador (P1)

**Goal**: aba Características com contador, descansos e reordenação.
**Independent Test**: quickstart.md passo 5.

- [x] T012 [US2] `CharacterFeature`, `CharacterFeatureData`, `FeatureRecovery`, regras de uso/descanso em backend/src/Companion.Domain/Characters/CharacterFeature.cs
- [x] T013 [US2] DbSet, config EF (FK cascata) e interface em backend/src/Companion.Infrastructure/Persistence/* e backend/src/Companion.Application/Contracts/Abstractions.cs
- [x] T019 [US2] Operações add/save/remove/uses/restore/move/rest e `Features` no DTO em backend/src/Companion.Application/Characters/CharacterService.cs e CharacterDtos.cs
- [x] T020 [US2] Rotas de características em backend/src/Companion.Api/Endpoints/CharacterEndpoints.cs
- [x] T021 [P] [US2] Testes unitários (validação, clamp, descanso) em backend/tests/Companion.UnitTests/CharacterFeatureTests.cs
- [x] T022 [US2] Testes de integração (CRUD, limites, descanso, move, outra campanha vê o mesmo, 100 máx.) em backend/tests/Companion.IntegrationTests/CharacterFeatureApiTests.cs
- [x] T023 [US2] Tipos, chamadas e store em frontend/app/types/character.ts, frontend/app/composables/useCharacterApi.ts, frontend/app/stores/character.ts
- [x] T024 [US2] FeatureList.vue e FeatureEditorModal.vue em frontend/app/components/character/ e aba em CharacterSheetView.vue
- [x] T025 [P] [US2] Vitest da FeatureList em frontend/tests/feature-list.spec.ts
- [x] T026 [US2] E2E características em frontend/tests/character-features.spec.ts

## Phase 5: User Story 3 - Habilidades do Vashkar (P2)

- [x] T027 [US3] Carga direta no banco (pedido do Marcos: "cadastre direto pelo banco"), gerada por um console .NET que usa o domínio real (`CharacterFeature`, mesma validação da API); fonte em specs/013-compendio-editavel-caracteristicas/scripts/vashkar-features.cs.txt, rodado depois do deploy

## Phase 6: Polish

- [x] T028 Suite completa backend + frontend (lint, typecheck, vitest, e2e)
- [x] T029 Crítico frio (sub-agente) sobre o diff
- [ ] T030 PR, merge, deploy

## Dependencies

T003/T004/T012/T013 → T002 → testes de integração. US1 e US2 independentes depois da migration. US3 depende de US2 e do deploy.

## Parallel

US1 backend (T003-T011) e US2 backend (T012-T022) tocam arquivos em comum só em `CharacterService.cs`, `CharacterEndpoints.cs` e a migration; por isso rodam em sequência nesta sessão.

## MVP

US1 + US2 juntas (as duas são P1 e foram pedidas na mesma mensagem).
