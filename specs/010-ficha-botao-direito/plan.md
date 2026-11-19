# Implementation Plan: Ficha pelo botão direito e resultado da rolagem visível por 2 s

**Branch**: `OficialMark/ficha-botao-direito-331fa84b` | **Date**: 2026-11-19 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/010-ficha-botao-direito/spec.md`

## Summary

Duas mudanças de interação, só no frontend:

- **Botão direito no token**: abre a ficha do token. A decisão "este token tem ficha?" fica num único ponto, `sheetFor(token)`.
- **Rolagem pela ficha**:
  - Mantém os dados e o total na tela por 2 s, dentro da área visível do modal.
  - Atualiza o histórico e o preset na revelação.
  - Libera o botão na hora.

Decisões em [research.md](research.md).

## Technical Context

- **Language/Version**: TypeScript 5 / Vue 3 (Nuxt 4).
- **Primary Dependencies**: as existentes (Pinia, @nuxt/icon). Nenhuma nova.
- **Storage**: N/A.
- **Testing**:
  - Vitest com timers falsos para a espera de 2 s.
  - Playwright num navegador real, com medição da visibilidade dentro da ficha.
- **Target Platform**: navegadores modernos no Windows e Android. O botão direito é gesto de mouse; o toque continua usando "Ficha e rolagens" no menu do token.
- **Project Type**: aplicação web. Só `frontend/` muda.
- **Performance Goals**: a ficha abre com 1 ação, e o resultado aparece imediatamente após a revelação do servidor.
- **Constraints**:
  - Nenhuma regressão de gesto no mapa (clique esquerdo, arraste, névoa, clique do meio).
  - Acessível por teclado.
  - Nenhum timer pendente ao fechar a ficha.
- **Scale/Scope**: 4 arquivos de código, 2 suítes de teste.

## Constitution Check

*GATE: passa antes da Fase 0 e foi reavaliado depois do design.*

| Princípio | Situação |
|---|---|
| I. Monólito modular | OK. Componentes e um utilitário existentes. |
| II. Servidor como autoridade | OK. A rolagem continua no servidor; só muda quanto tempo o resultado fica na tela. |
| III. Web responsiva e resiliente | OK. Equivalente de teclado (Shift+F10 / tecla Menu). No toque, o caminho pelo menu continua. A rolagem com erro limpa a tela e mostra a mensagem. |
| IV. Qualidade verificável | OK. Testes de unidade para gestos e timers e e2e no navegador real, ambos verificados por mutação. |
| V. Simplicidade | OK. Um timer, uma constante e uma função de decisão. |

Sem violações.

## Project Structure

### Documentation (this feature)

```text
specs/010-ficha-botao-direito/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/ui-interaction.md
├── checklists/requirements.md
└── tasks.md            # /speckit-tasks
```

### Source Code (repository root)

```text
frontend/app/
├── components/battlemap/BattlemapBoard.vue      # sheetFor, openTokenContext, dica na barra de status
├── components/battlemap/BattlemapToken.vue      # evento context (Shift+F10 / tecla Menu)
├── components/creatures/CreatureRollPanel.vue   # espera de 2 s, rolagem até os dados, data-roll-showing
├── utils/creatureRoll.ts                        # RESULT_HOLD_MS = 2000
└── assets/css/creatures.css                     # .roll-stage (scroll-margin)

frontend/tests/
├── token-sheet-shortcut.spec.ts                 # unidade
└── e2e/token-sheet-shortcut.spec.ts             # navegador real
```

**Structure Decision**: aplicação web existente; só `frontend/` muda.

## Complexity Tracking

Nenhuma violação.
