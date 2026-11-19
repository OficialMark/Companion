// Fixed PT-BR tables for every mechanical term of the catalogs. Keys are the 5e-bits `index`
// values (English, lowercase), so mapping is a lookup, never free translation.
// Translators (T057) must use exactly these PT forms; validate-translation.mjs enforces it.

export const ABILITIES = Object.freeze(['FOR', 'DES', 'CON', 'INT', 'SAB', 'CAR']);

/** 5e-bits ability index -> app AbilityCode and PT name. */
export const ABILITY_BY_SRD = Object.freeze({
  str: { code: 'FOR', pt: 'Força', en: 'Strength' },
  dex: { code: 'DES', pt: 'Destreza', en: 'Dexterity' },
  con: { code: 'CON', pt: 'Constituição', en: 'Constitution' },
  int: { code: 'INT', pt: 'Inteligência', en: 'Intelligence' },
  wis: { code: 'SAB', pt: 'Sabedoria', en: 'Wisdom' },
  cha: { code: 'CAR', pt: 'Carisma', en: 'Charisma' },
});

export const WEAPON_PROPERTIES = Object.freeze({
  ammunition: { pt: 'Munição', en: 'Ammunition' },
  finesse: { pt: 'Acuidade', en: 'Finesse' },
  heavy: { pt: 'Pesada', en: 'Heavy' },
  light: { pt: 'Leve', en: 'Light' },
  loading: { pt: 'Recarga', en: 'Loading' },
  range: { pt: 'Distância', en: 'Range' },
  reach: { pt: 'Alcance', en: 'Reach' },
  thrown: { pt: 'Arremesso', en: 'Thrown' },
  'two-handed': { pt: 'Duas Mãos', en: 'Two-Handed' },
  versatile: { pt: 'Versátil', en: 'Versatile' },
});

export const WEAPON_MASTERIES = Object.freeze({
  cleave: { pt: 'Trespassar', en: 'Cleave' },
  graze: { pt: 'Raspão', en: 'Graze' },
  nick: { pt: 'Talho', en: 'Nick' },
  push: { pt: 'Empurrão', en: 'Push' },
  sap: { pt: 'Enfraquecer', en: 'Sap' },
  slow: { pt: 'Lentidão', en: 'Slow' },
  topple: { pt: 'Derrubar', en: 'Topple' },
  vex: { pt: 'Afligir', en: 'Vex' },
});

export const DAMAGE_TYPES = Object.freeze({
  acid: { pt: 'ácido', en: 'Acid' },
  bludgeoning: { pt: 'contundente', en: 'Bludgeoning' },
  cold: { pt: 'gélido', en: 'Cold' },
  fire: { pt: 'fogo', en: 'Fire' },
  force: { pt: 'força', en: 'Force' },
  lightning: { pt: 'elétrico', en: 'Lightning' },
  necrotic: { pt: 'necrótico', en: 'Necrotic' },
  piercing: { pt: 'perfurante', en: 'Piercing' },
  poison: { pt: 'veneno', en: 'Poison' },
  psychic: { pt: 'psíquico', en: 'Psychic' },
  radiant: { pt: 'radiante', en: 'Radiant' },
  slashing: { pt: 'cortante', en: 'Slashing' },
  thunder: { pt: 'trovejante', en: 'Thunder' },
});

/** Normalized rarity keys (stored in the seed) and their PT labels. */
export const RARITIES = Object.freeze({
  common: { pt: 'Comum', en: 'Common' },
  uncommon: { pt: 'Incomum', en: 'Uncommon' },
  rare: { pt: 'Raro', en: 'Rare' },
  'very-rare': { pt: 'Muito Raro', en: 'Very Rare' },
  legendary: { pt: 'Lendário', en: 'Legendary' },
  artifact: { pt: 'Artefato', en: 'Artifact' },
  varies: { pt: 'Varia', en: 'Varies' },
});

export const ARMOR_TYPES = Object.freeze({
  light: { pt: 'Armadura leve', en: 'Light Armor' },
  medium: { pt: 'Armadura média', en: 'Medium Armor' },
  heavy: { pt: 'Armadura pesada', en: 'Heavy Armor' },
});

/** App item categories (InventoryItem.Category) and their PT labels. */
export const ITEM_CATEGORIES = Object.freeze({
  Weapon: 'Arma',
  Armor: 'Armadura',
  Shield: 'Escudo',
  Gear: 'Equipamento',
  Misc: 'Diversos',
  Potion: 'Poção',
});

/** Subcategory labels: 5e-bits equipment-category index (or a magic item type) -> PT text. */
export const SUBCATEGORIES = Object.freeze({
  'simple-melee-weapons': 'Arma simples corpo a corpo',
  'simple-ranged-weapons': 'Arma simples à distância',
  'martial-melee-weapons': 'Arma marcial corpo a corpo',
  'martial-ranged-weapons': 'Arma marcial à distância',
  'light-armor': 'Armadura leve',
  'medium-armor': 'Armadura média',
  'heavy-armor': 'Armadura pesada',
  shields: 'Escudo',
  'adventuring-gear': 'Equipamento de aventura',
  ammunition: 'Munição',
  'arcane-foci': 'Foco arcano',
  'druidic-foci': 'Foco druídico',
  'holy-symbols': 'Símbolo sagrado',
  'equipment-packs': 'Pacote de equipamento',
  'artisans-tools': 'Ferramentas de artesão',
  'musical-instruments': 'Instrumento musical',
  'gaming-sets': 'Kit de jogo',
  'other-tools': 'Ferramenta',
  potion: 'Poção',
  'magic-weapon': 'Arma mágica',
  'magic-armor': 'Armadura mágica',
  'magic-shield': 'Escudo mágico',
  'magic-ammunition': 'Munição mágica',
  'wondrous-item': 'Item maravilhoso',
  ring: 'Anel',
  rod: 'Bastão',
  staff: 'Cajado',
  wand: 'Varinha',
  scroll: 'Pergaminho',
});

export const WEAPON_KINDS = Object.freeze({ melee: 'corpo a corpo', ranged: 'à distância' });

/** Coin units of the SRD cost field -> PT abbreviation. */
export const COINS = Object.freeze({ cp: 'PC', sp: 'PP', ep: 'PE', gp: 'PO', pp: 'PL' });

/**
 * Forms a translator must use for rule terms that appear inside free text. The validator checks
 * that, whenever the English source contains the key phrase, the PT text contains the PT form.
 */
export const TEXT_TERMS = Object.freeze({
  'saving throw': 'teste de resistência',
  'attack roll': 'jogada de ataque',
  'hit point': 'ponto de vida',
  'armor class': 'Classe de Armadura',
  advantage: 'vantagem',
  disadvantage: 'desvantagem',
  attunement: 'sintonização',
  'proficiency bonus': 'bônus de proficiência',
  'bonus action': 'ação bônus',
  reaction: 'reação',
  'magic action': 'ação de Magia',
  dc: 'CD',
  'short rest': 'descanso curto',
  'long rest': 'descanso longo',
  strength: 'Força',
  dexterity: 'Destreza',
  constitution: 'Constituição',
  intelligence: 'Inteligência',
  wisdom: 'Sabedoria',
  charisma: 'Carisma',
});

/** English rule words that must not survive in a PT translation (whole words, singular or plural, case-insensitive). */
export const ENGLISH_BLACKLIST = Object.freeze([
  'advantage',
  'disadvantage',
  'saving throw',
  'damage',
  'attack roll',
  'hit point',
  'feet',
  'foot',
  'pound',
  'lb',
  'Strength',
  'Dexterity',
  'Constitution',
  'Intelligence',
  'Wisdom',
  'Charisma',
  'Armor Class',
  'DC',
  'bonus action',
  'reaction',
  'creature',
  'spell',
  'rest',
  'the',
  'you',
  'your',
  'with',
  'while',
]);

/** PT label lookup with a clear error, so a new source key fails loudly instead of leaking English. */
export function pt(table, key, tableName = 'glossary') {
  const entry = table[key];
  if (entry === undefined) throw new Error(`${tableName}: no PT-BR entry for "${key}"`);
  return typeof entry === 'string' ? entry : entry.pt;
}
