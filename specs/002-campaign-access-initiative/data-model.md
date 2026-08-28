# Data Model: Vida e Ataques em Combate

## Participant

| Field | Type | Rules |
|-------|------|-------|
| Id | opaque identifier | Stable within campaign |
| CampaignId | opaque identifier | Required |
| DisplayName | text | 1–40 characters |
| Role | enum | `Player` or `Master` |
| InitiativeModifier | integer/null | -100..100; null means unset |
| CurrentHitPoints | integer/null | 0..99,999; only players edit their own value |
| CreatedAt | timestamp | UTC |

`CurrentHitPoints` is a preparation preference. It is visible to the owning player and copied into an encounter; it is not updated by encounter damage.

## Encounter Combatant

| Field | Type | Rules |
|-------|------|-------|
| Id | opaque identifier | Primary identity |
| EncounterId | opaque identifier | Required |
| ParticipantId | opaque identifier/null | Unique per encounter when imported |
| Name | text | 1–60 characters |
| InitiativeModifier | integer | -100..100 |
| Initiative | integer/null | Server d20 plus modifier |
| CurrentHitPoints | integer/null | 0..99,999; required before encounter starts |
| CreationOrder | integer | Stable tiebreaker |

### State transitions

- Preparation import: copy participant modifier (or zero) and `CurrentHitPoints` (possibly null).
- Preparation edit: master may replace name, modifier and hit points.
- Start: rejected if any initiative or hit points is null.
- Active direct edit: master may edit any hit points; linked player may edit only their own.
- Attack: replace hit points with `max(0, current - max(0, roll total))`.

## CombatAttack

| Field | Type | Rules |
|-------|------|-------|
| Id | opaque identifier | Primary identity |
| EncounterId | opaque identifier | Active encounter |
| AuthorParticipantId | opaque identifier | Master or player linked to a combatant |
| DiceRollId | opaque identifier | Same campaign/author, revealed, one result |
| ClientRequestId | opaque identifier | Unique with author; retry key |
| Damage | integer | `max(0, dice roll total)` |
| CreatedAt | timestamp | UTC |

One attack owns one or more targets. A dice roll can resolve at most one attack.

## CombatAttackTarget

| Field | Type | Rules |
|-------|------|-------|
| AttackId | opaque identifier | Required attack |
| CombatantId | opaque identifier | Required distinct target in same encounter |
| DamageApplied | integer | Attack damage capped by available life for audit |
| HitPointsBefore | integer | 0..99,999 |
| HitPointsAfter | integer | 0..99,999 and never greater than before |

Composite identity `(AttackId, CombatantId)` prevents duplicate target application.

## Relationships

- Campaign 1—N Participant, DiceRoll and Encounter.
- Encounter 1—N Combatant and CombatAttack.
- Participant 0—N Combatant links and 1—N authored CombatAttack.
- DiceRoll 0—1 CombatAttack.
- CombatAttack 1—N CombatAttackTarget; each target references one Combatant.

Campaign deletion removes attack targets and attacks before combatants, rolls and participants. Encounter and combatant versions protect concurrent mutations.

## Visibility projection

- Master observer: every combatant includes `CurrentHitPoints`.
- Player observer: only the combatant whose `ParticipantId` matches the observer includes `CurrentHitPoints`; all other values are null/omitted.
- Public attack projection: author, roll total, damage and target names; before/after values only for a master or the player controlling that target.