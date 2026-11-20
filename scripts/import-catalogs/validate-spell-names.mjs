#!/usr/bin/env node
// Validates backend/.../Seed/spells.names-en.json against the PT-BR spell catalog
// (spells.magias5e.json) and the cached SRD 5.2.1 spell-name list
// (fixtures/srd-spell-names.json, from 5e-bits/5e-database src/2024/en/5e-SRD-Spells.json).
// No network, no deps. Exit 1 on any failure.
//
// Usage: node scripts/import-catalogs/validate-spell-names.mjs
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const repo = resolve(here, '..', '..');
const seed = resolve(repo, 'backend/src/Companion.Infrastructure/Persistence/Seed');
const load = (p) => JSON.parse(readFileSync(p, 'utf8'));

const catalog = load(resolve(seed, 'spells.magias5e.json'));
const { names } = load(resolve(seed, 'spells.names-en.json'));
const srd = new Set(load(resolve(here, 'fixtures/srd-spell-names.json')));

const errors = [];
const slugs = new Set(catalog.spells.map((s) => s.slug));
const keys = Object.keys(names ?? {});

for (const slug of slugs) if (!(slug in names)) errors.push(`missing entry for catalog slug "${slug}"`);
for (const key of keys) if (!slugs.has(key)) errors.push(`names key "${key}" is not a catalog slug`);

const sortedKeys = [...keys].sort();
if (keys.some((k, i) => k !== sortedKeys[i])) errors.push('names keys are not sorted by slug');

const seen = new Map();
let verified = 0, unverified = 0, nulls = 0;
for (const [slug, e] of Object.entries(names)) {
  if (e.nameEn === null) { nulls++; if (e.verified) errors.push(`${slug}: verified with null nameEn`); continue; }
  if (typeof e.nameEn !== 'string' || !e.nameEn.trim()) { errors.push(`${slug}: nameEn must be a non-empty string or null`); continue; }
  if (typeof e.verified !== 'boolean') errors.push(`${slug}: verified must be boolean`);
  const lower = e.nameEn.toLowerCase();
  if (seen.has(lower)) errors.push(`duplicate nameEn "${e.nameEn}" (${seen.get(lower)}, ${slug})`);
  else seen.set(lower, slug);
  if (e.verified) {
    verified++;
    if (!srd.has(e.nameEn) && !(e.nameEnSrd && srd.has(e.nameEnSrd)))
      errors.push(`${slug}: verified but neither "${e.nameEn}" nor nameEnSrd "${e.nameEnSrd ?? ''}" is in the SRD list`);
  } else {
    unverified++;
  }
  if (e.nameEnSrd !== undefined && !srd.has(e.nameEnSrd)) errors.push(`${slug}: nameEnSrd "${e.nameEnSrd}" is not in the SRD list`);
}

console.log(`spell names: total=${keys.length} verified=${verified} unverified=${unverified} null=${nulls} (catalog=${slugs.size}, srd=${srd.size})`);
if (errors.length) {
  for (const e of errors) console.error('FAIL', e);
  process.exit(1);
}
console.log('OK');
