# Research: Aprimoramentos de Combate no Battlemap

Todos os itens abaixo foram resolvidos por inspeção direta do código existente (não por pesquisa externa), já que a feature é uma extensão de um sistema já implementado (battlemap, iniciativa, áreas de ataque). Nenhum item permanece como NEEDS CLARIFICATION.

## Decisão: como calcular "de quem é o turno" no token

- **Decisão**: Não introduzir nenhum campo novo no backend para o destaque de turno. O frontend já recebe `activeEncounter.combatants[].isCurrent` (calculado em `DtoMapper.ToDto`, a partir de `Encounter.CurrentTurnIndex`) e `battlemap.tokens[].participantId` (que, para tokens de criatura, já carrega o `Combatant.Id` — ver `BattlemapService.ToSnapshot`). O destaque é um cruzamento local: para um token de personagem, buscar o combatente cujo `participantId` bate com o do token; para um token de criatura, buscar o combatente cujo `id` bate com o `participantId` do token (mesmo campo, semântica de "id do token").
- **Rationale**: O servidor já é a autoridade do turno (Princípio II da constituição); refazer esse cálculo no cliente localmente a partir de dados já confiáveis e já sincronizados por `BattlemapInvalidated`/refetch de encontro não viola a autoridade do servidor — é apenas uma leitura derivada, não um novo estado.
- **Alternatives considered**: Adicionar um campo `isCurrentTurn` diretamente em `BattlemapTokenDto` calculado no backend. Rejeitado por redundância: duplicaria informação já presente em `EncounterDto.Combatants[].IsCurrent`, exigindo manter dois lugares sincronizados sem benefício, quando o join no cliente é trivial e os dois DTOs já chegam juntos em `CampaignSnapshot`.

## Decisão: onde armazenar vida máxima

- **Decisão**: Adicionar `MaxHitPoints` (int?) tanto em `Participant` (baseline definido na tela inicial) quanto em `Combatant` (cópia usada durante o combate), espelhando exatamente o padrão já existente para `CurrentHitPoints`/`InitiativeModifier`: `RollInitiativeAsync` já copia `participant.CurrentHitPoints` para o novo `Combatant` ao rolar iniciativa (`EncounterService.cs:58-59`); o mesmo será feito para `MaxHitPoints`.
- **Rationale**: Reaproveita um padrão já testado e em produção (dois lugares para o mesmo conceito, com cópia em um ponto único de transição) em vez de inventar um mecanismo novo de "vida máxima única fora do combate".
- **Alternatives considered**: Guardar `MaxHitPoints` somente em `Combatant`. Rejeitado porque a spec exige que o jogador informe a vida máxima "na tela inicial" (antes de haver necessariamente um `Combatant`/encontro ativo) — teria que introduzir um lugar novo só para isso, quando `Participant` já cumpre esse papel para `CurrentHitPoints`.

## Decisão: remover a privacidade de HP existente (FR-009a)

- **Decisão**: `DtoMapper.ToDto(Encounter, Guid? observerParticipantId, bool includeAllHitPoints)` deixa de condicionar `CombatantDto.CurrentHitPoints` (e o novo `MaxHitPoints`) à identidade do observador; o valor passa a ser sempre incluído.
- **Rationale**: Decisão explícita do usuário (ver spec, Assumptions) após confronto com o comportamento atual descoberto durante esta pesquisa: hoje um jogador só vê o próprio HP numérico na lista de iniciativa, e só o mestre vê o de todos. Manter essa regra tornaria a barra de vida do battlemap inconsistente com o pedido original ("visível a todos"), então a spec resolve isso removendo a restrição em vez de introduzir uma barra "aproximada" para não-donos.
- **Alternatives considered**: Manter a privacidade existente e mostrar uma barra aproximada (sem número exato) para tokens de terceiros. Era a opção "recomendada" apresentada ao usuário, mas não foi a escolhida — descartada para não introduzir dois modos de exibição de barra (exato vs. aproximado) nesta versão.

## Decisão: tamanho do token e condições de status — nova entidade vs. estender `AttackArea`/`BattlemapTokenPosition`

- **Decisão**: Nova entidade `BattlemapTokenAppearance` (um por token, chave `BattlemapId` + `ParticipantId?`/`CombatantId?`/`MarkerId?`, campos `FootprintSize` (1-5) e `StatusConditions` (lista do enum `StatusCondition`, persistida como string delimitada)), análoga a `AttackArea`.
- **Rationale**: `BattlemapTokenPosition` é recriada (deletada e recriada) sempre que um token volta para a reserva e é reposicionado (`BattlemapService.MoveAsync`, remove/insert), então tamanho e condições seriam perdidos nesse ciclo se vivessem ali. `AttackArea` já demonstra o padrão correto para "um atributo opcional por token que sobrevive à reposição": uma tabela própria, chaveada pelo id do token, independente da posição. Reutilizar esse padrão em vez de inventar um novo é consistente com "Search before building" e com a constituição (evoluir o monólito existente, não introduzir modelos paralelos).
- **Alternatives considered**: Adicionar `FootprintSize`/`StatusConditions` diretamente em `Participant`/`Combatant`. Rejeitado porque tornaria esses campos exclusivos de personagens/criaturas — mas a spec exige que tokens representativos (marcadores, sem `Combatant` associado) também tenham tamanho e condições; uma entidade separada, chaveada pelo id genérico do token, cobre os três tipos (participante, criatura, marcador) sem duplicar colunas em três lugares.

## Decisão: condições de status como string delimitada vs. tabela de junção

- **Decisão**: Um enum fechado `StatusCondition` (Stunned, Restrained, Blinded, Poisoned, Invisible, Prone) persistido como uma única coluna de texto delimitado (ex.: `"Stunned,Blinded"`) em `BattlemapTokenAppearance`, convertido para `IReadOnlyList<StatusCondition>` no domínio.
- **Rationale**: Conjunto pequeno e fixo nesta versão (spec define um mínimo de 6 condições, sem exigir catálogo dinâmico/customizável). Uma tabela de junção (`TokenStatusConditions` com FK para `BattlemapTokenAppearance`) adicionaria uma tabela e um join para um requisito que não pede condições customizadas pelo usuário. Constituição, Princípio V: "otimizações e abstrações devem responder a um requisito ou medição concreta, não a uma possibilidade hipotética."
- **Alternatives considered**: Tabela de junção normalizada. Rejeitada por complexidade desnecessária para 6 valores fixos; pode ser promovida no futuro se a lista de condições precisar ser customizável por campanha (fora do escopo desta spec).

## Decisão: tokens representativos (marcadores) — nova entidade vs. reaproveitar `Combatant`

- **Decisão**: Nova entidade `BattlemapMarker` (Id, BattlemapId, OwnerParticipantId, Label, ColorKey, CreatedAt), com um terceiro identificador opcional (`MarkerId?`) adicionado a `BattlemapTokenPosition`, `AttackArea` e `BattlemapTokenAppearance` (mutuamente exclusivo com `ParticipantId?`/`CombatantId?`).
- **Rationale**: Um marcador, pela spec (Assumptions), não participa de iniciativa nem tem vida própria — inserir um `Combatant` para isso obrigaria a driblar as invariantes de `Encounter.AddCombatant` (que sempre calcula ordem de criação/iniciativa e é pensado para "quem luta"). Um tipo de linha própria, mais simples, evita ambiguidade sobre se um marcador "conta" para iniciativa/dano.
- **Alternatives considered**: Reaproveitar `Combatant` com `ParticipantId == null` (igual às criaturas do mestre) e um novo booleano `IsMarker`. Rejeitado: misturaria no mesmo conceito duas coisas com regras de autorização diferentes (criaturas só o mestre cria/controla; marcadores o próprio jogador cria/controla) e faria `EncounterService`/`InitiativeTracker` precisarem filtrar marcadores para não aparecerem na lista de iniciativa — mais acoplamento do que introduzir um terceiro identificador já no padrão dual existente.

## Decisão: sincronização em tempo real dos novos dados

- **Decisão**: Nenhum novo tipo de evento SignalR. Toda mutação nova (`SetTokenAppearanceAsync`, `CreateMarkerAsync`, `RemoveMarkerAsync`, e o `MoveAsync` já existente reaproveitado para mover marcadores) termina chamando `notifier.BattlemapInvalidatedAsync(...)`, o mesmo evento genérico `BattlemapInvalidated` que já dispara o refetch do snapshot completo do battlemap no frontend (`campaign.ts:170`).
- **Rationale**: Padrão já em produção para `AttackArea` (spec 005) e para posições de token (spec 003); adicionar tipos de evento novos multiplicaria código de serialização/handling sem ganho, já que o snapshot completo já é pequeno o bastante para ser refeito a cada mudança (mesma decisão já validada nas duas specs anteriores).
- **Alternatives considered**: Eventos dedicados (`TokenAppearanceChanged`, `MarkerCreated`, etc.), como o `tasks.md` de spec 005 chegou a cogitar antes de ser implementado — mas a implementação real de spec 005 já abandonou essa ideia em favor do invalidate genérico; manter consistência com a decisão já tomada.
