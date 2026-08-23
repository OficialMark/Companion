# Realtime Contract

## Connection

- Hub path: `/hubs/campaign`
- Client calls `JoinCampaign(campaignId, participantToken)` after connecting.
- The server validates the token belongs to the campaign before adding the connection to `campaign:{campaignId}`.
- Automatic reconnect calls `JoinCampaign` again, then fetches the REST snapshot.

## Event envelope

```json
{
  "eventId": "f4e19b13-3fae-4a04-a035-a638be652ce1",
  "campaignId": "c5d7b25e-0b24-4010-a83d-cf2fca17e675",
  "type": "DiceRolled",
  "occurredAt": "2026-08-23T18:30:00Z",
  "payload": {}
}
```

## Server-to-client methods

| Method | Payload | Meaning |
|--------|---------|---------|
| `CampaignEvent` | Event envelope | A persisted resource changed; payload contains current resource representation |
| `ParticipantsChanged` | Event envelope | Presence or membership changed; payload contains the complete participant list |

## Event types

- `ParticipantJoined`
- `DiceRolled`
- `EncounterStarted`
- `CombatantAdded`
- `InitiativeRolled`
- `TurnAdvanced`
- `EncounterEnded`

Clients deduplicate by `eventId`. Events are notifications, not the durable source of truth.
The `DiceRolled` payload uses the `DiceRoll` REST representation, including the ordered `results` array for `#` repetitions and the `terms` array for compound expressions.
