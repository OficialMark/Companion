# Implementation Plan: Layout Responsivo por Princípios de Design

**Branch**: `OficialMark/design-responsivo-6fc489fd` | **Date**: 2026-11-27 | **Spec**: [spec.md](spec.md)

## Summary

Reorganizar a Mesa em três zonas com grid CSS dirigido por largura, levar a navegação do celular para uma barra inferior, liberar o limite de 1380 px e tornar mapa e ficha proporcionais à tela. Tudo é medido por um auditor Playwright que roda como gate, e o resultado foi julgado por críticos cegos (antes × depois). Os testes ponta a ponta revelaram dois bugs da store da ficha (eco do SignalR), corrigidos aqui.

## Technical Context

**Language/Version**: TypeScript 5.9; Vue 3.5; CSS (grid, container queries, `:has()`)
**Primary Dependencies**: Nuxt 4, Pinia 3, Playwright (já no projeto)
**Storage**: nenhuma mudança
**Testing**: Vitest (componentes e store), Playwright e2e desktop + Android, auditor de layout
**Target Platform**: navegadores modernos Windows e Android, 360 a 2560 px
**Constraints**: sem framework de CSS novo; sem mudar contratos da API
**Scale/Scope**: `pages/index.vue`, CSS globais, `BattlemapBoard`, `TokenSourcePicker`, `DicePad`, `PlayerRollPanel`, `SheetCoreTab`, `useBattlemapViewport`, `stores/character`

## Constitution Check

- **Camadas / autoridade do servidor**: PASS. Só frontend; a correção da store mantém o servidor como fonte (recarrega o snapshot).
- **Web responsiva e resiliente (III)**: PASS. É o objetivo: mobile-first, toque de 44 px, teclado (foco não fica sob barras fixas).
- **Qualidade verificável (IV)**: PASS. Gate de layout em 7 larguras, e2e desktop + Android verdes, regressões com teste.
- **Simplicidade (V)**: PASS. CSS puro; nenhuma dependência nova.

## Project Structure

```text
frontend/
├── app/pages/index.vue                         zonas da Mesa
├── app/assets/css/{main,battlemap,character,creatures,inventory,roll-panel,campaign-knowledge}.css
├── app/components/battlemap/{BattlemapBoard,TokenSourcePicker}.vue
├── app/components/character/{DicePad,PlayerRollPanel,RollConfigFields,SheetCoreTab}.vue
├── app/components/campaign/CampaignNotebook.vue
├── app/composables/useBattlemapViewport.ts     ajuste ao palco + hold()
├── app/stores/character.ts                     remoteChangePending + reconcile()
├── app/utils/rollExpression.ts                 NO_DICE_ERROR
└── tests/
    ├── e2e/layout-audit.spec.ts                gate de layout
    ├── e2e/{creature-catalog,mesa-012,no-browser-context-menu,token-sheet-shortcut}.spec.ts
    └── {battlemap-viewport,character-store,dice-pad,player-roll-panel,token-source-picker,battlemap-markers}.spec.ts
docs/testes-e2e.md                              roteiro de validação
```
