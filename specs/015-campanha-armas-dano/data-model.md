# Data Model: Ataques Tipados e Caderno da Campanha

## WeaponDamageType

Represents a stable damage category. Seed values include bludgeoning, piercing, slashing, acid, cold, fire, force, lightning, necrotic, poison, psychic, radiant, and thunder, with Portuguese labels. Custom text is normalized and bounded.

## WeaponData (extended)

- `DamageDice`: existing base dice expression.
- `DamageType`: existing nullable primary type, retained for legacy JSON and call sites.
- `DamageTypes`: optional normalized list of available damage types; if absent, a non-empty legacy `DamageType` is promoted to a one-item list. If both exist, the legacy value is retained as the first option and duplicates are removed.
- Other range, mastery, property, versatile-dice, and ability fields remain unchanged.

## DamageComponent

- `Label`: optional short name such as "Lâmina" or "Encantamento".
- `Dice`: bounded dice-count mapping for one component.
- `DamageType`: one selected type for this roll.
- `Source`: optional reference to a weapon or effect. It is descriptive and does not cause client-side rule calculations.

## SavedRollData (extended)

- Existing `Kind`, base `Dice`, and source references remain valid.
- `DamageComponents`: optional list of additional typed components; defaults to empty for legacy saved rolls.
- For weapon attacks, base damage uses the referenced item dice and one selected damage type from its available type list.
- Components roll independently; their subtotals and types are retained in the roll result, with an aggregate total for the existing damage application flow.
- Limits: up to 8 extra components, up to 20 dice per die key per component, labels up to 40 characters, type values up to 30 characters.

## CampaignKnowledgeEntry

- `Id`: opaque UUID.
- `CampaignId`: owning campaign.
- `Kind`: `Npc`, `Map`, `Clue`, or `Journal`.
- `Title`: required, 1–120 characters.
- `Summary`: optional, up to 500 characters.
- `SharedDescription`: optional, up to 12,000 characters.
- `IsPlayerVisible`: visibility switch for NPC, map, and clue; diary entries remain shared.
- `CreatedByParticipantId`, `CreatedAt`, `UpdatedAt`: author and timestamps.
- `Version`: SQL Server rowversion for optimistic concurrency.
- Map-only fields: image bytes, validated media type and safe file name; maps with no image may be drafts.
- A campaign entry is visible to a player only when `IsPlayerVisible` is true or its kind is Journal. A player never receives private-note fields.

## CampaignKnowledgeContribution

- `Id`, `EntryId`: opaque identity and parent.
- `Body`: required shared contribution, up to 8,000 characters.
- `CreatedByParticipantId`, `CreatedAt`, `UpdatedAt`: attribution.
- Participants may add contributions and edit their own; the master can edit/remove any contribution. Contributions are returned only if their parent is visible to that role.

## CampaignKnowledgeMasterNote

- `EntryId`: unique parent key.
- `Body`: private master notes, up to 12,000 characters.
- `UpdatedByParticipantId`, `UpdatedAt`, `Version`: attribution and concurrency.
- This entity is loaded only after master authorization and never mapped onto a player DTO.

## CampaignMapPoint

- `Id`, `MapEntryId`: opaque identity and required map parent.
- `Title`: required, up to 100 characters.
- `Description`: optional, up to 2,000 characters.
- `X`, `Y`: normalized decimal coordinates in `[0,1]` relative to the original image.
- `CreatedByParticipantId`, `CreatedAt`, `UpdatedAt`: attribution.
- Point visibility inherits the parent map. A map cannot be deleted while points remain unless they are deleted in the same transaction.

## Validation and relationships

- `Campaign` 1:N `CampaignKnowledgeEntry`; deleting a campaign cascades to all entries and related data.
- Entry 1:N `CampaignKnowledgeContribution`; Entry 0:1 `CampaignKnowledgeMasterNote`; Map entry 1:N `CampaignMapPoint`.
- Only `Map` entries accept images or points. Only NPC, map, and clue can be hidden. Journal must stay visible.
- All IDs, authors, parent IDs, limits, enum values, and visibility rules are validated by the application/domain. Every operation revalidates that the acting participant belongs to the campaign; only a master may change privacy, master notes, or delete any content.
- Reads project by role before serialization. Player projections never contain notes or hidden records, including in list totals, search results, and realtime event payloads.
- Mutations persist before emitting generic `CampaignNotebookInvalidated` notifications. On a rowversion conflict, return HTTP 409 with enough information for the client to refresh and preserve the latest committed state.
