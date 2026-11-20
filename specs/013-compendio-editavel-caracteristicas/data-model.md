# Data model: spec 013

## CatalogItem / CatalogSpell (alterados)

| Campo | Tipo | Regra |
|---|---|---|
| `CampaignId` | Guid? | nulo = catálogo global (seed). FK `Campaigns` em cascata |
| `BaseId` | Guid? | só em linhas de campanha: a global que esta substitui. FK para a mesma tabela, NoAction |
| `Slug` | string | global: do seed (único entre globais). Campanha: `campanha-{id}` |

Origem derivada: `CampaignId` nulo → `Compendium`; com `BaseId` → `Edited`; sem `BaseId` → `Custom`.

Índices: `Slug` único filtrado `[CampaignId] IS NULL`; `(CampaignId, BaseId)` único filtrado; `(CampaignId)`.

Visão de uma campanha C: `CampaignId == C` ∪ (`CampaignId == null` ∧ `!IsRemovedFromCatalog` ∧ `Id ∉ BaseId das linhas de C`).

Seeder: lê e marca como removidas só linhas com `CampaignId == null`.

Exclusão de campanha: apaga as linhas de catálogo da campanha (explícito em `DeleteCampaignAggregateAsync`).

## CharacterFeature (nova, tabela `CharacterFeatures`)

| Campo | Tipo | Regra |
|---|---|---|
| `Id` | Guid | |
| `ParticipantId` | Guid | chave da ficha; FK `CharacterSheets` em cascata |
| `SortOrder` | int | ordem na lista |
| `Data` | JSON `CharacterFeatureData` | validado no domínio |
| `CreatedAt` | DateTimeOffset | |

`CharacterFeatureData(Name, Source, Description, MaxUses?, CurrentUses, Recovery)`:

- `Name`: 1..80, obrigatório. `Source`: até 60. `Description`: até 8000.
- `MaxUses`: nulo (sem contador) ou 1..999. `CurrentUses`: 0..MaxUses (sem contador: 0).
- `Recovery`: `None`, `ShortRest`, `LongRest`. Sem contador: forçado `None`.
- Máximo 100 por ficha.

Transições:

- `Spend(delta)`: `Current = clamp(Current + delta, 0, Max)`.
- `Restore()`: `Current = Max`.
- Descanso curto: restaura `ShortRest`. Descanso longo: restaura `ShortRest` e `LongRest`.
- Criar com contador: `Current = Max`. Editar: `Current = min(Current, Max)`; se o contador acabou de ser ligado, `Current = Max`.
