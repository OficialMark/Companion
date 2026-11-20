#!/usr/bin/env node
// Writes the English names of spells.names-en.json into the committed spell seed, without fetching
// the source again (the importer does the same on a full run). Idempotent.
// Usage: node scripts/import-catalogs/apply-spell-names.mjs
import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { loadSpellNames, withEnglishNames } from './spell-names.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const seedPath = resolve(here, '../../backend/src/Companion.Infrastructure/Persistence/Seed/spells.magias5e.json');
const seed = JSON.parse(readFileSync(seedPath, 'utf8'));
seed.spells = withEnglishNames(seed.spells, loadSpellNames());
writeFileSync(seedPath, JSON.stringify(seed, null, 2) + '\n', 'utf8');
console.log(`nameEn: ${seed.spells.filter((s) => s.nameEn).length}/${seed.spells.length}`);
