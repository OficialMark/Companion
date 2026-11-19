import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, writeFileSync, mkdirSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { tokensEn, tokensPt, diffTokens, validateEntry, validateAll, mergeBatches, requiredTerms, orderLike } from './validate-translation.mjs';

// Real source entries (from translations/source.en.json, units already converted).
const POTION = {
  name: 'Potion of Healing',
  text:
    'This potion is a magic item. As a Bonus Action, you can drink it or administer it to another creature within 1.5 meters of yourself. ' +
    'The creature that drinks the magical red fluid in this vial regains 2d4 + 2 Hit Points.',
};
const POTION_PT = {
  name: 'Poção de Cura',
  text:
    'Esta poção é um item mágico. Como uma ação bônus, você pode bebê-la ou administrá-la a outra criatura a até 1,5 metro de você. ' +
    'A criatura que bebe o fluido vermelho mágico deste frasco recupera 2d4 + 2 pontos de vida.',
};

const RING = { name: 'Ring of Protection', text: 'Ring\n\nYou gain a +1 bonus to Armor Class and saving throws while wearing this ring.' };
const RING_PT = { name: 'Anel de Proteção', text: 'Anel\n\nVocê recebe +1 de bônus na Classe de Armadura e nos testes de resistência enquanto usar este anel.' };

test('number extraction: dice, decimals, thousands, units converted on the EN side', () => {
  assert.deepEqual(tokensEn('regains 2d4 + 2 Hit Points within 5 feet'), ['1.5', '2', '2d4']);
  assert.deepEqual(tokensEn('a d20 Test, DC 15, 1,000 GP'), ['1000', '15', '1d20']);
  assert.deepEqual(tokensPt('recupera 2d4 + 2 a até 1,5 metro'), ['1.5', '2', '2d4']);
  assert.deepEqual(tokensPt('um teste de d20, CD 15, 1.000 PO'), ['1000', '15', '1d20']);
  assert.deepEqual(tokensPt('1.5 metro'), ['1.5']);
  assert.deepEqual(tokensPt('o 3º nível'), ['3']);
  assert.deepEqual(diffTokens(['1', '2', '2'], ['2', '3']), { missing: ['1', '2'], extra: ['3'] });
});

test('a faithful translation passes', () => {
  assert.deepEqual(validateEntry('potion-of-healing', POTION, POTION_PT), []);
  assert.deepEqual(validateEntry('ring-of-protection', RING, RING_PT), []);
});

test('a changed or missing number fails', () => {
  const bad = { ...POTION_PT, text: POTION_PT.text.replace('2d4 + 2', '2d4 + 3') };
  const errors = validateEntry('potion-of-healing', POTION, bad);
  assert.equal(errors.length, 1);
  assert.match(errors[0], /potion-of-healing\.text: números diferentes do original \(faltam \[2\], sobram \[3\]\)/);
});

test('an unconverted distance fails (5 feet kept as 5)', () => {
  const bad = { ...POTION_PT, text: POTION_PT.text.replace('1,5 metro', '5 pés') };
  assert.match(validateEntry('potion-of-healing', POTION, bad).join('\n'), /faltam \[1\.5\], sobram \[5\]/);
});

test('English rule words left behind fail', () => {
  const bad = { ...RING_PT, text: RING_PT.text.replace('testes de resistência', 'saving throws') };
  const errors = validateEntry('ring-of-protection', RING, bad).join('\n');
  assert.match(errors, /palavra em inglês "saving throw"/);
  assert.match(errors, /"saving throw" deve aparecer como "teste de resistência"/);
  const feet = validateEntry('x', { name: 'X', text: 'within 9 meters' }, { name: 'X', text: 'a até 9 feet' }).join('\n');
  assert.match(feet, /palavra em inglês "feet"/);
});

test('glossary forms are required for rule terms and damage types', () => {
  assert.deepEqual(
    requiredTerms('take 2d10 Poison damage on a failed Constitution saving throw, with Advantage, Finesse property').map((t) => t[1]),
    ['teste de resistência', 'vantagem', 'Constituição', 'veneno', 'Acuidade'],
  );
  const en = { name: 'Dagger of Venom', text: 'The target takes 2d10 Poison damage.' };
  const free = { name: 'Adaga Peçonhenta', text: 'O alvo sofre 2d10 de dano tóxico.' };
  assert.match(validateEntry('dagger-of-venom', en, free).join('\n'), /"Poison damage" deve aparecer como "veneno"/);
  assert.deepEqual(validateEntry('dagger-of-venom', en, { ...free, text: 'O alvo sofre 2d10 de dano de veneno.' }), []);
});

test('empty fields and text presence must match the source', () => {
  assert.deepEqual(validateEntry('longsword', { name: 'Longsword', text: '' }, { name: 'Espada Longa', text: '' }), []);
  assert.match(validateEntry('longsword', { name: 'Longsword', text: '' }, { name: '', text: '' })[0], /name vazio/);
  assert.match(validateEntry('a', { name: 'A', text: 'Some text.' }, { name: 'A', text: '' }).join(), /text vazio/);
  assert.match(validateEntry('a', { name: 'A', text: null, textFrom: 'p' }, { name: 'A', text: 'x' }).join(), /text preenchido/);
  assert.deepEqual(validateEntry('a', { name: 'A', text: 'x' }, undefined), ['a: sem tradução']);
});

test('validateAll requires every index unless partial, and rejects unknown ones', () => {
  const source = { 'potion-of-healing': POTION, 'ring-of-protection': RING };
  const full = validateAll(source, { 'potion-of-healing': POTION_PT });
  assert.deepEqual(full.errors, ['ring-of-protection: sem tradução']);
  assert.equal(full.valid, 1);
  const partial = validateAll(source, { 'potion-of-healing': POTION_PT, ghost: { name: 'x', text: '' } }, { partial: true });
  assert.deepEqual(partial.errors, ['ghost: index não existe na fonte']);
});

test('mergeBatches merges NNN.json in order and flags duplicates', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'batches-'));
  mkdirSync(dir, { recursive: true });
  writeFileSync(join(dir, '001.json'), JSON.stringify({ a: { name: 'A', text: 'x' } }));
  writeFileSync(join(dir, '002.json'), JSON.stringify({ b: { name: 'B', text: '' }, a: { name: 'A2', text: 'y' } }));
  writeFileSync(join(dir, 'notes.txt'), 'ignored');
  const r = await mergeBatches(dir);
  assert.deepEqual(r.files, ['001.json', '002.json']);
  assert.deepEqual(r.errors, ['a: aparece em 001.json e em 002.json']);
  assert.deepEqual(Object.keys(r.merged), ['a', 'b']);
  assert.deepEqual(await mergeBatches(join(dir, 'missing')), { files: [], merged: null, errors: [] });
  assert.deepEqual(Object.keys(orderLike({ b: 1, a: 1 }, r.merged)), ['b', 'a']);
});
