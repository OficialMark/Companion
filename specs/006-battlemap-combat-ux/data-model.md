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
- `BattlemapTokenDto`: + `FootprintSize` (int, default 1), `StatusConditions` (string[]), `IsMarker` (bool, default false), `OwnerParticipantId` (Guid?, preenchido apenas para marcadores, para autorização no cliente) — **sem** campos de HP (ver nota abaixo)
- Novo `SetTokenAppearanceRequest(int FootprintSize, IReadOnlyList<string> StatusConditions)`
- Novo `CreateMarkerRequest(string Label)`
- Novo `BattlemapMarkerDto` — na prática representado como mais um `BattlemapTokenDto` com `IsMarker = true`, para que o frontend trate participantes/criaturas/marcadores de forma uniforme (mesma lista `battlemap.tokens`)

## Resolução de "vida do token" — client-side, não no DTO do battlemap

**Decisão revisada (achado da análise pré-implementação)**: `BattlemapTokenDto` NÃO carrega HP. `Combatant.UpdateHitPoints`/`ApplyDamage` disparam `EncounterInvalidatedAsync` (refetch do encontro), não `BattlemapInvalidatedAsync` (refetch do battlemap) — se a vida fosse duplicada em `BattlemapTokenDto`, ela ficaria desatualizada até a próxima mutação *do battlemap* (mover, área, aparência), violando FR-007/SC-003 (atualização em até 3s).

Em vez disso, o frontend resolve a vida de um token com o mesmo tipo de junção local já usado para o destaque de turno (US1):

1. Buscar, em `store.snapshot.activeEncounter.combatants`, o combatente ligado a esse token (`participantId` para pino de jogador, ou `id` do combatente para criatura — mesma semântica de `participantId` do token usada em `research.md`). Se encontrado, usar `combatant.currentHitPoints`/`maxHitPoints`.
2. Caso contrário (jogador ainda sem `Combatant` no encontro atual), buscar em `store.snapshot.participants` o participante correspondente e usar `participant.currentHitPoints`/`maxHitPoints` como baseline.
3. Marcadores nunca casam com nenhum combatente/participante — resolução retorna "sem vida", barra de vida não exibida (FR-009), sem regra especial de exclusão necessária.

Como `activeEncounter.combatants` e `participants` já são sincronizados em tempo real por eventos existentes (`EncounterInvalidated`, `ParticipantsChanged`), a barra de vida se mantém atualizada automaticamente, sem exigir nenhuma mudança de contrato do battlemap.

## Regras de autorização (reaproveitando `ResolveTokenForMutation`)

| Ação | Quem pode |
|---|---|
| Mover pino de personagem | Dono do participante, ou mestre |
| Mover/configurar criatura | Mestre |
| Criar marcador | Qualquer jogador (para si mesmo), ou mestre |
| Mover/configurar/remover marcador | `OwnerParticipantId` do marcador, ou mestre |
| Definir tamanho/condições de qualquer token | Mesma regra de quem pode configurar a área daquele token (FR-011, FR-013, FR-018, FR-020) |
