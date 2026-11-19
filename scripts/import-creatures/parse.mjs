// Pure parsing functions for Pocket DM creature pages (https://pocketdm.com.br/monstros/{slug}).
// No I/O, no network: everything here is deterministic over an HTML string, so it is unit-tested
// against saved fixtures (see parse.test.mjs).
//
// Strategy: the rendered stat block DOM (<section id="ficha"><article>) is the source of every
// Portuguese text field, because that is what the site shows. The Next.js RSC payload
// (self.__next_f.push) carries a structured English copy of the same monster; it is used for the
// English name, for the ruleset/source flags, and to cross-check the numbers parsed from the DOM.

// ---------------------------------------------------------------------------------------------
// Minimal HTML tree parser (enough for React server-rendered markup; no dependency needed).
// ---------------------------------------------------------------------------------------------

const VOID_TAGS = new Set([
  'area', 'base', 'br', 'col', 'embed', 'hr', 'img', 'input', 'link', 'meta', 'param', 'source', 'track', 'wbr',
]);
const RAW_TEXT_TAGS = new Set(['script', 'style', 'textarea']);

const NAMED_ENTITIES = { amp: '&', lt: '<', gt: '>', quot: '"', apos: "'", nbsp: ' ' };

export function decodeEntities(s) {
  return s.replace(/&(#x[0-9a-fA-F]+|#[0-9]+|[a-zA-Z]+);/g, (m, e) => {
    if (e[0] === '#') {
      const cp = e[1] === 'x' || e[1] === 'X' ? parseInt(e.slice(2), 16) : parseInt(e.slice(1), 10);
      return Number.isFinite(cp) ? String.fromCodePoint(cp) : m;
    }
    return Object.hasOwn(NAMED_ENTITIES, e) ? NAMED_ENTITIES[e] : m;
  });
}

function parseAttrs(src) {
  const attrs = {};
  const re = /([^\s=/>]+)(?:\s*=\s*(?:"([^"]*)"|'([^']*)'|([^\s>]+)))?/g;
  let m;
  while ((m = re.exec(src))) attrs[m[1].toLowerCase()] = decodeEntities(m[2] ?? m[3] ?? m[4] ?? '');
  return attrs;
}

/**
 * Parses HTML starting at `start` and returns the first element found there as a tree.
 * Stops as soon as that element is closed, so it can be pointed at a sub-tree of a large page.
 */
export function parseElementAt(html, start) {
  const root = { tag: '#root', attrs: {}, children: [], parent: null };
  let cur = root;
  let i = start;
  const tagRe = /<(\/?)([a-zA-Z][a-zA-Z0-9-]*)((?:[^>"']|"[^"]*"|'[^']*')*)>/y;
  while (i < html.length) {
    const lt = html.indexOf('<', i);
    if (lt === -1) break;
    if (lt > i && cur !== root) cur.children.push({ text: decodeEntities(html.slice(i, lt)) });
    if (html.startsWith('<!--', lt)) {
      const end = html.indexOf('-->', lt + 4);
      i = end === -1 ? html.length : end + 3;
      continue;
    }
    tagRe.lastIndex = lt;
    const m = tagRe.exec(html);
    if (!m) {
      // Stray '<' in text.
      if (cur !== root) cur.children.push({ text: '<' });
      i = lt + 1;
      continue;
    }
    i = tagRe.lastIndex;
    const closing = m[1] === '/';
    const tag = m[2].toLowerCase();
    if (closing) {
      let n = cur;
      while (n !== root && n.tag !== tag) n = n.parent;
      if (n === root) continue; // unmatched close tag: ignore
      cur = n.parent;
      if (cur === root) return root.children[0];
      continue;
    }
    const rawAttrs = m[3];
    const selfClosing = /\/\s*$/.test(rawAttrs);
    const el = { tag, attrs: parseAttrs(rawAttrs.replace(/\/\s*$/, '')), children: [], parent: cur };
    cur.children.push(el);
    if (RAW_TEXT_TAGS.has(tag)) {
      const end = html.toLowerCase().indexOf(`</${tag}`, i);
      const stop = end === -1 ? html.length : end;
      el.children.push({ text: html.slice(i, stop) });
      i = html.indexOf('>', stop) + 1 || html.length;
      if (cur === root) return el;
      continue;
    }
    if (VOID_TAGS.has(tag) || selfClosing) {
      if (cur === root) return el;
      continue;
    }
    cur = el;
  }
  return root.children[0] ?? null;
}

const isEl = (n) => n && typeof n.tag === 'string';

export function textOf(node, skip) {
  if (!node) return '';
  if (!isEl(node)) return node.text;
  if (skip && skip(node)) return '';
  if (node.tag === 'br') return '\n';
  if (RAW_TEXT_TAGS.has(node.tag) || node.tag === 'svg') return '';
  let s = '';
  for (const c of node.children) s += textOf(c, skip);
  return s;
}

export const collapse = (s) => s.replace(/\s+/g, ' ').trim();

/**
 * Plain text for descriptions: whitespace runs collapsed to one space inside each line, lines trimmed,
 * blank lines dropped, single "\n" kept between lines. The site separates spell groups
 * ("...Prestidigitação Arcana\n2/dia cada: ...") and paragraphs with real line breaks; flattening them
 * would glue unrelated sentences together.
 */
export const cleanText = (s) =>
  s
    .split(/\r?\n/)
    .map((l) => l.replace(/[^\S\n]+/g, ' ').trim())
    .filter(Boolean)
    .join('\n');

function elementChildren(node) {
  return node.children.filter(isEl);
}

// ---------------------------------------------------------------------------------------------
// RSC payload (Next.js App Router flight data).
// ---------------------------------------------------------------------------------------------

/** Concatenates every `self.__next_f.push([1,"..."])` string chunk of the page. */
export function extractFlightData(html) {
  const re = /self\.__next_f\.push\((\[[\s\S]*?\])\)<\/script>/g;
  let out = '';
  let m;
  while ((m = re.exec(html))) {
    try {
      const arr = JSON.parse(m[1]);
      if (typeof arr[1] === 'string') out += arr[1];
    } catch {
      /* non-JSON chunk: ignore */
    }
  }
  return out;
}

/** Extracts the JSON object that starts at `start` (a '{') by brace matching, string-aware. */
function sliceJsonObject(s, start) {
  let depth = 0;
  for (let j = start; j < s.length; j++) {
    const c = s[j];
    if (c === '"') {
      j++;
      while (j < s.length && s[j] !== '"') {
        if (s[j] === '\\') j++;
        j++;
      }
      continue;
    }
    if (c === '{') depth++;
    else if (c === '}' && --depth === 0) return s.slice(start, j + 1);
  }
  return null;
}

/** Returns the stat block props ({ monster, initialTranslation, slug, ... }) or null. */
export function extractMonsterProps(html) {
  const flight = extractFlightData(html);
  const at = flight.indexOf('{"monster":{');
  if (at === -1) return null;
  const json = sliceJsonObject(flight, at);
  if (!json) return null;
  try {
    return JSON.parse(json);
  } catch {
    return null;
  }
}

// ---------------------------------------------------------------------------------------------
// Index page.
// ---------------------------------------------------------------------------------------------

/** Every creature slug on /monstros, in page order, de-duplicated. Category links are excluded. */
export function extractIndexSlugs(html) {
  const re = /<a class="cb-in[^"]*" href="\/monstros\/([^"/?#]+)"/g;
  const seen = new Set();
  const out = [];
  let m;
  while ((m = re.exec(html))) {
    const slug = decodeURIComponent(m[1]);
    if (!seen.has(slug)) {
      seen.add(slug);
      out.push(slug);
    }
  }
  return out;
}

// ---------------------------------------------------------------------------------------------
// Field helpers.
// ---------------------------------------------------------------------------------------------

const SIZE_MAP = new Map(
  Object.entries({
    miúdo: 'Tiny', miúda: 'Tiny', minúsculo: 'Tiny', minúscula: 'Tiny',
    pequeno: 'Small', pequena: 'Small',
    médio: 'Medium', média: 'Medium',
    grande: 'Large',
    enorme: 'Huge',
    imenso: 'Gargantuan', imensa: 'Gargantuan', colossal: 'Gargantuan',
    // A few Monster a Day pages keep the English size word ("Medium (tiny) fera (enxame)").
    tiny: 'Tiny', small: 'Small', medium: 'Medium', large: 'Large', huge: 'Huge', gargantuan: 'Gargantuan',
  }),
);
const SIZE_ORDER = ['Tiny', 'Small', 'Medium', 'Large', 'Huge', 'Gargantuan'];
const EN_SIZES = new Set(SIZE_ORDER);

const ABILITY_KEYS = { FOR: 'str', DES: 'dex', CON: 'con', INT: 'int', SAB: 'wis', CAR: 'cha' };

/** Property labels of the stat block, mapped to the output field they feed. */
const PROPERTY_LABELS = new Map(
  Object.entries({
    'classe de armadura': 'ac',
    'pontos de vida': 'hp',
    movimento: 'speed',
    deslocamento: 'speed',
    iniciativa: 'initiative',
    salvaguardas: 'saves',
    'testes de resistência': 'saves',
    perícias: 'skills',
    'vulnerabilidades a dano': 'dmgVuln',
    'vulnerabilidades de dano': 'dmgVuln',
    vulnerabilidades: 'dmgVuln',
    'resistências a dano': 'dmgRes',
    'resistências de dano': 'dmgRes',
    resistências: 'dmgRes',
    'imunidades a dano': 'dmgImm',
    'imunidades de dano': 'dmgImm',
    'imunidades a condição': 'condImm',
    'imunidades a condições': 'condImm',
    'imunidades de condição': 'condImm',
    imunidades: 'immunities', // 2024 combined line: "dano; condições"
    sentidos: 'senses',
    idiomas: 'languages',
    'nível de desafio': 'cr',
    nd: 'cr',
    'bônus de proficiência': 'pb',
    equipamento: 'gear',
    fonte: 'source',
  }),
);

/** Section headings (<h2>) mapped to output arrays. */
const SECTION_MAP = new Map(
  Object.entries({
    ações: 'actions',
    'ações bônus': 'bonusActions',
    'ações bonus': 'bonusActions',
    reações: 'reactions',
    'ações lendárias': 'legendaryActions',
  }),
);

/** Removes "(30 ft)" / "(80/320 ft)" style imperial duplicates that follow metric values. */
export function dropFeet(s) {
  return s.replace(/\s*\((?:[\d.,/]+\s*(?:ft|pés)\.?(?:\s*,\s*)?)+\)/g, '');
}

const nullIfEmpty = (s) => {
  if (s == null) return null;
  const t = collapse(s);
  return t === '' || t === '—' || t === '-' || t === '–' ? null : t;
};

const stripTrailingPeriod = (s) => (s == null ? s : s.replace(/\s*\.$/, ''));

function parseSignedInt(s) {
  const m = /([+\-−–]?)\s*(\d+)/.exec(s ?? '');
  if (!m) return null;
  const n = parseInt(m[2], 10);
  return m[1] && m[1] !== '+' ? -n : n;
}

/** "10 (5.900 XP)" -> { cr: "10", xp: 5900, pb: null }; also handles "XP 5.900; PB +4". */
export function parseChallenge(s) {
  const t = collapse(s ?? '');
  const crM = /^(\d+(?:\/\d+)?)/.exec(t);
  const xpM = /([\d.]+)\s*XP\b/i.exec(t) || /\bXP\s*([\d.]+)/i.exec(t);
  const pbM = /\b(?:PB|BP|Bônus de Proficiência)\s*\+?\s*(\d+)/i.exec(t);
  return {
    cr: crM ? crM[1] : null,
    xp: xpM ? parseInt(xpM[1].replace(/\./g, ''), 10) : null,
    pb: pbM ? parseInt(pbM[1], 10) : null,
  };
}

/** Proficiency bonus from challenge rating (SRD rule, CR 0 through 30). */
export function proficiencyFromCr(cr) {
  if (cr == null) return null;
  const v = cr.includes('/') ? 0 : parseInt(cr, 10);
  if (!Number.isFinite(v)) return null;
  return v < 5 ? 2 : 2 + Math.floor((v - 1) / 4);
}

/** "15 (armadura de couro, escudo)" -> { ac: 15, note: "armadura de couro, escudo" }. */
export function parseArmorClass(s) {
  const t = collapse(s ?? '');
  const m = /^(\d+)\s*(.*)$/.exec(t);
  if (!m) return { ac: null, note: null };
  let rest = m[2].trim();
  if (rest.startsWith('(') && rest.endsWith(')')) rest = rest.slice(1, -1).trim();
  return { ac: parseInt(m[1], 10), note: rest === '' ? null : rest };
}

/**
 * "150 (20d10 + 40)" -> { hp: 150, dice: "20d10 + 40" }.
 * The dice must be a well-formed expression. Junk after a complete expression (a stray "}" and
 * leaked markup on one page) is cut; a malformed expression ("12d1Q + 36") gives dice null.
 */
export function parseHitPoints(s) {
  const t = collapse(s ?? '');
  const m = /^(\d+)\s*(?:\((.*)\))?/.exec(t);
  if (!m) return { hp: null, dice: null, diceIssue: null };
  const hp = parseInt(m[1], 10);
  const raw = m[2] ? collapse(m[2]) : null;
  if (raw == null) return { hp, dice: null, diceIssue: null };
  const d = /^(\d+d\d+(?:\s*[+\-−]\s*\d+)?)(.*)$/.exec(raw);
  if (d && d[2] === '') return { hp, dice: d[1], diceIssue: null };
  if (d && /^[^\w]/.test(d[2]) && !/^\s*[+\-−]?\s*\d/.test(d[2]))
    return { hp, dice: d[1], diceIssue: `hit dice "${raw}" has trailing junk, kept "${d[1]}"` };
  return { hp, dice: null, diceIssue: `hit dice "${raw}" is malformed, set to null` };
}

function splitTopLevelCommas(t) {
  const parts = [];
  let depth = 0;
  let from = 0;
  for (let i = 0; i < t.length; i++) {
    if (t[i] === '(') depth++;
    else if (t[i] === ')') depth = Math.max(0, depth - 1);
    else if (t[i] === ',' && depth === 0) {
      parts.push(t.slice(from, i));
      from = i + 1;
    }
  }
  parts.push(t.slice(from));
  return parts.map((p) => p.trim());
}

const unbalanced = (s) => (s.match(/\(/g) ?? []).length !== (s.match(/\)/g) ?? []).length;

/** "Grande aberração, leal e mau" -> { size, sizes, type, alignment, alignmentIssue }. */
export function parseTypeLine(s) {
  const t = collapse(s ?? '');
  const parts = splitTopLevelCommas(t);
  const head = parts[0];
  let alignmentRaw = parts.length > 1 ? parts.slice(1).join(', ') : null;
  let alignmentIssue = null;
  // Malformed lines such as "humanoide (human,shapechanger), shapechanger), neutral evil":
  // the leftover ")" shows the tail belongs to the type; the alignment is the last segment.
  if (alignmentRaw && unbalanced(alignmentRaw)) alignmentRaw = parts[parts.length - 1];
  // A sentence leaked into the alignment slot (one Monster a Day page): not an alignment.
  if (alignmentRaw && (/[.:;]/.test(alignmentRaw) || alignmentRaw.split(' ').length > 14)) {
    alignmentIssue = `alignment slot holds text that is not an alignment: "${alignmentRaw.slice(0, 60)}"`;
    alignmentRaw = null;
  }
  const words = head.split(' ');
  const sizes = [];
  let k = 0;
  while (k < words.length) {
    const w = words[k].toLocaleLowerCase('pt-BR');
    const inner = /^\((.+)\)$/.exec(w)?.[1];
    if (SIZE_MAP.has(w)) {
      sizes.push(SIZE_MAP.get(w));
      k++;
    } else if (sizes.length && (w === 'ou' || w === 'e' || w === '/')) k++;
    else if (sizes.length && inner && SIZE_MAP.has(inner)) k++; // "Medium (tiny)": size of swarm members
    else break;
  }
  const type = words.slice(k).join(' ').trim();
  let alignment = nullIfEmpty(alignmentRaw);
  if (alignment) alignment = alignment[0].toLocaleUpperCase('pt-BR') + alignment.slice(1);
  // "Pequeno ou Médio" (2024 humanoids): keep the largest size shown.
  const largest = sizes.length
    ? sizes.reduce((a, b) => (SIZE_ORDER.indexOf(b) > SIZE_ORDER.indexOf(a) ? b : a))
    : null;
  return {
    size: largest,
    sizes,
    type: type ? type.toLocaleLowerCase('pt-BR') : null,
    alignment,
    alignmentIssue,
  };
}

/** True for the "(Grappled)" style English gloss span the site appends after condition links. */
function isConditionGloss(node) {
  if (!isEl(node) || node.tag !== 'span') return false;
  if (!/^\s*\(\s*[A-Za-z][A-Za-z' -]*\s*\)\s*$/.test(textOf(node))) return false;
  const siblings = node.parent?.children ?? [];
  const idx = siblings.indexOf(node);
  for (let j = idx - 1; j >= 0; j--) {
    const sib = siblings[j];
    if (!isEl(sib)) {
      if (sib.text.trim() === '') continue;
      return false;
    }
    return sib.tag === 'a' && /^\/condicoes\//.test(sib.attrs.href ?? '');
  }
  return false;
}

function firstMeaningfulChild(node) {
  for (const c of node.children) {
    if (isEl(c)) return c;
    if (c.text.trim() !== '') return c;
  }
  return null;
}

// ---------------------------------------------------------------------------------------------
// Creature page.
// ---------------------------------------------------------------------------------------------

/**
 * Parses one creature page.
 * @returns {{ creature?: object, skip?: string, warnings: string[] }}
 * @throws when the page has no recognisable stat block.
 */
export function parseCreaturePage(html, { slug, baseUrl = 'https://pocketdm.com.br' }) {
  const warnings = [];
  const fichaAt = html.indexOf('id="ficha"');
  if (fichaAt === -1) throw new Error('no stat block (section#ficha) in page');
  const articleAt = html.indexOf('<article', fichaAt);
  if (articleAt === -1) throw new Error('no <article> in section#ficha');
  const article = parseElementAt(html, articleAt);
  if (!article || article.tag !== 'article') throw new Error('could not parse stat block article');

  const props = extractMonsterProps(html);
  const mon = props?.monster ?? null;
  if (!mon) warnings.push('no RSC monster payload (English name and cross-checks unavailable)');

  const state = {
    name: null,
    edition: null,
    typeLine: null,
    imageSrc: null,
    seenHr: false,
    properties: {},
    unknownProperties: [],
    abilities: {},
    section: 'traits',
    unknownSections: [],
    entries: { traits: [], actions: [], bonusActions: [], reactions: [], legendaryActions: [] },
    legendaryIntro: null,
    droppedSections: {},
  };

  const descriptionOf = (p, strong) =>
    cleanText(textOf(p, (n) => n === strong || isConditionGloss(n)));

  function visit(node) {
    if (!isEl(node)) return;
    const { tag } = node;
    if (tag === 'footer' || tag === 'button' && !state.seenHr || tag === 'script' || tag === 'style') return;
    if (tag === 'hr') {
      state.seenHr = true;
      return;
    }
    if (tag === 'img' && !state.seenHr) {
      if (!state.imageSrc && /\btoken$/i.test(node.attrs.alt ?? '')) state.imageSrc = node.attrs.src ?? null;
      return;
    }
    if (tag === 'h1') {
      state.name = collapse(textOf(node));
      return;
    }
    if (tag === 'span' && !state.seenHr && state.name && /^\s*20(14|24)\s*$/.test(textOf(node))) {
      state.edition ??= textOf(node).trim();
      return;
    }
    if (tag === 'h2' || tag === 'h3') {
      const heading = collapse(textOf(node));
      const key = SECTION_MAP.get(heading.toLocaleLowerCase('pt-BR'));
      if (key) state.section = key;
      else {
        state.section = `?${heading}`;
        state.unknownSections.push(heading);
      }
      return;
    }
    // Ability score block: <div><div>FOR</div><div>21</div>...</div>
    if (tag === 'div') {
      const kids = elementChildren(node);
      if (kids.length >= 2 && kids[0].tag === 'div' && kids[1].tag === 'div') {
        const label = collapse(textOf(kids[0]));
        if (Object.hasOwn(ABILITY_KEYS, label)) {
          const v = parseSignedInt(textOf(kids[1]));
          state.abilities[ABILITY_KEYS[label]] = v;
          return;
        }
      }
    }
    if (tag === 'p') {
      handleParagraph(node);
      return;
    }
    if ((tag === 'ul' || tag === 'ol') && state.seenHr) {
      appendToLastEntry(cleanText(node.children.filter(isEl).map((li) => textOf(li, isConditionGloss)).join('\n')));
      return;
    }
    for (const c of node.children) visit(c);
  }

  function currentEntries() {
    if (state.section.startsWith('?')) {
      state.droppedSections[state.section.slice(1)] ??= [];
      return state.droppedSections[state.section.slice(1)];
    }
    return state.entries[state.section];
  }

  function appendToLastEntry(text) {
    if (!text) return;
    const list = currentEntries();
    const last = list[list.length - 1];
    if (last) last.description = cleanText(`${last.description}\n${text}`);
    else if (state.section === 'legendaryActions')
      state.legendaryIntro = cleanText(`${state.legendaryIntro ?? ''}\n${text}`);
    else warnings.push(`orphan paragraph in ${state.section}: ${text.slice(0, 60)}`);
  }

  function handleParagraph(p) {
    const first = firstMeaningfulChild(p);
    if (!state.seenHr) {
      if (state.name && !state.typeLine) state.typeLine = collapse(textOf(p));
      return;
    }
    if (isEl(first) && (first.tag === 'strong' || first.tag === 'b')) {
      const rawName = collapse(textOf(first));
      const label = rawName.replace(/[.:]\s*$/, '');
      // Property labels ("Classe de Armadura") never end with a period; entry names ("Anfíbio.") do.
      // The period decides, because some trait names equal a label (a trait called "Deslocamento.").
      const isEntryName = /\.\s*$/.test(rawName);
      const noEntriesYet = Object.values(state.entries).every((l) => l.length === 0);
      if (!isEntryName && state.section === 'traits' && noEntriesYet) {
        const propKey = PROPERTY_LABELS.get(label.toLocaleLowerCase('pt-BR'));
        if (propKey) state.properties[propKey] = collapse(textOf(p, (n) => n === first));
        else {
          state.unknownProperties.push(label);
          warnings.push(`unknown property label: ${label}`);
        }
        return;
      }
      currentEntries().push({ name: label, description: descriptionOf(p, first) });
      return;
    }
    // Paragraph without a bold name.
    const text = cleanText(textOf(p, isConditionGloss));
    if (!text) return;
    if (state.section === 'legendaryActions' && state.entries.legendaryActions.length === 0) {
      state.legendaryIntro = state.legendaryIntro ? `${state.legendaryIntro}\n${text}` : text;
      return;
    }
    appendToLastEntry(text);
  }

  for (const c of article.children) visit(c);

  if (!state.name) throw new Error('stat block has no <h1> name');

  // ---- Skip rules (restricted / non-SRD third-party content). ----
  const sourceText = state.properties.source ? collapse(state.properties.source) : null;
  if (/kobold press/i.test(sourceText ?? '') || /kobold press/i.test(mon?.source ?? '')) {
    return { skip: `restricted source: ${sourceText ?? mon.source}`, warnings };
  }
  const isMonsterADay = mon?.source === 'MAD' || /monster a day/i.test(sourceText ?? '');
  if (mon && mon.is_srd === false && !isMonsterADay) {
    return { skip: `restricted: not SRD (source ${mon.source ?? sourceText ?? '?'})`, warnings };
  }

  // ---- Field assembly. ----
  const P = state.properties;
  const typeInfo = parseTypeLine(state.typeLine ?? '');
  const ac = parseArmorClass(P.ac);
  const hp = parseHitPoints(P.hp);
  const ch = parseChallenge(P.cr);
  if (hp.diceIssue) warnings.push(hp.diceIssue);
  if (typeInfo.alignmentIssue) warnings.push(typeInfo.alignmentIssue);

  let size = typeInfo.size;
  if (!size && EN_SIZES.has(mon?.size)) {
    size = mon.size;
    warnings.push(`size taken from RSC payload (type line: "${state.typeLine}")`);
  }

  // 2024 combined "Imunidades" line: "dano; condições". Use the RSC payload to tell which part is which.
  let dmgImm = P.dmgImm ?? null;
  let condImm = P.condImm ?? null;
  if (P.immunities != null) {
    const parts = P.immunities.split(';').map((x) => x.trim()).filter(Boolean);
    if (parts.length >= 2) {
      dmgImm ??= parts[0];
      condImm ??= parts.slice(1).join('; ');
    } else if (mon && !mon.damage_immunities && mon.condition_immunities) condImm ??= parts[0] ?? null;
    else dmgImm ??= parts[0] ?? null;
  }

  const ruleset = state.edition ?? mon?.ruleset_version ?? null;
  let sourceBook;
  if (isMonsterADay) sourceBook = 'Monster a Day';
  else if (ruleset === '2024') sourceBook = 'SRD 5.2';
  else if (ruleset === '2014') sourceBook = 'SRD 5.1';
  else {
    sourceBook = null;
    warnings.push('could not determine sourceBook');
  }

  const abilities = {};
  for (const k of ['str', 'dex', 'con', 'int', 'wis', 'cha']) abilities[k] = state.abilities[k] ?? null;

  const image = state.imageSrc ? new URL(state.imageSrc, baseUrl).href : null;

  const creature = {
    slug,
    name: state.name,
    nameEn: mon?.name ? collapse(mon.name) : null,
    sourceBook,
    sourceUrl: `${baseUrl}/monstros/${slug}`,
    size,
    type: typeInfo.type,
    alignment: typeInfo.alignment,
    armorClass: ac.ac,
    armorClassNote: ac.note,
    hitPoints: hp.hp,
    hitDice: hp.dice,
    speed: nullIfEmpty(P.speed != null ? dropFeet(P.speed) : null),
    initiative: P.initiative != null ? parseSignedInt(P.initiative) : null,
    abilities,
    savingThrows: nullIfEmpty(P.saves),
    skills: nullIfEmpty(P.skills),
    damageVulnerabilities: nullIfEmpty(P.dmgVuln),
    damageResistances: nullIfEmpty(P.dmgRes),
    damageImmunities: nullIfEmpty(dmgImm),
    conditionImmunities: nullIfEmpty(condImm),
    senses: nullIfEmpty(P.senses != null ? stripTrailingPeriod(dropFeet(P.senses)) : null),
    languages: nullIfEmpty(P.languages != null ? stripTrailingPeriod(dropFeet(P.languages)) : null),
    challengeRating: ch.cr,
    experiencePoints: ch.xp,
    proficiencyBonus: ch.pb ?? (P.pb != null ? parseSignedInt(P.pb) : null) ?? proficiencyFromCr(ch.cr),
    traits: state.entries.traits,
    actions: state.entries.actions,
    bonusActions: state.entries.bonusActions,
    reactions: state.entries.reactions,
    legendaryActions: state.entries.legendaryActions,
    legendaryActionsIntro: state.legendaryIntro,
    imageUrl: image,
  };

  // ---- Cross-checks against the structured RSC payload. ----
  const PAYLOAD_SECTIONS = [
    ['traits', 'special_abilities'],
    ['actions', 'actions'],
    ['bonusActions', 'bonus_actions'],
    ['reactions', 'reactions'],
    ['legendaryActions', 'legendary_actions'],
  ];
  if (mon) {
    // The site occasionally renders an entry's English description although the payload carries its
    // PT translation. Detect it exactly (DOM text == English original) and use the translation.
    const tr = props.initialTranslation ?? {};
    for (const [field, key] of PAYLOAD_SECTIONS) {
      const src = mon[key];
      if (!Array.isArray(src) || src.length !== creature[field].length) continue;
      src.forEach((orig, i) => {
        const entry = creature[field][i];
        const pt = tr[key]?.[orig.name];
        if (
          typeof orig.desc === 'string' &&
          collapse(entry.description) === collapse(orig.desc) &&
          typeof pt === 'string' &&
          !/^\$/.test(pt) &&
          collapse(pt) !== collapse(entry.description)
        ) {
          entry.description = cleanText(pt);
          warnings.push(`${field}[${i}] "${entry.name}": page shows English description, used PT translation from payload`);
        }
      });
    }
  }
  if (mon) {
    const checks = [
      ['armorClass', creature.armorClass, mon.armor_class],
      ['hitPoints', creature.hitPoints, mon.hit_points],
      ['challengeRating', creature.challengeRating, mon.cr],
      ['experiencePoints', creature.experiencePoints, mon.xp],
      ...['str', 'dex', 'con', 'int', 'wis', 'cha'].map((k) => [k, abilities[k], mon[k]]),
    ];
    for (const [field, mine, theirs] of checks) {
      if (theirs != null && mine != null && String(mine) !== String(theirs))
        warnings.push(`${field}: page shows ${mine}, payload has ${theirs}`);
    }
    const payloadSizes = String(mon.size ?? '').split('/').filter((x) => EN_SIZES.has(x));
    if (payloadSizes.length && size && !payloadSizes.includes(size))
      warnings.push(`size: page shows ${size}, payload has ${mon.size}`);
    for (const [field, key] of PAYLOAD_SECTIONS) {
      const arr = mon[key];
      if (Array.isArray(arr) && arr.length !== creature[field].length)
        warnings.push(`${field}: page shows ${creature[field].length}, payload has ${arr.length}`);
    }
  }
  for (const s of state.unknownSections) warnings.push(`unmapped section dropped: ${s}`);

  return { creature, warnings };
}

// ---------------------------------------------------------------------------------------------
// Validation report.
// ---------------------------------------------------------------------------------------------

export const PASS_THRESHOLD = 0.98;

export function buildReport({ indexSlugs, creatures, failures, skipped, warnings }) {
  const count = (fn) => creatures.filter(fn).length;
  const tally = (key) => {
    const m = {};
    for (const c of creatures) m[c[key] ?? '(null)'] = (m[c[key] ?? '(null)'] ?? 0) + 1;
    return Object.fromEntries(Object.entries(m).sort((a, b) => b[1] - a[1]));
  };
  const fullCore = (c) =>
    c.hitPoints != null && c.armorClass != null && Object.values(c.abilities).every((v) => Number.isInteger(v));
  const attempted = creatures.length + failures.length + skipped.length;
  const complete = count(fullCore);
  const eligible = indexSlugs.length - skipped.length;
  const completeRatio = eligible > 0 ? complete / eligible : 0;
  return {
    totalIndexSlugs: indexSlugs.length,
    attempted,
    parsedOk: creatures.length,
    failed: failures,
    skipped,
    perSourceBook: tally('sourceBook'),
    perSize: tally('size'),
    missing: {
      hitPoints: count((c) => c.hitPoints == null),
      armorClass: count((c) => c.armorClass == null),
      abilities: count((c) => !Object.values(c.abilities).every((v) => Number.isInteger(v))),
      imageUrl: count((c) => c.imageUrl == null),
    },
    withLegendaryActions: count((c) => c.legendaryActions.length > 0),
    completeCore: complete,
    completeRatio,
    warnings,
    passed: attempted === indexSlugs.length && completeRatio >= PASS_THRESHOLD,
  };
}

export function renderReportMarkdown(r, { importedAt, outFile, outBytes, network } = {}) {
  const pct = (x) => `${(x * 100).toFixed(1)}%`;
  const table = (obj) =>
    ['| Value | Count |', '| --- | ---: |', ...Object.entries(obj).map(([k, v]) => `| ${k} | ${v} |`)].join('\n');
  const lines = [
    '# Pocket DM creature import: last run',
    '',
    `- Imported at: ${importedAt ?? '?'}`,
    outFile ? `- Output: \`${outFile.replace(/\\/g, '/').replace(/^.*\/backend\//, 'backend/')}\` (${outBytes} bytes)` : null,
    network ? `- Network: ${network.network} requests, ${network.cacheHits} cache hits, ${network.retries} retries` : null,
    `- Result: **${r.passed ? 'PASS' : 'FAIL'}** (target: 100% of index slugs attempted, >= ${pct(PASS_THRESHOLD)} with hitPoints, armorClass and all six abilities)`,
    '',
    '## Totals',
    '',
    '| Metric | Value |',
    '| --- | ---: |',
    `| Slugs on index | ${r.totalIndexSlugs} |`,
    `| Attempted | ${r.attempted} |`,
    `| Parsed OK | ${r.parsedOk} |`,
    `| Failed | ${r.failed.length} |`,
    `| Skipped (restricted) | ${r.skipped.length} |`,
    `| With HP + AC + 6 abilities | ${r.completeCore} (${pct(r.completeRatio)}) |`,
    `| With legendary actions | ${r.withLegendaryActions} |`,
    `| Missing hitPoints | ${r.missing.hitPoints} |`,
    `| Missing armorClass | ${r.missing.armorClass} |`,
    `| Missing any ability score | ${r.missing.abilities} |`,
    `| Missing imageUrl | ${r.missing.imageUrl} |`,
    '',
    '## Per source book',
    '',
    table(r.perSourceBook),
    '',
    '## Per size',
    '',
    table(r.perSize),
    '',
    '## Failures',
    '',
    r.failed.length ? r.failed.map((f) => `- \`${f.slug}\`: ${f.reason}`).join('\n') : 'None.',
    '',
    '## Skipped (restricted content)',
    '',
    r.skipped.length ? r.skipped.map((f) => `- \`${f.slug}\`: ${f.reason}`).join('\n') : 'None.',
    '',
    `## Warnings (${r.warnings.length})`,
    '',
    r.warnings.length ? r.warnings.map((w) => `- \`${w.slug}\`: ${w.warning}`).join('\n') : 'None.',
    '',
  ];
  return lines.filter((l) => l !== null).join('\n');
}
