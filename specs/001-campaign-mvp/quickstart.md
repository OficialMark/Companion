# Quickstart Validation: MVP de Campanha D&D

## Prerequisites

- Docker Desktop or Docker Engine with Compose
- A host connected to the target LAN
- TCP port 3000 available and permitted by the host firewall

## Start

1. Copy `.env.example` to `.env`.
2. Replace `MSSQL_SA_PASSWORD` with a strong SQL Server password.
3. Run `docker compose up --build -d` from the repository root.
4. Wait until `docker compose ps` reports the database and API healthy.
5. Open `http://localhost:3000` on the host.

## Validate campaigns and dice

1. Create a campaign as `Mestre` and copy its code.
2. Open a private window or a second device and join as `Jogador`.
3. Roll `2d6+3` from either device.
4. Confirm both devices show the same individual values and total.
5. Reload a page and confirm the roll remains in the history.
6. Roll `3#3d5+4` and confirm three separately numbered results appear in one history entry.
7. Roll `2d3+4+2d8+3` and confirm both dice terms, accumulated `+7` modifier and total appear together.

## Validate initiative

1. As master, create an encounter.
2. Add at least three combatants with different modifiers.
3. Roll all initiatives and start combat.
4. Advance through all combatants and confirm round becomes 2.
5. Confirm the player device follows the active combatant in real time.
6. Confirm a player cannot execute master commands.

## Validate reconnection

1. Disconnect the player device from the network.
2. Roll a die and advance the turn from the master device.
3. Reconnect the player device.
4. Confirm it reports reconnection and shows current history/turn without duplicates.

## Validate LAN and VPN-style addressing

1. On Windows, run `ipconfig` and locate the IPv4 address of the active LAN or VPN adapter.
2. From another device with connectivity to that adapter, open `http://<host-ip>:3000`.
3. If blocked, allow inbound TCP 3000 for private networks in Windows Firewall.
4. Repeat the campaign and dice validation from that device.

## Persistence and diagnostics

- Restart: `docker compose restart`
- Inspect services: `docker compose ps`
- Follow logs: `docker compose logs -f api frontend sqlserver`
- Stop while retaining data: `docker compose down`
- Delete stored development data only when intended: `docker compose down -v`

After a normal restart, campaign, roll history and active encounter must remain available.

## Validation record (2026-08-23)

- Backend build: passed with 0 warnings and 0 errors.
- Backend unit tests: 26 passed.
- SQL Server integration tests: 6 passed using Testcontainers.
- Frontend typecheck: passed.
- Frontend component tests: 7 passed.
- Nuxt static generation: passed.
- Playwright desktop and Android journeys: 8 passed, including two-client realtime delivery, offline reconnection/resynchronization, initiative rerolls, missing native UUID and advanced dice expressions.
- Docker Compose configuration and image builds: passed.
- Running-stack smoke test: frontend and proxied API health checks passed.
- Manual browser review: legacy history, grouped repetitions and compound terms passed in desktop and 390×844 mobile viewports.
