// Pure mapping of the 5e-bits SRD 5.2.1 JSON (2024/en) to CatalogItem (data-model.md).
// No I/O. Mechanical fields come from structured data and the fixed glossary; free text stays in
// English here and becomes the translation source (translations/source.en.json).

import {
  WEAPON_PROPERTIES,
  WEAPON_MASTERIES,
  DAMAGE_TYPES,
  RARITIES,
  SUBCATEGORIES,
  COINS,
  ABILITY_BY_SRD,
  pt,
} from './glossary.mjs';
import { feetToMeters, poundsToKg, convertUnitsInText } from './units.mjs';

// ---------------------------------------------------------------------------------------------
// Small helpers
// ---------------------------------------------------------------------------------------------

const catIndexes = (e) => (e.equipment_categories ?? []).map((c) => c.index);

/** "Uncommon (+1)" -> "uncommon"; "Rarity Varies" or "A, B, or C" -> "varies". Throws on unknown words. */
export function normalizeRarity(name) {
  const s = String(name).trim();
  if (/^rarity varies$/i.test(s) || /,|\bor\b/i.test(s)) return 'varies';
  const base = s.replace(/\s*\(.*\)\s*$/, '').trim().toLowerCase().replace(/\s+/g, '-');
  if (!Object.hasOwn(RARITIES, base)) throw new Error(`unknown rarity "${name}"`);
  return base;
}

export const slugify = (s) =>
  s
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-|-$/g, '');

export function costText(cost) {
  if (!cost || !Number.isFinite(cost.quantity)) return null;
  return `${cost.quantity} ${pt(COINS, cost.unit, 'COINS')}`;
}

const weightKg = (lb) => (Number.isFinite(lb) ? poundsToKg(lb) : null);

/** Joins description paragraphs; consecutive markdown table lines stay on adjacent lines. */
export function joinParagraphs(paragraphs) {
  let out = '';
  let prevTable = false;
  for (const p of paragraphs) {
    const line = String(p).trim();
    if (!line) continue;
    const isTable = line.startsWith('|');
    if (out) out += isTable && prevTable ? '\n' : '\n\n';
    out += line;
    prevTable = isTable;
  }
  return out;
}

export function countWords(s) {
  return (String(s ?? '').match(/\S+/g) ?? []).length;
}

// ---------------------------------------------------------------------------------------------
// Blocks
// ---------------------------------------------------------------------------------------------

export function weaponBlock(e) {
  if (!e.damage) return null;
  const cats = catIndexes(e);
  const kind = cats.includes('ranged-weapons') ? 'ranged' : 'melee';
  const properties = (e.properties ?? []).map((p) => {
    pt(WEAPON_PROPERTIES, p.index, 'WEAPON_PROPERTIES');
    return p.index;
  });
  const damageType = e.damage.damage_type.index;
  pt(DAMAGE_TYPES, damageType, 'DAMAGE_TYPES');
  const range = kind === 'ranged' ? e.range : e.throw_range;
  const mastery = e.mastery?.index ?? null;
  if (mastery) pt(WEAPON_MASTERIES, mastery, 'WEAPON_MASTERIES');
  return {
    damageDice: e.damage.damage_dice,
    damageType,
    versatileDice: e.two_handed_damage?.damage_dice ?? null,
    kind,
    properties,
    rangeNormal: range?.normal != null ? feetToMeters(range.normal) : null,
    rangeLong: range?.long != null ? feetToMeters(range.long) : null,
    mastery,
  };
}

export function armorType(ac) {
  if (!ac.dex_bonus) return 'heavy';
  if (ac.max_bonus === 2) return 'medium';
  if (ac.max_bonus == null) return 'light';
  throw new Error(`unexpected dex cap ${ac.max_bonus}`);
}

/**
 * Armor block. The type comes from the DEX cap (none = light, 2 = medium, no DEX = heavy); a source
 * category that disagrees is reported in `warnings` (5e-bits lists 2024 Hide, a medium armor, as light).
 */
export function armorBlock(e, warnings = []) {
  const ac = e.armor_class;
  const type = armorType(ac);
  const cats = catIndexes(e);
  if (!cats.includes(`${type}-armor`)) {
    const said = cats.filter((c) => c.endsWith('-armor')).join(',');
    warnings.push(`${e.index}: tipo ${type} pelo limite de DES, mas a fonte diz ${said}; usado ${type}`);
  }
  return {
    baseAc: ac.base,
    type,
    strengthMin: e.str_minimum ? e.str_minimum : null,
    stealthDisadvantage: Boolean(e.stealth_disadvantage),
  };
}

// ---------------------------------------------------------------------------------------------
// Mundane equipment
// ---------------------------------------------------------------------------------------------

const GEAR_SUBCATS = ['equipment-packs', 'holy-symbols', 'arcane-foci', 'druidic-foci'];
const TOOL_SUBCATS = ['artisans-tools', 'musical-instruments', 'gaming-sets', 'other-tools'];
const WEAPON_SUBCATS = ['simple-melee-weapons', 'simple-ranged-weapons', 'martial-melee-weapons', 'martial-ranged-weapons'];

/** English text of a mundane item for translation (units already metric). */
export function equipmentText(e) {
  const parts = [];
  if (e.description) parts.push(...String(e.description).split(/\n+/));
  for (const n of e.notes ?? []) parts.push(`${n}.`.replace(/\.\.$/, '.'));
  if (e.ability) parts.push(`Ability: ${ABILITY_BY_SRD[e.ability.index].en}.`);
  if (e.utilize?.length) {
    parts.push(
      `Utilize: ${e.utilize
        .map((u) => (u.dc ? `${u.name} (DC ${u.dc.dc_value})` : u.name))
        .join(', ')}.`,
    );
  }
  if (e.craft?.length) parts.push(`Craft: ${e.craft.map((c) => c.name).join(', ')}.`);
  return convertUnitsInText(joinParagraphs(parts));
}

export function mapEquipment(e, warnings = []) {
  const cats = catIndexes(e);
  const base = {
    slug: e.index,
    source: 'equipment',
    nameEn: e.name,
    category: null,
    subcategory: null,
    rarity: null,
    requiresAttunement: false,
    weight: weightKg(e.weight),
    cost: costText(e.cost),
    weapon: null,
    armor: null,
    shield: null,
    magicBonus: 0,
    effects: [],
  };
  if (cats.includes('weapons')) {
    const sub = WEAPON_SUBCATS.find((c) => cats.includes(c));
    if (!sub) throw new Error(`${e.index}: weapon without simple/martial melee/ranged category`);
    return { ...base, category: 'Weapon', subcategory: SUBCATEGORIES[sub], weapon: weaponBlock(e) };
  }
  if (cats.includes('shields')) {
    return { ...base, category: 'Shield', subcategory: SUBCATEGORIES.shields, shield: { acBonus: e.armor_class.base } };
  }
  if (cats.includes('armor')) {
    const armor = armorBlock(e, warnings);
    return { ...base, category: 'Armor', subcategory: SUBCATEGORIES[`${armor.type}-armor`], armor };
  }
  if (cats.includes('tools')) {
    const sub = TOOL_SUBCATS.find((c) => cats.includes(c)) ?? 'other-tools';
    return { ...base, category: 'Gear', subcategory: SUBCATEGORIES[sub] };
  }
  if (cats.includes('ammunition')) return { ...base, category: 'Gear', subcategory: SUBCATEGORIES.ammunition };
  if (cats.includes('adventuring-gear')) {
    if (/^potion\b/i.test(e.name)) return { ...base, category: 'Potion', subcategory: SUBCATEGORIES.potion };
    if (/^spell-scroll/.test(e.index)) return { ...base, category: 'Misc', subcategory: SUBCATEGORIES.scroll };
    const sub = GEAR_SUBCATS.find((c) => cats.includes(c)) ?? 'adventuring-gear';
    return { ...base, category: 'Gear', subcategory: SUBCATEGORIES[sub] };
  }
  throw new Error(`${e.index}: no category mapping for ${cats.join(',')}`);
}

// ---------------------------------------------------------------------------------------------
// Magic items
// ---------------------------------------------------------------------------------------------

/**
 * Effects mapped by hand, only where the rule is unambiguous. Each entry names the English phrase
 * that justifies it: if the source text stops saying that, the mapping throws instead of guessing.
 * Weapon/Armor/Shield +N bonuses are not effects: they go to magicBonus.
 */
export const KNOWN_EFFECTS = Object.freeze({
  'ring-of-protection': {
    requires: /\+1 bonus to Armor Class and saving throws/,
    effects: [
      { type: 'armorClass', target: null, op: 'add', value: 1 },
      { type: 'saveBonus', target: 'all', op: 'add', value: 1 },
    ],
  },
  'cloak-of-protection': {
    requires: /\+1 bonus to Armor Class and saving throws/,
    effects: [
      { type: 'armorClass', target: null, op: 'add', value: 1 },
      { type: 'saveBonus', target: 'all', op: 'add', value: 1 },
    ],
  },
  'gauntlets-of-ogre-power': {
    requires: /Your Strength is 19 while you wear/,
    effects: [{ type: 'ability', target: 'FOR', op: 'min', value: 19 }],
  },
  'amulet-of-health': {
    requires: /Your Constitution is 19 while you wear/,
    effects: [{ type: 'ability', target: 'CON', op: 'min', value: 19 }],
  },
  'headband-of-intellect': {
    requires: /Your Intelligence is 19 while you wear/,
    effects: [{ type: 'ability', target: 'INT', op: 'min', value: 19 }],
  },
  'wand-of-the-war-mage-1': {
    requires: /\+1 bonus to spell attack rolls/,
    effects: [{ type: 'attackBonus', target: 'spell', op: 'add', value: 1 }],
  },
  'wand-of-the-war-mage-2': {
    requires: /\+2 bonus to spell attack rolls/,
    effects: [{ type: 'attackBonus', target: 'spell', op: 'add', value: 2 }],
  },
  'wand-of-the-war-mage-3': {
    requires: /\+3 bonus to spell attack rolls/,
    effects: [{ type: 'attackBonus', target: 'spell', op: 'add', value: 3 }],
  },
});

/**
 * Items whose rarity table is expanded into one catalog item per row. `skipRowsNamed` drops a row
 * that already exists as a mundane item (Potion of Healing is sold in the equipment list).
 */
export const TABLE_VARIANTS = Object.freeze({
  'belt-of-giant-strength': {
    header: ['Belt', 'Str.', 'Rarity'],
    row: ([, str]) => ({ effects: [{ type: 'ability', target: 'FOR', op: 'min', value: Number(str) }] }),
  },
  'potions-of-healing': {
    header: ['Potion', 'HP Regained', 'Rarity'],
    row: ([, hp]) => ({ healingDice: hp.replace(/\s+/g, '') }),
    mergeIntoEquipment: { 'Potion of Healing': 'potion-of-healing' },
  },
});

/** Parses the first markdown table of desc[] into rows of trimmed cells (header row first). */
export function parseMarkdownTable(desc) {
  const lines = desc.filter((d) => String(d).trim().startsWith('|'));
  const rows = lines
    .map((l) => l.trim().replace(/^\|/, '').replace(/\|$/, '').split('|').map((c) => c.trim()))
    .filter((cells) => !cells.every((c) => /^:?-+:?$/.test(c)));
  return rows;
}

const TYPE_LINE = /^(Weapon|Armor) \((.+)\)$/;

function magicCategory(m) {
  const typeLine = m.desc[0] ?? '';
  switch (m.equipment_category.index) {
    case 'armor':
      return /\(Shield\)/.test(typeLine)
        ? { category: 'Shield', subcategory: SUBCATEGORIES['magic-shield'] }
        : { category: 'Armor', subcategory: SUBCATEGORIES['magic-armor'] };
    case 'weapons':
      return /^ammunition/.test(m.index) || /Ammunition/.test(typeLine)
        ? { category: 'Gear', subcategory: SUBCATEGORIES['magic-ammunition'] }
        : { category: 'Weapon', subcategory: SUBCATEGORIES['magic-weapon'] };
    case 'potions':
      return { category: 'Potion', subcategory: SUBCATEGORIES.potion };
    case 'rings':
      return { category: 'Gear', subcategory: SUBCATEGORIES.ring };
    case 'staffs':
      return { category: 'Gear', subcategory: SUBCATEGORIES.staff };
    case 'wands':
      return { category: 'Gear', subcategory: SUBCATEGORIES.wand };
    case 'wondrous-items':
      if (typeLine === 'Scroll') return { category: 'Misc', subcategory: SUBCATEGORIES.scroll };
      if (typeLine === 'Rod') return { category: 'Gear', subcategory: SUBCATEGORIES.rod };
      return { category: 'Gear', subcategory: SUBCATEGORIES['wondrous-item'] };
    default:
      throw new Error(`${m.index}: unknown magic item category ${m.equipment_category.index}`);
  }
}

/** Base mundane item named by "Weapon (Dagger)" / "Armor (Plate Armor)", when exactly one is named. */
export function baseItemFor(m, equipmentByName) {
  const t = TYPE_LINE.exec(m.desc[0] ?? '');
  if (!t) return null;
  const inner = t[2];
  if (/,|\bor\b|\bAny\b/i.test(inner)) return null;
  return equipmentByName.get(inner.toLowerCase()) ?? null;
}

/** +N from the rules text (or the variant name for ammunition). 0 when none. */
export function magicBonusFor(m, category) {
  const text = m.desc.join(' ');
  let r;
  if (category === 'Weapon' && (r = /\+(\d) bonus to attack rolls and damage rolls made with this (?:magic )?weapon/i.exec(text))) return Number(r[1]);
  if (category === 'Armor' && (r = /\+(\d) bonus to Armor Class while (?:wearing|you wear) this armor/i.exec(text))) return Number(r[1]);
  if (category === 'Shield' && (r = /\+(\d) bonus to Armor Class, in addition to the Shield's normal bonus/i.exec(text))) return Number(r[1]);
  if (/^ammunition-\d$/.test(m.index) && (r = /\+(\d)$/.exec(m.name))) return Number(r[1]);
  return 0;
}

/** English text of a magic item for translation (type line first, units already metric). */
export function magicText(m) {
  const parts = [...m.desc];
  if (m['limited-to']) parts.splice(1, 0, `(Requires Attunement by a ${m['limited-to']})`);
  return convertUnitsInText(joinParagraphs(parts));
}

/**
 * Maps one magic item to zero or more catalog items:
 * - a parent whose variants exist as their own records (armor -> armor-1..3) maps to [] (the variants carry the data);
 * - a TABLE_VARIANTS item maps to one item per table row;
 * - anything else maps to one item.
 */
export function mapMagicItem(m, { equipmentByName = new Map() } = {}) {
  if (m.variants?.length) return [];
  const { category, subcategory } = magicCategory(m);
  const base = baseItemFor(m, equipmentByName);
  const known = KNOWN_EFFECTS[m.index];
  if (known && !known.requires.test(m.desc.join(' '))) throw new Error(`${m.index}: text no longer supports its mapped effects`);
  const item = {
    slug: m.index,
    source: 'magic-items',
    nameEn: m.name,
    category,
    subcategory,
    rarity: normalizeRarity(m.rarity.name),
    requiresAttunement: Boolean(m.attunement),
    weight: base ? weightKg(base.weight) : null,
    cost: null,
    weapon: category === 'Weapon' && base ? weaponBlock(base) : null,
    armor: category === 'Armor' && base?.armor_class ? armorBlock(base, []) : null,
    shield: category === 'Shield' ? { acBonus: 2 } : null,
    magicBonus: magicBonusFor(m, category),
    effects: known ? known.effects.map((e) => ({ ...e })) : [],
    baseItem: base ? base.index : null,
  };

  const spec = TABLE_VARIANTS[m.index];
  if (!spec) return [item];
  const [header, ...rows] = parseMarkdownTable(m.desc);
  if (JSON.stringify(header) !== JSON.stringify(spec.header)) throw new Error(`${m.index}: table header changed: ${header}`);
  const out = [];
  for (const cells of rows) {
    const [rowName, , rarityName] = cells;
    const extra = spec.row(cells);
    const target = spec.mergeIntoEquipment?.[rowName];
    if (target) {
      out.push({ mergeInto: target, rarity: normalizeRarity(rarityName), ...extra });
      continue;
    }
    const paren = /\(([^)]+)\)\s*$/.exec(rowName);
    if (!paren) throw new Error(`${m.index}: table row "${rowName}" has no variant name`);
    out.push({
      ...item,
      ...extra,
      effects: extra.effects ?? item.effects,
      slug: `${m.index}-${slugify(paren[1])}`,
      nameEn: rowName,
      rarity: normalizeRarity(rarityName),
      textFrom: m.index,
    });
  }
  return out;
}

// ---------------------------------------------------------------------------------------------
// Whole catalog
// ---------------------------------------------------------------------------------------------

/**
 * Maps both source files. Returns { items, translationSource, warnings } where items are CatalogItem
 * objects without PT name/description, and translationSource is ordered { index: { name, text } }.
 * Derived table variants carry `text: null` and `textFrom` (they reuse the parent's text).
 */
export function mapCatalog(equipment, magicItems) {
  const equipmentByName = new Map(equipment.map((e) => [e.name.toLowerCase(), e]));
  const items = [];
  const warnings = [];
  const translationSource = {};
  const bySlug = new Map();
  const add = (item, name, text, textFrom) => {
    if (bySlug.has(item.slug)) throw new Error(`duplicate slug ${item.slug}`);
    bySlug.set(item.slug, item);
    items.push(item);
    translationSource[item.slug] = textFrom ? { name, text: null, textFrom } : { name, text };
  };

  for (const e of equipment) add(mapEquipment(e, warnings), convertUnitsInText(e.name), equipmentText(e));

  const merges = [];
  const parentTexts = [];
  for (const m of magicItems) {
    const mapped = mapMagicItem(m, { equipmentByName });
    for (const it of mapped) {
      if (it.mergeInto) {
        merges.push(it);
        continue;
      }
      if (it.textFrom) {
        if (!parentTexts.includes(m.index)) {
          parentTexts.push(m.index);
          // The parent itself is not a catalog item, but its text is shared by its rows.
          translationSource[m.index] = { name: convertUnitsInText(m.name), text: magicText(m) };
        }
        add(it, convertUnitsInText(it.nameEn), null, it.textFrom);
      } else {
        add(it, convertUnitsInText(m.name), magicText(m));
      }
    }
  }
  for (const mg of merges) {
    const target = bySlug.get(mg.mergeInto);
    if (!target) throw new Error(`merge target ${mg.mergeInto} not found`);
    const { mergeInto, ...fields } = mg;
    Object.assign(target, fields);
  }
  return { items, translationSource, warnings };
}

/** Counts for the report and for source.en.json. */
export function sourceStats(translationSource, items) {
  const entries = Object.values(translationSource);
  const nameWords = entries.reduce((n, e) => n + countWords(e.name), 0);
  const textWords = entries.reduce((n, e) => n + countWords(e.text), 0);
  const byCategory = {};
  for (const it of items) byCategory[it.category] = (byCategory[it.category] ?? 0) + 1;
  return {
    catalogItems: items.length,
    translationEntries: entries.length,
    entriesWithText: entries.filter((e) => e.text).length,
    nameWords,
    textWords,
    totalWords: nameWords + textWords,
    byCategory,
    withEffects: items.filter((i) => i.effects.length).length,
    withMagicBonus: items.filter((i) => i.magicBonus).length,
  };
}

/** Splits the ordered translation source into batches of at most maxItems entries and ~maxWords words. */
export function planBatches(translationSource, { maxItems = 40, maxWords = 4000 } = {}) {
  const batches = [];
  let cur = null;
  for (const [index, e] of Object.entries(translationSource)) {
    const words = countWords(e.name) + countWords(e.text);
    if (!cur || cur.indices.length >= maxItems || (cur.words + words > maxWords && cur.indices.length > 0)) {
      cur = { batch: String(batches.length + 1).padStart(3, '0'), indices: [], words: 0 };
      batches.push(cur);
    }
    cur.indices.push(index);
    cur.words += words;
  }
  return batches.map((b) => ({ ...b, count: b.indices.length, from: b.indices[0], to: b.indices.at(-1) }));
}
