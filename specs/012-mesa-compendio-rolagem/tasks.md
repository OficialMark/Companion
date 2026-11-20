# Tasks: Mesa com compêndio em abas, busca ampla, rolagem completa e overview da ficha

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/catalog-search.md](contracts/catalog-search.md)

## Phase 1: Setup

- [x] T001 Criar `frontend/app/utils/scrollToResult.ts` (`scrollIntoView` com `block: 'center'`; `behavior: 'auto'` quando `prefers-reduced-motion: reduce`), com teste em `frontend/tests/scroll-to-result.spec.ts`

## Phase 2: Foundational (busca do catálogo, usada por US2)

- [x] T002 Gerar `backend/src/Companion.Infrastructure/Persistence/Seed/spells.names-en.json` (slug PT → `{ nameEn, verified }`) e `scripts/import-catalogs/validate-spell-names.mjs`, que confere os nomes contra a lista de magias do SRD 5.2.1 (5e-bits, commit fixado), com teste em `scripts/import-catalogs/validate-spell-names.test.mjs`
- [x] T003 Adicionar `CatalogSpell.NameEn`, `SearchText` em `CatalogItem` e `CatalogSpell`, e `CatalogText.SearchText(name, nameEn, description)` (sem HTML, normalizado) em `backend/src/Companion.Domain/Catalog/CatalogEntities.cs`, com teste unitário em `backend/tests/Companion.UnitTests/Catalog/CatalogSearchTextTests.cs`
- [x] T004 Configurar as colunas novas em `backend/src/Companion.Infrastructure/Persistence/CharacterConfigurations.cs` e criar a migration `AddCatalogSearchText`
- [x] T005 Fazer o seeder aplicar o `nameEn` da tabela e trocar a versão do seed em `backend/src/Companion.Application/Catalog/CatalogSeeder.cs`
- [x] T006 Implementar a busca de duas faixas (nome, depois descrição com 3+ caracteres) e expor `nameEn` nos DTOs de magia, em `backend/src/Companion.Application/Catalog/CatalogService.cs` e `Contracts`, com teste de integração em `backend/tests/Companion.IntegrationTests/CatalogSearchTests.cs` (nome EN, descrição, ordem, termo curto)

## Phase 3: US1, rolagem completa (P1)

- [x] T007 [US1] Na visão completa, o jogador usa o `PlayerRollPanel` (carregar a ficha também fora do Modo Jogo) em `frontend/app/pages/index.vue`
- [x] T008 [US1] Criar `frontend/app/components/MasterRollPanel.vue`:
  - seletor com as criaturas do encontro ativo (combatente com `creatureId`) e "Rolagem livre";
  - criatura escolhida → `CreatureRollPanel`;
  - "Rolagem livre" → `DiceRoller` na visão completa ou `QuickDiceRoller` no Modo Jogo;
  - voltar para "Rolagem livre" quando a criatura sai da mesa.

  Teste em `frontend/tests/master-roll-panel.spec.ts`.
- [x] T009 [US1] Usar o `MasterRollPanel` para o mestre na visão completa e no Modo Jogo, em `frontend/app/pages/index.vue`
- [x] T010 [P] [US1] Chamar `scrollToResult` ao revelar o resultado em `PlayerRollPanel.vue`, `creatures/CreatureRollPanel.vue`, `DiceRoller.vue` e `QuickDiceRoller.vue`, com teste em `frontend/tests/scroll-to-result.spec.ts`

## Phase 4: US2, compêndio em abas (P1)

- [x] T011 [US2] Adicionar o modo `browse` (só leitura, sem botão Adicionar, detalhe expandido) e exibir `nameEn` em `frontend/app/components/character/CatalogPicker.vue`, com teste em `frontend/tests/catalog-picker-browse.spec.ts`
- [x] T012 [US2] Criar `frontend/app/components/CompendiumTabs.vue`:
  - abas Criaturas, Magias e Itens com `role="tablist"` e navegação por setas;
  - aba ativa em `?aba=`.

  Usar o componente no `view === 'compendio'` de `frontend/app/pages/index.vue`. Teste em `frontend/tests/compendium-tabs.spec.ts`.

## Phase 5: US3, overview da ficha (P2)

- [x] T013 [US3] Criar `frontend/app/utils/hitPoints.ts` (`applyHitPointDelta(current, max, delta)` limitado entre 0 e max), com teste em `frontend/tests/hit-points.spec.ts`
- [x] T014 [US3] Criar `frontend/app/components/SheetOverviewModal.vue`:
  - resumo de atributos, salvaguardas, CA, iniciativa, deslocamento, proficiência e percepção passiva;
  - PV atual, máximo e temporário;
  - Dano, Cura, −1 e +1;
  - "Abrir ficha completa";
  - foco preso no modal, e Esc devolve o foco.

  Teste em `frontend/tests/sheet-overview.spec.ts`.
- [x] T015 [US3] Trocar o `InitiativeModifier.vue` pelo resumo de rodapé que abre o overview (o perfil manual sai) e ajustar os testes existentes do perfil de combate

## Phase 6: US4, código do mestre e optgroup (P3)

- [x] T016 [P] [US4] Mover o código do mestre para a barra superior, mascarado, com botão de olho (`aria-pressed`), e remover o `master-code-panel` em `frontend/app/pages/index.vue`, com teste em `frontend/tests/master-code-toggle.spec.ts`
- [x] T017 [P] [US4] Aplicar a regra global de `optgroup` e `option` escuros em `frontend/app/assets/css/main.css` (ou no CSS global existente) e verificar o contraste com `scripts/contrast-check.mjs`

## Phase 7: Polish

- [x] T018 Rodar o e2e `frontend/tests/e2e/mesa-012.spec.ts` (desktop e Pixel 7) com o roteiro do [quickstart.md](quickstart.md), na instância de validação
- [x] T019 Rodar a revisão crítica fria do diff, corrigir os achados e rodar `dotnet test backend` e `pnpm --dir frontend test`
- [x] T020 Abrir o PR, fazer o merge na main e subir com `docker compose up -d --build api frontend`

## Dependencies

- O T002 vem antes do T005.
- T003 → T004 → T005 → T006 (backend sequencial).
- US1, US3 e US4 não dependem do backend.
- Dentro da US2, o T011 depende do T006 para a busca ampla aparecer.
- O MVP é a US1 junto com a US2.

## Parallel

- T002, T007 a T010, T013 a T017 podem andar em paralelo com T003 a T006, porque os arquivos são diferentes.
