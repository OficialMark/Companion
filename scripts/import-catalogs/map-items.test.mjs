// Gate tests for the SRD 5.2.1 item mapping. Fixtures are real records copied from
// 5e-bits/5e-srd-api@c33bc0aab997 (2024/en): fixtures/equipment.sample.json and fixtures/magic-items.sample.json.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import {
  mapCatalog,
  mapEquipment,
  mapMagicItem,
  normalizeRarity,
  parseMarkdownTable,
  joinParagraphs,
  planBatches,
  sourceStats,
  costText,
  KNOWN_EFFECTS,
} from './map-items.mjs';
import { buildSeedItems, checkSource, EXPECTED_SOURCE } from './import-items.mjs';
import { SRD_FILES, srdCacheName } from './sources.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
const load = (f) => JSON.parse(readFileSync(join(HERE, 'fixtures', f), 'utf8'));
const equipment = load('equipment.sample.json');
const magic = load('magic-items.sample.json');
const { items, translationSource, warnings } = mapCatalog(equipment, magic);
const item = (slug) => {
  const it = items.find((i) => i.slug === slug);
  assert.ok(it, `missing ${slug}`);
  return it;
};

test('longsword: martial melee, versatile, mastery Sap, weight and cost', () => {
  const it = item('longsword');
  assert.equal(it.category, 'Weapon');
  assert.equal(it.subcategory, 'Arma marcial corpo a corpo');
  assert.equal(it.weight, 1.5);
  assert.equal(it.cost, '15 PO');
  assert.equal(it.rarity, null);
  assert.deepEqual(it.weapon, {
    damageDice: '1d8',
    damageType: 'slashing',
    versatileDice: '1d10',
    kind: 'melee',
    properties: ['versatile'],
    rangeNormal: null,
    rangeLong: null,
    mastery: 'sap',
  });
});

test('longbow: ranged weapon, range in meters', () => {
  const w = item('longbow').weapon;
  assert.equal(w.kind, 'ranged');
  assert.deepEqual([w.rangeNormal, w.rangeLong], [45, 180]);
  assert.deepEqual(w.properties, ['ammunition', 'heavy', 'two-handed']);
  assert.equal(w.mastery, 'slow');
  assert.equal(item('longbow').subcategory, 'Arma marcial à distância');
});

test('dagger: melee with thrown range 20/60 ft -> 6/18 m', () => {
  const w = item('dagger').weapon;
  assert.equal(w.kind, 'melee');
  assert.deepEqual([w.rangeNormal, w.rangeLong], [6, 18]);
  assert.deepEqual(w.properties, ['finesse', 'light', 'thrown']);
  assert.equal(w.mastery, 'nick');
});

test('armor type comes from the DEX cap', () => {
  assert.deepEqual(item('chain-mail').armor, { baseAc: 16, type: 'heavy', strengthMin: 13, stealthDisadvantage: true });
  assert.deepEqual(item('breastplate').armor, { baseAc: 14, type: 'medium', strengthMin: null, stealthDisadvantage: false });
  assert.deepEqual(item('leather-armor').armor, { baseAc: 11, type: 'light', strengthMin: null, stealthDisadvantage: false });
  assert.equal(item('chain-mail').weight, 27.5);
  assert.equal(item('breastplate').subcategory, 'Armadura média');
});

test('hide armor: DEX cap 2 wins over the light-armor category, with a warning', () => {
  assert.equal(item('hide-armor').armor.type, 'medium');
  assert.deepEqual(warnings, ['hide-armor: tipo medium pelo limite de DES, mas a fonte diz light-armor; usado medium']);
});

test('shield: Shield category with acBonus 2', () => {
  const it = item('shield');
  assert.equal(it.category, 'Shield');
  assert.deepEqual(it.shield, { acBonus: 2 });
  assert.equal(it.armor, null);
});

test('tools and gear map to Gear; tool text lists ability, utilize and craft', () => {
  assert.equal(item('alchemists-supplies').category, 'Gear');
  assert.equal(item('alchemists-supplies').subcategory, 'Ferramentas de artesão');
  assert.equal(
    translationSource['alchemists-supplies'].text,
    'Ability: Intelligence.\n\nUtilize: Identify a substance (DC 15), Start a fire (DC 15).\n\nCraft: Acid, Alchemist\'s Fire, Component Pouch, Oil, Paper, Perfume.',
  );
  assert.equal(item('arrows').subcategory, 'Munição');
  assert.match(translationSource.net.text, /within 4\.5 meters of yourself/);
  assert.doesNotMatch(translationSource.net.text, /feet/);
});

test('+1/+2/+3 parent maps to nothing; each variant record is its own item with magicBonus and tier rarity', () => {
  assert.deepEqual(mapMagicItem(magic.find((m) => m.index === 'weapon')), []);
  assert.equal(items.some((i) => i.slug === 'weapon'), false);
  assert.deepEqual(
    ['weapon-1', 'weapon-2', 'weapon-3'].map((s) => [item(s).magicBonus, item(s).rarity, item(s).category]),
    [
      [1, 'uncommon', 'Weapon'],
      [2, 'rare', 'Weapon'],
      [3, 'very-rare', 'Weapon'],
    ],
  );
  assert.deepEqual([item('armor-2').magicBonus, item('armor-2').rarity, item('armor-2').category], [2, 'very-rare', 'Armor']);
  assert.deepEqual([item('shield-1').magicBonus, item('shield-1').category, item('shield-1').shield], [1, 'Shield', { acBonus: 2 }]);
  assert.deepEqual(item('weapon-1').effects, []);
});

test('magic weapon on a named base copies the base weapon block', () => {
  const it = item('dagger-of-venom');
  assert.equal(it.baseItem, 'dagger');
  assert.equal(it.magicBonus, 1);
  assert.equal(it.rarity, 'rare');
  assert.deepEqual(it.weapon, item('dagger').weapon);
});

test('ring and cloak of protection: armorClass +1 and saveBonus all +1', () => {
  const expected = [
    { type: 'armorClass', target: null, op: 'add', value: 1 },
    { type: 'saveBonus', target: 'all', op: 'add', value: 1 },
  ];
  assert.deepEqual(item('ring-of-protection').effects, expected);
  assert.deepEqual(item('cloak-of-protection').effects, expected);
  assert.equal(item('ring-of-protection').requiresAttunement, true);
  assert.equal(item('ring-of-protection').category, 'Gear');
  assert.equal(item('ring-of-protection').subcategory, 'Anel');
});

test('gauntlets of ogre power: FOR min 19; wand of the war mage +2: spell attack +2', () => {
  assert.deepEqual(item('gauntlets-of-ogre-power').effects, [{ type: 'ability', target: 'FOR', op: 'min', value: 19 }]);
  assert.deepEqual(item('wand-of-the-war-mage-2').effects, [{ type: 'attackBonus', target: 'spell', op: 'add', value: 2 }]);
  assert.equal(item('wand-of-the-war-mage-2').magicBonus, 0);
});

test('belt of giant strength expands its table into five items with FOR min 21..29', () => {
  const belts = items.filter((i) => i.slug.startsWith('belt-of-giant-strength-'));
  assert.deepEqual(
    belts.map((b) => [b.slug, b.rarity, b.effects[0].value]),
    [
      ['belt-of-giant-strength-hill', 'rare', 21],
      ['belt-of-giant-strength-frost-or-stone', 'very-rare', 23],
      ['belt-of-giant-strength-fire', 'very-rare', 25],
      ['belt-of-giant-strength-cloud', 'legendary', 27],
      ['belt-of-giant-strength-storm', 'legendary', 29],
    ],
  );
  assert.deepEqual(translationSource['belt-of-giant-strength-hill'], {
    name: 'Belt of Giant Strength (hill)',
    text: null,
    textFrom: 'belt-of-giant-strength',
  });
  assert.match(translationSource['belt-of-giant-strength'].text, /^Wondrous Item\n\nWhile wearing this belt/);
  assert.match(translationSource['belt-of-giant-strength'].text, /\| Belt \| Str\. \| Rarity \|\n\|---\|---\|---\|\n\| Belt of Giant Strength \(hill\) \| 21 \| Rare \|/);
});

test('potions of healing: base row merges into the mundane potion, other rows become items, no effects', () => {
  const base = item('potion-of-healing');
  assert.equal(base.category, 'Potion');
  assert.equal(base.rarity, 'common');
  assert.equal(base.healingDice, '2d4+2');
  assert.equal(base.cost, '50 PO');
  assert.deepEqual(
    items.filter((i) => i.slug.startsWith('potions-of-healing-')).map((p) => [p.slug, p.rarity, p.healingDice, p.effects.length]),
    [
      ['potions-of-healing-greater', 'uncommon', '4d4+4', 0],
      ['potions-of-healing-superior', 'rare', '8d4+8', 0],
      ['potions-of-healing-supreme', 'very-rare', '10d4+20', 0],
    ],
  );
});

test('spell scroll is Misc with rarity "varies"; horn variant keeps its own rarity', () => {
  assert.deepEqual([item('spell-scroll').category, item('spell-scroll').rarity], ['Misc', 'varies']);
  assert.deepEqual([item('horn-of-valhalla-1').rarity, item('horn-of-valhalla-1').magicBonus], ['rare', 0]);
});

test('normalizeRarity', () => {
  assert.equal(normalizeRarity('Very Rare'), 'very-rare');
  assert.equal(normalizeRarity('Uncommon (+1)'), 'uncommon');
  assert.equal(normalizeRarity('Rarity Varies'), 'varies');
  assert.equal(normalizeRarity('Uncommon (+1), Rare (+2), or Very Rare (+3)'), 'varies');
  assert.equal(normalizeRarity('Artifact'), 'artifact');
  assert.throws(() => normalizeRarity('Mythic'), /unknown rarity/);
});

test('helpers: cost, tables, paragraphs', () => {
  assert.equal(costText({ quantity: 5, unit: 'sp' }), '5 PP');
  assert.equal(costText(undefined), null);
  assert.deepEqual(parseMarkdownTable(['text', '| A | B |', '|---|---|', '| 1 | 2 |']), [
    ['A', 'B'],
    ['1', '2'],
  ]);
  assert.equal(joinParagraphs(['a', '| x |', '| y |', 'b']), 'a\n\n| x |\n| y |\n\nb');
});

test('a known effect whose text changed fails instead of guessing', () => {
  const ring = structuredClone(magic.find((m) => m.index === 'ring-of-protection'));
  ring.desc[1] = 'You gain a +2 bonus to Armor Class.';
  assert.throws(() => mapMagicItem(ring), /ring-of-protection: text no longer supports its mapped effects/);
  assert.ok(Object.keys(KNOWN_EFFECTS).includes('cloak-of-protection'));
});

test('an unknown weapon property fails loudly', () => {
  const bad = structuredClone(equipment.find((e) => e.index === 'longsword'));
  bad.properties.push({ index: 'special', name: 'Special' });
  assert.throws(() => mapEquipment(bad), /no PT-BR entry for "special"/);
});

test('batch plan respects 40 items and ~4000 words', () => {
  const src = Object.fromEntries(Array.from({ length: 95 }, (_, i) => [`i${i}`, { name: 'x', text: i === 50 ? 'w '.repeat(3999) : 'a b' }]));
  const plan = planBatches(src);
  assert.deepEqual(plan.map((b) => b.count), [40, 10, 1, 40, 4]);
  assert.deepEqual(plan.map((b) => b.batch), ['001', '002', '003', '004', '005']);
  assert.equal(plan[0].from, 'i0');
  assert.equal(plan[0].to, 'i39');
});

test('buildSeedItems puts PT name/description and keeps mechanics', () => {
  const translation = Object.fromEntries(Object.keys(translationSource).map((k) => [k, { name: `PT ${k}`, text: translationSource[k].text ? `texto ${k}` : null }]));
  const seed = buildSeedItems(items, translation);
  const belt = seed.find((s) => s.slug === 'belt-of-giant-strength-hill');
  assert.equal(belt.name, 'PT belt-of-giant-strength-hill');
  assert.equal(belt.description, 'texto belt-of-giant-strength');
  assert.equal(belt.nameEn, 'Belt of Giant Strength (hill)');
  assert.equal('textFrom' in belt, false);
  assert.equal('source' in belt, false);
  const sword = seed.find((s) => s.slug === 'longsword');
  assert.equal(sword.description, null);
  assert.equal(sword.weapon.damageDice, '1d8');
  assert.deepEqual(Object.keys(sword).slice(0, 10), ['slug', 'name', 'nameEn', 'category', 'subcategory', 'rarity', 'requiresAttunement', 'weight', 'cost', 'description']);
  assert.equal(sourceStats(translationSource, items).catalogItems, items.length);
});

const cacheFile = (f) => join(HERE, '.cache', srdCacheName(f));
const haveCache = Object.values(SRD_FILES).every((f) => existsSync(cacheFile(f)));

test('full pinned source maps to 459 items with expected counts', { skip: haveCache ? false : 'cache missing: run import-items.mjs --source-only once' }, () => {
  const read = (f) => JSON.parse(readFileSync(cacheFile(f), 'utf8'));
  const src = {
    equipment: read(SRD_FILES.equipment),
    magicItems: read(SRD_FILES.magicItems),
    weaponProperties: read(SRD_FILES.weaponProperties),
    weaponMastery: read(SRD_FILES.weaponMastery),
  };
  assert.ok(checkSource(src).every((c) => c.pass));
  assert.equal(src.equipment.length, EXPECTED_SOURCE.equipment);
  const full = mapCatalog(src.equipment, src.magicItems);
  const s = sourceStats(full.translationSource, full.items);
  assert.equal(s.catalogItems, 459);
  assert.equal(s.translationEntries, 461);
  assert.deepEqual(s.byCategory, { Gear: 322, Weapon: 71, Armor: 26, Potion: 27, Shield: 10, Misc: 3 });
  for (const it of full.items) {
    if (it.category === 'Weapon' && it.source === 'equipment') assert.ok(it.weapon, it.slug);
    if (it.category === 'Armor' && it.source === 'equipment') assert.ok(it.armor, it.slug);
  }
});
