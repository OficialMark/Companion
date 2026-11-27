# Tasks: Tokens do Compêndio e de Itens no Battlemap

**Input**: Design documents from `/specs/016-tokens-compendio-itens/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/battlemap-token-source-api.md, quickstart.md

**Status**: implementada no commit f1a2810; todas as tarefas concluídas.

## Phase 1: Setup

- [X] T001 Registrar a origem opcional no domínio: `CreatureId`, `CatalogItemId`, `MaxLabelLength` e `LabelFrom` em backend/src/Companion.Domain/Models/BattlemapMarker.cs (origem única, corte em palavra)

## Phase 2: Foundational

- [X] T002 [P] Gerar migration `AddMarkerSources` (SQL Server) em backend/src/Companion.Infrastructure/Persistence/Migrations/
- [X] T003 [P] Gerar migration `AddMarkerSources` (PostgreSQL) em backend/src/Companion.Infrastructure/Persistence/PostgreSqlMigrations/
- [X] T004 [P] Gerar migration `AddMarkerSources` (SQLite) em backend/src/Companion.Infrastructure/Persistence/SqliteMigrations/ e conferir que as três só adicionam duas colunas anuláveis
- [X] T005 Estender `CreateMarkerRequest(Label?, CreatureId?, ItemId?)` e `BattlemapTokenDto` (`SourceKind`, `SourceId`) em backend/src/Companion.Application/Contracts/Dtos.cs
- [X] T006 [P] Testes de domínio (origem guardada, duas origens recusadas, `LabelFrom` em 4 casos) em backend/tests/Companion.UnitTests/BattlemapMarkerTests.cs

## Phase 3: User Story 1 - Jogador coloca criatura do compêndio (P1) 🎯 MVP

**Independent Test**: jogador adiciona "ogro" e o token entra na reserva 2x2 com imagem.

- [X] T007 [US1] `CreatureTokenOptionDto`/`CreatureTokenOptionPageDto` em backend/src/Companion.Application/Creatures/CreatureDtos.cs
- [X] T008 [US1] `CreatureService.ListTokenOptions` (busca sem acento, paginada, sem ficha) em backend/src/Companion.Application/Creatures/CreatureService.cs
- [X] T009 [US1] Rota `GET /creatures/token-options` em backend/src/Companion.Api/Endpoints/CreatureEndpoints.cs
- [X] T010 [US1] `CreateMarkerAsync` resolve criatura, deriva nome e cria aparência com footprint da ficha em backend/src/Companion.Application/Battlemaps/BattlemapService.cs
- [X] T011 [US1] Snapshot carrega imagens das criaturas de marcadores num único SELECT e preenche `ImageUrl`/`SourceKind`/`SourceId` em backend/src/Companion.Application/Battlemaps/BattlemapService.cs
- [X] T012 [P] [US1] Tipos `CreatureTokenOption`, `sourceKind/sourceId`, `CreateMarkerRequest` em frontend/app/types/creatures.ts e frontend/app/types/campaign.ts
- [X] T013 [P] [US1] `listCreatureTokenOptions` em frontend/app/composables/useCompanionApi.ts
- [X] T014 [US1] `createMarker` aceita texto ou origem e devolve sucesso em frontend/app/stores/campaign.ts
- [X] T015 [US1] Componente `TokenSourcePicker` (aba Criaturas, busca com debounce, adicionar, offline, erro) em frontend/app/components/battlemap/TokenSourcePicker.vue
- [X] T016 [US1] Botão "Do compêndio" para todos os participantes em frontend/app/components/battlemap/BattlemapBoard.vue
- [X] T017 [US1] Integração: jogador adiciona criatura Grande, nome/imagem/footprint, move e remove em backend/tests/Companion.IntegrationTests/BattlemapTokenSourceApiTests.cs

## Phase 4: User Story 2 - Item no mapa (P1)

**Independent Test**: aba Itens, "corda", token quadrado na reserva.

- [X] T018 [US2] `CreateMarkerAsync` resolve item por `CatalogScope.ResolveItem` em backend/src/Companion.Application/Battlemaps/BattlemapService.cs
- [X] T019 [US2] Aba Itens com ícone e rótulo por categoria em frontend/app/components/battlemap/TokenSourcePicker.vue
- [X] T020 [US2] Item desenhado como `rect` arredondado (corpo e anel) em frontend/app/components/battlemap/BattlemapToken.vue
- [X] T021 [P] [US2] Integração: item com nome padrão e renomeado em backend/tests/Companion.IntegrationTests/BattlemapTokenSourceApiTests.cs

## Phase 5: User Story 4 - Segredos do mestre não vazam (P1)

**Independent Test**: criatura própria do mestre não aparece nem pode ser usada pelo jogador.

- [X] T022 [US4] `TokenCreature` restringe jogador ao compêndio público em backend/src/Companion.Application/Battlemaps/BattlemapService.cs
- [X] T023 [US4] Integração: busca e criação recusadas para jogador, permitidas ao mestre; opções sem PV/CA; acesso anônimo recusado em backend/tests/Companion.IntegrationTests/BattlemapTokenSourceApiTests.cs
- [X] T024 [US4] Integração: duas origens (400), entradas desconhecidas (404), label vazio (400) em backend/tests/Companion.IntegrationTests/BattlemapTokenSourceApiTests.cs

## Phase 6: User Story 3 - Seletor só de tokens também para o mestre (P2)

**Independent Test**: mestre alterna o modo e vê o seletor de combate.

- [X] T025 [US3] Seletor sem modo de combate (revisado após a review de design: removido o "No combate" que duplicava o catálogo do painel Combatentes) em frontend/app/components/battlemap/TokenSourcePicker.vue

## Phase 7: Polish

- [X] T026 [P] Estilos do seletor (alvos 44 px, botões só ícone abaixo de 420 px, lista com rolagem própria) em frontend/app/assets/css/battlemap.css
- [X] T027 [P] Testes de componente do seletor e do formato do token em frontend/tests/token-source-picker.spec.ts
- [X] T028 Atualizar teste do botão de compêndio (agora para todos) em frontend/tests/battlemap-markers.spec.ts
- [X] T029 Rodar quickstart automático: 10 unitários, 6 integração, suíte Vitest completa e `nuxi typecheck`

## Dependencies

Setup → Foundational → US1 → (US2, US4 em paralelo) → US3 → Polish. US2 e US4 dependem só de T005/T010.

## Parallel Example

T002, T003 e T004 (uma migration por provedor) rodam juntos; T012 e T013 também.

## Implementation Strategy

MVP = US1 (jogador com criatura). US2 e US4 completam o pedido; US3 mantém o combate só no painel Combatentes.
