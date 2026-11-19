import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import {
  ABILITIES,
  ABILITY_BY_SRD,
  WEAPON_PROPERTIES,
  WEAPON_MASTERIES,
  DAMAGE_TYPES,
  RARITIES,
  ARMOR_TYPES,
  ITEM_CATEGORIES,
  SUBCATEGORIES,
  COINS,
  pt,
} from './glossary.mjs';
import { SRD_FILES, srdCacheName } from './sources.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));

test('fixed PT-BR forms for weapon properties and masteries', () => {
  assert.deepEqual(
    Object.fromEntries(Object.entries(WEAPON_PROPERTIES).map(([k, v]) => [k, v.pt])),
    {
      ammunition: 'Munição',
      finesse: 'Acuidade',
      heavy: 'Pesada',
      light: 'Leve',
      loading: 'Recarga',
      range: 'Distância',
      reach: 'Alcance',
      thrown: 'Arremesso',
      'two-handed': 'Duas Mãos',
      versatile: 'Versátil',
    },
  );
  assert.deepEqual(
    Object.fromEntries(Object.entries(WEAPON_MASTERIES).map(([k, v]) => [k, v.pt])),
    {
      cleave: 'Trespassar',
      graze: 'Raspão',
      nick: 'Talho',
      push: 'Empurrão',
      sap: 'Enfraquecer',
      slow: 'Lentidão',
      topple: 'Derrubar',
      vex: 'Afligir',
    },
  );
});

test('rarities, armor types, abilities and categories', () => {
  assert.deepEqual(Object.keys(RARITIES), ['common', 'uncommon', 'rare', 'very-rare', 'legendary', 'artifact', 'varies']);
  assert.equal(pt(RARITIES, 'very-rare'), 'Muito Raro');
  assert.equal(pt(RARITIES, 'varies'), 'Varia');
  assert.equal(pt(ARMOR_TYPES, 'medium'), 'Armadura média');
  assert.deepEqual(Object.values(ABILITY_BY_SRD).map((a) => a.code), ABILITIES);
  assert.deepEqual(Object.keys(ITEM_CATEGORIES), ['Weapon', 'Armor', 'Shield', 'Gear', 'Misc', 'Potion']);
  assert.equal(pt(COINS, 'gp'), 'PO');
  assert.equal(pt(DAMAGE_TYPES, 'slashing'), 'cortante');
});

test('every PT form is unique inside its table (no two keys collapse)', () => {
  for (const table of [WEAPON_PROPERTIES, WEAPON_MASTERIES, DAMAGE_TYPES, RARITIES, ARMOR_TYPES]) {
    const forms = Object.values(table).map((v) => v.pt.toLowerCase());
    assert.equal(new Set(forms).size, forms.length);
  }
});

test('pt() fails loudly on an unknown key', () => {
  assert.throws(() => pt(WEAPON_PROPERTIES, 'special', 'WEAPON_PROPERTIES'), /no PT-BR entry for "special"/);
});

const cacheFile = (f) => join(HERE, '.cache', srdCacheName(f));
const haveCache = Object.values(SRD_FILES).every((f) => existsSync(cacheFile(f)));

test('glossary covers every key used by the pinned SRD data', { skip: haveCache ? false : 'cache missing: run import-items.mjs --source-only once' }, () => {
  const load = (f) => JSON.parse(readFileSync(cacheFile(f), 'utf8'));
  assert.deepEqual(load(SRD_FILES.weaponProperties).map((p) => p.index).sort(), Object.keys(WEAPON_PROPERTIES).sort());
  assert.deepEqual(load(SRD_FILES.weaponMastery).map((p) => p.index).sort(), Object.keys(WEAPON_MASTERIES).sort());
  const equipment = load(SRD_FILES.equipment);
  for (const e of equipment) {
    if (e.damage) assert.ok(DAMAGE_TYPES[e.damage.damage_type.index], e.index);
    for (const c of e.equipment_categories) {
      if (!['weapons', 'melee-weapons', 'ranged-weapons', 'simple-weapons', 'martial-weapons', 'armor', 'tools'].includes(c.index)) {
        assert.ok(SUBCATEGORIES[c.index], `${e.index}: ${c.index}`);
      }
    }
  }
});
