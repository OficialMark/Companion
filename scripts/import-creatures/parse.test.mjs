// Gate tests for the Pocket DM parser. Run: node --test scripts/import-creatures/
// Fixtures are real pages saved from pocketdm.com.br on 2026-11-18:
//   goblin.html   SRD 5.1 (2014), fractional CR 1/4, AC with armor note
//   aboleth.html  SRD 5.2 (2024), legendary actions + intro, condition glosses
//   ahuizotl.html Monster a Day, no token image, languages shown as "—"
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import {
  parseCreaturePage,
  extractIndexSlugs,
  extractMonsterProps,
  parseTypeLine,
  parseHitPoints,
  parseArmorClass,
  parseChallenge,
  proficiencyFromCr,
  dropFeet,
  cleanText,
  buildReport,
} from './parse.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
const fixture = (name) => readFileSync(join(HERE, 'fixtures', `${name}.html`), 'utf8');
const parse = (slug) => parseCreaturePage(fixture(slug), { slug });

test('goblin: SRD 5.1 creature with fractional CR', () => {
  const { creature: c, warnings } = parse('goblin');
  assert.deepEqual(warnings, []);
  assert.equal(c.slug, 'goblin');
  assert.equal(c.name, 'Goblin');
  assert.equal(c.nameEn, 'Goblin');
  assert.equal(c.sourceBook, 'SRD 5.1');
  assert.equal(c.sourceUrl, 'https://pocketdm.com.br/monstros/goblin');
  assert.equal(c.size, 'Small');
  assert.equal(c.type, 'humanoide (goblinoide)');
  assert.equal(c.alignment, 'Neutro e mau');
  assert.equal(c.armorClass, 15);
  assert.equal(c.armorClassNote, 'armadura de couro, escudo');
  assert.equal(c.hitPoints, 7);
  assert.equal(c.hitDice, '2d6');
  assert.equal(c.speed, '9 m');
  assert.equal(c.initiative, 2);
  assert.deepEqual(c.abilities, { str: 8, dex: 14, con: 10, int: 10, wis: 8, cha: 8 });
  assert.equal(c.savingThrows, null);
  assert.equal(c.skills, 'Furtividade +6');
  assert.equal(c.senses, 'visão no escuro 18 m, Percepção Passiva 9');
  assert.equal(c.languages, 'Comum, Goblin');
  assert.equal(c.challengeRating, '1/4');
  assert.equal(c.experiencePoints, 50);
  assert.equal(c.proficiencyBonus, 2);
  assert.deepEqual(c.traits, [
    {
      name: 'Fuga Ágil',
      description: 'O goblin pode realizar a ação Desengajar ou Esconder como uma ação bônus em cada um de seus turnos.',
    },
  ]);
  assert.deepEqual(c.actions.map((a) => a.name), ['Cimitarra', 'Arco Curto']);
  assert.equal(
    c.actions[1].description,
    'Ataque com Arma à Distância: +4 para acertar, alcance 24/96 m (80/320 ft), um alvo. 5 (1d6 + 2) de dano perfurante.',
  );
  assert.deepEqual(c.bonusActions, []);
  assert.deepEqual(c.reactions, []);
  assert.deepEqual(c.legendaryActions, []);
  assert.equal(c.legendaryActionsIntro, null);
  assert.equal(c.imageUrl, 'https://pocketdm.com.br/api/token/MM/Goblin.webp?w=192');
});

test('aboleth: SRD 5.2 creature with legendary actions', () => {
  const { creature: c, warnings } = parse('aboleth');
  assert.deepEqual(warnings, []);
  assert.equal(c.name, 'Aboleth');
  assert.equal(c.nameEn, 'Aboleth');
  assert.equal(c.sourceBook, 'SRD 5.2');
  assert.equal(c.size, 'Large');
  assert.equal(c.type, 'aberração');
  assert.equal(c.alignment, 'Leal e mau');
  assert.equal(c.armorClass, 17);
  assert.equal(c.armorClassNote, null);
  assert.equal(c.hitPoints, 150);
  assert.equal(c.hitDice, '20d10 + 40');
  assert.equal(c.speed, '3 m, nado 12 m');
  assert.equal(c.initiative, 7);
  assert.deepEqual(c.abilities, { str: 21, dex: 9, con: 15, int: 18, wis: 15, cha: 18 });
  assert.equal(c.savingThrows, 'Des +3, Con +6, Int +8, Sab +6');
  assert.equal(c.skills, 'História +12, Percepção +10');
  assert.equal(c.damageImmunities, null);
  assert.equal(c.conditionImmunities, null);
  assert.equal(c.senses, 'visão no escuro 36 m, Percepção Passiva 20');
  assert.equal(c.languages, 'Dialeto Obscuro; telepatia 36 m');
  assert.equal(c.challengeRating, '10');
  assert.equal(c.experiencePoints, 5900);
  assert.equal(c.proficiencyBonus, 4);
  assert.deepEqual(c.traits.map((t) => t.name), [
    'Anfíbio',
    'Restauração Bruxológica',
    'Resistência Lendária (3/Dia, ou 4/Dia no Covil)',
    'Nuvem de Muco',
    'Telepatia Investigativa',
  ]);
  // Multi-paragraph trait keeps its line break.
  assert.match(c.traits[3].description, /a menos que esteja submerso\.\nEnquanto a criatura amaldiçoada/);
  assert.deepEqual(c.actions.map((a) => a.name), ['Ataque Múltiplo', 'Tentáculo', 'Consumir Memórias', 'Dominar Mente (2/Dia)']);
  // The "(Grappled)" English gloss after the condition link is not part of the text.
  assert.equal(
    c.actions[1].description,
    'Ataque Corpo a Corpo: +9, alcance 4,5 m (15 ft). 12 (2d6 + 5) de dano de concussão. Se o alvo for uma criatura Grande ou menor, ele fica com a condição Imobilizado (CD 14 para escapar) por um de quatro tentáculos.',
  );
  assert.deepEqual(c.legendaryActions, [
    { name: 'Chicotada', description: 'O aboleth realiza um ataque de Tentáculo.' },
    {
      name: 'Dreno Psíquico',
      description:
        'Se o aboleth mantiver pelo menos uma criatura com a condição Enfeitiçado ou Imobilizado, ele usa Consumir Memórias e recupera 5 (1d10) Pontos de Vida.',
    },
  ]);
  assert.equal(
    c.legendaryActionsIntro,
    'Usos de Ação Lendária: 3 (4 no Covil). Imediatamente após o turno de outra criatura, Aboleth pode gastar um uso para realizar uma das ações a seguir. Aboleth recupera todos os usos gastos no início de cada um de seus turnos.',
  );
  assert.equal(c.imageUrl, 'https://pocketdm.com.br/api/token/XMM/Aboleth.webp?w=192');
});

test('ahuizotl: Monster a Day creature', () => {
  const { creature: c, warnings } = parse('ahuizotl');
  assert.deepEqual(warnings, []);
  assert.equal(c.name, 'Ahuizotl');
  assert.equal(c.sourceBook, 'Monster a Day');
  assert.equal(c.size, 'Small');
  assert.equal(c.type, 'monstruosidade');
  assert.equal(c.alignment, 'Neutro e mau');
  assert.equal(c.armorClass, 15);
  assert.equal(c.hitPoints, 153);
  assert.equal(c.hitDice, '34d6 + 34');
  assert.equal(c.speed, '9 m, nado 12 m');
  assert.deepEqual(c.abilities, { str: 18, dex: 15, con: 13, int: 9, wis: 15, cha: 8 });
  assert.equal(c.languages, null, '"—" means no languages');
  assert.equal(c.challengeRating, '6');
  assert.equal(c.experiencePoints, 2300);
  assert.equal(c.proficiencyBonus, 3);
  assert.deepEqual(c.traits.map((t) => t.name), ['Emboscador', 'Anfíbio', 'Camuflagem Aquática', 'Afogar']);
  assert.deepEqual(c.actions.map((a) => a.name), ['Garra', 'Cauda']);
  assert.equal(c.imageUrl, null, 'Monster a Day pages without a token get null, not a placeholder');
});

test('every output object has exactly the seed keys, in order', () => {
  const keys = [
    'slug', 'name', 'nameEn', 'sourceBook', 'sourceUrl', 'size', 'type', 'alignment', 'armorClass',
    'armorClassNote', 'hitPoints', 'hitDice', 'speed', 'initiative', 'abilities', 'savingThrows', 'skills',
    'damageVulnerabilities', 'damageResistances', 'damageImmunities', 'conditionImmunities', 'senses',
    'languages', 'challengeRating', 'experiencePoints', 'proficiencyBonus', 'traits', 'actions',
    'bonusActions', 'reactions', 'legendaryActions', 'legendaryActionsIntro', 'imageUrl',
  ];
  for (const slug of ['goblin', 'aboleth', 'ahuizotl']) assert.deepEqual(Object.keys(parse(slug).creature), keys);
});

test('RSC payload is extracted from the flight data', () => {
  const props = extractMonsterProps(fixture('aboleth'));
  assert.equal(props.monster.id, 'aboleth-xmm-2024');
  assert.equal(props.monster.hit_points, 150);
  assert.equal(props.slug, 'aboleth');
});

// ---- Regression tests for defects found in the full run (synthetic minimal pages). ----

function page(body) {
  return `<html><body><section id="ficha"><article><div><img src="/api/token/X/Y.webp?w=192" alt="Y token"/>
<div><div><h1>Teste</h1><a><span>2014</span></a></div><p>Grande dragão, caótico e bom</p></div></div><hr/>
<div><p><strong>Classe de Armadura</strong> 18</p><p><strong>Pontos de Vida</strong> 100 (<button>10d10 + 50</button>)</p>
<p><strong>Movimento</strong> 12 m (40 ft), voo 24 m (80 ft)</p></div><hr/>
<div><div><div>FOR</div><div>20</div></div><div><div>DES</div><div>10</div></div><div><div>CON</div><div>18</div></div>
<div><div>INT</div><div>12</div></div><div><div>SAB</div><div>14</div></div><div><div>CAR</div><div>16</div></div></div><hr/>
<div><p><strong>Nível de Desafio</strong> 8 (3.900 XP)</p></div><hr/>${body}<footer>x</footer></article></section></body></html>`;
}

test('a trait named like a property label ("Deslocamento.") is a trait, not the speed line', () => {
  const html = page('<div><p><strong>Deslocamento<!-- -->.</strong> <span>A forma do dragão tremula.</span></p></div>');
  const { creature: c } = parseCreaturePage(html, { slug: 't' });
  assert.equal(c.speed, '12 m, voo 24 m');
  assert.deepEqual(c.traits, [{ name: 'Deslocamento', description: 'A forma do dragão tremula.' }]);
});

test('condition gloss spans are dropped only right after a condition link', () => {
  const html = page(
    '<div><p><strong>Olhar.</strong> <span>Fica </span><a href="/condicoes/caido">Caído</a><span> (<!-- -->Prone<!-- -->)</span>' +
      '<span> e sofre dano </span><span>(Fire)</span><span>.</span></p></div>',
  );
  const { creature: c } = parseCreaturePage(html, { slug: 't' });
  assert.equal(c.traits[0].description, 'Fica Caído e sofre dano (Fire).');
});

test('sections: bonus actions, reactions, legendary intro, and unknown sections are reported', () => {
  const html = page(
    '<h2>Ações</h2><div><p><strong>Mordida.</strong> <span>Ataque.</span></p></div>' +
      '<h2>Ações Bônus</h2><div><p><strong>Salto.</strong> <span>Pula.</span></p></div>' +
      '<h2>Reações</h2><div><p><strong>Aparar.</strong> <span>+2 CA.</span></p></div>' +
      '<h2>Ações Lendárias</h2><p>Pode realizar 3 ações.</p><div><p><strong>Golpe.</strong> <span>Ataca.</span></p></div>' +
      '<h2>Ações de Covil</h2><div><p><strong>Tremor.</strong> <span>Treme.</span></p></div>',
  );
  const { creature: c, warnings } = parseCreaturePage(html, { slug: 't' });
  assert.deepEqual(c.actions, [{ name: 'Mordida', description: 'Ataque.' }]);
  assert.deepEqual(c.bonusActions, [{ name: 'Salto', description: 'Pula.' }]);
  assert.deepEqual(c.reactions, [{ name: 'Aparar', description: '+2 CA.' }]);
  assert.deepEqual(c.legendaryActions, [{ name: 'Golpe', description: 'Ataca.' }]);
  assert.equal(c.legendaryActionsIntro, 'Pode realizar 3 ações.');
  assert.ok(warnings.includes('unmapped section dropped: Ações de Covil'));
  assert.equal(c.challengeRating, '8');
  assert.equal(c.proficiencyBonus, 3);
  assert.equal(c.size, 'Large');
  assert.equal(c.alignment, 'Caótico e bom');
});

test('a page without a stat block throws', () => {
  assert.throws(() => parseCreaturePage('<html><body>404</body></html>', { slug: 'x' }), /no stat block/);
});

test('index: only creature cards, de-duplicated, category links excluded', () => {
  const html =
    '<a href="/monstros/fora-do-srd">x</a><a href="/monstros/tamanho">x</a>' +
    '<a class="cb-in relative" href="/monstros/aboleth">A</a><a class="cb-in relative" href="/monstros/goblin">G</a>' +
    '<a class="cb-in relative" href="/monstros/aboleth">A</a>';
  assert.deepEqual(extractIndexSlugs(html), ['aboleth', 'goblin']);
});

test('parseTypeLine handles multi-size, swarms, malformed and leaked alignments', () => {
  assert.deepEqual(
    pick(parseTypeLine('Pequeno ou Médio humanoide (mago), neutro')),
    { size: 'Medium', type: 'humanoide (mago)', alignment: 'Neutro' },
  );
  assert.deepEqual(
    pick(parseTypeLine('Medium (tiny) fera (enxame), sem alinhamento')),
    { size: 'Medium', type: 'fera (enxame)', alignment: 'Sem alinhamento' },
  );
  assert.deepEqual(
    pick(parseTypeLine('Médio humanoide (human,shapechanger), shapechanger), neutral evil')),
    { size: 'Medium', type: 'humanoide (human,shapechanger)', alignment: 'Neutral evil' },
  );
  const leaked = parseTypeLine('Grande monstruosidade (yuan-ti), it is grappled (escape DC 15). Until this grapple ends.');
  assert.equal(leaked.alignment, null);
  assert.match(leaked.alignmentIssue, /not an alignment/);
  assert.equal(parseTypeLine('Médio humanoide (half-orc)').alignment, null);
  assert.equal(parseTypeLine('Imenso dragão, caótico e mau').size, 'Gargantuan');
  assert.equal(parseTypeLine('Miúdo fera, sem alinhamento').size, 'Tiny');
});

function pick({ size, type, alignment }) {
  return { size, type, alignment };
}

test('parseHitPoints validates the dice expression', () => {
  assert.deepEqual(parseHitPoints('150 (20d10 + 40)'), { hp: 150, dice: '20d10 + 40', diceIssue: null });
  assert.deepEqual(parseHitPoints('1 (1d4 - 1)'), { hp: 1, dice: '1d4 - 1', diceIssue: null });
  assert.deepEqual(parseHitPoints('7'), { hp: 7, dice: null, diceIssue: null });
  const junk = parseHitPoints('126 (12d12 + 48} Speed 40 ft. STR 21 (+5)');
  assert.equal(junk.hp, 126);
  assert.equal(junk.dice, '12d12 + 48');
  const bad = parseHitPoints('102 (12d1Q + 36)');
  assert.equal(bad.hp, 102);
  assert.equal(bad.dice, null);
  assert.match(bad.diceIssue, /malformed/);
});

test('armor class, challenge, proficiency, feet removal, text cleanup', () => {
  assert.deepEqual(parseArmorClass('17 (armadura natural)'), { ac: 17, note: 'armadura natural' });
  assert.deepEqual(parseArmorClass('12'), { ac: 12, note: null });
  assert.deepEqual(parseChallenge('1/8 (25 XP)'), { cr: '1/8', xp: 25, pb: null });
  assert.deepEqual(parseChallenge('0'), { cr: '0', xp: null, pb: null });
  assert.deepEqual(parseChallenge('21 (XP 33.000; PB +7)'), { cr: '21', xp: 33000, pb: 7 });
  assert.deepEqual(['0', '1/2', '4', '5', '8', '9', '12', '13', '17', '21', '25', '30'].map(proficiencyFromCr), [
    2, 2, 2, 3, 3, 4, 4, 5, 6, 7, 8, 9,
  ]);
  assert.equal(dropFeet('9 m (30 ft), escalada 9 m (30 ft)'), '9 m, escalada 9 m');
  assert.equal(dropFeet('alcance 24/96 m (80/320 ft)'), 'alcance 24/96 m');
  assert.equal(cleanText('  a   b \n\n  c\t d  '), 'a b\nc d');
});

test('report passes at 100% and fails below 98% or when slugs were not attempted', () => {
  const ok = {
    hitPoints: 1,
    armorClass: 1,
    abilities: { str: 1, dex: 1, con: 1, int: 1, wis: 1, cha: 1 },
    imageUrl: null,
    legendaryActions: [],
  };
  const bad = { ...ok, hitPoints: null };
  const slugs = Array.from({ length: 100 }, (_, i) => `s${i}`);
  const base = { indexSlugs: slugs, failures: [], skipped: [], warnings: [] };
  assert.equal(buildReport({ ...base, creatures: Array(100).fill(ok) }).passed, true);
  assert.equal(buildReport({ ...base, creatures: [...Array(98).fill(ok), ...Array(2).fill(bad)] }).passed, true);
  assert.equal(buildReport({ ...base, creatures: [...Array(97).fill(ok), ...Array(3).fill(bad)] }).passed, false);
  assert.equal(buildReport({ ...base, creatures: Array(99).fill(ok) }).passed, false, 'one slug never attempted');
});
