# Implementation Plan: Compêndio na barra superior e ficha de criatura legível

**Branch**: `OficialMark/compendium-ficha-331fa84b` | **Date**: 2026-11-19 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/009-compendio-ficha/spec.md`

## Summary

Trabalho só de interface: a API, os dados e as regras da spec 008 não mudam. São duas frentes.

- **Ficha (ver e editar)**: o layout passa a usar uma escala única de espaçamento (4/8/12/16/24 px).
  - A leitura vira um bloco de estatísticas.
  - O editor usa uma grade de 6 colunas, com spans por largura que deixam cada linha sem buraco.
  - A barra Cancelar/Salvar sai da área de rolagem.
- **Compêndio**: a navegação Mesa/Compêndio do mestre sai do meio da página e vai para a barra superior.
  - O Compêndio vira tela própria de largura total.
  - A tela escolhida fica no endereço (`?tela=compendio`).

Detalhes das decisões em [research.md](research.md).

## Technical Context

- **Language/Version**: TypeScript 5 / Vue 3 (Nuxt 4), CSS próprio.
- **Primary Dependencies**: Nuxt 4 (router e auto-imports), Pinia, @nuxt/icon (Lucide). Nenhuma dependência nova.
- **Storage**: N/A. A tela fica no endereço da página; nada é gravado no servidor.
- **Testing**:
  - Vitest + @vue/test-utils (unidade).
  - Playwright (e2e, projetos `desktop` e `android`) contra uma stack Docker isolada da sessão.
- **Target Platform**: navegadores modernos no Windows e Android, a PWA existente.
- **Project Type**: aplicação web (`frontend/` Nuxt + `backend/` .NET). Só o frontend muda.
- **Performance Goals**: trocar Mesa/Compêndio sem nova carga de página e sem nova requisição além da listagem que já existe.
- **Constraints**:
  - Alvos de toque de pelo menos 44 px.
  - Sem rolagem horizontal em 375 px.
  - Nenhum texto sobreposto em 1280, 768 e 375 px.
  - Comportamento da ficha preservado (FR-011).
- **Scale/Scope**: 2 componentes novos, 1 composable novo, 5 arquivos alterados, 2 suítes de teste novas.

## Constitution Check

*GATE: passa antes da Fase 0 e foi reavaliado depois do design.*

| Princípio | Situação |
|---|---|
| I. Monólito modular e Clean Architecture | OK. Nenhuma camada nova; a mudança fica em componentes, um composable e CSS do frontend. |
| II. Servidor como autoridade do jogo | OK. Nada de regra passa para o cliente; o filtro "Minhas criaturas" usa o filtro de origem que a API já valida. |
| III. Experiência web responsiva e resiliente | Centro da feature. Mobile-first, toque e teclado (`aria-current`, foco visível), verificado em 1280/768/375 px e no projeto Android. |
| IV. Qualidade verificável | OK. Testes de unidade para navegação, endereço, guarda de jogador e "Minhas criaturas". E2E que mede sobreposição, 44 px e rolagem horizontal. |
| V. Simplicidade operacional | OK. Query string com o router que já existe; nenhum serviço, rota ou armazenamento novo. |

Sem violações; a seção Complexity Tracking não se aplica.

## Project Structure

### Documentation (this feature)

```text
specs/009-compendio-ficha/
├── spec.md
├── plan.md              # este arquivo
├── research.md          # decisões de design (Fase 0)
├── data-model.md        # estado de interface (Fase 1)
├── quickstart.md        # roteiro de validação (Fase 1)
├── contracts/
│   └── ui-navigation.md # contrato do endereço e da navegação (Fase 1)
├── checklists/requirements.md
└── tasks.md             # gerado por /speckit-tasks
```

### Source Code (repository root)

```text
frontend/app/
├── composables/useMasterView.ts                 # novo: tela <-> ?tela, guarda de jogador, rolagem ao topo
├── components/MasterViewNav.vue                 # novo: Mesa / Compêndio na barra superior
├── pages/index.vue                              # nav na topbar, wrapper .topbar-center, Compêndio em tela cheia
├── components/creatures/CreatureLibrary.vue     # título, Minhas criaturas, rótulos curtos, estado vazio
├── components/creatures/CreatureSheetModal.vue  # 4 números + linha de deslocamento, rodapé
├── components/creatures/CreatureEditor.vue      # spans da grade, área de campos rolável, itens em cartão
└── assets/css/
    ├── creatures.css                            # ficha, editor, compêndio
    └── main.css                                 # navegação na topbar

frontend/tests/
├── compendium-nav.spec.ts                       # unidade
└── e2e/compendium-sheet.spec.ts                 # e2e: 1280/768/375 + Android
```

**Structure Decision**: aplicação web existente. Só `frontend/` muda; `backend/` fica intocado.

## Complexity Tracking

Nenhuma violação a justificar.
