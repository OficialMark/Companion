#!/usr/bin/env node
// Importer for the Pocket DM creature compendium (https://pocketdm.com.br/monstros).
//
// Usage: node scripts/import-creatures/import-pocketdm.mjs [--fetch-only] [--offline] [--cache-dir <dir>] [--out <file>]
//
// - Fetches the index, collects every creature slug, fetches every page (cached on disk),
//   parses each page with ./parse.mjs and writes the seed JSON + last-run-report.md.
// - Politeness: at most 2 requests in flight, >= 300 ms between request starts,
//   retries 429/5xx/network errors with exponential backoff (honours Retry-After).
// - Pages already in the cache are never re-fetched. Delete the cache dir to force a refresh.
// No npm dependencies: Node 20+ built-ins only.

import { mkdir, readFile, writeFile, access } from 'node:fs/promises';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { tmpdir } from 'node:os';
import { extractIndexSlugs, parseCreaturePage, buildReport, renderReportMarkdown } from './parse.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = resolve(HERE, '..', '..');
const BASE_URL = 'https://pocketdm.com.br';
const INDEX_URL = `${BASE_URL}/monstros`;
const USER_AGENT = 'CompanionImporter/1.0 (+authorized import)';
const MAX_CONCURRENCY = 2;
const MIN_INTERVAL_MS = 300;
const MAX_ATTEMPTS = 6;
const REQUEST_TIMEOUT_MS = 30_000;

export const ATTRIBUTION =
  'Tradução e curadoria © Pocket DM (pocketdm.com.br), usadas com autorização. ' +
  'Conteúdo SRD sob Creative Commons Attribution 4.0 (Wizards of the Coast).';

function parseArgs(argv) {
  const args = {
    fetchOnly: false,
    offline: false,
    cacheDir: process.env.POCKETDM_CACHE_DIR || join(tmpdir(), 'pocketdm-cache'),
    out: join(REPO_ROOT, 'backend', 'src', 'Companion.Infrastructure', 'Persistence', 'Seed', 'creatures.pocketdm.json'),
    report: join(HERE, 'last-run-report.md'),
  };
  for (let i = 2; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--fetch-only') args.fetchOnly = true;
    else if (a === '--offline') args.offline = true;
    else if (a === '--cache-dir') args.cacheDir = resolve(argv[++i]);
    else if (a === '--out') args.out = resolve(argv[++i]);
    else if (a === '--report') args.report = resolve(argv[++i]);
    else if (a === '--help' || a === '-h') {
      console.log('node import-pocketdm.mjs [--fetch-only] [--offline] [--cache-dir <dir>] [--out <file>] [--report <file>]');
      process.exit(0);
    } else throw new Error(`Unknown argument: ${a}`);
  }
  return args;
}

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

// Global request pacing: every request start reserves a slot >= MIN_INTERVAL_MS after the previous one.
let nextSlot = 0;
async function waitForSlot() {
  const now = Date.now();
  const slot = Math.max(now, nextSlot);
  nextSlot = slot + MIN_INTERVAL_MS;
  if (slot > now) await sleep(slot - now);
}

const stats = { network: 0, cacheHits: 0, retries: 0 };

/** Validates that fetched HTML is a real page worth caching (not an error shell). */
function looksValid(html, kind) {
  if (kind === 'index') return html.includes('data-ficha-id=');
  return html.includes('id="ficha"');
}

async function fetchWithRetry(url, kind) {
  let lastError;
  for (let attempt = 1; attempt <= MAX_ATTEMPTS; attempt++) {
    await waitForSlot();
    stats.network++;
    let res;
    try {
      res = await fetch(url, {
        headers: { 'User-Agent': USER_AGENT, Accept: 'text/html', 'Accept-Language': 'pt-BR' },
        signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS),
        redirect: 'follow',
      });
    } catch (err) {
      lastError = `network error: ${err.message}`;
      await backoff(attempt, null);
      continue;
    }
    if (res.status === 429 || res.status >= 500) {
      lastError = `HTTP ${res.status}`;
      await res.body?.cancel();
      await backoff(attempt, res.headers.get('retry-after'));
      continue;
    }
    if (res.status !== 200) {
      await res.body?.cancel();
      return { ok: false, reason: `HTTP ${res.status}` };
    }
    const html = await res.text();
    if (!looksValid(html, kind)) {
      // Next.js sometimes serves an error shell with 200; retry a couple of times, never cache it.
      lastError = 'HTTP 200 but no stat block in page (error shell, restricted or login-only)';
      if (attempt >= 3) break;
      await backoff(attempt, null);
      continue;
    }
    return { ok: true, html };
  }
  return { ok: false, reason: lastError };
}

async function backoff(attempt, retryAfter) {
  stats.retries++;
  let ms = Math.min(30_000, 1000 * 2 ** (attempt - 1)) + Math.floor(Math.random() * 250);
  const ra = Number(retryAfter);
  if (Number.isFinite(ra) && ra > 0) ms = Math.max(ms, ra * 1000);
  await sleep(ms);
}

async function exists(p) {
  try {
    await access(p);
    return true;
  } catch {
    return false;
  }
}

/** Returns cached HTML or fetches + caches it. */
async function getPage(url, cacheFile, kind, offline) {
  if (await exists(cacheFile)) {
    const html = await readFile(cacheFile, 'utf8');
    if (looksValid(html, kind)) {
      stats.cacheHits++;
      return { ok: true, html, cached: true };
    }
  }
  if (offline) return { ok: false, reason: 'not in cache (offline mode)' };
  const r = await fetchWithRetry(url, kind);
  if (r.ok) await writeFile(cacheFile, r.html, 'utf8');
  return r;
}

async function mapPool(items, limit, fn) {
  const results = new Array(items.length);
  let i = 0;
  const workers = Array.from({ length: limit }, async () => {
    while (i < items.length) {
      const idx = i++;
      results[idx] = await fn(items[idx], idx);
    }
  });
  await Promise.all(workers);
  return results;
}

async function main() {
  const args = parseArgs(process.argv);
  await mkdir(args.cacheDir, { recursive: true });
  console.log(`cache: ${args.cacheDir}`);

  const index = await getPage(INDEX_URL, join(args.cacheDir, '_index.html'), 'index', args.offline);
  if (!index.ok) throw new Error(`Could not load index ${INDEX_URL}: ${index.reason}`);
  const slugs = extractIndexSlugs(index.html);
  console.log(`index: ${slugs.length} creature slugs`);
  if (slugs.length === 0) throw new Error('Index parsed but no slugs found: page layout changed?');

  const started = Date.now();
  let done = 0;
  const pages = await mapPool(slugs, MAX_CONCURRENCY, async (slug) => {
    const url = `${BASE_URL}/monstros/${slug}`;
    const page = await getPage(url, join(args.cacheDir, `${slug}.html`), 'creature', args.offline);
    done++;
    if (done % 50 === 0 || done === slugs.length) {
      const secs = ((Date.now() - started) / 1000).toFixed(0);
      console.log(`  ${done}/${slugs.length} pages (${stats.network} network, ${stats.cacheHits} cached, ${stats.retries} retries, ${secs}s)`);
    }
    return { slug, url, ...page };
  });

  if (args.fetchOnly) {
    const failed = pages.filter((p) => !p.ok);
    console.log(`fetch-only: ${pages.length - failed.length} ok, ${failed.length} failed`);
    for (const f of failed) console.log(`  FAIL ${f.slug}: ${f.reason}`);
    return;
  }

  const creatures = [];
  const failures = [];
  const skipped = [];
  const warnings = [];
  for (const p of pages) {
    if (!p.ok) {
      failures.push({ slug: p.slug, reason: `fetch: ${p.reason}` });
      continue;
    }
    try {
      const r = parseCreaturePage(p.html, { slug: p.slug, baseUrl: BASE_URL });
      if (r.skip) {
        skipped.push({ slug: p.slug, reason: r.skip });
        continue;
      }
      for (const w of r.warnings) warnings.push({ slug: p.slug, warning: w });
      creatures.push(r.creature);
    } catch (err) {
      failures.push({ slug: p.slug, reason: `parse: ${err.message}` });
    }
  }
  creatures.sort((a, b) => (a.slug < b.slug ? -1 : a.slug > b.slug ? 1 : 0));

  const output = {
    source: 'Pocket DM',
    sourceUrl: INDEX_URL,
    attribution: ATTRIBUTION,
    importedAt: new Date().toISOString(),
    creatures,
  };
  const json = JSON.stringify(output, null, 2) + '\n';
  await mkdir(dirname(args.out), { recursive: true });
  await writeFile(args.out, json, 'utf8');

  const report = buildReport({ indexSlugs: slugs, creatures, failures, skipped, warnings });
  const md = renderReportMarkdown(report, {
    importedAt: output.importedAt,
    outFile: args.out,
    outBytes: Buffer.byteLength(json, 'utf8'),
    network: stats,
  });
  await writeFile(args.report, md, 'utf8');
  console.log('\n' + md);
  if (!report.passed) {
    console.error('VALIDATION FAILED: see report above');
    process.exitCode = 1;
  }
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
