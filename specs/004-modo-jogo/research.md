# Phase 0 Research: Modo Jogo (Visão Focada de Gameplay)

Nenhum item ficou marcado como `NEEDS CLARIFICATION` na spec ou no plano — as decisões
abaixo documentam as investigações feitas no código existente para embasar o desenho da
feature (Phase 1), evitando reinventar comportamento já resolvido pelo sistema.

## 1. Como o Modo Jogo deve ser implementado sem um sistema de abas existente

- **Decision**: Modo Jogo é um estado local de UI (`ref<boolean>` ou similar) na página
  `frontend/app/pages/index.vue`, que alterna quais blocos do `.dashboard-grid` são
  renderizados — não é uma rota separada.
- **Rationale**: `app.vue` hoje só contém `<NuxtPage />` e `index.vue` é a única página de
  sessão, com um `.topbar` sem abas (linhas 67-79) e um `.dashboard-grid` com todos os
  painéis (linhas 97-136). Criar uma rota nova exigiria duplicar toda a lógica de conexão
  SignalR/snapshot já inicializada em `index.vue`. Alternar visibilidade preserva o mesmo
  estado de sessão/loja (`useCampaignStore`) e evita remontar conexões.
- **Alternatives considered**: (a) nova rota `/session/:code/play` — rejeitada por duplicar
  bootstrap de sessão e complicar a reconexão SignalR; (b) query param — desnecessário já
  que não há requisito de deep-link para o Modo Jogo.

## 2. Como a rolagem rápida deve aplicar o modificador sem violar "servidor como autoridade"

- **Decision**: O componente de rolagem rápida monta uma expressão textual (ex. `1d20+3`,
  `1d20-2`, `1d8+0`) e a envia via `api.rollDice(session, expression, publish)`, exatamente
  como `DiceRoller.vue` já faz hoje (linha 18/22 de `DiceRoller.vue`). O modificador é
  apenas um valor de UI usado para montar a expressão; o total exibido sempre vem da
  resposta do servidor (`DiceRoll.total` / `result.results[].total`).
- **Rationale**: Constituição Princípio II exige que resultados de dados sejam calculados
  e persistidos pelo backend, nunca pelo cliente. Montar a expressão e delegar o cálculo ao
  mesmo endpoint já usado hoje satisfaz isso sem exigir novos contratos.
- **Alternatives considered**: somar o modificador no cliente sobre o valor bruto do dado —
  rejeitado por violar a autoridade do servidor e por já existir suporte nativo a `+N`/`-N`
  na gramática de expressão aceita pelo backend (`DiceExpression.cs`).

## 3. Como disparar o fluxo de acerto/dano a partir de um d20 rolado pelos botões rápidos

- **Decision**: O fluxo de acerto (`AttackConfirmModal.vue`) e de dano/alvo
  (`DamageRollModal.vue`) reaproveitam a mesma API (`api.rollDice` para a rolagem de dano e
  `api.resolveAttack` para aplicar o dano ao(s) alvo(s) escolhido(s)) já usada por
  `CombatAttack.vue` (linhas 9-15 e 21-22). A lógica de seleção de alvos (checkboxes sobre
  `encounter.combatants`, gate `canAttack`) é reaproveitada/extraída, não reescrita.
- **Rationale**: `resolveAttack` já resolve exatamente "aplicar o dano de uma rolagem a uma
  lista de combatentes alvo" (`useCompanionApi.ts` linha 51), incluindo idempotência via
  `Idempotency-Key`. Reaproveitar evita duplicar regras de permissão (`canAttack`) e mantém
  consistência com o fluxo de combate já existente fora do Modo Jogo.
- **Alternatives considered**: novo endpoint dedicado para "dano vindo de rolagem rápida" —
  rejeitado por não haver diferença de domínio entre um ataque iniciado pelo painel de
  Combate tradicional e um iniciado pela rolagem rápida do Modo Jogo; o back-end já modela
  isso como "resolver ataque de um encontro a partir de uma rolagem".

## 4. Natureza real do bug de animação de dados não convencionais

- **Decision**: O bug não é "usa a animação do d100 incorretamente" — é que
  `diceForAnimation` (`frontend/app/utils/diceAnimation.ts` linha 47) descarta
  silenciosamente qualquer termo cujo número de faces não esteja em
  `ANIMATED_DICE_FACES = [4,6,8,10,12,20,100]`, e `diceWithResults` (linha 21) faz o mesmo
  ao tentar casar os valores retornados pelo servidor com os dados visuais. Isso resulta em
  nenhuma animação para esse termo (ou, se a expressão inteira só tiver termos não padrão,
  nenhuma animação alguma — `DiceRoller.vue` linha 17 pula direto para o resultado). A
  correção deve permitir que qualquer face válida (o backend aceita 2–1000,
  `DiceExpression.cs` linhas 76-82) gere um dado visual, usando uma forma/CSS genérica
  quando não houver uma forma específica desenhada para aquele número de faces.
- **Rationale**: Confirmado lendo o código (não há nenhum caminho que troque a face por
  100); a spec original do usuário descrevia o sintoma observado (nenhuma animação = "nada
  acontece visualmente, parece bugado") mas a causa raiz é a omissão, não uma substituição
  incorreta. Documentar isso evita "corrigir" um comportamento que não existe.
- **Alternatives considered**: mapear todo número de faces não padrão para a animação do
  d20 (mais próxima poliedricamente) — considerado, mas uma classe CSS neutra/genérica
  (não anexada visualmente a nenhum valor de face específico, ex. um dado com textura
  neutra) comunica melhor que o valor real virá do servidor, evitando qualquer impressão de
  que o dado tem 20 faces.
