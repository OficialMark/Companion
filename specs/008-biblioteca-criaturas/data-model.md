# Data Model: spec 008

## Creature (nova tabela `Creatures`)

| Campo | Tipo | Regra |
|---|---|---|
| Id | Guid | PK |
| CampaignId | Guid? | null = compêndio global; FK Campaign (cascade) |
| BaseCreatureId | Guid? | versão da mesa derivada de uma criatura do compêndio; único por (CampaignId, BaseCreatureId) |
| Slug | string(120)? | único entre as globais (filtro CampaignId IS NULL) |
| Name | string(120) | obrigatório, 1-120 |
| NameEn | string(120)? | |
| SearchKey | string(260) | nome PT + EN em minúsculas e sem acento; índice |
| SourceBook | string(40) | "SRD 5.1", "SRD 5.2", "Monster a Day", "Mesa" |
| SourceUrl | string(300)? | |
| Size | enum CreatureSize | Tiny, Small, Medium, Large, Huge, Gargantuan |
| Type | string(80) | |
| Alignment | string(80)? | |
| ArmorClass | int | 0-50 |
| ArmorClassNote | string(120)? | |
| HitPoints | int | 1-9999 |
| HitDice | string(40)? | |
| Speed | string(200)? | |
| Initiative | int? | -20..+30 |
| Strength..Charisma | int | 1-30 cada |
| SavingThrows, Skills, DamageVulnerabilities, DamageResistances, DamageImmunities, ConditionImmunities, Senses, Languages | string(500)? | |
| ChallengeRating | string(10) | "0", "1/8", "1/4", "1/2", "1".."30" |
| ChallengeValue | decimal(5,3) | derivado do ND, para ordenar e filtrar |
| ExperiencePoints | int? | |
| ProficiencyBonus | int? | |
| Traits, Actions, BonusActions, Reactions, LegendaryActions | JSON `[{name, description}]` (nvarchar(max)) | até 40 itens por lista, nome ≤ 120, descrição ≤ 4000 |
| LegendaryActionsIntro | string(2000)? | |
| Notes | string(4000)? | só em criaturas da mesa |
| ImageSourceUrl | string(400)? | origem remota (compêndio) |
| ImageRevision | Guid? | muda a cada upload ou fetch bem-sucedido |
| ImageFetchFailedAt | DateTimeOffset? | backoff de 24 h |
| IsRemovedFromCompendium | bool | o seed marca em vez de apagar (combatentes podem apontar para ela) |
| CreatedAt, UpdatedAt | DateTimeOffset | UTC |
| Version | rowversion | concorrência otimista na edição |

Derivados: `Modifier(score) = floor((score - 10) / 2)`. `FootprintSize`: Large 2, Huge 3, Gargantuan 4, demais 1.

Operações: `Creature.CreateCampaignCopy(campaignId, base)`, `Update(fields)`, `SetImage(revision)`, `SetNotes(text)`.

## CreatureImage (nova tabela `CreatureImages`)

| Campo | Tipo |
|---|---|
| CreatureId | Guid PK/FK (cascade) |
| Content | varbinary(max) ≤ 1 MB |
| MediaType | string(32) |
| Revision | Guid |
| FromSource | bool |

## SeedVersion (nova tabela `SeedVersions`)

`Key` string(80) PK, `Hash` string(64), `AppliedAt`.

## Alterações

- `Combatant.CreatureId` Guid? (FK NoAction para Creature).
- `BattlemapTokenAppearance`: `LastRollCount` int?, `LastRollDie` int?, `LastRollAbility` string(3)? (`str|dex|con|int|wis|cha`), `LastRollBonus` int?. Regras: count 1-20, die ∈ {4,6,8,10,12,20,100}, bonus -20..20.
- `DiceRoll.Label` string(80)?.
- `Battlemap`: sem mudança de esquema; `Configure` aceita a máscara inicial.

## DTOs

- `CreatureSummaryDto(Id, Name, NameEn, Size, Type, ChallengeRating, ArmorClass, HitPoints, Origin: "Compendium"|"Edited"|"Custom", ImageUrl?)`.
- `CreatureDto` = todos os campos + `Origin`, `BaseCreatureId`, `Notes`, `ImageUrl`, `Revision`.
- `CreaturePageDto(Items, Total, Page, PageSize)`.
- `SaveCreatureRequest` (campos editáveis + `ExpectedRevision`).
- `AddCreaturesToEncounterRequest(CreatureId, Count)`.
- `BattlemapTokenDto` + `ImageUrl?`, `CreatureId?`, `RollPreset?(Count, Die, Ability, Bonus)`.
- `RollDiceRequest` + `Label?`; `DiceRollDto` + `Label?`.
- `CombatantDto` + `CreatureId?`.
