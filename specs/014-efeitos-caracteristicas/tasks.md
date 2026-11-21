# Tasks: Efeitos de características na ficha

## Phase 1: Setup

- [X] T001 Confirmar compatibilidade dos artefatos e arquivos de ignore em `.gitignore`, `.dockerignore` e `specs/014-efeitos-caracteristicas/`

## Phase 2: Foundational

- [X] T002 Adicionar coleção compatível de efeitos e representação de cálculo em `backend/src/Companion.Domain/Characters/CharacterFeature.cs`
- [X] T003 [P] Expor efeitos no contrato TypeScript em `frontend/app/types/character.ts`

## Phase 3: User Story 1 — Característica altera rolagens (P1)

**Goal**: Efeitos de características participam do cálculo autoritativo e aparecem na composição.

**Independent Test**: Dueling +2 melee altera apenas o dano da arma corpo a corpo.

- [X] T004 [US1] Escrever testes de regra e integração para efeitos de características em `backend/tests/Companion.UnitTests/CharacterRulesTests.cs` e `backend/tests/Companion.IntegrationTests/CharacterFeatureApiTests.cs`
- [X] T005 [US1] Combinar efeitos de itens e características em `backend/src/Companion.Domain/Characters/CharacterRules.cs` e recalcular respostas em `backend/src/Companion.Application/Characters/CharacterService.cs`

## Phase 4: User Story 2 — Editar efeitos da característica (P2)

**Goal**: O jogador configura os efeitos no editor da característica.

**Independent Test**: Adicionar, persistir, reabrir e remover um bônus de dano no modal.

- [X] T006 [US2] Adicionar cobertura do editor de efeitos em `frontend/tests/feature-list.spec.ts`
- [X] T007 [US2] Implementar linhas de efeitos e validação no editor em `frontend/app/components/character/FeatureEditorModal.vue`

## Phase 5: User Story 3 — Espaços em Traços não desaparecem (P3)

**Goal**: A confirmação do autosave não substitui o texto local em edição.

**Independent Test**: `Ataque ` permanece após o autosave e aceita a palavra seguinte.

- [X] T008 [US3] Adicionar regressão e preservar o rascunho confirmado em `frontend/tests/character-sheet.spec.ts` e `frontend/app/stores/character.ts`

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T009 Executar testes backend/frontend e typecheck, atualizar o checklist e marcar as tarefas em `specs/014-efeitos-caracteristicas/tasks.md`

## Dependencies

- T002 e T003 habilitam as histórias.
- US1 (T004–T005) valida o contrato autoritativo antes da UI.
- US2 depende de T002, T003 e T005.
- US3 é independente de US1/US2 e pode ser validada isoladamente.
- T009 depende de todas as histórias.

## Parallel Opportunities

- T003 pode ser executada em paralelo com T002.
- A regressão de Traços (T008) não toca o cálculo de efeitos e pode avançar em paralelo conceitual.

## Implementation Strategy

Entregar primeiro o cálculo de Dueling (US1), depois a configuração pelo editor (US2) e por fim a regressão de digitação (US3). Nenhuma história exige nova infraestrutura.

