# Tasks: Compêndio na barra superior e ficha de criatura legível

**Input**: Design documents from `specs/009-compendio-ficha/`
**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/ui-navigation.md](contracts/ui-navigation.md), [quickstart.md](quickstart.md)

**Tests**: pedidos pela constituição (IV) e pelos critérios SC-001 e SC-004. São testes de unidade em Vitest e um e2e em Playwright que mede o layout.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Setup

- [X] T001 Capturar a ficha (ver e editar) e a aba antiga em 1280, 768 e 375 px antes de mudar, salvando em `scratchpad/before/` da sessão
- [X] T002 Subir uma stack isolada da sessão (portas 3638/5254, projeto `companion-331fa84b`) com um override de compose no scratchpad, e gerar `.nuxt` com `pnpm nuxt prepare` em `frontend/`

## Phase 2: Foundational

- [X] T003 Criar o composable `frontend/app/composables/useMasterView.ts`: tela derivada de `route.query.tela`, `router.replace` preservando os outros parâmetros, guarda de jogador e rolagem ao topo (contrato em `contracts/ui-navigation.md`)

## Phase 3: User Story 1 - Ler a ficha de relance (P1) 🎯 MVP

**Goal**: ficha em bloco de estatísticas, legível em qualquer largura.
**Independent Test**: abrir o Dragão Vermelho Adulto em 1280/768/375 px; nenhuma sobreposição, nenhum alvo abaixo de 44 px, nenhuma rolagem horizontal.

- [X] T004 [US1] Trocar os 5 números rápidos por 4 cartões mais a linha `[data-creature-speed]` em `frontend/app/components/creatures/CreatureSheetModal.vue`
- [X] T005 [US1] Reescrever o bloco "Sheet modal" de `frontend/app/assets/css/creatures.css` na escala 4/8/12/16/24: cabeçalho, cartões, atributos, detalhes `dt | dd` com divisória, seções e entradas
- [X] T006 [US1] Rodapé da ficha em uma linha até 680 px, com "Editar ficha" como ícone de 44 px (`span.edit-label`, `aria-label`), em `CreatureSheetModal.vue` e `creatures.css`
- [X] T007 [US1] C6: compactar o cabeçalho em modo de edição até 680 px (sem subtítulo e selos, retrato de 44 px), com a classe `.editing` no `.creature-sheet` em `frontend/app/components/creatures/CreatureSheetModal.vue` e `creatures.css`

## Phase 4: User Story 2 - Editar sem se perder (P1)

**Goal**: editor arejado, com grade uniforme, título das seções dentro do cartão e barra de ações que nunca cobre um campo.
**Independent Test**: "Editar ficha" e "Nova criatura" em 1280/768/375 px; toda linha da grade soma 6 colunas e, no celular, campos de texto ocupam a linha inteira.

- [X] T008 [US2] Grade de 6 colunas com `.col-*` e `.m-col-*`, `legend` dentro do cartão e itens de lista em cartão em `frontend/app/components/creatures/CreatureEditor.vue` e `creatures.css`
- [X] T009 [US2] Barra Cancelar/Salvar como linha própria: wrapper `.creature-editor-fields` rolável e `.creature-sheet-body.editing` sem rolagem, em `CreatureEditor.vue`, `CreatureSheetModal.vue` e `creatures.css`
- [X] T010 [US2] C1: no celular, só números em par ou trio. Tipo, Dados de vida e Nota da CA passam a `m-col-6`; Tamanho fica `m-col-6` antes do Alinhamento, em `frontend/app/components/creatures/CreatureEditor.vue`. Atualizar a tabela de `data-model.md`
- [X] T011 [P] [US2] C7: centralizar os números dos atributos no editor (`appearance: textfield`, sem `::-webkit-inner/outer-spin-button`) em `frontend/app/assets/css/creatures.css`

## Phase 5: User Story 3 - Compêndio na barra superior (P2)

**Goal**: Mesa/Compêndio na barra superior, Compêndio em tela cheia que sobrevive a recarregar, nunca para jogador.
**Independent Test**: mestre vai ao Compêndio em 1 clique, filtra "Minhas criaturas", cria uma criatura, recarrega e continua lá; jogador com `?tela=compendio` cai na Mesa.

- [X] T012 [US3] Criar `frontend/app/components/MasterViewNav.vue` (`nav` com `aria-current`) e colocá-lo na topbar de `frontend/app/pages/index.vue`, dentro do wrapper `.topbar-center`
- [X] T013 [US3] Topbar em duas linhas até 900 px e alvos de 44 px (`.code-badge`, ações sem encolher) em `frontend/app/assets/css/main.css`
- [X] T014 [US3] Compêndio em tela cheia (sem boas-vindas, aviso do código e grade da mesa) em `index.vue`. Título "Compêndio", atalho "Minhas criaturas", rótulos curtos e estado vazio em `frontend/app/components/creatures/CreatureLibrary.vue`
- [X] T015 [P] [US3] Testes de unidade em `frontend/tests/compendium-nav.spec.ts`: navegação só para o mestre, endereço com `replace`, jogador na Mesa, "Minhas criaturas" sincronizado com a origem
- [X] T016 [US3] C2: nome da campanha visível no celular. Até 480 px ele não some mais: vai para a linha 1, ao lado do logo, truncado. O tooltip extra do código sai. Arquivos: `frontend/app/pages/index.vue` e `main.css`. Atualizar D2 em `research.md` e o layout em `contracts/ui-navigation.md`
- [X] T017 [US3] C3: o aviso "Comandos pausados até a conexão voltar" aparece também no Compêndio, renderizado fora da troca de tela em `frontend/app/pages/index.vue`
- [X] T018 [US3] C4: filtros numa grade definida. A busca ocupa a linha inteira e os selects (Tamanho, Tipo, ND mín., ND máx., Origem) têm largura igual em cada linha, em `frontend/app/components/creatures/CreatureLibrary.vue` e `creatures.css`

## Phase 6: Polish & Cross-Cutting

- [X] T019 Ajustar testes existentes à nova navegação (`tests/e2e/creature-catalog.spec.ts`, seletor do "Limpar" em `tests/creature-critic-regressions.spec.ts`)
- [X] T020 E2E `frontend/tests/e2e/compendium-sheet.spec.ts`: sobreposição por linha renderizada recortada pelas áreas de rolagem, alvos de 44 px e rolagem horizontal, em 1280/768/375 e Android
- [X] T021 C5/C9: no e2e `frontend/tests/e2e/compendium-sheet.spec.ts`, verificar paginação, link do crédito Pocket DM e `getByRole('navigation', { name: 'Áreas do mestre' })`, e salvar captura de página inteira do Compêndio
- [X] T022 C8: no e2e em 375 px, verificar que as linhas da grade do editor somam 6 colunas e que campos de texto ocupam a linha inteira, em `frontend/tests/e2e/compendium-sheet.spec.ts`
- [X] T023 Rodar `pnpm vitest run`, `pnpm nuxt typecheck` e os e2e `compendium-sheet` e `creature-catalog` contra a stack da sessão
- [X] T024 Novo crítico frio sobre as capturas e a rubrica; corrigir até PASS
- [X] T025 README (seção Biblioteca de criaturas), commit, PR e merge na `main`

## Dependencies & Execution Order

- Setup e Foundational (T001–T003) feitos; as US1–US3 foram implementadas sobre eles.
- Correções do crítico: T007 (US1), T010 e T011 (US2), T016–T018 (US3). São independentes entre si, exceto T016 e T017, que mexem no mesmo `index.vue` e vão em sequência.
- T021 e T022 dependem de T010 e T018. T023 depende de todas as correções. Depois vêm T024 e, por fim, T025.

## Parallel Example

```text
T011 (creatures.css, atributos) || T016 (main.css, topbar)
T007 → T010 → T018   (mesmos arquivos de CSS e componentes da ficha e da biblioteca, em sequência)
```

## Implementation Strategy

O MVP é a US1 (ficha legível), já entregue. Esta rodada fecha as correções do crítico (T007, T010, T011, T016–T018), endurece os testes (T021, T022) e só então passa ao novo crítico e ao merge.
