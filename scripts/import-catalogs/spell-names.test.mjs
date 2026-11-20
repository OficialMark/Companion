import { test } from 'node:test';
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { loadSpellNames, withEnglishNames } from './spell-names.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const seedPath = resolve(here, '../../backend/src/Companion.Infrastructure/Persistence/Seed/spells.magias5e.json');

test('withEnglishNames sets nameEn by slug and null when unknown, without touching the input', () => {
  const spells = [{ slug: 'bola-de-fogo', name: 'Bola de Fogo' }, { slug: 'nova', name: 'Nova' }];
  const out = withEnglishNames(spells, { 'bola-de-fogo': { nameEn: 'Fireball', verified: true } });
  assert.deepEqual(out.map((s) => s.nameEn), ['Fireball', null]);
  assert.equal(spells[0].nameEn, undefined);
});

test('the names table passes the validator (every slug, SRD names checked, no duplicates)', () => {
  const output = execFileSync(process.execPath, [resolve(here, 'validate-spell-names.mjs')], { encoding: 'utf8' });
  assert.match(output, /OK/);
});

test('the committed seed carries the table names (apply-spell-names.mjs was run)', () => {
  const seed = JSON.parse(readFileSync(seedPath, 'utf8'));
  const names = loadSpellNames();
  for (const spell of seed.spells) assert.equal(spell.nameEn ?? null, names[spell.slug]?.nameEn ?? null, spell.slug);
  assert.equal(seed.spells.find((s) => s.name === 'Bola de Fogo')?.nameEn, 'Fireball');
});
