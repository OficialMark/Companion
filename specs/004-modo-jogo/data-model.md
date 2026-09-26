# Phase 1 Data Model: Modo Jogo (Visão Focada de Gameplay)

Esta feature não introduz novas entidades persistidas no backend. Os "dados" abaixo são
estados de UI (client-side, não persistidos) e as entidades de domínio já existentes que
são consumidas via `useCompanionApi`.

## Estados de UI (novos, não persistidos)

### GameModeState
- `active: boolean` — se a visão Modo Jogo está ativa na página de sessão.
- Escopo: local ao componente de página (`index.vue`); reinicia para `false` a cada
  carregamento de página (não é uma preferência persistida entre sessões do navegador).

### QuickRollModifier
- `value: number` — modificador atual aplicado às rolagens rápidas (d20/d8/d6).
- Regras: inteiro, pode ser negativo, zero ou positivo; sem limite mínimo/máximo definido
  pela spec (assume-se um intervalo razoável, ex. -99 a +99, para evitar expressões
  degeneradas).
- Escopo: local ao componente `QuickDiceRoller.vue`; reinicia para `0` a cada carregamento
  de página (ver Assunções da spec).

### AttackConfirmState (transitório, dentro de `AttackConfirmModal.vue`)
- `pendingRoll: DiceRoll | null` — a rolagem de d20 que originou a pergunta "Acertou?".
- `expiresAt: number` — timestamp usado para o timeout de 10s.
- Transições: `pending` → `hit` (usuário confirma acerto → abre `DamageRollModal`) |
  `miss` (usuário nega ou timeout de 10s → descarta, nenhuma rolagem de dano é iniciada).

### DamageTargetSelectionState (transitório, dentro de `DamageRollModal.vue`)
- Reaproveita o mesmo modelo já usado por `CombatAttack.vue`: `roll: DiceRoll | null`,
  `selected: string[]` (ids de combatentes escolhidos), `requestId: string` (idempotência).

## Entidades de domínio existentes (consumidas, não alteradas)

### DiceRoll (já existe em `frontend/app/types/campaign.ts`, originado do backend)
- Campos relevantes usados por esta feature: `id`, `total`, `results[].terms[].expression`,
  `results[].terms[].values`.
- Usado tanto para a rolagem de ataque (d20 rápido) quanto para a rolagem de dano.

### Encounter / Combatant (já existentes)
- `encounter.status` (deve ser `Active` para permitir o fluxo de dano, mesma regra de
  `canAttack` em `CombatAttack.vue`).
- `encounter.combatants[]` — lista de alvos possíveis para o dano (`id`, `name`,
  `participantId`).

### AttackResult (já existe, retornado por `resolveAttack`)
- `attack.damage` — valor de dano aplicado, usado na mensagem de confirmação.
- `encounter` — snapshot atualizado do encontro após aplicar o dano.

## Correção de bug: modelo de animação de dados

### AnimatedDie (já existe em `frontend/app/utils/diceAnimation.ts`)
- Campo `faces` deixa de ser restrito ao union type `AnimatedDieFace` (4|6|8|10|12|20|100)
  e passa a aceitar qualquer inteiro de faces válido aceito pelo backend (2–1000).
- Novo conceito derivado (não persistido): "forma visual" do dado — `standard` (quando
  `faces` está no conjunto com CSS dedicado) vs. `generic` (fallback neutro para qualquer
  outro número de faces), usado apenas para escolher a classe CSS em
  `DiceRollAnimation.vue`.
