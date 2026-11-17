# Data Model: spec 007

## StatusCondition (enum, alterado)

Valores existentes (mantidos, mesma ordem): `Stunned, Restrained, Blinded, Poisoned, Invisible, Prone`.
Novos (anexados): `Paralyzed, Bleeding, Grappled, Deafened, Frightened, Charmed, Unconscious, Petrified, Incapacitated, Exhausted, Concentrating, Burning`.

Persistido em `BattlemapTokenAppearances.StatusConditions` como CSV de nomes, `nvarchar(512)` (antes 128). Sem migração de dados.

| Valor | Rótulo | Ícone (lucide) | Cor |
|---|---|---|---|
| Prone | Caído | arrow-down-to-line | #d9a35f |
| Stunned | Atordoado | sparkles | #f2d16b |
| Paralyzed | Paralisado | zap | #ffe066 |
| Poisoned | Envenenado | biohazard | #7fd858 |
| Bleeding | Sangrando | droplets | #ff5a6e |
| Restrained | Imobilizado | link | #b9a7ff |
| Grappled | Agarrado | grab | #c79bff |
| Blinded | Cego | eye-off | #a3b1c6 |
| Deafened | Surdo | ear-off | #8fa3b8 |
| Invisible | Invisível | ghost | #cfe8ff |
| Frightened | Amedrontado | frown | #b98cff |
| Charmed | Enfeitiçado | heart-handshake | #ff8fcf |
| Unconscious | Inconsciente | bed | #7f9cff |
| Petrified | Petrificado | mountain | #b7a99a |
| Incapacitated | Incapacitado | ban | #ff9f6e |
| Exhausted | Exausto | battery-low | #e6a15c |
| Concentrating | Concentrando | brain | #5ce1c4 |
| Burning | Em chamas | flame | #ff7a3d |

Ordem de exibição no menu: a da tabela (mais comuns primeiro). Ordem no token: a ordem em que foram marcadas.

## Combatant (alterado)

| Campo | Tipo | Regra |
|---|---|---|
| IsHitPointsHidden | bool, default false | Só pode ser `true` para criaturas (`ParticipantId == null`); `SetHitPointsHidden(true)` num personagem lança `InvalidOperationException`. |

## Battlemap (alterado)

| Campo | Tipo | Regra |
|---|---|---|
| FogMask | byte[]? (`varbinary(1250)`) | `null` = tudo revelado. Quando presente, `Length == ceil(Columns*Rows/8)`; bit `i` (índice `row*Columns+column`, bit `i%8` do byte `i/8`) = célula coberta. Uma máscara toda zero é normalizada para `null`. |

Operações de domínio:

- `SetFog(byte[]? mask)`: valida tamanho; bits além de `Columns*Rows` devem ser zero (são zerados); normaliza zeros para `null`.
- `IsCellFogged(column, row)`: falso fora do grid ou sem máscara.
- `IsAreaFogged(column, row, footprint)`: verdadeiro se todas as células da área recortada ao grid estão cobertas (e há ao menos uma).
- `Replace(...)`: se `Columns` ou `Rows` mudarem, `FogMask = null`.

## DTOs

- `CombatantDto` + `bool IsHitPointsHidden = false` (enviado a todos; para jogadores, quando `true`, `CurrentHitPoints` e `MaxHitPoints` vêm `null`).
- `BattlemapSnapshotDto` + `string? FogMask` (base64, `null` = revelado). `IsFogged` é derivável no cliente.
- `BattlemapTokenDto` sem mudança (tokens cobertos simplesmente não vêm para jogadores).
- `SetFogRequest(string? Mask)`.
- `SetHitPointsVisibilityRequest(bool Hidden)`.

## Regras de visibilidade (por observador)

| Observador | HP de personagem | HP de criatura visível | HP de criatura oculta | Criatura/marcador do mestre sob névoa total | Token de jogador sob névoa |
|---|---|---|---|---|---|
| Mestre | vê | vê | vê + indicador | vê (esmaecido) | vê |
| Jogador | vê | vê | não recebe (só `IsDefeated`) | não recebe (token e área) | vê |

"Marcador do mestre" = `BattlemapMarker.OwnerParticipantId` pertence a um participante com papel Master.
