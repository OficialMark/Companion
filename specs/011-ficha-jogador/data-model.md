# Data Model: Ficha pessoal, inventário e rolagens do jogador

Todas as tabelas novas ficam no SQL Server, criadas por uma migration EF Core aditiva. Nenhuma coluna existente é removida nesta feature. Identificadores são `Guid`. Datas em UTC.

## Participant (existente, alterado)

| Campo | Tipo | Regra |
|---|---|---|
| `DisplayName` | string(40) | Passa a ser sempre guardado normalizado (`PersonName.Normalize`, FR-041). |
| `NameKey` | string(40)? **novo** | `DisplayName` normalizado em maiúsculas invariantes. Índice único filtrado `(CampaignId, Role, NameKey) WHERE NameKey IS NOT NULL`. É a identidade da conta (FR-042). |
| `TokenHash` | byte[32] | **Mantido** para compatibilidade, deixa de ser lido pela autorização. Recebe um hash aleatório inutilizável em contas novas. Remoção fica para uma feature futura. |

**Backfill (startup, idempotente).** Para cada participante com `NameKey` nulo: normaliza `DisplayName`; dentro de cada grupo `(CampaignId, Role, chave)`, só o participante mais recente (`CreatedAt`) recebe `NameKey`; os demais ficam com `NameKey` nulo e continuam acessíveis pelas sessões existentes (FR-044). Um participante sem `NameKey` e sem sessão fica inalcançável, mas os dados dele não são apagados.

## ParticipantSession (nova)

Uma linha por aparelho conectado a uma conta (FR-043).

| Campo | Tipo | Regra |
|---|---|---|
| `Id` | Guid | PK |
| `ParticipantId` | Guid | FK → Participant, cascade |
| `CampaignId` | Guid | Desnormalizado para a consulta de autorização |
| `TokenHash` | byte[32] | SHA-256 do token; índice único |
| `CreatedAt` | DateTimeOffset | |
| `LastSeenAt` | DateTimeOffset | Atualizado no máximo 1x por hora (evita escrita por requisição) |

A migration copia `Participant.TokenHash` para uma sessão de cada participante existente, para que nenhum navegador já conectado perca o acesso.

**Autorização.** `ParticipantAuthorization.Require` passa a buscar a sessão pelo hash (índice único, sem varrer a campanha), confere `CampaignId` e carrega o participante.

## CharacterSheet (nova)

Um para um com `Participant` de papel Jogador. PK = `ParticipantId`.

| Campo | Tipo | Regra |
|---|---|---|
| `ParticipantId` | Guid | PK e FK, cascade |
| `CharacterName`, `ClassName`, `Subclass`, `Species`, `Background`, `Alignment` | string(60)? | livres |
| `Level` | int | 1..20 |
| `Experience` | int | 0..999999 |
| `Strength` … `Charisma` | int ×6 | 1..30 (valores-base) |
| `SavingThrowProficiencies` | JSON `AbilityCode[]` | sem repetição |
| `Skills` | JSON `{skill: "none" \| "proficient" \| "expertise"}` | 18 perícias fixas |
| `GeneralProficiencies` | JSON `{armor: ArmorType[], weapons: ("simple"\|"martial")[], shields: bool, other: string(500)}` | |
| `BaseMaxHitPoints` | int | 1..9999 |
| `CurrentHitPoints` | int | 0..máx final (ajustado ao recalcular) |
| `TemporaryHitPoints` | int | 0..9999 |
| `UnarmoredBaseArmorClass` | int | 0..30, padrão 10 |
| `UnarmoredAbility` | AbilityCode? | 2º atributo somado sem armadura (Defesa sem Armadura); nulo = só DES |
| `BaseSpeed` | decimal(5,1) | em metros, padrão 9 |
| `BaseSenses` | JSON `{darkvision?: int, …}` | metros |
| `SpellcastingAbility` | AbilityCode? | |
| `Traits`, `Notes` | string(4000)? | |
| `Version` | rowversion | concorrência otimista (409) |
| `CreatedAt`, `UpdatedAt` | DateTimeOffset | |

## InventoryItem (nova)

| Campo | Tipo | Regra |
|---|---|---|
| `Id` | Guid | PK |
| `ParticipantId` | Guid | FK → CharacterSheet, cascade; índice |
| `Name` | string(80) | obrigatório |
| `Category` | enum `Weapon \| Armor \| Shield \| Gear \| Misc \| Potion` | |
| `Quantity` | int | 0..9999 |
| `Weight` | decimal(7,2)? | kg |
| `Description`, `Notes` | string(4000)? | |
| `Equipped` | bool | só Weapon, Armor, Shield, Gear |
| `Proficient` | bool | só Weapon, Armor, Shield; valor inicial vindo das proficiências gerais (FR-030) |
| `MagicBonus` | int | 0..3 |
| `Weapon` | JSON? `{damageDice: "1d8", damageType, versatileDice?, kind: "melee"\|"ranged", properties: WeaponProperty[], rangeNormal?, rangeLong?, abilityOverride?: AbilityCode}` | obrigatório quando Category=Weapon |
| `Armor` | JSON? `{baseAc, type: "light"\|"medium"\|"heavy", strengthMin?, stealthDisadvantage}` | obrigatório quando Category=Armor |
| `Shield` | JSON? `{acBonus}` (padrão 2) | obrigatório quando Category=Shield |
| `Effects` | JSON `ItemEffect[]` (até 20) | |
| `CatalogItemId` | Guid? | origem; nulo = personalizado |
| `SortOrder` | int | |
| `Version` | rowversion | |

**Invariante de equipamento (FR-017)**: no máximo um `Equipped` com `Category=Armor` e um com `Category=Shield` por ficha. A regra vive em `CharacterSheet.Equip(item)` no domínio: equipar armadura ou escudo desequipa o anterior da mesma categoria na mesma transação.

## ItemEffect (value object, JSON)

| Campo | Valores |
|---|---|
| `type` | `ability`, `maxHp`, `armorClass`, `speed`, `vision`, `resistance`, `immunity`, `condition`, `attackBonus`, `damageBonus`, `saveBonus`, `skillBonus`, `initiative`, `text` |
| `target` | depende do tipo: `FOR…CAR`; `walk\|fly\|swim\|climb`; `darkvision\|blindsight\|tremorsense\|truesight`; tipo de dano; `all\|melee\|ranged\|spell`; perícia; texto livre (até 80) |
| `op` | `add` (soma) ou `min` ("passa a ser pelo menos": vale o maior entre o valor atual e este) |
| `value` | int −30..30 para `add`; 0..300 para `min`; nulo para `resistance`, `immunity`, `condition`, `text` |

**Chave de duplicidade (FR-016)**: `type|target(minúsculo, sem espaços nas pontas)|op|value`. Dois efeitos de itens diferentes com a mesma chave valem uma vez; o primeiro por `SortOrder` vence e o outro é reportado em `stats.suppressedEffects`. Tipos novos entram no enum e no `CharacterRules`, sem código por item.

## CharacterSpell (nova)

| Campo | Tipo | Regra |
|---|---|---|
| `Id`, `ParticipantId` | Guid | cascade |
| `Name` | string(80) | |
| `Level` | int | 0..9 |
| `School`, `CastingTime`, `Range`, `Components`, `Duration` | string(80)? | |
| `Ritual`, `Concentration` | bool | |
| `Description` | string(8000)? | HTML saneado na importação (só `p`, `strong`, `em`, `ul`, `li`, `br`) |
| `AttackKind` | `none \| attack \| save` | |
| `SaveAbility` | AbilityCode? | |
| `DamageDice` | string(40)? | ex. `8d6`, validado pela gramática de dados |
| `DamageType` | string(30)? | |
| `AddAbilityToDamage` | bool | padrão falso |
| `CatalogSpellId` | Guid? | |

## SavedRoll (nova)

| Campo | Tipo | Regra |
|---|---|---|
| `Id`, `ParticipantId` | Guid | cascade |
| `Name` | string(40) | |
| `Icon` | string(40) | um de `SavedRollIcons.All` (lista fechada, ícones lucide) |
| `Kind` | `check \| attack \| damage \| free` | |
| `Dice` | JSON `{d4,d6,d8,d10,d12,d20,d100: int}` | 0..20 cada |
| `Ability` | AbilityCode? | |
| `Skill` / `SavingThrow` | string? / AbilityCode? | exclusivos entre si; só quando `Kind=check` |
| `InventoryItemId` | Guid? | arma; sem FK com cascade (a rolagem fica "incompleta" se o item some, FR-035) |
| `CharacterSpellId` | Guid? | idem |
| `ExtraModifiers` | JSON `{label, value}[]` (até 5) | |
| `Advantage` | `none \| advantage \| disadvantage` | |
| `Repetitions` | int | 1..20 |
| `SortOrder` | int | |

## CatalogItem e CatalogSpell (novas, globais)

Mesmo padrão de `Creature` global: somente leitura para o app, preenchidas pelo `CatalogSeeder` a partir de JSON embutido.

- **CatalogItem**: `Id`, `Slug` (único), `Name`, `NameEn`, `Category`, `Subcategory` (ex. "Arma marcial corpo a corpo"), `Rarity?`, `RequiresAttunement`, `Weight?`, `Cost?`, `Description`, `Weapon?`, `Armor?`, `Shield?`, `Effects` (efeitos mecânicos óbvios mapeados na importação, ex. Anel de Proteção → `armorClass add 1` e `saveBonus all add 1`), `MagicBonus`, `SearchKey`, `IsRemovedFromCatalog`.
- **CatalogSpell**: `Id`, `Slug` (único, = `data-name` da fonte), `Name`, `Level`, `School`, `Ritual`, `CastingTime`, `Range`, `Components`, `Duration`, `Concentration`, `Description`, `HigherLevels?`, `Classes` (JSON), `SuggestedDamageDice?`, `SuggestedDamageType?`, `AttackKind`, `SaveAbility?`, `SearchKey`, `IsRemovedFromCatalog`.
- `SeedVersion` (existente) ganha as chaves `items.srd52` e `spells.magias5e`.

## DiceRoll (existente, alterado)

- `Expression`: `HasMaxLength(128)` → `256` (expressões com rótulos ficam mais longas). Alargar é seguro e não perde dados.
- `ValuesJson`: o formato ganha campos opcionais (ver `contracts/dice-expression.md`); rolagens antigas continuam legíveis.

## Valores calculados (não persistidos): `CharacterStats`

Saída de `CharacterRules.Compute(sheet, items, spells)`, devolvida pela API em toda leitura e escrita da ficha. Inclui, para cada valor, a composição `{label, value, source}`:

`abilities[6] {base, final, modifier, parts}`, `proficiencyBonus`, `savingThrows[6]`, `skills[18]`, `passivePerception`, `initiative`, `armorClass`, `maxHitPoints`, `speed {walk, fly?, swim?, climb?}`, `senses`, `resistances`, `immunities`, `conditions`, `textEffects`, `spellSaveDc?`, `spellAttackBonus?`, `weaponRolls[] {itemId, attack: parts[], damage: {dice, parts[]}, versatile?}`, `spellRolls[] {spellId, attack?: parts[], saveDc?, damage?: {dice, parts[]}}`, `armorWarnings[]`, `suppressedEffects[] {itemId, effectKey}`, `rollDefaults {disadvantageOnStrDex: bool}`.

`parts` é o que o frontend coloca na expressão como `(rótulo) valor`. O frontend nunca recalcula regras: só junta dados escolhidos + `parts` + vantagem + repetição.

## Transições de estado

- **Ficha**: inexistente → criada no primeiro `GET` do dono → editada (versão incrementa) → apagada em cascata com o participante.
- **Item**: na mochila ↔ equipado. Equipar armadura ou escudo desequipa o anterior da mesma categoria.
- **Rolagem salva**: completa ↔ incompleta (item ou magia referenciados ausentes; volta a completa ao ser editada).
