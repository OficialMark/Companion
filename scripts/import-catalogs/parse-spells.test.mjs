// Gate tests for the magias5e parser. Run: node --test scripts/import-catalogs/
// Fixtures are real cards cut from spells.js (2026-11-19): the first 5 spells, Alarme (ritual),
// Mísseis Mágicos, Bola de Fogo and Vidência (has tables); plus spells-by-class.js trimmed to them.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import {
  parseSpells,
  parseSpellsByClass,
  extractDocumentWrite,
  sanitizeHtml,
  splitHigherLevels,
  firstDice,
  rollKind,
  damageTypeNear,
  summarize,
  checkExpected,
  EXPECTED,
} from './parse-spells.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
const read = (p) => readFileSync(join(HERE, p), 'utf8');
const fixture = parseSpells(read('fixtures/spells.trimmed.js'), read('fixtures/spells-by-class.trimmed.js'));
const spell = (slug) => fixture.spells.find((s) => s.slug === slug);

test('fixture parses without structural errors', () => {
  assert.deepEqual(fixture.errors, []);
  assert.equal(fixture.spells.length, 9);
});

test('amizade: concentration, no damage, no roll, classes', () => {
  const s = spell('amizade');
  assert.equal(s.name, 'Amizade');
  assert.equal(s.level, 0);
  assert.equal(s.school, 'encantamento');
  assert.equal(s.ritual, false);
  assert.equal(s.castingTime, '1 ação');
  assert.equal(s.range, 'pessoal');
  assert.equal(s.components, 'S, M');
  assert.equal(s.duration, 'até 1 minuto');
  assert.equal(s.concentration, true);
  assert.equal(s.higherLevels, null);
  assert.deepEqual(s.classes, ['bardo', 'bruxo', 'feiticeiro', 'mago']);
  assert.equal(s.suggestedDamageDice, null);
  assert.equal(s.suggestedDamageType, null);
  assert.equal(s.attackKind, 'none');
  assert.equal(s.saveAbility, null);
  assert.match(s.descriptionHtml, /^<p><strong>Material<\/strong>: uma pequena quantidade de maquiagem/);
});

test('alarme: ritual flag from the icon', () => {
  const s = spell('alarme');
  assert.equal(s.ritual, true);
  assert.equal(s.level, 1);
  assert.equal(s.school, 'abjuração');
  assert.equal(s.castingTime, '1 minuto');
  assert.equal(s.duration, '8 horas');
  assert.equal(s.concentration, false);
  assert.deepEqual(s.classes, ['mago', 'patrulheiro']);
});

test('bola de fogo: save DES, 8d6 fire, higher levels split out', () => {
  const s = spell('bola de fogo');
  assert.equal(s.level, 3);
  assert.equal(s.school, 'evocação');
  assert.equal(s.range, '45 metros');
  assert.equal(s.suggestedDamageDice, '8d6');
  assert.equal(s.suggestedDamageType, 'fire');
  assert.equal(s.attackKind, 'save');
  assert.equal(s.saveAbility, 'DES');
  assert.equal(
    s.higherLevels,
    'Quando você conjurar essa magia usando um espaço de magia de 4° nível ou superior, o dano aumenta em 1d6 para cada nível do espaço acima do 3°.',
  );
  assert.doesNotMatch(s.descriptionHtml, /Níveis Superiores/);
  assert.deepEqual(s.classes, ['feiticeiro', 'mago']);
});

test('chicote de espinhos: spell attack, 1d6 piercing, hyphen line wrap rejoined', () => {
  const s = spell('chicote de espinhos');
  assert.equal(s.attackKind, 'attack');
  assert.equal(s.saveAbility, null);
  assert.equal(s.suggestedDamageDice, '1d6');
  assert.equal(s.suggestedDamageType, 'piercing');
  assert.match(s.descriptionHtml, /ataque corpo-a-corpo com magia/);
});

test('chama sagrada: radiant save; mísseis mágicos: force with no roll', () => {
  assert.deepEqual(
    [spell('chama sagrada').suggestedDamageType, spell('chama sagrada').attackKind, spell('chama sagrada').saveAbility],
    ['radiant', 'save', 'DES'],
  );
  const mm = spell('mísseis mágicos');
  assert.deepEqual([mm.suggestedDamageDice, mm.suggestedDamageType, mm.attackKind], ['1d4', 'force', 'none']);
});

test('bordão místico: "um d8" normalizes to 1d8, no damage type in that sentence', () => {
  const s = spell('bordão místico');
  assert.equal(s.suggestedDamageDice, '1d8');
  assert.equal(s.suggestedDamageType, null);
});

test('vidência: tables become rows, save SAB', () => {
  const s = spell('vidência');
  assert.equal(s.attackKind, 'save');
  assert.equal(s.saveAbility, 'SAB');
  assert.match(s.descriptionHtml, /<p><strong>Conhecimento \| Modificador de Resistência<\/strong><\/p>/);
  assert.match(s.descriptionHtml, /<p>Segunda mão \(você ouviu falar do alvo\) \| \+5<\/p>/);
  assert.doesNotMatch(s.descriptionHtml, /<t[a-z]/);
});

test('description HTML only uses the allowlist and has no attributes', () => {
  for (const s of fixture.spells) {
    for (const tag of s.descriptionHtml.match(/<[^>]+>/g) ?? []) {
      assert.match(tag, /^<\/?(p|strong|em|ul|li|br)>$/, `${s.slug}: ${tag}`);
    }
  }
});

test('sanitizeHtml drops unknown tags, attributes and scripts-as-text', () => {
  assert.equal(
    sanitizeHtml('<p class="x" onclick="alert(1)">a <b>b</b> <i>c</i><script>evil()</script> <img src=x></p>'),
    '<p>a <strong>b</strong> <em>c</em> evil()</p>',
  );
  assert.equal(sanitizeHtml('<p>1 &lt; 2 &amp; <strong> x </strong>y</p>'), '<p>1 &lt; 2 &amp; <strong>x</strong> y</p>');
  assert.equal(sanitizeHtml('<ul>\n<li> um </li>\n<li>dois<br/>três</li>\n</ul>'), '<ul><li>um</li><li>dois<br>três</li></ul>');
});

test('splitHigherLevels keeps text after the label', () => {
  const r = splitHigherLevels('<p>Corpo.</p><p><strong>Em Níveis Superiores</strong>: mais 1d6.</p>');
  assert.deepEqual(r, { descriptionHtml: '<p>Corpo.</p>', higherLevels: 'mais 1d6.' });
});

test('dice, damage type and roll kind helpers', () => {
  assert.equal(firstDice('sofre 10d10 de dano').dice, '10d10');
  assert.equal(firstDice('o dado se torna um d12').dice, '1d12');
  assert.equal(firstDice('sem dados aqui, d e 12'), null);
  assert.equal(damageTypeNear('sofre 2d8 de dano gélido. Outra frase com fogo.', 6), 'cold');
  assert.equal(damageTypeNear('sofre 2d8 de dano. Depois, dano de fogo.', 6), null);
  assert.equal(damageTypeNear('sofre 4d6 de dano de concussão', 6), 'bludgeoning');
  assert.deepEqual(rollKind('faça um teste de resistência de Constituição'), { attackKind: 'save', saveAbility: 'CON' });
  assert.deepEqual(rollKind('realize um ataque à distância com magia'), { attackKind: 'attack', saveAbility: null });
  assert.deepEqual(rollKind('o efeito pode acabar com magia'), { attackKind: 'none', saveAbility: null });
  assert.deepEqual(rollKind('teste de resistência de Vontade'), { attackKind: 'none', saveAbility: null });
});

test('extractDocumentWrite refuses interpolations and missing wrappers', () => {
  assert.throws(() => extractDocumentWrite('var x = 1'), /wrapper not found/);
  assert.throws(() => extractDocumentWrite('document.write(`<div>${alert(1)}</div>`)'), /\$\{/);
});

test('parseSpellsByClass reads the lists as text', () => {
  const lists = parseSpellsByClass('spellByClass = {}\nspellByClass["mago"] = [\n"a",\n\n"b c",\n]\n');
  assert.deepEqual(lists, { mago: ['a', 'b c'] });
});

test('an unknown spell in a class list is a structural error', () => {
  const r = parseSpells(read('fixtures/spells.trimmed.js'), 'spellByClass["mago"] = ["não existe"]');
  assert.deepEqual(r.errors, ['class mago lists unknown spell "não existe"']);
});

test('a card missing a meta field is a structural error', () => {
  const broken = read('fixtures/spells.trimmed.js').replace('<img src="img/range.svg">', '<img src="img/unknown-icon.svg">');
  const r = parseSpells(broken, read('fixtures/spells-by-class.trimmed.js'));
  assert.equal(r.errors[0], 'amizade: unknown meta icon unknown-icon');
  // The broken card is dropped, so the class lists that name it fail too.
  assert.ok(r.errors.slice(1).every((e) => e.endsWith('unknown spell "amizade"')));
});

const CACHE = join(HERE, '.cache', 'magias5e');
const haveCache = existsSync(join(CACHE, 'spells.js')) && existsSync(join(CACHE, 'spells-by-class.js'));

test('full source matches the expected totals', { skip: haveCache ? false : 'cache missing: run import-spells.mjs once to download' }, () => {
  const r = parseSpells(readFileSync(join(CACHE, 'spells.js'), 'utf8'), readFileSync(join(CACHE, 'spells-by-class.js'), 'utf8'));
  assert.deepEqual(r.errors, []);
  const summary = summarize(r.spells, r.classLists);
  assert.equal(summary.total, EXPECTED.total);
  assert.deepEqual(summary.byLevel, EXPECTED.byLevel);
  assert.equal(summary.ritual, 32);
  assert.equal(summary.concentration, 154);
  assert.equal(summary.classEntriesMatched, summary.classEntries);
  assert.deepEqual(summary.withoutClass, []);
  assert.ok(checkExpected(summary).every((c) => c.pass));
  for (const s of r.spells) {
    for (const tag of s.descriptionHtml.match(/<[^>]+>/g) ?? []) assert.match(tag, /^<\/?(p|strong|em|ul|li|br)>$/, s.slug);
    if (s.suggestedDamageDice) assert.match(s.suggestedDamageDice, /^\d+d\d+$/);
  }
});
