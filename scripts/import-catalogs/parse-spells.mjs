// Pure parser for the magias5e spell list (https://avribacki.gitlab.io/magias5e).
// Input: the text of spells.js (one document.write(`...`) holding the HTML of every spell card)
// and the text of spells-by-class.js (spellByClass["bardo"] = ["amizade", ...]).
// Output: CatalogSpell objects (data-model.md). No I/O, no network, no eval: the downloaded
// JavaScript is only ever read as text.

import { DAMAGE_TYPES, ABILITIES } from './glossary.mjs';

/** Counts the source had when this importer was written (2026-11-19). A different count fails the run. */
export const EXPECTED = Object.freeze({
  total: 361,
  byLevel: [27, 63, 59, 49, 35, 42, 32, 20, 18, 16],
  ritual: 32,
  concentration: 154,
  classes: ['bardo', 'bruxo', 'clérigo', 'druida', 'feiticeiro', 'mago', 'paladino', 'patrulheiro'],
});

const ENTITIES = { amp: '&', lt: '<', gt: '>', quot: '"', apos: "'", nbsp: ' ' };

export function decodeEntities(s) {
  return s.replace(/&(#x[0-9a-fA-F]+|#[0-9]+|[a-zA-Z]+);/g, (m, e) => {
    if (e[0] === '#') {
      const cp = e[1] === 'x' || e[1] === 'X' ? parseInt(e.slice(2), 16) : parseInt(e.slice(1), 10);
      return Number.isFinite(cp) ? String.fromCodePoint(cp) : m;
    }
    return Object.hasOwn(ENTITIES, e) ? ENTITIES[e] : m;
  });
}

const escapeHtml = (s) => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
export const collapse = (s) => s.replace(/\s+/g, ' ').trim();
/** Visible text of an HTML fragment, whitespace collapsed. */
export const htmlToText = (html) => collapse(decodeEntities(html.replace(/<br\s*\/?>/gi, ' ').replace(/<[^>]*>/g, ' ')));

/**
 * Extracts the template literal body of `document.write(`...`)`. Throws when the wrapper is missing
 * or the body contains `${` (an interpolation would mean the file is no longer static HTML).
 */
export function extractDocumentWrite(js) {
  const start = js.indexOf('document.write(`');
  const end = js.lastIndexOf('`)');
  if (start === -1 || end === -1 || end <= start) throw new Error('spells.js: document.write(`...`) wrapper not found');
  const body = js.slice(start + 'document.write(`'.length, end);
  if (body.includes('${')) throw new Error('spells.js: template literal contains ${...}; refusing to guess its value');
  return body.replace(/\\`/g, '`').replace(/\\\\/g, '\\');
}

/** Parses spells-by-class.js into { classKey: slug[] } without evaluating it. */
export function parseSpellsByClass(js) {
  const out = {};
  const re = /spellByClass\[\s*"([^"]+)"\s*\]\s*=\s*\[([\s\S]*?)\]/g;
  let m;
  while ((m = re.exec(js))) {
    const slugs = [];
    const strRe = /"((?:[^"\\]|\\.)*)"/g;
    let s;
    while ((s = strRe.exec(m[2]))) slugs.push(s[1]);
    out[m[1]] = slugs;
  }
  return out;
}

/** Returns the index just after the </div> that closes the <div ...> opened at `openAt`. */
function closeOfDiv(html, openAt) {
  const re = /<(\/?)div\b[^>]*>/gi;
  re.lastIndex = openAt;
  let depth = 0;
  let m;
  while ((m = re.exec(html))) {
    depth += m[1] ? -1 : 1;
    if (depth === 0) return { innerEnd: m.index, end: re.lastIndex };
  }
  throw new Error('unbalanced <div> in spell card');
}

function innerOfDiv(html, className) {
  const re = new RegExp(`<div class="${className}"[^>]*>`, 'i');
  const m = re.exec(html);
  if (!m) return null;
  const { innerEnd } = closeOfDiv(html, m.index);
  return html.slice(m.index + m[0].length, innerEnd);
}

const ALLOWED = new Set(['p', 'strong', 'em', 'ul', 'li', 'br']);
const TAG_ALIASES = { b: 'strong', i: 'em', ol: 'ul' };

/**
 * Filters spell description HTML to the allowlist p, strong, em, ul, li, br with no attributes.
 * Tables become one <p> per row with cells joined by " | " (header rows in <strong>).
 * Every other tag is dropped and its text kept. Text is entity-decoded, whitespace-collapsed and re-escaped.
 */
export function sanitizeHtml(html) {
  let out = '';
  let cellIndex = 0;
  let inHeaderRow = false;
  const tokenRe = /<(\/?)([a-zA-Z][a-zA-Z0-9]*)\b[^>]*>|([^<]+)|(<)/g;
  let m;
  while ((m = tokenRe.exec(html))) {
    if (m[3] !== undefined || m[4] !== undefined) {
      // "corpo-a-\ncorpo": the source wraps lines after hyphens; rejoin before collapsing whitespace.
      const text = decodeEntities(m[3] ?? '<').replace(/(\p{L})-[^\S\n]*\n\s*(?=\p{L})/gu, '$1-');
      out += escapeHtml(text.replace(/\s+/g, ' '));
      continue;
    }
    const closing = m[1] === '/';
    let tag = m[2].toLowerCase();
    tag = TAG_ALIASES[tag] ?? tag;
    if (tag === 'tr') {
      if (closing) out += inHeaderRow ? '</strong></p>' : '</p>';
      else {
        cellIndex = 0;
        inHeaderRow = /^\s*<th\b/i.test(html.slice(tokenRe.lastIndex));
        out += inHeaderRow ? '<p><strong>' : '<p>';
      }
      continue;
    }
    if (tag === 'td' || tag === 'th') {
      if (!closing && cellIndex++ > 0) out += ' | ';
      continue;
    }
    if (!ALLOWED.has(tag)) {
      out += ' ';
      continue;
    }
    if (tag === 'br') out += '<br>';
    else out += closing ? `</${tag}>` : `<${tag}>`;
  }
  const trimBlocks = (s) => s.replace(/ *(<\/?(?:p|ul|li)>|<br>) */g, '$1');
  let clean = trimBlocks(out.replace(/ {2,}/g, ' '));
  // Inline tags: move inner edge spaces outside, so "<strong> X </strong>" -> " <strong>X</strong> ".
  clean = clean.replace(/<(strong|em)> /g, ' <$1>').replace(/ <\/(strong|em)>/g, '</$1> ');
  clean = trimBlocks(clean.replace(/ {2,}/g, ' '));
  return clean.replace(/<(strong|em)><\/\1>/g, '').replace(/<p><\/p>/g, '').trim();
}

const HIGHER_RE = /<p>\s*<strong>\s*Em Níveis Superiores\s*<\/strong>/i;

/** Splits the sanitized description into the main body and the "Em Níveis Superiores" text. */
export function splitHigherLevels(cleanHtml) {
  const m = HIGHER_RE.exec(cleanHtml);
  if (!m) return { descriptionHtml: cleanHtml, higherLevels: null };
  const higher = htmlToText(cleanHtml.slice(m.index))
    .replace(/^Em Níveis Superiores\s*[.:]?\s*/i, '')
    .trim();
  return { descriptionHtml: cleanHtml.slice(0, m.index).trim(), higherLevels: higher || null };
}

// ---------------------------------------------------------------------------------------------
// Mechanics derived from the PT text
// ---------------------------------------------------------------------------------------------

const DICE_RE = /(?<![\p{L}\d])(\d*)d(\d+)(?![\p{L}\d])/u;

/** First dice notation (NdM or dM) of the text, normalized to NdM. */
export function firstDice(text) {
  const m = DICE_RE.exec(text);
  if (!m) return null;
  return { dice: `${m[1] || '1'}d${m[2]}`, index: m.index };
}

/** PT damage words (2014 and 2024 translations) -> glossary key. */
export const PT_DAMAGE_WORDS = Object.freeze({
  ácido: 'acid',
  contundente: 'bludgeoning',
  concussão: 'bludgeoning',
  cortante: 'slashing',
  elétrico: 'lightning',
  relâmpago: 'lightning',
  fogo: 'fire',
  força: 'force',
  energia: 'force',
  gélido: 'cold',
  frio: 'cold',
  necrótico: 'necrotic',
  perfurante: 'piercing',
  psíquico: 'psychic',
  radiante: 'radiant',
  trovejante: 'thunder',
  trovão: 'thunder',
  veneno: 'poison',
  venenoso: 'poison',
});
for (const key of new Set(Object.values(PT_DAMAGE_WORDS))) {
  if (!Object.hasOwn(DAMAGE_TYPES, key)) throw new Error(`PT_DAMAGE_WORDS maps to unknown damage type ${key}`);
}

const DAMAGE_WORD_RE = new RegExp(
  `\\bdano\\s+(?:de\\s+)?(${Object.keys(PT_DAMAGE_WORDS).join('|')})(?![\\p{L}])`,
  'iu',
);

/** Sentence of `text` around `index`, split on . ! ? ; followed by space (decimal commas are safe). */
function sentenceAt(text, index) {
  const before = text.slice(0, index);
  const startMatch = /[.!?;]\s[^.!?;]*$/.exec(before);
  const start = startMatch ? startMatch.index + 2 : 0;
  const after = /[.!?;](\s|$)/.exec(text.slice(index));
  const end = after ? index + after.index : text.length;
  return text.slice(start, end);
}

/** Damage type key named after "dano" in the same sentence as the first dice, or null. */
export function damageTypeNear(text, index) {
  const m = DAMAGE_WORD_RE.exec(sentenceAt(text, index));
  return m ? PT_DAMAGE_WORDS[m[1].toLowerCase()] : null;
}

const PT_ABILITY_WORDS = Object.freeze({
  força: 'FOR',
  destreza: 'DES',
  constituição: 'CON',
  inteligência: 'INT',
  sabedoria: 'SAB',
  carisma: 'CAR',
});
for (const code of Object.values(PT_ABILITY_WORDS)) {
  if (!ABILITIES.includes(code)) throw new Error(`unknown ability code ${code}`);
}

const SPELL_ATTACK_RE = /\bataques?\s+(?:(?:corpo-a-\s*corpo|à\s+distância)\s+)?com\s+magia\b/iu;
const SAVE_RE = new RegExp(`teste\\s+de\\s+resistência\\s+de\\s+(${Object.keys(PT_ABILITY_WORDS).join('|')})(?![\\p{L}])`, 'iu');

/** attack | save | none, plus the save ability. The earliest of the two cues in the text wins. */
export function rollKind(text) {
  const a = SPELL_ATTACK_RE.exec(text);
  const s = SAVE_RE.exec(text);
  if (a && (!s || a.index <= s.index)) return { attackKind: 'attack', saveAbility: null };
  if (s) return { attackKind: 'save', saveAbility: PT_ABILITY_WORDS[s[1].toLowerCase()] };
  return { attackKind: 'none', saveAbility: null };
}

// ---------------------------------------------------------------------------------------------
// Spell cards
// ---------------------------------------------------------------------------------------------

const META_KEYS = { cast: 'castingTime', range: 'range', components: 'components', duration: 'duration' };

/** Parses one spell card (the outer div.spell-item HTML). Throws on any structural surprise. */
export function parseSpellCard(cardHtml) {
  const open = /^<div\s+([^>]*)>/i.exec(cardHtml);
  const attr = (n) => new RegExp(`\\b${n}="([^"]*)"`).exec(open?.[1] ?? '')?.[1];
  const slug = attr('data-name');
  const levelAttr = attr('data-level');
  if (!slug || levelAttr === undefined) throw new Error('spell card without data-name/data-level');
  const fail = (msg) => {
    throw new Error(`${slug}: ${msg}`);
  };
  const level = Number(levelAttr);
  if (!Number.isInteger(level) || level < 0 || level > 9) fail(`bad data-level ${levelAttr}`);

  const nameDiv = innerOfDiv(cardHtml, 'name') ?? fail('no .name');
  const nameMatch = /<p>([\s\S]*?)<\/p>/i.exec(nameDiv) ?? fail('no name <p>');
  const name = htmlToText(nameMatch[1]);
  if (!name) fail('empty name');
  const ritual = /src="img\/ritual\.svg"/.test(nameDiv);
  const schoolMatch = /<p class="school">([\s\S]*?)<\/p>/i.exec(nameDiv) ?? fail('no p.school');
  const schoolText = htmlToText(schoolMatch[1]);
  const sm = /^(.*?)\s+(\d)$/.exec(schoolText) ?? fail(`school without level: ${schoolText}`);
  if (Number(sm[2]) !== level) fail(`school level ${sm[2]} != data-level ${level}`);
  const school = sm[1];

  const meta = innerOfDiv(cardHtml, 'meta') ?? fail('no .meta');
  const fields = {};
  let concentration = false;
  const itemRe = /<div class="meta-item">([\s\S]*?)<div class="meta-content">([\s\S]*?)<\/div>/gi;
  let im;
  while ((im = itemRe.exec(meta))) {
    const icons = [...im[1].matchAll(/src="img\/([a-z-]+)\.svg"/g)].map((x) => x[1]);
    const key = META_KEYS[icons[0]] ?? fail(`unknown meta icon ${icons[0]}`);
    if (key in fields) fail(`duplicate meta ${key}`);
    fields[key] = htmlToText(im[2]);
    if (icons.includes('concentration')) {
      if (key !== 'duration') fail('concentration icon outside duration');
      concentration = true;
    }
  }
  for (const k of Object.values(META_KEYS)) if (!fields[k]) fail(`missing ${k}`);
  // Some sources write the marker as text too; the icon is the flag, so drop any textual copy.
  fields.duration = fields.duration.replace(/^concentração,?\s*/i, '').trim();

  const rawDescription = innerOfDiv(cardHtml, 'description') ?? fail('no .description');
  const { descriptionHtml, higherLevels } = splitHigherLevels(sanitizeHtml(rawDescription));
  if (!descriptionHtml) fail('empty description');

  const text = htmlToText(descriptionHtml);
  const dice = firstDice(text);
  const { attackKind, saveAbility } = rollKind(text);

  return {
    slug,
    name,
    level,
    school,
    ritual,
    castingTime: fields.castingTime,
    range: fields.range,
    components: fields.components,
    duration: fields.duration,
    concentration,
    descriptionHtml,
    higherLevels,
    classes: [],
    suggestedDamageDice: dice?.dice ?? null,
    suggestedDamageType: dice ? damageTypeNear(text, dice.index) : null,
    attackKind,
    saveAbility,
  };
}

/** Splits the document.write HTML into spell card strings. */
export function splitCards(html) {
  const cards = [];
  const re = /<div\s+data-name="[^"]*"\s+data-level="[^"]*"\s+class="grid-item spell-item">/gi;
  let m;
  while ((m = re.exec(html))) {
    const { end } = closeOfDiv(html, m.index);
    cards.push(html.slice(m.index, end));
    re.lastIndex = end;
  }
  return cards;
}

/**
 * Parses both files. Returns { spells, classLists, errors }: errors lists every structural problem
 * (an importer must refuse to write when it is not empty).
 */
export function parseSpells(spellsJs, byClassJs) {
  const errors = [];
  const html = extractDocumentWrite(spellsJs);
  const declared = (html.match(/class="grid-item spell-item"/g) ?? []).length;
  const cards = splitCards(html);
  if (cards.length !== declared) errors.push(`found ${declared} spell-item markers but split ${cards.length} cards`);

  const spells = [];
  const bySlug = new Map();
  for (const card of cards) {
    try {
      const s = parseSpellCard(card);
      if (bySlug.has(s.slug)) errors.push(`duplicate slug ${s.slug}`);
      bySlug.set(s.slug, s);
      spells.push(s);
    } catch (err) {
      errors.push(err.message);
    }
  }

  const classLists = parseSpellsByClass(byClassJs);
  for (const [cls, slugs] of Object.entries(classLists)) {
    const seen = new Set();
    for (const slug of slugs) {
      if (seen.has(slug)) continue;
      seen.add(slug);
      const s = bySlug.get(slug);
      if (!s) errors.push(`class ${cls} lists unknown spell "${slug}"`);
      else s.classes.push(cls);
    }
  }
  for (const s of spells) s.classes.sort((a, b) => a.localeCompare(b, 'pt-BR'));
  spells.sort((a, b) => a.level - b.level || a.name.localeCompare(b.name, 'pt-BR'));
  return { spells, classLists, errors };
}

/** Counts used by the report and the structural gate. */
export function summarize(spells, classLists) {
  const byLevel = Array(10).fill(0);
  for (const s of spells) byLevel[s.level]++;
  const listed = Object.values(classLists).flat();
  const known = new Set(spells.map((s) => s.slug));
  return {
    total: spells.length,
    byLevel,
    ritual: spells.filter((s) => s.ritual).length,
    concentration: spells.filter((s) => s.concentration).length,
    classes: Object.keys(classLists).sort((a, b) => a.localeCompare(b, 'pt-BR')),
    classEntries: listed.length,
    classEntriesMatched: listed.filter((x) => known.has(x)).length,
    withoutClass: spells.filter((s) => s.classes.length === 0).map((s) => s.slug),
    withHigherLevels: spells.filter((s) => s.higherLevels).length,
    withDamageDice: spells.filter((s) => s.suggestedDamageDice).length,
    withDamageType: spells.filter((s) => s.suggestedDamageType).length,
    attack: spells.filter((s) => s.attackKind === 'attack').length,
    save: spells.filter((s) => s.attackKind === 'save').length,
  };
}

/** Compares a summary to EXPECTED. Returns a list of { check, expected, actual, pass }. */
export function checkExpected(summary, expected = EXPECTED) {
  const rows = [
    { check: 'total', expected: expected.total, actual: summary.total },
    ...expected.byLevel.map((n, lvl) => ({ check: `nível ${lvl}`, expected: n, actual: summary.byLevel[lvl] })),
    { check: 'ritual', expected: expected.ritual, actual: summary.ritual },
    { check: 'concentração', expected: expected.concentration, actual: summary.concentration },
    { check: 'classes', expected: expected.classes.join(', '), actual: summary.classes.join(', ') },
    { check: 'nomes das listas por classe encontrados', expected: summary.classEntries, actual: summary.classEntriesMatched },
  ];
  return rows.map((r) => ({ ...r, pass: r.expected === r.actual }));
}
