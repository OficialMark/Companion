#!/usr/bin/env node
// Validator for the frozen PT-BR item translation (research.md R7, tasks.md T058).
//
// Pure function validateEntry(index, en, pt) + validateAll(source, translation), and a CLI:
//   node scripts/import-catalogs/validate-translation.mjs [--source <source.en.json>] [<translation.json> ...]
// With no translation files it validates translations/items.pt-BR.json, or every translations/batches/*.json
// when that file does not exist. A batch file only has to cover its own indices (use --partial implicitly).
//
// Rules for each translated entry { name, text }:
//   1. the index exists in the source and in the translation, and name is not empty;
//   2. the multiset of numbers and dice notations is the same in EN (after unit conversion) and PT,
//      checked separately for name and for text; "1,5" and "1.5" both read as 1.5;
//   3. every rule term of the EN text appears in PT in its glossary form (TEXT_TERMS, damage types,
//      weapon properties and masteries);
//   4. no English rule word from ENGLISH_BLACKLIST and no imperial unit survives in PT.

import { readFile, readdir } from 'node:fs/promises';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { convertUnitsInText, hasImperialUnits } from './units.mjs';
import { TEXT_TERMS, ENGLISH_BLACKLIST, DAMAGE_TYPES, WEAPON_PROPERTIES, WEAPON_MASTERIES } from './glossary.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
export const TRANSLATIONS_DIR = join(HERE, 'translations');

const DICE_RE = /(?<![\p{L}\d])(\d*)d(\d+)(?![\p{L}\d])/gu;

function sortedTokens(tokens) {
  return tokens.sort((a, b) => (a < b ? -1 : a > b ? 1 : 0));
}

/** Dice and numbers of an English text (units are converted first). "1,000" = 1000, "1.5" = 1.5. */
export function tokensEn(text) {
  let s = convertUnitsInText(String(text ?? ''));
  const tokens = [];
  s = s.replace(DICE_RE, (_m, n, f) => {
    tokens.push(`${n || '1'}d${f}`);
    return ' ';
  });
  for (const m of s.matchAll(/\d{1,3}(?:,\d{3})+(?:\.\d+)?|\d+(?:\.\d+)?/g)) tokens.push(String(Number(m[0].replace(/,/g, ''))));
  return sortedTokens(tokens);
}

/**
 * Dice and numbers of a PT text. Decimal comma ("1,5"); a dot followed by exactly three digits is a
 * thousands separator ("1.000"); any other dot between digits is read as a decimal point ("1.5").
 */
export function tokensPt(text) {
  let s = String(text ?? '');
  const tokens = [];
  s = s.replace(DICE_RE, (_m, n, f) => {
    tokens.push(`${n || '1'}d${f}`);
    return ' ';
  });
  for (const m of s.matchAll(/\d{1,3}(?:\.\d{3})+(?!\d)(?:,\d+)?|\d+(?:[.,]\d+)?/g)) {
    const raw = m[0];
    const value = /^\d{1,3}(?:\.\d{3})+(?:,\d+)?$/.test(raw) ? raw.replace(/\./g, '').replace(',', '.') : raw.replace(',', '.');
    tokens.push(String(Number(value)));
  }
  return sortedTokens(tokens);
}

/** Multiset difference report: what EN has that PT lacks and vice versa. */
export function diffTokens(en, pt) {
  const count = (arr) => arr.reduce((m, t) => m.set(t, (m.get(t) ?? 0) + 1), new Map());
  const a = count(en);
  const b = count(pt);
  const missing = [];
  const extra = [];
  for (const [t, n] of a) for (let i = 0; i < n - (b.get(t) ?? 0); i++) missing.push(t);
  for (const [t, n] of b) for (let i = 0; i < n - (a.get(t) ?? 0); i++) extra.push(t);
  return { missing, extra };
}

const escapeRe = (s) => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

/** One word of a term, accepting the EN/PT plural of that word (s, es, ão -> ões/ães, m -> ns). */
function inflect(token) {
  if (/ão$/i.test(token)) return `${escapeRe(token.slice(0, -2))}(?:ão|ões|ães)`;
  if (/m$/i.test(token)) return `${escapeRe(token.slice(0, -1))}(?:m|ns)`;
  return `${escapeRe(token)}(?:e?s)?`;
}

/** Whole-term regex, case-insensitive, each word in singular or plural, any whitespace between words. */
export const word = (w) => new RegExp(`(?<![\\p{L}])${w.split(/\s+/).map(inflect).join('\\s+')}(?![\\p{L}])`, 'iu');

/** [enPattern, ptForm, label] for every glossary term the EN text uses. */
export function requiredTerms(enText) {
  const out = [];
  for (const [en, ptForm] of Object.entries(TEXT_TERMS)) if (word(en).test(enText)) out.push([en, ptForm]);
  for (const v of Object.values(DAMAGE_TYPES)) if (word(`${v.en} damage`).test(enText)) out.push([`${v.en} damage`, v.pt]);
  for (const table of [WEAPON_PROPERTIES, WEAPON_MASTERIES]) {
    for (const v of Object.values(table)) if (word(`${v.en} property`).test(enText)) out.push([`${v.en} property`, v.pt]);
  }
  return out;
}

/** Errors (strings) for one entry; empty array = valid. `pt` may be undefined. */
export function validateEntry(index, en, pt) {
  const errors = [];
  if (!pt) return [`${index}: sem tradução`];
  if (typeof pt.name !== 'string' || !pt.name.trim()) errors.push(`${index}: name vazio`);
  const enHasText = typeof en.text === 'string' && en.text.trim() !== '';
  const ptText = typeof pt.text === 'string' ? pt.text : '';
  if (enHasText && !ptText.trim()) errors.push(`${index}: text vazio, mas o original tem texto`);
  if (!enHasText && ptText.trim()) errors.push(`${index}: text preenchido, mas o original não tem texto`);

  for (const [field, enValue, ptValue] of [
    ['name', en.name, pt.name ?? ''],
    ['text', en.text ?? '', ptText],
  ]) {
    const { missing, extra } = diffTokens(tokensEn(enValue), tokensPt(ptValue));
    if (missing.length || extra.length) {
      errors.push(`${index}.${field}: números diferentes do original (faltam [${missing.join(', ')}], sobram [${extra.join(', ')}])`);
    }
    for (const bad of ENGLISH_BLACKLIST) {
      if (word(bad).test(ptValue)) errors.push(`${index}.${field}: palavra em inglês "${bad}"`);
    }
    if (hasImperialUnits(ptValue)) errors.push(`${index}.${field}: unidade imperial no texto traduzido`);
  }

  const ptAll = `${pt.name ?? ''}\n${ptText}`;
  for (const [enTerm, ptForm] of requiredTerms(`${en.name}\n${en.text ?? ''}`)) {
    if (!word(ptForm).test(ptAll)) errors.push(`${index}: "${enTerm}" deve aparecer como "${ptForm}"`);
  }
  return errors;
}

/**
 * Validates a translation map against the source map ({ index: { name, text } }).
 * partial=true only checks the indices present in the translation (for a single batch).
 */
export function validateAll(source, translation, { partial = false } = {}) {
  const errors = [];
  for (const index of Object.keys(translation)) {
    if (!Object.hasOwn(source, index)) errors.push(`${index}: index não existe na fonte`);
  }
  const indices = partial ? Object.keys(translation).filter((i) => Object.hasOwn(source, i)) : Object.keys(source);
  let valid = 0;
  for (const index of indices) {
    const e = validateEntry(index, source[index], translation[index]);
    if (e.length === 0) valid++;
    errors.push(...e);
  }
  return { checked: indices.length, valid, errors };
}

/** Reads every translations/batches/*.json and merges them; duplicate indices are an error. */
export async function mergeBatches(dir = join(TRANSLATIONS_DIR, 'batches')) {
  let files;
  try {
    files = (await readdir(dir)).filter((f) => /^\d{3}\.json$/.test(f)).sort();
  } catch {
    return { files: [], merged: null, errors: [] };
  }
  const merged = {};
  const origin = {};
  const errors = [];
  for (const f of files) {
    let data;
    try {
      data = JSON.parse(await readFile(join(dir, f), 'utf8'));
    } catch (err) {
      errors.push(`${f}: JSON inválido (${err.message})`);
      continue;
    }
    for (const [index, entry] of Object.entries(data)) {
      if (Object.hasOwn(merged, index)) errors.push(`${index}: aparece em ${origin[index]} e em ${f}`);
      merged[index] = { name: entry?.name, text: entry?.text ?? null };
      origin[index] = f;
    }
  }
  return { files, merged: files.length ? merged : null, errors };
}

/** Orders a translation map like the source (stable diffs). */
export function orderLike(source, translation) {
  const out = {};
  for (const k of Object.keys(source)) if (Object.hasOwn(translation, k)) out[k] = translation[k];
  for (const k of Object.keys(translation)) if (!Object.hasOwn(out, k)) out[k] = translation[k];
  return out;
}

async function cli() {
  const args = process.argv.slice(2);
  let sourceFile = join(TRANSLATIONS_DIR, 'source.en.json');
  const files = [];
  for (let i = 0; i < args.length; i++) {
    if (args[i] === '--source') sourceFile = resolve(args[++i]);
    else files.push(resolve(args[i]));
  }
  const source = JSON.parse(await readFile(sourceFile, 'utf8')).items;
  let failed = false;
  const report = (label, r) => {
    console.log(`${label}: ${r.valid}/${r.checked} válidos, ${r.errors.length} erros`);
    for (const e of r.errors) console.log(`  - ${e}`);
    if (r.errors.length) failed = true;
  };
  if (files.length) {
    for (const f of files) report(f, validateAll(source, JSON.parse(await readFile(f, 'utf8')), { partial: true }));
  } else {
    let full = null;
    try {
      full = JSON.parse(await readFile(join(TRANSLATIONS_DIR, 'items.pt-BR.json'), 'utf8'));
    } catch {
      /* fall back to batches */
    }
    if (full) report('items.pt-BR.json', validateAll(source, full));
    else {
      const { files: batchFiles, merged, errors } = await mergeBatches();
      if (!merged) {
        console.log('Nenhuma tradução encontrada (items.pt-BR.json ou batches/NNN.json).');
        process.exitCode = 1;
        return;
      }
      const r = validateAll(source, merged, { partial: true });
      r.errors.unshift(...errors);
      report(`batches (${batchFiles.length} arquivos)`, r);
    }
  }
  if (failed) process.exitCode = 1;
}

const isMain = process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url);
if (isMain) {
  cli().catch((err) => {
    console.error(err);
    process.exit(1);
  });
}
