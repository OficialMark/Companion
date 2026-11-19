# Contrato de interface: navegação, ficha, mochila e painel de rolagem

Seletores `data-*` estáveis para os testes e2e.

## Navegação na barra superior

- Componente `ViewNav.vue` (renomeado de `MasterViewNav.vue`, mesma posição e mesmo estilo). Mestre: `[data-view="mesa"]`, `[data-view="compendio"]`. Jogador: `[data-view="mesa"]`, `[data-view="ficha"]` (ícone `lucide:scroll-text`).
- Estado em `?tela=`. `useAppView()` (renomeado de `useMasterView`) devolve `'mesa' | 'compendio' | 'ficha'`, com `compendio` só para mestre e `ficha` só para jogador; qualquer outro caso cai em `'mesa'`.
- `data-master-nav` continua existindo no nav do mestre (teste `compendium-nav.spec.ts` inalterado).

## Tela Ficha (`components/character/CharacterSheetView.vue`)

- Abas internas: `[data-tab="ficha"]`, `[data-tab="mochila"]`, `[data-tab="magias"]`, `[data-tab="rolagens"]`.
- Campo editável: `[data-field="<nome>"]`. Valor derivado: `[data-derived="<nome>"]`, somente leitura, com `title`/popover listando a composição (`parts`).
- Gravação automática: cada campo grava 400 ms depois da última tecla ou ao sair do campo. Indicador `[data-save-state]` = `salvo | salvando | erro | conflito`. Em `conflito`, botão "Recarregar ficha".
- Enquanto a gravação não volta, o valor derivado mostra o último calculado pelo servidor com opacidade reduzida (`[data-stale]`); nunca um número calculado no navegador.

## Mochila (`components/character/InventoryPanel.vue`)

- Mesmo componente na tela Ficha e no Modo Jogo (`[data-inventory]`).
- Item: `[data-item-id]`, alternador `[data-equip]` (aria-pressed), `[data-proficient]`, badge de efeito ignorado `[data-suppressed]`.
- Editor de item em modal (`ItemEditorModal.vue`), com lista de efeitos (tipo → alvo → operação → valor, campos que aparecem conforme o tipo).
- Botão "Do catálogo" abre `CatalogPicker.vue` (busca, filtros, paginação, "Adicionar").

## Painel de rolagem do jogador (`components/character/PlayerRollPanel.vue`)

- Aparece no Modo Jogo para jogadores no lugar do `QuickDiceRoller`; jogador sem ficha vê o `QuickDiceRoller` como hoje.
- Dados: `[data-die="d4|d6|d8|d10|d12|d20|d100"]` com contador. Clique soma 1; `pointerdown` mantido por 500 ms remove 1 (e cancela o clique); teclas `Delete`/`Backspace`/`-` com o dado em foco removem 1. `[data-clear-dice]` limpa.
- Fonte: `[data-source="weapon:<id>|spell:<id>|ability|skill:<x>|save:<x>"]`; tipo `[data-kind="check|attack|damage|free"]`.
- `[data-advantage="none|advantage|disadvantage"]` (grupo exclusivo), `[data-repetitions]` 1..20, adicionais `[data-extra]`.
- Expressão ao vivo: `[data-expression]`, texto igual à forma canônica do contrato de dados.
- Rolagens salvas: barra `[data-saved-roll]` com ícone e nome; `[data-incomplete]` quando falta arma ou magia.
- Ataque com arma, sendo combatente num encontro ativo: abre o `AttackConfirmModal` existente; "acertou" abre o `DamageRollModal` pré-preenchido com a expressão de dano da mesma arma.

## Histórico

- `RollHistory.vue` mostra, quando há `executions`, as duas execuções com a escolhida destacada (`[data-selected-execution]`) e o rótulo "maior" ou "menor"; constantes com rótulo aparecem como `(FOR) 3`.

## Ícones permitidos nas rolagens salvas

`lucide:sword`, `lucide:swords`, `lucide:axe`, `lucide:crosshair`, `lucide:shield`, `lucide:wand-sparkles`, `lucide:sparkles`, `lucide:flame`, `lucide:snowflake`, `lucide:zap`, `lucide:skull`, `lucide:heart-pulse`, `lucide:footprints`, `lucide:eye`, `lucide:brain`, `lucide:dices`. A lista vive em `SavedRollIcons` (backend) e `savedRollIcons.ts` (frontend), com teste que compara as duas.
