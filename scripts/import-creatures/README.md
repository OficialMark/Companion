# Pocket DM creature importer

Builds the creature library seed `backend/src/Companion.Infrastructure/Persistence/Seed/creatures.pocketdm.json`
from the Portuguese compendium at <https://pocketdm.com.br/monstros>.

Plain Node.js (ESM, Node 20+), built-in `fetch` only, no npm dependencies.

| File | Role |
| --- | --- |
| `import-pocketdm.mjs` | Fetch (throttled, cached), parse every creature, write the seed JSON and the run report |
| `parse.mjs` | Pure parsing functions (HTML string in, creature object out). No I/O |
| `parse.test.mjs` | `node:test` suite over three saved pages in `fixtures/` |
| `last-run-report.md` | Validation report written by the last full run |

## Attribution and permission

The Pocket DM team authorized Companion to use this compendium. Their license page still asks for
credit and a link, so the seed carries this `attribution` string and every creature keeps its
`sourceUrl`; surface both wherever the library is shown:

> Tradução e curadoria © Pocket DM (pocketdm.com.br), usadas com autorização. Conteúdo SRD sob
> Creative Commons Attribution 4.0 (Wizards of the Coast).

Monster a Day creatures come from the r/monsteraday community and are published by Pocket DM.
Anything marked Kobold Press or otherwise restricted (non-SRD and not Monster a Day) is skipped and
listed in the report under "Skipped". Pages without a stat block (login-only, error shells) are
never cached and are reported as failures.

## Run it

From the repository root:

```bash
node scripts/import-creatures/import-pocketdm.mjs --cache-dir "<some dir>/pocketdm-cache"
```

Options:

| Flag | Default | Meaning |
| --- | --- | --- |
| `--cache-dir <dir>` | `$POCKETDM_CACHE_DIR` or `<os tmp>/pocketdm-cache` | Where fetched HTML is stored |
| `--out <file>` | `backend/src/Companion.Infrastructure/Persistence/Seed/creatures.pocketdm.json` | Seed JSON |
| `--report <file>` | `scripts/import-creatures/last-run-report.md` | Validation report |
| `--fetch-only` | off | Only download pages into the cache, no parsing |
| `--offline` | off | Never touch the network; fail any page that is not cached |

The process exits with code 1 when validation fails (see below), so it can gate CI or a script.

Tests:

```bash
node --test scripts/import-creatures/
```

## Politeness: throttling, retries, caching

- At most 2 requests in flight, and at least 300 ms between the start of two requests (global pacing).
- User-Agent `CompanionImporter/1.0 (+authorized import)`.
- HTTP 429, 5xx and network errors are retried up to 6 attempts with exponential backoff
  (1 s, 2 s, 4 s, ... capped at 30 s, plus jitter); `Retry-After` is honoured when larger.
- A 200 response without a stat block is treated as an error shell: retried twice, never cached.
- Every page that parses as a real page is written to the cache as `<slug>.html` (the index is
  `_index.html`). Cached pages are never re-fetched. A full cold run is about 725 requests and takes
  roughly 4 minutes; a warm run is offline-speed.

## Re-running

- **Re-parse only** (after changing `parse.mjs`): run the same command again, or add `--offline` to be
  sure nothing is fetched. Output is deterministic for the same cache, apart from `importedAt`.
- **Refresh from the site**: delete the cache directory (or single `<slug>.html` files) and run again.
  Delete `_index.html` to pick up creatures added to the index.
- Review the diff of `creatures.pocketdm.json` and `last-run-report.md` before committing.

## How a page is parsed

Pages are Next.js App Router output. Two sources exist in each page:

1. The rendered stat block DOM (`<section id="ficha"><article>`): the source of every Portuguese field,
   since it is what the site shows.
2. The RSC flight payload (`self.__next_f.push(...)`), which carries a structured English copy of the
   monster plus a PT translation map. It supplies `nameEn` and the SRD / Monster a Day flags, and is used
   to cross-check AC, HP, CR, XP, the six abilities, size and entry counts. Disagreements become warnings
   in the report. When the site leaves an entry's description in English while the payload holds its PT
   translation, the PT translation is used (counted in the report warnings).

Field rules:

- `size`: PT size word mapped to `Tiny Small Medium Large Huge Gargantuan` (a few Monster a Day pages
  show the English word, also accepted). "Pequeno ou Médio" (2024 humanoids) keeps the largest size
  shown, `Medium`. Swarms shown as "Medium (tiny) fera (enxame)" drop the member size `(tiny)`.
- `type`: lowercase PT text between size and the first top-level comma, e.g. `humanoide (goblinoide)`.
- `alignment`: text after that comma, first letter capitalized (`Leal e mau`), `null` if absent. On
  malformed lines with a stray `)` the last comma segment is used; text that is clearly not an alignment
  (a sentence leaked from an action) becomes `null` with a warning.
- `hitDice`: must be a well-formed dice expression. Trailing junk after a complete expression is cut;
  a malformed one (`12d1Q + 36`) becomes `null` with a warning. `hitPoints` is unaffected.
- `speed`, `senses`, `languages`: as shown, with the `(30 ft)` imperial duplicates removed and a trailing
  period dropped. Separators are kept as the site shows them (commas, semicolons).
- `armorClassNote`: the parenthesis after AC (`armadura natural`), `null` if none.
- `challengeRating` keeps fractions (`1/4`). `experiencePoints` parses `5.900 XP`.
- `proficiencyBonus`: from the page when shown, otherwise derived from CR with the SRD table
  (CR 0-4: +2, 5-8: +3, ... 29-30: +9). It is a rules function of CR, not invented data.
- `initiative`: the modifier shown on the page (2014 pages show it too, computed by the site from DEX).
- Descriptions: full plain text, no HTML. Whitespace is collapsed inside each line, but the site's real
  line breaks are kept as a single `\n` (they separate paragraphs and spell groups such as
  `...Prestidigitação Arcana\n2/dia cada: Voo, ...`; flattening them glues unrelated text together).
  Render with `white-space: pre-line`.
  The English condition glosses the site appends after condition links, e.g. `Imobilizado (Grappled)`, are
  removed. Distances inside descriptions are kept exactly as shown.
- `languages` shown as `—` becomes `null`.
- Sections other than Ações / Ações Bônus / Reações / Ações Lendárias are not part of the seed shape and
  are reported as warnings if they appear.

## Validation report

Every run writes `last-run-report.md` and prints it: slugs on the index, parsed OK, failures (slug +
reason), skipped restricted pages, counts per source book and size, and how many creatures lack
`hitPoints`, `armorClass`, any ability score or `imageUrl`. The run passes when 100% of index slugs were
attempted and at least 98% of the non-skipped creatures have HP, AC and all six abilities.

## Crédito na interface

A equipe do Pocket DM autorizou o uso e pediu que o app não exiba crédito a ela (spec 011, FR-040, 2026-11-19). O app mostra só o aviso da licença CC BY 4.0 do conteúdo SRD.
