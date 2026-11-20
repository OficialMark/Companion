# Tasks: Características com efeitos nas rolagens

**Input**: spec.md, plan.md, research.md, data-model.md, contracts/api.md
**Tests**: obrigatórios (CLAUDE.md).

## Phase 1: Setup

- [x] T001 Nenhum setup novo (sem migration, sem dependência)

## Phase 2: Foundational

- [x] T002 Alvo `melee-one-handed` aceito em AttackBonus/DamageBonus em backend/src/Companion.Domain/Characters/ItemEffect.cs

## Phase 3: User Story 1 - Característica com efeito muda números e rolagens (P1)

**Independent Test**: Duelismo +2 corpo a corpo de uma mão: Martelo de Guerra +2 no dano, versátil e Malho sem mudança.

- [x] T003 [US1] `Effects` em `CharacterFeatureData` (validação, máximo 20, null = vazio) em backend/src/Companion.Domain/Characters/CharacterFeature.cs
- [x] T004 [US1] `CharacterRules.Compute` recebe `FeatureEntry`s; efeitos ativos com origem genérica; alvo `melee-one-handed` (fora do versátil) em backend/src/Companion.Domain/Characters/CharacterRules.cs e CharacterStats.cs
- [x] T005 [US1] `CharacterService` passa as características ao Compute em backend/src/Companion.Application/Characters/CharacterService.cs
- [x] T006 [P] [US1] Testes de regras (Duelismo, versátil, duas mãos, à distância, CA/salvaguarda, repetido item × característica, JSON antigo) em backend/tests/Companion.UnitTests/FeatureEffectRulesTests.cs
- [x] T007 [US1] Teste de integração (salvar efeito, stats mudam, remover volta, efeito inválido 400) em backend/tests/Companion.IntegrationTests/CharacterFeatureApiTests.cs
- [x] T008 [US1] Componente compartilhado de efeitos em frontend/app/components/character/EffectRowsEditor.vue; ItemEditorModal passa a usá-lo
- [x] T009 [US1] FeatureEditorModal com a seção de efeitos; FeatureList mostra os efeitos; tipos em frontend/app/types/character.ts
- [x] T010 [P] [US1] Vitest do editor de característica com efeitos e do item (sem regressão) em frontend/tests/feature-list.spec.ts e tests existentes
- [x] T011 [US1] E2E: Duelismo muda o dano mostrado no painel de rolagem (e sai no versátil e no Malho) em frontend/tests/e2e/features-014.spec.ts

## Phase 4: User Story 2 - Duelismo do Vashkar (P2)

- [ ] T012 [US2] Depois do deploy, gravar "Estilo de Luta: Duelismo" no Vashkar (banco, JSON validado pelo domínio) e conferir pela API

## Phase 5: Polish

- [ ] T013 Suítes completas, crítico frio, PR, merge, deploy

## Dependencies

T002 → T004. T003 → T004 → T005 → T007. T008 → T009. US2 depende do deploy.
