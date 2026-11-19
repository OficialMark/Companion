// Polite, cached downloader shared by the catalog importers. Node 20+ built-ins only.
//
// - At most 2 requests in flight, >= 300 ms between request starts (global pacing).
// - Generic User-Agent, no personal data.
// - Retries 429/5xx/network errors with exponential backoff (honours Retry-After).
// - Every body that passes `validate` is cached under `.cache/`; cached files are never re-fetched.
// - Downloaded content is treated as text data only: it is never executed or evaluated.

import { mkdir, readFile, writeFile, access } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = dirname(fileURLToPath(import.meta.url));
export const DEFAULT_CACHE_DIR = join(HERE, '.cache');
export const USER_AGENT = 'CompanionCatalogImporter/1.0';
export const MAX_CONCURRENCY = 2;
export const MIN_INTERVAL_MS = 300;
const MAX_ATTEMPTS = 5;
const REQUEST_TIMEOUT_MS = 60_000;

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

let nextSlot = 0;
let inFlight = 0;
const waiters = [];

async function acquire() {
  while (inFlight >= MAX_CONCURRENCY) await new Promise((r) => waiters.push(r));
  inFlight++;
  const now = Date.now();
  const slot = Math.max(now, nextSlot);
  nextSlot = slot + MIN_INTERVAL_MS;
  if (slot > now) await sleep(slot - now);
}

function release() {
  inFlight--;
  const w = waiters.shift();
  if (w) w();
}

export const stats = { network: 0, cacheHits: 0, retries: 0 };

async function exists(p) {
  try {
    await access(p);
    return true;
  } catch {
    return false;
  }
}

async function fetchText(url) {
  let lastError = 'unknown error';
  for (let attempt = 1; attempt <= MAX_ATTEMPTS; attempt++) {
    await acquire();
    stats.network++;
    let outcome;
    try {
      const res = await fetch(url, {
        headers: { 'User-Agent': USER_AGENT, Accept: '*/*' },
        signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS),
        redirect: 'follow',
      });
      if (res.status === 429 || res.status >= 500) {
        await res.body?.cancel();
        outcome = { retry: `HTTP ${res.status}`, retryAfter: res.headers.get('retry-after') };
      } else if (res.status !== 200) {
        await res.body?.cancel();
        outcome = { fatal: `HTTP ${res.status} for ${url}` };
      } else {
        outcome = { text: await res.text() };
      }
    } catch (err) {
      outcome = { retry: `network error: ${err.message}`, retryAfter: null };
    } finally {
      release();
    }
    if (outcome.fatal) throw new Error(outcome.fatal);
    if (outcome.text !== undefined) return outcome.text;
    lastError = outcome.retry;
    await backoff(attempt, outcome.retryAfter);
  }
  throw new Error(`Failed to download ${url}: ${lastError}`);
}

async function backoff(attempt, retryAfter) {
  stats.retries++;
  let ms = Math.min(30_000, 1000 * 2 ** (attempt - 1)) + Math.floor(Math.random() * 250);
  const ra = Number(retryAfter);
  if (Number.isFinite(ra) && ra > 0) ms = Math.max(ms, ra * 1000);
  await sleep(ms);
}

/**
 * Returns the cached body for `name`, or downloads `url`, validates it and caches it.
 * @param {{url: string, name: string, cacheDir?: string, offline?: boolean, validate?: (t: string) => boolean}} o
 */
export async function getCached({ url, name, cacheDir = DEFAULT_CACHE_DIR, offline = false, validate = () => true }) {
  const file = join(cacheDir, name);
  if (await exists(file)) {
    const text = await readFile(file, 'utf8');
    if (validate(text)) {
      stats.cacheHits++;
      return text;
    }
  }
  if (offline) throw new Error(`${name} is not in the cache (${file}) and --offline was given`);
  const text = await fetchText(url);
  if (!validate(text)) throw new Error(`Downloaded ${url} but its content failed validation; not cached`);
  await mkdir(dirname(file), { recursive: true });
  await writeFile(file, text, 'utf8');
  return text;
}
