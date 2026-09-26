# Implementation Plan: Modo Jogo (Visão Focada de Gameplay)

**Branch**: `004-modo-jogo` | **Date**: 2026-09-06 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/004-modo-jogo/spec.md`

## Summary

Adicionar uma visão alternativa ("Modo Jogo") na página única de sessão do frontend, que
oculta os painéis administrativos e mostra apenas Rolagem de Dados, Combate, Mapa e
Histórico. Dentro dessa visão, o componente de rolagem de dados ganha uma nova mecânica de
rolagem rápida (d20/d8/d6 + modificador persistente com incrementar/decrementar), e uma
rolagem de d20 dispara um fluxo guiado de confirmação de acerto → rolagem de dano → escolha
de alvo, reaproveitando o fluxo de ataque/dano já existente no backend (`resolveAttack`).
Também corrige um bug de animação: dados com número de faces fora do conjunto atualmente
animado (d4/d6/d8/d10/d12/d20/d100) hoje não recebem nenhuma animação (o termo é descartado
silenciosamente); a correção garante uma animação genérica para qualquer face válida
aceita pelo backend (2–1000), sem nunca aplicar/rotular a face errada.

Toda a mudança é **frontend-only**: nenhuma rota de API nova é necessária, pois os
endpoints existentes de rolagem de dados (`POST /dice-rolls`) e resolução de ataque
(`POST /encounters/{id}/attacks`) já cobrem os requisitos (o servidor continua sendo a
única autoridade sobre resultados de dados e dano, conforme Princípio II da constituição).

## Technical Context

**Language/Version**: TypeScript 5 / Vue 3 (`<script setup>`) sobre Nuxt 4, consistente com o restante do frontend.

**Primary Dependencies**: Vue 3, Nuxt 4, Pinia (`useCampaignStore`), composable `useCompanionApi` (já existente, sem novos endpoints).

**Storage**: N/A para esta feature — nenhuma persistência nova; reaproveita os registros de `DiceRoll` e `Encounter`/`Combatant` já existentes no backend.

**Testing**: Vitest + `@vue/test-utils` para componentes/composables (`frontend/tests/*.spec.ts`); Playwright para os fluxos e2e críticos (`frontend/tests/e2e`), conforme já usado no projeto.

**Target Platform**: Navegador web moderno, desktop e Android (mobile-first), igual ao restante do app (Princípio III da constituição).

**Project Type**: Web application (Nuxt SPA/PWA) — mudança confinada a `frontend/`, sem alterações em `backend/`.

**Performance Goals**: Sem metas específicas novas; a animação de rolagem deve permanecer fluida (mesmo orçamento visual do fluxo atual, ~750ms de rolagem + 250ms de assentamento).

**Constraints**: O resultado de qualquer dado (incluindo os das rolagens rápidas) DEVE vir do backend via `rollDice`; o modificador é aplicado como parte da expressão enviada ao servidor (ex. `"1d20+3"`), nunca somado apenas no cliente sobre um valor não confirmado pelo servidor — preservando o Princípio II (servidor como autoridade do jogo).

**Scale/Scope**: Uma página existente (`frontend/app/pages/index.vue`), ~4 componentes Vue novos/alterados (visão do Modo Jogo, rolador rápido, modal de acerto, modal de dano/alvo) e um utilitário de animação corrigido.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

1. **Camadas e módulos** — OK. Não há mudança de backend; nenhuma nova camada é criada. Componentes novos ficam dentro de `frontend/app/components/`, organizados por domínio de gameplay (dados, combate), como já é o padrão do projeto.
2. **Autoridade do servidor** — OK. Todas as rolagens (incluindo as rápidas) e a aplicação de dano continuam sendo feitas via `api.rollDice` e `api.resolveAttack`; o cliente só monta a expressão (`1d20+{modificador}`) e exibe o resultado que o servidor retorna. Nenhum valor de dado ou dano é calculado/persistido no cliente.
3. **Desktop/Android/reconexão** — OK. Os novos componentes seguem o mesmo padrão responsivo (mobile-first) e os mesmos estados de `store.connection` (offline desabilita os botões de dado), sem introduzir novo estado de conexão.
4. **Testes** — Planejado: testes unitários para o novo utilitário de modificador e para a correção de `diceForAnimation`/`diceWithResults` (Vitest), e um teste de componente para o fluxo acerto→dano→alvo. Teste de regressão específico para o bug de dado não convencional (ex. d27) exigido pela constituição para correções reproduzíveis.
5. **SQL Server / migrations** — N/A, sem alteração de persistência.
6. **Nova dependência operacional** — Nenhuma nova dependência é introduzida.

Nenhuma violação identificada. Tabela de complexidade não aplicável.

## Project Structure

### Documentation (this feature)

```text
specs/004-modo-jogo/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (nota: reaproveita contratos existentes)
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
frontend/
├── app/
│   ├── pages/
│   │   └── index.vue                       # adiciona botão "Modo Jogo" e alterna conjunto de painéis visíveis
│   ├── components/
│   │   ├── GameModeToggle.vue               # NOVO: botão da barra superior + estado ativo/inativo
│   │   ├── QuickDiceRoller.vue               # NOVO: 3 botões de dado + modificador +/-, substitui DiceRoller no Modo Jogo
│   │   ├── AttackConfirmModal.vue            # NOVO: modal "Acertou?" com timeout de 10s
│   │   ├── DamageRollModal.vue               # NOVO: modal de rolagem de dano + seleção de alvo (reaproveita lógica de CombatAttack)
│   │   ├── DiceRoller.vue                    # existente, reaproveitado fora do Modo Jogo
│   │   ├── CombatAttack.vue                  # existente, lógica de seleção de alvo/resolveAttack reaproveitada
│   │   └── DiceRollAnimation.vue             # alterado: fallback de visual genérico para faces não padrão
│   └── utils/
│       └── diceAnimation.ts                  # alterado: não descarta mais faces fora do conjunto padrão
└── tests/
    ├── dice.spec.ts                          # estendido: caso de d27/dado não convencional
    ├── attack.spec.ts                        # estendido: fluxo acerto→dano→alvo do Modo Jogo
    └── e2e/                                  # cenário e2e do Modo Jogo, se aplicável
```

**Structure Decision**: Mudança inteiramente dentro de `frontend/app/` (componentes e um utilitário) e `frontend/tests/`, sem novas rotas de API nem migrations. Reaproveita os composables e componentes existentes (`useCompanionApi`, `useCampaignStore`, `CombatAttack.vue`) em vez de recriar lógica de resolução de ataque.

## Complexity Tracking

*Nenhuma violação da constituição identificada — tabela não aplicável.*
