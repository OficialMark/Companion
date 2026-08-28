# Quickstart Validation: Vida e Ataques em Combate

## Prerequisites

- Docker Compose
- `.env` based on `.env.example`
- SQL Server container healthy
- Frontend dependencies restored with `pnpm install`

## Start

```powershell
docker compose up --build -d
docker compose ps
```

## Player combat profile

1. Join as player and confirm initiative and life fields appear at the bottom of the main page.
2. Save modifier `+3` and life `24`; refresh and confirm both return.
3. Join as master and confirm the personal fields are absent.
4. Attempt negative, decimal and values above `99999`; confirm validation without state change.

## Encounter preparation

1. With two online players, save lives `24` and `18` and roll general initiative as master.
2. Confirm each player appears once with copied life; add a manual creature with life `30`.
3. Edit a copied life and reroll initiatives; confirm the edit is preserved.
4. Leave one life empty and confirm combat cannot start; fill it and start successfully.

## Direct health changes and privacy

1. Open the same active encounter as master and two players.
2. Change player A's own life; confirm player A and master update in at most one second.
3. Confirm player B never receives player A's life in the rendered UI or snapshot response.
4. As master, edit the creature and both players. Confirm each player only receives their own life.
5. Disconnect a player, change life, reconnect and confirm the authorized snapshot is current.

## Attack

1. As a player linked to a combatant, choose `Atacar`, roll `2d6+3`, wait for the result and select two targets.
2. Confirm the review and apply. Verify both targets lose the same total, non-targets remain unchanged and no life becomes negative.
3. Replay the same confirmation with the same idempotency key and confirm no second damage.
4. Roll and attack outside the player's turn; confirm it is accepted.
5. As master, attack any targets and confirm all lives remain visible.
6. Change a target between roll and confirmation; confirm damage uses the latest confirmed life.
7. Remove a selected target before confirmation; confirm no target is changed and the selection must be reviewed.

## Automated validation

```powershell
cd backend
dotnet test tests/Companion.UnitTests/Companion.UnitTests.csproj
dotnet test tests/Companion.IntegrationTests/Companion.IntegrationTests.csproj
cd ../frontend
pnpm test
pnpm typecheck
pnpm test:e2e
```

Expected: all suites pass; integration tests prove persistence, authorization, privacy, idempotency and atomic target updates; Playwright covers desktop and Android-sized journeys.