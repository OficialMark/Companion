#!/usr/bin/env node
// Spell catalog importer: magias5e (https://avribacki.gitlab.io/magias5e) -> spells.magias5e.json.
//
// Usage: node scripts/import-catalogs/import-spells.mjs [--offline] [--cache-dir <dir>] [--out <file>] [--report <file>]
//
// Downloads spells.js and spells-by-class.js (2 requests, cached under .cache/), parses them as
// text with ./parse-spells.mjs and writes the seed only when every structural check passes.
// On any failure it exits 1 and leaves the existing seed untouched.
// The owner authorized the use and asked for no credit, so the seed has "attribution": null.

import { mkdir, writeFile } from 'node:fs/promises';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { getCached, stats, DEFAULT_CACHE_DIR } from './fetch-cache.mjs';
import { SPELLS_URL, SPELLS_BY_CLASS_URL } from './sources.mjs';
import { parseSpells, summarize, checkExpected } from './parse-spells.mjs';
import { updateReportSection } from './report.mjs';
import { loadSpellNames, withEnglishNames } from './spell-names.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = resolve(HERE, '..', '..');
const SEED_DIR = join(REPO_ROOT, 'backend', 'src', 'Companion.Infrastructure', 'Persistence', 'Seed');

function parseArgs(argv) {
  const args = {
    offline: false,
    cacheDir: DEFAULT_CACHE_DIR,
    out: join(SEED_DIR, 'spells.magias5e.json'),
    report: join(HERE, 'last-run-report.md'),
  };
  for (let i = 2; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--offline') args.offline = true;
    else if (a === '--cache-dir') args.cacheDir = resolve(argv[++i]);
    else if (a === '--out') args.out = resolve(argv[++i]);
    else if (a === '--report') args.report = resolve(argv[++i]);
    else if (a === '--help' || a === '-h') {
      console.log('node import-spells.mjs [--offline] [--cache-dir <dir>] [--out <file>] [--report <file>]');
      process.exit(0);
    } else throw new Error(`Unknown argument: ${a}`);
  }
  return args;
}

async function main() {
  const args = parseArgs(process.argv);
  const [spellsJs, byClassJs] = await Promise.all([
    getCached({
      url: SPELLS_URL,
      name: 'magias5e/spells.js',
      cacheDir: args.cacheDir,
      offline: args.offline,
      validate: (t) => t.includes('document.write(`') && t.includes('spell-item'),
    }),
    getCached({
      url: SPELLS_BY_CLASS_URL,
      name: 'magias5e/spells-by-class.js',
      cacheDir: args.cacheDir,
      offline: args.offline,
      validate: (t) => t.includes('spellByClass['),
    }),
  ]);

  const { spells, classLists, errors } = parseSpells(spellsJs, byClassJs);
  const summary = summarize(spells, classLists);
  const checks = checkExpected(summary);
  const failedChecks = checks.filter((c) => !c.pass);
  const passed = errors.length === 0 && failedChecks.length === 0;

  let outBytes = null;
  if (passed) {
    // No sourceUrl and no credit text: the author asked not to be credited in the app.
    // "source" is only the seed key (SeedVersion "spells.magias5e"), never meant for display.
    const seed = {
      source: 'magias5e',
      attribution: null,
      importedAt: new Date().toISOString(),
      // English names from the fixed table (spec 012): the source has only the Portuguese name.
      spells: withEnglishNames(spells, loadSpellNames()),
    };
    const json = JSON.stringify(seed, null, 2) + '\n';
    await mkdir(dirname(args.out), { recursive: true });
    await writeFile(args.out, json, 'utf8');
    outBytes = Buffer.byteLength(json, 'utf8');
  }

  const md = renderSpellReport({ summary, checks, errors, passed, outFile: args.out, outBytes });
  await updateReportSection(args.report, 'spells', md);
  console.log(md);
  console.log(`network: ${stats.network} requests, ${stats.cacheHits} cache hits, ${stats.retries} retries`);
  if (!passed) {
    console.error('SPELLS IMPORT FAILED: seed not written');
    process.exitCode = 1;
  }
}

export function renderSpellReport({ summary, checks, errors, passed, outFile, outBytes }) {
  const rel = (p) => p.replace(REPO_ROOT, '').replace(/\\/g, '/').replace(/^\//, '');
  const lines = [
    '## Magias (magias5e)',
    '',
    `Resultado: **${passed ? 'PASS' : 'FAIL'}**${passed ? `, \`${rel(outFile)}\` escrito (${outBytes} bytes)` : ', seed não escrito'}.`,
    '',
    '| Verificação | Esperado | Obtido | Status |',
    '| --- | --- | --- | --- |',
    ...checks.map((c) => `| ${c.check} | ${c.expected} | ${c.actual} | ${c.pass ? 'PASS' : 'FAIL'} |`),
    '',
    'Informativo (sem valor esperado fixo):',
    '',
    `- Magias com "Em Níveis Superiores": ${summary.withHigherLevels}`,
    `- Com dado de dano sugerido: ${summary.withDamageDice}; com tipo de dano: ${summary.withDamageType}`,
    `- Tipo de rolagem: ataque ${summary.attack}, salvaguarda ${summary.save}, nenhum ${summary.total - summary.attack - summary.save}`,
    `- Entradas nas listas por classe: ${summary.classEntries}`,
    `- Magias sem nenhuma classe: ${summary.withoutClass.length}${summary.withoutClass.length ? ` (${summary.withoutClass.join(', ')})` : ''}`,
    '',
  ];
  if (errors.length) {
    lines.push('Erros estruturais:', '', ...errors.map((e) => `- ${e}`), '');
  }
  return lines.join('\n');
}

const isMain = process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url);
if (isMain) {
  main().catch((err) => {
    console.error(err);
    process.exit(1);
  });
}
