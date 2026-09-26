# Data Model: Aprimoramentos de Combate no Battlemap

## Entidades alteradas

### `Participant` (existente)

| Campo | Tipo | Regra |
|---|---|---|
| `MaxHitPoints` (novo) | `int?` | 0–99_999 quando informado (mesmo intervalo de `CurrentHitPoints`); definido pelo próprio jogador na tela inicial/configuração via `UpdateCombatProfileAsync` |

### `Combatant` (existente)

| Campo | Tipo | Regra |
|---|---|---|
| `MaxHitPoints` (novo) | `int?` | 0–99_999 quando informado; copiado de `Participant.MaxHitPoints` em `RollInitiativeAsync` para combatentes ligados a um participante; definido diretamente pelo mestre em `AddCombatantAsync`/`UpdateCombatantAsync` para criaturas/NPCs |

Validação: se `CurrentHitPoints` e `MaxHitPoints` estão ambos definidos, nenhuma ordem é imposta entre eles (uma cura acima do máximo é permitida no dado bruto; a barra de vida apenas satura visualmente em 100%, por FR-008).

## Entidades novas

### `StatusCondition` (enum)

`Stunned` (Atordoado) · `Restrained` (Imobilizado) · `Blinded` (Cego) · `Poisoned` (Envenenado) · `Invisible` (Invisível) · `Prone` (Caído)

### `BattlemapTokenAppearance`

Uma linha opcional por token (ausência = tamanho 1x1, sem condições), mesma forma de chaveamento dual/triplo de `AttackArea`/`BattlemapTokenPosition`.

| Campo | Tipo | Regra |
|---|---|---|
| `Id` | `Guid` | PK |
| `BattlemapId` | `Guid` | FK para `Battlemap` |
| `ParticipantId` | `Guid?` | Preenchido quando o token é o pino de um jogador |
| `CombatantId` | `Guid?` | Preenchido quando o token é uma criatura/NPC (`Combatant.ParticipantId == null`) |
| `MarkerId` | `Guid?` | Preenchido quando o token é um marcador representativo |
| `FootprintSize` | `int` | 1–5 (FR-011); exatamente um de `ParticipantId`/`CombatantId`/`MarkerId` deve estar preenchido |
| `StatusConditions` | `IReadOnlyList<StatusCondition>` | Zero ou mais valores, sem duplicatas; persistido como coluna de texto delimitado |
| `Version` | `byte[]` | Concorrência otimista, mesmo padrão das demais entidades por-token |

Índice único em `(BattlemapId, ParticipantId)`, `(BattlemapId, CombatantId)` e `(BattlemapId, MarkerId)` (parciais, análogos ao índice único de `AttackArea` citado no research).

### `BattlemapMarker`

| Campo | Tipo | Regra |
|---|---|---|
| `Id` | `Guid` | PK |
| `BattlemapId` | `Guid` | FK para `Battlemap` |
| `OwnerParticipantId` | `Guid` | Participante (jogador) dono do marcador; só ele ou o mestre podem mover/configurar/remover |
| `Label` | `string` | 1–40 caracteres (mesmo limite de `Participant.DisplayName`); usado para iniciais e identificação |
| `ColorKey` | `int` | Calculado como `DtoMapper.ColorKey(marker.Id)`, mesma função já usada para participantes/criaturas |
| `CreatedAt` | `DateTimeOffset` | — |

Ao ser removido: `BattlemapTokenPosition`, `AttackArea` e `BattlemapTokenAppearance` associados ao mesmo `MarkerId` são removidos em cascata (FR-019, edge case de condições órfãs).

## Entidades existentes reaproveitadas (extensão de chave)

### `BattlemapTokenPosition` (existente)

Adiciona `MarkerId` (`Guid?`) ao lado de `ParticipantId`/`CombatantId` já existentes; exatamente um dos três é preenchido por linha.

### `AttackArea` (existente)

Adiciona `MarkerId` (`Guid?`) da mesma forma, para permitir que um marcador também tenha área de alcance configurada (FR-018, User Story 5).

## DTOs alterados (`Companion.Application/Contracts/Dtos.cs`)

- `ParticipantDto`: + `MaxHitPoints` (int?)
- `CombatantDto`: + `MaxHitPoints` (int?); `CurrentHitPoints` deixa de ser condicional ao observador (FR-009a)
- `BattlemapTokenDto`: + `CurrentHitPoints` (int?), `MaxHitPoints` (int?), `FootprintSize` (int, default 1), `StatusConditions` (string[]), `IsMarker` (bool, default false), `OwnerParticipantId` (Guid?, preenchido apenas para marcadores, para autorização no cliente)
- Novo `SetTokenAppearanceRequest(int FootprintSize, IReadOnlyList<string> StatusConditions)`
- Novo `CreateMarkerRequest(string Label)`
- Novo `BattlemapMarkerDto` — na prática representado como mais um `BattlemapTokenDto` com `IsMarker = true`, para que o frontend trate participantes/criaturas/marcadores de forma uniforme (mesma lista `battlemap.tokens`)

## Resolução de "vida do token" em `BattlemapService.ToSnapshot`

Para cada token na lista final:

1. Se existe um `Combatant` no encontro atual ligado a esse token (via `ParticipantId` para pinos de jogador, ou o próprio `Combatant` para criaturas), usar `Combatant.CurrentHitPoints`/`MaxHitPoints`.
2. Caso contrário (jogador ainda não entrou na iniciativa deste encontro), usar `Participant.CurrentHitPoints`/`MaxHitPoints` como baseline.
3. Marcadores nunca têm vida (campos `null`, barra de vida não exibida — FR-009).

## Regras de autorização (reaproveitando `ResolveTokenForMutation`)

| Ação | Quem pode |
|---|---|
| Mover pino de personagem | Dono do participante, ou mestre |
| Mover/configurar criatura | Mestre |
| Criar marcador | Qualquer jogador (para si mesmo), ou mestre |
| Mover/configurar/remover marcador | `OwnerParticipantId` do marcador, ou mestre |
| Definir tamanho/condições de qualquer token | Mesma regra de quem pode configurar a área daquele token (FR-011, FR-013, FR-018, FR-020) |
