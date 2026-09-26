# Contratos: Modo Jogo (Visão Focada de Gameplay)

Esta feature **não introduz nenhum endpoint novo**. Todos os fluxos (rolagem rápida,
confirmação de acerto, rolagem de dano e aplicação a um alvo) são implementados no
frontend reaproveitando os contratos REST já existentes e documentados abaixo, expostos
por `frontend/app/composables/useCompanionApi.ts`.

## Endpoints reaproveitados

### `POST /campaigns/{campaignId}/dice-rolls`
- Usado por: rolagem rápida (d20/d8/d6 + modificador) e rolagem de dano dentro do modal de
  dano.
- Request: `{ expression: string, publish: boolean }` — a expressão inclui o modificador
  somado no formato aceito pela gramática existente (ex. `"1d20+3"`, `"2d20+3"`,
  `"2#1d20+3"` para múltiplas rolagens, `"2d6+1d8+3"` para dano composto).
- Response: `DiceRoll` (contém `id`, `total`, `results[].terms[]` com valores brutos por
  dado) — usado tanto para exibir o resultado quanto para alimentar a animação corrigida.
- Contrato inalterado; nenhuma mudança de schema necessária.

### `POST /campaigns/{campaignId}/dice-rolls/{rollId}/reveal`
- Usado por: mesma sequência de "revelar" já existente em `DiceRoller.vue`, reaproveitada
  pelo novo componente de rolagem rápida para manter o mesmo comportamento de animação
  (rolar velado → revelar → registrar no histórico).
- Contrato inalterado.

### `POST /campaigns/{campaignId}/encounters/{encounterId}/attacks`
- Usado por: modal de dano/alvo (`DamageRollModal.vue`), para aplicar o dano de uma rolagem
  já revelada aos combatentes selecionados.
- Request: `{ diceRollId: string, targetCombatantIds: string[] }` com header
  `Idempotency-Key`.
- Response: `AttackResult { attack: { damage: number, ... }, encounter: Encounter }`.
- Contrato inalterado; a única diferença é a origem da chamada (modal do Modo Jogo em vez
  do painel `CombatAttack.vue` sempre visível).

## Contrato de UI (novo, interno ao frontend — não uma API pública)

Não há uma "API" formal aqui, mas os componentes novos definem as seguintes interações
observáveis, que servem como contrato entre os componentes:

- `GameModeToggle` emite um evento/estado booleano consumido por `index.vue` para decidir
  quais painéis renderizar.
- `QuickDiceRoller` emite `rolled: DiceRoll` quando uma rolagem de d8/d6 é concluída, e
  `rolled-d20: DiceRoll` (ou um discriminador equivalente) quando a rolagem é de d20, para
  que `index.vue`/o container do Modo Jogo saiba quando abrir `AttackConfirmModal`.
- `AttackConfirmModal` emite `hit: DiceRoll` (abre `DamageRollModal` com a rolagem de
  ataque como contexto) ou `dismissed` (timeout ou "Não").
- `DamageRollModal` reaproveita as mesmas props/eventos já validados em `CombatAttack.vue`
  (`rolled`, seleção de alvos, `confirm`/`cancel`) para não recriar a lógica de permissão
  (`canAttack`) e idempotência.
