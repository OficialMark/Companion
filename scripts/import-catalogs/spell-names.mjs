// English names of the spells (spec 012): the magias5e source has only the Portuguese name, so the
// official English name comes from the fixed, versioned table spells.names-en.json (slug -> nameEn),
// checked by validate-spell-names.mjs. The importer and apply-spell-names.mjs both use this.
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
export const NAMES_PATH = resolve(here, '../../backend/src/Companion.Infrastructure/Persistence/Seed/spells.names-en.json');

export function loadSpellNames(path = NAMES_PATH) {
  return JSON.parse(readFileSync(path, 'utf8')).names ?? {};
}

/**
 * Returns the spells with `nameEn` set from the table (null when the slug is absent or has no name).
 * Pure: same input, same output; the spells are copied, never changed in place.
 */
export function withEnglishNames(spells, names) {
  return spells.map((spell) => ({ ...spell, nameEn: names[spell.slug]?.nameEn ?? null }));
}
