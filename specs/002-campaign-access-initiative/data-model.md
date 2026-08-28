# Data Model: Acesso, Administração e Iniciativa da Campanha

## Campaign

| Field | Type | Rules |
|-------|------|-------|
| Id | opaque identifier | Primary identity |
| Name | text | 1–80 characters |
| CreatedAt | timestamp | UTC, immutable |

Owns exactly two access codes, participants, rolls and encounters. Deletion removes the aggregate.

## CampaignAccessCode

| Field | Type | Rules |
|-------|------|-------|
| Code | text | Four ASCII digits; globally unique; leading zero allowed |
| CampaignId | opaque identifier | Required campaign |
| Role | enum | `Player` or `Master`; immutable |

Unique `(CampaignId, Role)` ensures one code per role. Codes are deleted with the campaign and reusable only after commit.

## Participant

| Field | Type | Rules |
|-------|------|-------|
| Id | opaque identifier | Stable within campaign |
| CampaignId | opaque identifier | Required |
| DisplayName | text | 1–40 characters; unique in campaign |
| Role | enum | Derived from access code |
| TokenHash | bytes | Unique credential hash |
| InitiativeModifier | integer/null | Explicit range -100..100; null means unset |
| CreatedAt | timestamp | UTC |

Only the authenticated participant may replace the stored modifier.

## Encounter Combatant

| Field | Type | Rules |
|-------|------|-------|
| Id | opaque identifier | Primary identity |
| EncounterId | opaque identifier | Required |
| ParticipantId | opaque identifier/null | Imported player; unique per encounter when present |
| Name | text | 1–60 characters |
| InitiativeModifier | integer | -100..100; copied at import |
| Initiative | integer/null | Server d20 plus modifier |
| CreationOrder | integer | Stable tiebreaker |

Manual combatants keep `ParticipantId = null`. Import adds only missing online players in preparation. Master edits preserve the link and are not overwritten by rerolls. Active edits/removals recalculate turn position.

## Administrative Audit Event

Structured operational event with event type, optional campaign identifier, UTC time and trace identifier. It never contains codes, participant tokens or the administrative credential.
