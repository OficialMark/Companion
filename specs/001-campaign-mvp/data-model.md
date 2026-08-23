# Data Model: MVP de Campanha D&D

## Campaign

| Field | Type | Rules |
|-------|------|-------|
| Id | unique identifier | Primary key |
| Code | string(8) | Required, uppercase, unique, excludes ambiguous characters |
| Name | string(80) | Required, trimmed |
| CreatedAt | UTC timestamp | Immutable |

Relationships: has many Participants, DiceRolls and Encounters. At most one Encounter may be active.

## Participant

| Field | Type | Rules |
|-------|------|-------|
| Id | unique identifier | Primary key |
| CampaignId | unique identifier | Required foreign key |
| DisplayName | string(40) | Required, trimmed, unique case-insensitively within campaign |
| Role | enum | `Master` or `Player` |
| TokenHash | binary(32) | Required, unique; raw token is never stored |
| CreatedAt | UTC timestamp | Immutable |

Presence is not persisted. It is derived from active Hub connections.

## DiceRoll

| Field | Type | Rules |
|-------|------|-------|
| Id | unique identifier | Primary key and event identifier |
| CampaignId | unique identifier | Required foreign key |
| ParticipantId | unique identifier | Required foreign key |
| ClientRequestId | unique identifier | Required; unique with ParticipantId |
| Expression | string(128) | Normalized compound form with optional `R#` prefix |
| ValuesJson | string(max) | JSON array of repetitions, dice terms, individual values, modifier and total |
| Modifier | integer | -10000 through 10000 |
| Total | integer | Checked sum |
| CreatedAt | UTC timestamp | Immutable |

DiceRoll is append-only. A row represents one submitted test; `R#` repetitions remain grouped in that row and each has its own total. Existing flat value arrays remain readable for backward compatibility. The API returns the 50 newest tests per campaign.

## Encounter

| Field | Type | Rules |
|-------|------|-------|
| Id | unique identifier | Primary key |
| CampaignId | unique identifier | Required foreign key |
| Status | enum | `Preparing`, `Active`, `Ended` |
| Round | integer | 0 while preparing, starts at 1 |
| CurrentTurnIndex | integer/null | Null while preparing or ended |
| CreatedAt | UTC timestamp | Immutable |
| EndedAt | UTC timestamp/null | Set when ended |
| Version | rowversion | Optimistic concurrency token |

Transitions:

```text
Preparing --start with combatants/initiative--> Active --end--> Ended
Preparing ------------------------------end------------------> Ended
```

## Combatant

| Field | Type | Rules |
|-------|------|-------|
| Id | unique identifier | Primary key |
| EncounterId | unique identifier | Required foreign key |
| Name | string(60) | Required, trimmed |
| InitiativeModifier | integer | -100 through 100 |
| Initiative | integer/null | Server roll plus modifier |
| CreationOrder | integer | Required, unique within encounter |

Ordering is Initiative descending, then CreationOrder ascending. The stored collection does not require a separate mutable sort index.
An initiative result may be replaced while an encounter is preparing or active. When a reroll changes ordering during combat, `CurrentTurnIndex` is recalculated so the same combatant remains active.

## Realtime Event Envelope

| Field | Type | Rules |
|-------|------|-------|
| EventId | unique identifier | Used for client deduplication |
| CampaignId | unique identifier | Group routing and validation |
| Type | string | Stable event name |
| OccurredAt | UTC timestamp | Server generated |
| Payload | object | Type-specific snapshot or resource |
