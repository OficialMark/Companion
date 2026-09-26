# Data Model: Exibição de Área de Ataque no Battlemap

## AttackArea (nova entidade de domínio)

Representa a área de ataque/efeito desenhada sobre o battlemap para um token.

| Campo | Tipo | Regras |
|---|---|---|
| `Id` | Guid | Identificador único. |
| `BattlemapId` | Guid | Battlemap ao qual a área pertence (FK). |
| `CombatantId` | Guid? | Combatente de origem, quando associado a um combatente da iniciativa. |
| `ParticipantId` | Guid? | Participante dono do token de origem (herdado do token/posição), usado para autorização. |
| `Shape` | enum `AttackAreaShape` { `Circle`, `Square`, `Cone`, `Cylinder` } | Obrigatório. |
| `Radius` | int (blocos) | Obrigatório. 1–`Battlemap.MaxGridSize` (100). Para `Circle`/`Square`: raio/meio-lado. Para `Cone`/`Cylinder`: distância/alcance. |
| `Width` | int? (blocos) | Obrigatório somente para `Cone`/`Cylinder` (1–100); `null`/ignorado para `Circle`/`Square`. |
| `DirectionDegrees` | int? | Obrigatório somente para `Cone`/`Cylinder` (0–359); `null` para `Circle`/`Square`. |
| `CreatedByParticipantId` | Guid | Quem criou/editou por último (participante ou mestre) — para auditoria simples, não exibido na UI. |
| `CreatedAt` / `UpdatedAt` | DateTimeOffset | Timestamps padrão. |
| `Version` | byte[] | Concorrência otimista (mesmo padrão de `Battlemap.Version`/`BattlemapTokenPosition.Version`). |

**Invariantes/validações** (no construtor/métodos de domínio, espelhando `BattlemapTokenPosition`/`Battlemap`):
- `Radius` deve estar entre 1 e `Battlemap.MaxGridSize`.
- Se `Shape` for `Cone` ou `Cylinder`, `Width` e `DirectionDegrees` são obrigatórios e validados (`Width` 1–100, `DirectionDegrees` 0–359).
- Se `Shape` for `Circle` ou `Square`, `Width`/`DirectionDegrees` devem ser nulos (não fazem sentido nesses formatos).
- Exatamente um token de origem (`CombatantId` ou posição equivalente) por área; no máximo uma `AttackArea` ativa por token de origem (nova configuração substitui a anterior — ver Assumptions do spec).

**Relacionamentos**:
- `Battlemap` (1) → `AttackArea` (0..N), mas restrito a no máximo 1 por token de origem.
- `Combatant`/token de origem (1) → `AttackArea` (0..1).

**Ciclo de vida** (regras de exclusão em cascata, ver research.md #6):
- Remover o combatente/token de origem remove a `AttackArea` associada.
- Encerrar o encontro (`Encounter.Status` → `Ended`) ou substituir o `Battlemap` (`Battlemap.Replace(...)`) remove todas as `AttackArea` daquele battlemap.
- Remoção explícita pelo dono do token ou pelo mestre.

## Entidades existentes reaproveitadas (sem alteração de schema)

- **Battlemap**: dono lógico das áreas (`BattlemapId`); fornece `Columns`/`Rows`/`MaxGridSize` como limites de validação.
- **BattlemapTokenPosition** / **Combatant**: fonte da posição de origem (`Column`/`Row`) usada para desenhar a área; fonte do `ParticipantId` usado na autorização.
- **Participant/Session** (`store.session`, backend de autorização por participante): usado para checar `isMaster` vs. dono do token, tanto no frontend (UX) quanto no backend (autoridade real).

## DTOs de transporte (API/SignalR)

- `AttackAreaDto`: `{ id, battlemapId, combatantId, shape, radius, width?, directionDegrees?, updatedAt }` — retornado no GET do snapshot do battlemap (lista) e enviado no evento SignalR de criação/atualização/remoção.
- Requisição de criação/atualização: `{ shape, radius, width?, directionDegrees? }` (o `tokenId`/`combatantId` vem da rota, `battlemapId` do contexto da campanha).
