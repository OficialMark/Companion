# Tasks: Acesso, Administração e Iniciativa da Campanha

**Input**: Design documents from `/specs/002-campaign-access-initiative/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/openapi.yaml, quickstart.md

**Tests**: Automated tests are required by the specification success criteria and project constitution.

**Organization**: Tasks are grouped by user story so each journey remains independently testable.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Establish configuration and public contract scaffolding shared by all stories.

- [x] T001 Add installation-level admin secret examples to `.env.example`, `docker-compose.yml`, and `backend/src/Companion.Api/appsettings.json`
- [x] T002 [P] Extend shared requests, responses, DTOs, and service abstractions in `backend/src/Companion.Application/Contracts/Dtos.cs` and `backend/src/Companion.Application/Contracts/Abstractions.cs`
- [x] T003 [P] Extend frontend campaign, participant, combatant, session, and admin types in `frontend/app/types/campaign.ts`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Implement the storage, identity, security, and domain foundation required by every story.

**CRITICAL**: No user story work begins until this phase is complete.

- [x] T004 Create the `CampaignAccessCode` entity and adapt `Campaign`, `Participant`, `Combatant`, and `Encounter` domain models in `backend/src/Companion.Domain/Models/`
- [x] T005 Configure access codes, participant modifiers, stable combatant links, uniqueness constraints, and aggregate deletion in `backend/src/Companion.Infrastructure/Persistence/CompanionDbContext.cs` and `backend/src/Companion.Infrastructure/Persistence/ModelConfigurations.cs`
- [x] T006 Add and validate schema/data migrations in `backend/src/Companion.Infrastructure/Persistence/Migrations/20260823180817_CampaignAccessAndInitiative.cs`, `backend/src/Companion.Infrastructure/Persistence/Migrations/20260823181433_AllowDuplicateParticipantNames.cs`, and `backend/src/Companion.Infrastructure/Persistence/Migrations/CompanionDbContextModelSnapshot.cs`
- [x] T007 Implement four-digit numeric code generation and constant-time administrative credential validation in `backend/src/Companion.Infrastructure/Security/ParticipantTokenService.cs` and `backend/src/Companion.Infrastructure/Security/AdminCredentialValidator.cs`
- [x] T008 Register admin configuration, validators, services, and fixed-window rate limits for join and admin requests in `backend/src/Companion.Api/Program.cs`

**Checkpoint**: Persistence and security foundations support all three stories.

---

## Phase 3: User Story 1 - Entrar como jogador ou mestre pelo código correto (Priority: P1) MVP

**Goal**: Create globally unique four-digit player/master codes and grant the role represented by the supplied code without leaking the master code.

**Independent Test**: Create a campaign, use each code in clean sessions, confirm the roles differ, and verify player responses never contain the master code.

### Tests for User Story 1

- [x] T009 [P] [US1] Add unit tests for exact four-digit numeric access-code generation in `backend/tests/Companion.UnitTests/CampaignTests.cs`
- [x] T010 [P] [US1] Add integration tests for creation, player/master joins, invalid formats, uniqueness, and master-code non-disclosure in `backend/tests/Companion.IntegrationTests/CampaignApiTests.cs`
- [x] T011 [P] [US1] Add frontend tests for numeric code input, role recovery, and reserved-code presentation in `frontend/tests/campaign.spec.ts`

### Implementation for User Story 1

- [x] T012 [US1] Implement atomic allocation of two free codes and role-aware joins in `backend/src/Companion.Application/Campaigns/CampaignService.cs`
- [x] T013 [US1] Update campaign creation/join/snapshot endpoints and safe response mapping in `backend/src/Companion.Api/Endpoints/CampaignEndpoints.cs` and `backend/src/Companion.Application/Contracts/DtoMapper.cs`
- [x] T014 [US1] Update API calls and campaign state persistence for player/master code contracts in `frontend/app/composables/useCompanionApi.ts` and `frontend/app/stores/campaign.ts`
- [x] T015 [US1] Implement four-digit entry and a concealed reserved master-code presentation in `frontend/app/components/CampaignAccess.vue` and `frontend/app/pages/index.vue`
- [x] T016 [US1] Cover the complete create/player-join/master-join journey in `frontend/tests/e2e/mvp.spec.ts`

**Checkpoint**: Campaign access is independently functional and role-safe.

---

## Phase 4: User Story 2 - Preparar iniciativa a partir dos jogadores presentes (Priority: P1)

**Goal**: Persist each participant's modifier and import online players once into initiative while preserving master edits and manual combatants.

**Independent Test**: Save distinct modifiers for online players, roll all as master, and verify stable, editable, non-duplicated combatants while offline players are omitted.

### Tests for User Story 2

- [x] T017 [P] [US2] Add domain tests for participant linkage, combatant edit/removal, and turn preservation in `backend/tests/Companion.UnitTests/EncounterTests.cs`
- [x] T018 [P] [US2] Add integration tests for own-modifier authorization, online import, deduplication, edit preservation, and combatant removal in `backend/tests/Companion.IntegrationTests/EncounterApiTests.cs`
- [x] T019 [P] [US2] Add frontend component tests for modifier persistence and master combatant controls in `frontend/tests/encounter.spec.ts`

### Implementation for User Story 2

- [x] T020 [US2] Implement own-participant modifier updates in `backend/src/Companion.Application/Campaigns/CampaignService.cs` and `backend/src/Companion.Api/Endpoints/CampaignEndpoints.cs`
- [x] T021 [US2] Import online players by stable participant identity and implement combatant edit/removal in `backend/src/Companion.Application/Encounters/EncounterService.cs`
- [x] T022 [US2] Expose combatant update/removal contracts and routes in `backend/src/Companion.Api/Endpoints/EncounterEndpoints.cs`
- [x] T023 [US2] Extend client API/state handling for modifiers, participant-linked combatants, and campaign deletion events in `frontend/app/composables/useCompanionApi.ts`, `frontend/app/stores/campaign.ts`, and `frontend/app/composables/useCampaignRealtime.ts`
- [x] T024 [US2] Add the bottom-of-page modifier component in `frontend/app/components/InitiativeModifier.vue` and `frontend/app/pages/index.vue`
- [x] T025 [US2] Add editable/removable master controls and enable empty-list auto-import in `frontend/app/components/EncounterSetup.vue` and `frontend/app/components/InitiativeTracker.vue`
- [x] T026 [US2] Cover modifier persistence and automatic initiative preparation in `frontend/tests/e2e/initiative.spec.ts`

**Checkpoint**: Initiative preparation works from current presence and remains independently editable.

---

## Phase 5: User Story 3 - Administrar mesas existentes (Priority: P2)

**Goal**: Protect a hidden `/admin` page that lists all campaigns and permanently deletes a confirmed campaign aggregate.

**Independent Test**: Verify unauthorized requests reveal nothing, then authenticate, list two campaigns with both codes/presence, delete one, and confirm its codes and sessions stop working.

### Tests for User Story 3

- [x] T027 [P] [US3] Add integration tests for admin denial, listing, confirmed aggregate deletion, session invalidation, and safe auditing in `backend/tests/Companion.IntegrationTests/AdminApiTests.cs`
- [x] T028 [P] [US3] Add frontend tests for admin authentication, campaign rendering, cancellation, and confirmed deletion in `frontend/tests/admin.spec.ts`

### Implementation for User Story 3

- [x] T029 [US3] Implement protected campaign listing and aggregate deletion with post-commit notification in `backend/src/Companion.Application/Campaigns/AdminCampaignService.cs`
- [x] T030 [US3] Add rate-limited admin list/delete routes and secret-safe audit logging in `backend/src/Companion.Api/Endpoints/AdminEndpoints.cs`
- [x] T031 [US3] Add admin API calls and the unlinked authenticated panel with explicit deletion confirmation in `frontend/app/composables/useCompanionApi.ts` and `frontend/app/pages/admin.vue`
- [x] T032 [US3] Handle campaign-closed realtime state and responsive admin/modifier styling in `frontend/app/stores/campaign.ts` and `frontend/app/assets/css/main.css`
- [x] T033 [US3] Cover the protected list-and-delete journey in `frontend/tests/e2e/admin.spec.ts`

**Checkpoint**: All user stories are independently functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Confirm security, contract alignment, regressions, and the documented acceptance path.

- [x] T034 [P] Reconcile implemented endpoints and security behavior with `specs/002-campaign-access-initiative/contracts/openapi.yaml` and `specs/002-campaign-access-initiative/quickstart.md`
- [x] T035 Run backend unit/integration tests and frontend unit/E2E tests, fixing regressions in files touched by this feature
- [x] T036 Validate production builds, migration startup, rate-limit behavior, secret non-disclosure, and all scenarios in `specs/002-campaign-access-initiative/quickstart.md`

---

## Dependencies & Execution Order

- Phase 1 precedes Phase 2; Phase 2 blocks every user story.
- US1 and US2 are both P1 and may proceed in parallel after Phase 2, but this implementation executes US1 before US2 to stabilize shared campaign contracts.
- US3 depends only on Phase 2 at the backend, but its frontend state handling follows US1/US2 contract changes.
- Phase 6 follows completion of all selected stories.
- Within each story, tests are written before implementation, then services precede endpoints and UI integration.

## Parallel Opportunities

- T002 and T003 touch separate backend/frontend contracts.
- T009, T010, and T011 can be authored in parallel after the foundation.
- T017, T018, and T019 can be authored in parallel.
- T027 and T028 can be authored in parallel.
- US1 backend, US2 domain tests, and US3 frontend test scaffolding can be split among contributors once shared contracts are stable.

## Implementation Strategy

1. Complete setup and persistence/security foundations.
2. Deliver US1 as the access-control MVP and validate no master-code leakage.
3. Deliver US2 and validate presence-driven initiative without duplicates.
4. Deliver US3 and validate permanent deletion/session invalidation.
5. Run the complete cross-stack regression and quickstart suite.
