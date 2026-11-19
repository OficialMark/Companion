import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { replaceSection } from './report.mjs';

test('run-tests.mjs imports every *.test.mjs (Node 22+ runs only that entry point)', () => {
  const dir = fileURLToPath(new URL('.', import.meta.url));
  const entry = readFileSync(new URL('./run-tests.mjs', import.meta.url), 'utf8');
  for (const f of readdirSync(dir).filter((n) => n.endsWith('.test.mjs'))) assert.ok(entry.includes(`'./${f}'`), f);
});

test('replaceSection keeps the other importer section and a fixed order', () => {
  const first = replaceSection('', 'items', '## Itens\nA');
  const both = replaceSection(first, 'spells', '## Magias\nB\n');
  assert.ok(both.indexOf('## Magias') < both.indexOf('## Itens'));
  const again = replaceSection(both, 'items', '## Itens\nC');
  assert.match(again, /## Magias\nB\n/);
  assert.match(again, /## Itens\nC\n/);
  assert.doesNotMatch(again, /## Itens\nA/);
  assert.equal((again.match(/<!-- section:items -->/g) ?? []).length, 1);
});
