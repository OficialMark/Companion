# Quickstart Validation: Acesso, Administração e Iniciativa

## Prerequisites

- Docker Compose
- `.env` from `.env.example`
- Non-placeholder administrative secret configured for the API

## Start

```powershell
docker compose up --build -d
docker compose ps
```

## Four-digit access

1. Create a campaign and record player/master codes.
2. Confirm both are distinct four-digit values.
3. Join clean browsers with each code and verify the resulting role.
4. Verify the player cannot obtain the master code.
5. Try malformed and unknown codes; all fail without role disclosure.

## Participant initiative

1. Two online players save modifiers `+3` and `-1`; refresh and verify persistence.
2. Keep a third participant offline.
3. As master, create preparation and roll all.
4. Confirm the online players appear once and the offline participant is absent.
5. Edit an imported combatant, add a manual monster and reroll; confirm no duplicates or overwritten edits.
6. Start combat, edit/remove a combatant and verify turn continuity.

## Administration

1. Open `/admin`, authenticate and verify all campaigns, both codes and presence counts.
2. Cancel a deletion and verify no change.
3. Confirm deletion of a disposable campaign.
4. Verify connected clients are informed and old codes/tokens stop working.

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
