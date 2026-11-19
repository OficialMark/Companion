#!/usr/bin/env node
// Item catalog importer: SRD 5.2.1 (5e-bits/5e-srd-api, pinned commit) -> items.srd52.json.
//
// Usage: node scripts/import-catalogs/import-items.mjs [--source-only] [--offline] [--cache-dir <dir>] [--out <file>] [--report <file>]
//
// Steps:
//   1. download (or read from .cache/) the 2024/en Equipment, Magic Items, Weapon Properties and
//      Weapon Mastery files at SRD_SHA;
//   2. map them to CatalogItem with ./map-items.mjs and write translations/source.en.json and
//      translations/batch-plan.json (deterministic: same SHA, same files);
//   3. --source-only stops here (exit 0);
//   4. otherwise merge translations/batches/NNN.json into translations/items.pt-BR.json (when batches
//      exist), validate every entry with ./validate-translation.mjs and write the seed only when
//      everything passes. Any failure exits 1 and leaves the existing seed untouched.

import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { getCached, stats, DEFAULT_CACHE_DIR } from './fetch-cache.mjs';
import { SRD_FILES, SRD_SHA, SRD_REPO, SRD_DIR, SRD_ATTRIBUTION, srdUrl, srdCacheName } from './sources.mjs';
import { mapCatalog, sourceStats, planBatches } from './map-items.mjs';
import { validateAll, mergeBatches, orderLike, TRANSLATIONS_DIR } from './validate-translation.mjs';
import { WEAPON_PROPERTIES, WEAPON_MASTERIES } from './glossary.mjs';
import { updateReportSection } from './report.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = resolve(HERE, '..', '..');
const SEED_DIR = join(REPO_ROOT, 'backend', 'src', 'Companion.Infrastructure', 'Persistence', 'Seed');

/** Source counts this importer was written against (SRD_SHA). A different count fails the run. */
export const EXPECTED_SOURCE = Object.freeze({
  equipment: 182,
  weapons: 38,
  armor: 13,
  adventuringGear: 90,
  tools: 36,
  magicItems: 277,
  potions: 24,
});

function parseArgs(argv) {
  const args = {
    sourceOnly: false,
    offline: false,
    cacheDir: DEFAULT_CACHE_DIR,
    out: join(SEED_DIR, 'items.srd52.json'),
    report: join(HERE, 'last-run-report.md'),
    translationsDir: TRANSLATIONS_DIR,
  };
  for (let i = 2; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--source-only') args.sourceOnly = true;
    else if (a === '--offline') args.offline = true;
    else if (a === '--cache-dir') args.cacheDir = resolve(argv[++i]);
    else if (a === '--out') args.out = resolve(argv[++i]);
    else if (a === '--report') args.report = resolve(argv[++i]);
    else if (a === '--translations-dir') args.translationsDir = resolve(argv[++i]);
    else if (a === '--help' || a === '-h') {
      console.log('node import-items.mjs [--source-only] [--offline] [--cache-dir <dir>] [--out <file>] [--report <file>] [--translations-dir <dir>]');
      process.exit(0);
    } else throw new Error(`Unknown argument: ${a}`);
  }
  return args;
}

const isJsonArray = (t) => {
  try {
    return Array.isArray(JSON.parse(t));
  } catch {
    return false;
  }
};

async function loadSource(args) {
  const out = {};
  await Promise.all(
    Object.entries(SRD_FILES).map(async ([key, file]) => {
      const text = await getCached({
        url: srdUrl(file),
        name: srdCacheName(file),
        cacheDir: args.cacheDir,
        offline: args.offline,
        validate: isJsonArray,
      });
      out[key] = JSON.parse(text);
    }),
  );
  return out;
}

/** Structural checks on the raw source: counts and glossary coverage. */
export function checkSource(src) {
  const cat = (e, c) => e.equipment_categories.some((x) => x.index === c);
  const actual = {
    equipment: src.equipment.length,
    weapons: src.equipment.filter((e) => cat(e, 'weapons')).length,
    armor: src.equipment.filter((e) => cat(e, 'armor')).length,
    adventuringGear: src.equipment.filter((e) => cat(e, 'adventuring-gear')).length,
    tools: src.equipment.filter((e) => cat(e, 'tools')).length,
    magicItems: src.magicItems.length,
    potions: src.magicItems.filter((m) => m.equipment_category.index === 'potions').length,
  };
  const rows = Object.entries(EXPECTED_SOURCE).map(([k, v]) => ({ check: k, expected: v, actual: actual[k], pass: v === actual[k] }));
  const sameKeys = (a, b) => JSON.stringify([...a].sort()) === JSON.stringify([...b].sort());
  const props = src.weaponProperties.map((p) => p.index);
  const masteries = src.weaponMastery.map((p) => p.index);
  rows.push({ check: 'propriedades no glossário', expected: Object.keys(WEAPON_PROPERTIES).length, actual: props.length, pass: sameKeys(props, Object.keys(WEAPON_PROPERTIES)) });
  rows.push({ check: 'maestrias no glossário', expected: Object.keys(WEAPON_MASTERIES).length, actual: masteries.length, pass: sameKeys(masteries, Object.keys(WEAPON_MASTERIES)) });
  return rows;
}

async function readJson(file) {
  try {
    return JSON.parse(await readFile(file, 'utf8'));
  } catch {
    return null;
  }
}

/** Builds the seed items: CatalogItem fields in PT plus nameEn. */
export function buildSeedItems(items, translation) {
  return items.map((it) => {
    const t = translation[it.slug];
    const textKey = it.textFrom ?? it.slug;
    const { source: _source, textFrom: _textFrom, ...rest } = it;
    return {
      slug: it.slug,
      name: t.name.trim(),
      nameEn: it.nameEn,
      category: it.category,
      subcategory: it.subcategory,
      rarity: it.rarity,
      requiresAttunement: it.requiresAttunement,
      weight: it.weight,
      cost: it.cost,
      description: (translation[textKey]?.text ?? '').trim() || null,
      ...Object.fromEntries(Object.entries(rest).filter(([k]) => !['slug', 'nameEn', 'category', 'subcategory', 'rarity', 'requiresAttunement', 'weight', 'cost'].includes(k))),
    };
  });
}

async function main() {
  const args = parseArgs(process.argv);
  const src = await loadSource(args);
  const sourceChecks = checkSource(src);
  const { items, translationSource, warnings } = mapCatalog(src.equipment, src.magicItems);
  const stats_ = sourceStats(translationSource, items);
  const batches = planBatches(translationSource);

  await mkdir(args.translationsDir, { recursive: true });
  const sourceDoc = {
    source: `${SRD_REPO}@${SRD_SHA}`,
    path: SRD_DIR,
    note: 'Texto em inglês com as unidades já convertidas (pés -> metros, libras -> kg). Entradas com textFrom reaproveitam o texto do índice indicado e só traduzem o nome.',
    stats: stats_,
    items: translationSource,
  };
  await writeFile(join(args.translationsDir, 'source.en.json'), JSON.stringify(sourceDoc, null, 2) + '\n', 'utf8');
  await writeFile(
    join(args.translationsDir, 'batch-plan.json'),
    JSON.stringify({ maxItems: 40, maxWords: 4000, batches }, null, 2) + '\n',
    'utf8',
  );

  const sourcePass = sourceChecks.every((c) => c.pass);
  let translation = null;
  let validation = null;
  let mergeInfo = null;
  let outBytes = null;
  let passed = false;

  if (!args.sourceOnly) {
    const merged = await mergeBatches(join(args.translationsDir, 'batches'));
    mergeInfo = { files: merged.files.length, errors: merged.errors };
    if (merged.merged) {
      translation = orderLike(translationSource, merged.merged);
      await writeFile(join(args.translationsDir, 'items.pt-BR.json'), JSON.stringify(translation, null, 2) + '\n', 'utf8');
    } else {
      translation = await readJson(join(args.translationsDir, 'items.pt-BR.json'));
    }
    if (translation) {
      validation = validateAll(translationSource, translation);
      validation.errors.unshift(...merged.errors);
    }
    passed = sourcePass && Boolean(translation) && validation.errors.length === 0;
    if (passed) {
      const seed = {
        source: 'SRD 5.2.1',
        sourceUrl: `https://github.com/${SRD_REPO}/tree/${SRD_SHA}/${SRD_DIR}`,
        attribution: SRD_ATTRIBUTION,
        importedAt: new Date().toISOString(),
        items: buildSeedItems(items, translation),
      };
      const json = JSON.stringify(seed, null, 2) + '\n';
      await mkdir(dirname(args.out), { recursive: true });
      await writeFile(args.out, json, 'utf8');
      outBytes = Buffer.byteLength(json, 'utf8');
    }
  }

  const md = renderItemReport({ args, sourceChecks, stats: stats_, batches, warnings, translation, validation, mergeInfo, passed, outBytes });
  await updateReportSection(args.report, 'items', md);
  console.log(md);
  console.log(`network: ${stats.network} requests, ${stats.cacheHits} cache hits, ${stats.retries} retries`);
  if (!sourcePass || (!args.sourceOnly && !passed)) {
    console.error(args.sourceOnly ? 'ITEMS SOURCE CHECK FAILED' : 'ITEMS IMPORT FAILED: seed not written');
    process.exitCode = 1;
  }
}

export function renderItemReport({ args, sourceChecks, stats: s, batches, warnings, translation, validation, mergeInfo, passed, outBytes }) {
  const rel = (p) => p.replace(REPO_ROOT, '').replace(/\\/g, '/').replace(/^\//, '');
  const status = args.sourceOnly
    ? sourceChecks.every((c) => c.pass)
      ? '**SOURCE OK**, tradução pendente (modo `--source-only`, seed não escrito)'
      : '**FAIL** na checagem da fonte'
    : passed
      ? `**PASS**, \`${rel(args.out)}\` escrito (${outBytes} bytes)`
      : '**FAIL**, seed não escrito';
  const lines = [
    '## Itens (SRD 5.2.1, 5e-bits)',
    '',
    `Fonte: \`${SRD_REPO}\` no commit \`${SRD_SHA}\`, pasta \`${SRD_DIR}\`.`,
    '',
    `Resultado: ${status}.`,
    '',
    '| Verificação da fonte | Esperado | Obtido | Status |',
    '| --- | --- | --- | --- |',
    ...sourceChecks.map((c) => `| ${c.check} | ${c.expected} | ${c.actual} | ${c.pass ? 'PASS' : 'FAIL'} |`),
    '',
    'Catálogo mapeado:',
    '',
    `- Itens no catálogo: ${s.catalogItems} (${Object.entries(s.byCategory).map(([k, v]) => `${k} ${v}`).join(', ')})`,
    `- Com efeitos mecânicos: ${s.withEffects}; com bônus mágico (+N): ${s.withMagicBonus}`,
    `- Entradas a traduzir: ${s.translationEntries} (${s.entriesWithText} com texto), ${s.totalWords} palavras (${s.nameWords} em nomes, ${s.textWords} em textos)`,
    `- Lotes planejados: ${batches.length} (até 40 itens ou cerca de 4000 palavras cada), em \`translations/batch-plan.json\``,
    '',
  ];
  if (warnings.length) lines.push('Avisos do mapeamento:', '', ...warnings.map((w) => `- ${w}`), '');
  if (!args.sourceOnly) {
    lines.push(`Tradução: ${translation ? `${Object.keys(translation).length} entradas` : 'ausente (nem `items.pt-BR.json` nem `batches/NNN.json`)'}` + (mergeInfo?.files ? `, juntadas de ${mergeInfo.files} lotes` : '') + '.', '');
    if (validation) {
      lines.push(`Validação: ${validation.valid}/${validation.checked} entradas válidas, ${validation.errors.length} erros.`, '');
      const shown = validation.errors.slice(0, 200);
      if (shown.length) lines.push(...shown.map((e) => `- ${e}`), validation.errors.length > shown.length ? `- ... e mais ${validation.errors.length - shown.length}` : '', '');
    }
  } else {
    lines.push('Tradução: pendente (T057). Rode sem `--source-only` depois que os lotes existirem.', '');
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
