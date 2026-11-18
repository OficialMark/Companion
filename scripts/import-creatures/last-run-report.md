# Pocket DM creature import: last run

- Imported at: 2026-11-18T12:50:49.648Z
- Output: `backend/src/Companion.Infrastructure/Persistence/Seed/creatures.pocketdm.json` (1936726 bytes)
- Network: 0 requests, 726 cache hits, 0 retries
- Result: **PASS** (target: 100% of index slugs attempted, >= 98.0% with hitPoints, armorClass and all six abilities)

## Totals

| Metric | Value |
| --- | ---: |
| Slugs on index | 725 |
| Attempted | 725 |
| Parsed OK | 725 |
| Failed | 0 |
| Skipped (restricted) | 0 |
| With HP + AC + 6 abilities | 725 (100.0%) |
| With legendary actions | 43 |
| Missing hitPoints | 0 |
| Missing armorClass | 0 |
| Missing any ability score | 0 |
| Missing imageUrl | 327 |

## Per source book

| Value | Count |
| --- | ---: |
| Monster a Day | 357 |
| SRD 5.2 | 331 |
| SRD 5.1 | 37 |

## Per size

| Value | Count |
| --- | ---: |
| Medium | 301 |
| Large | 197 |
| Huge | 81 |
| Small | 61 |
| Tiny | 58 |
| Gargantuan | 27 |

## Failures

None.

## Skipped (restricted content)

None.

## Warnings (4)

- `besouro-veado-gigante`: actions[0] "Ataque Múltiplo": page shows English description, used PT translation from payload
- `monitor-da-ninhada`: hit dice "12d12 + 48} Speed 40 ft., climb 40 ft. STR CON WIS DEX INT CHA 21 (+5" has trailing junk, kept "12d12 + 48"
- `spellskite`: hit dice "12d1Q + 36" is malformed, set to null
- `yuan-ti-ignan`: alignment slot holds text that is not an alignment: "it is grappled (escape DC 15). Until this grapple ends, the "
