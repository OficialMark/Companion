# Research: Tokens do Compêndio e de Itens

## R1. Novo tipo de token ou marcador com origem?

- **Decision**: marcador (`BattlemapMarker`) com `CreatureId` ou `CatalogItemId` opcionais.
- **Rationale**: marcadores já têm dono, posição, aparência, área de ataque, remoção e regra de névoa. Um tipo novo duplicaria os quatro ramos (`participant`/`combatant`/`marker`) em cada operação do `BattlemapService`.
- **Alternatives**: tabela própria de "tokens de compêndio" (rejeitada: duplicação); combatente sem iniciativa (rejeitada: jogadores não criam combatentes e a criatura entraria na ordem de turnos).

## R2. Chave estrangeira para criatura e item?

- **Decision**: colunas anuláveis sem FK.
- **Rationale**: criaturas próprias podem ser excluídas e itens do catálogo podem ser removidos ou substituídos por cópia da mesa. O token deve sobreviver com o nome; o snapshot só perde a imagem.
- **Alternatives**: FK com `SET NULL` (rejeitada: SQL Server recusa múltiplos caminhos em cascata nesta árvore e a perda do vínculo não é erro).

## R3. O que o jogador pode buscar?

- **Decision**: rota `GET /creatures/token-options` para qualquer participante; jogador vê compêndio global ativo, mestre vê também as criaturas da mesa; resposta sem ficha.
- **Rationale**: a biblioteca completa é só do mestre (spec 008). Criaturas próprias e edições podem revelar a trama; estatísticas do compêndio também não são para a mesa.
- **Alternatives**: liberar `GET /creatures` para jogadores (rejeitada: expõe PV/CA e criaturas próprias).

## R4. Itens

- **Decision**: reutilizar `GET /catalog/items` (já aberto a participantes) com `CatalogScope.ResolveItem` na criação.
- **Rationale**: mesma visão de catálogo da ficha; uma regra só.

## R5. Nome padrão

- **Decision**: `BattlemapMarker.LabelFrom`: até 40 caracteres, corta no último espaço se ele estiver na segunda metade; senão corte seco.
- **Rationale**: limite de 40 já existente; nomes de itens mágicos passam de 40.

## R6. Distinção visual

- **Decision**: itens como quadrado arredondado (`rect` com `rx`), seres como círculo.
- **Rationale**: forma é lida mais rápido que cor (cor já codifica o dono); funciona em daltonismo.
