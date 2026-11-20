# Research: spec 012

## R1. Busca em nome, nome EN e descrição

- **Decision**:
  - Coluna `SearchText` (`nvarchar(max)`) com `Creature.Normalize(nome) | Normalize(nomeEn) | Normalize(descrição sem HTML)`, calculada no domínio em `Apply`.
  - A busca usa `SearchKey.Contains(q)` OR (`q.Length >= 3` AND `SearchText.Contains(q)`).
  - Ordenação: acerto no `SearchKey` primeiro, depois o resto, cada grupo pela ordem atual.
- **Rationale**:
  - O catálogo tem cerca de 820 linhas, então full-text search do SQL Server seria peso operacional sem ganho.
  - A normalização é a mesma das criaturas.
  - O mínimo de 3 caracteres evita que "a" traga o catálogo inteiro pela descrição.
- **Alternatives**:
  - Full-text index: exige catálogo FTS no container e não lida com acentos como `Normalize`.
  - Busca em memória: exige carregar todas as descrições a cada busca.

## R2. Nome em inglês das magias

- **Decision**: tabela fixa `spells.names-en.json` (slug PT → nome EN), versionada.
  - Gerada uma vez e validada por script contra a lista de nomes do SRD 5.2.1 (5e-bits, commit fixado `c33bc0a…`).
  - Os nomes fora do SRD ficam com `"verified": false`.
  - O seeder aplica a tabela, e o hash do seed muda para reprocessar.
- **Rationale**: a fonte magias5e não traz o nome original. É o mesmo padrão da tradução congelada dos itens (spec 011).
- **Alternatives**:
  - Casar por nível, escola e componentes contra o SRD: é ambíguo.
  - Pedir ao Marcos: é dado determinístico de referência, então não cabe pergunta.

## R3. Rolagem do mestre por criatura

- **Decision**: `MasterRollPanel` com `<select>` das criaturas na mesa.
  - Candidatas: combatentes do encontro ativo com `creatureId`, rotulados pelo nome do combatente.
  - A criatura vem de `getCreature` (com cache em memória por id) e vai para o `CreatureRollPanel` existente.
  - A opção "Rolagem livre" mostra o `DiceRoller` na visão completa e o `QuickDiceRoller` no Modo Jogo.
- **Rationale**: reaproveita a expressão e a animação já testadas na spec 008/010.

## R4. Rolar até o resultado

- **Decision**: util `scrollToResult(el)`, que chama `scrollIntoView({ block: 'center', behavior })`.
  - `behavior` é `auto` quando `prefers-reduced-motion: reduce` e `smooth` nos outros casos.
  - É chamado quando o resultado aparece no `PlayerRollPanel`, `CreatureRollPanel`, `DiceRoller` e `QuickDiceRoller`.
  - O resultado já tem `aria-live` ou recebe um.

## R5. optgroup legível

- **Decision**: regra global `select optgroup { background: #1a1418; color: #f3e9e4; }` e `select option { background: #241d23; color: inherit; }`.
  - #1a1418 é um tom abaixo do #241d23 já usado nas opções.
  - O contraste é verificado por script (WCAG). O mínimo é 4,5:1.

## R6. Overview da ficha

- **Decision**: o `InitiativeModifier` vira um botão-resumo fixo no rodapé, que abre o `SheetOverviewModal`.
  - Dano e cura passam por `character.editSheet({ currentHitPoints })` + `flush()`, a mesma via que já sincroniza a mesa (FR-010 da spec 011).
  - O perfil manual sai: o overview chama `character.load()`, que cria a ficha.
