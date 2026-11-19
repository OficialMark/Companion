import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
  feetToMeters,
  milesToKm,
  inchesToCm,
  poundsToKg,
  formatPt,
  metersTextPt,
  kgTextPt,
  convertUnitsInText,
  hasImperialUnits,
} from './units.mjs';

test('number conversions follow the PT book rules', () => {
  assert.equal(feetToMeters(5), 1.5);
  assert.equal(feetToMeters(30), 9);
  assert.equal(feetToMeters(15), 4.5);
  assert.equal(feetToMeters(150), 45);
  assert.equal(feetToMeters(600), 180);
  assert.equal(feetToMeters(1), 0.3);
  assert.equal(feetToMeters(1000), 300);
  assert.equal(milesToKm(1), 1.5);
  assert.equal(inchesToCm(3), 7.5);
  assert.equal(poundsToKg(3), 1.5);
  assert.equal(poundsToKg(55), 27.5);
  assert.equal(poundsToKg(0.25), 0.13);
});

test('PT text formatting uses a decimal comma', () => {
  assert.equal(formatPt(1.5), '1,5');
  assert.equal(formatPt(9), '9');
  assert.equal(metersTextPt(5), '1,5 m');
  assert.equal(metersTextPt(120), '36 m');
  assert.equal(kgTextPt(1), '0,5 kg');
});

test('convertUnitsInText rewrites real SRD phrases', () => {
  const cases = [
    ['within 30 feet of yourself', 'within 9 meters of yourself'],
    ['a 20-foot-radius Sphere', 'a 6-meter-radius Sphere'],
    ['a 10-Foot Pole', 'a 3-Meter Pole'],
    ['in a 15- foot radius', 'in a 4.5-meter radius'],
    ['Speed 30 ft., Swim 30 ft. (or 0 ft. for boats)', 'Speed 9 m, Swim 9 m (or 0 m for boats)'],
    ['reach 5 ft. *Hit:* 7 (2d6)', 'reach 1.5 m *Hit:* 7 (2d6)'],
    ['| 01–20 | 3 ft. × 5 ft. | 200 lb. | 80 feet |', '| 01–20 | 0.9 m × 1.5 m | 100 kg | 24 meters |'],
    ['range 20/60 feet', 'range 6/18 meters'],
    ['up to 1,000 feet away', 'up to 300 meters away'],
    ['1 foot', '0.3 meters'],
    ['up to 4 cubic feet of solid material', 'up to 0.108 cubic meters of solid material'],
    ['a 1-foot square surface', 'a 0.3-meter square surface'],
    ['3/4 of an inch in diameter', '1.9 centimeters in diameter'],
    ['12 inches long', '30 centimeters long'],
    ['within 1 mile of you', 'within 1.5 kilometers of you'],
    ['a 2-mile radius', 'a 3-kilometer radius'],
    ['It weighs 500 pounds', 'It weighs 250 kilograms'],
    ['a 1-pound weight', 'a 0.5-kilogram weight'],
    ['a number of feet equal to 5 times your Intelligence modifier', 'a number of meters equal to 1.5 times your Intelligence modifier'],
    ['holds 3 gallons, 1 pint and 4 ounces', 'holds 3 gallons, 1 pint and 4 ounces'],
    ['half a pound, an inch across', 'half a pound, an inch across'],
  ];
  for (const [en, expected] of cases) assert.equal(convertUnitsInText(en), expected, en);
});

test('convertUnitsInText is idempotent and leaves dice alone', () => {
  const s = 'Each creature within 20 feet takes 2d6 + 3 damage and is pushed 10 feet; it weighs 5 lb.';
  const once = convertUnitsInText(s);
  assert.equal(once, 'Each creature within 6 meters takes 2d6 + 3 damage and is pushed 3 meters; it weighs 2.5 kg');
  assert.equal(convertUnitsInText(once), once);
});

test('hasImperialUnits detects leftovers only when a number is attached', () => {
  assert.equal(hasImperialUnits('within 30 feet'), true);
  assert.equal(hasImperialUnits('a 10-foot pole'), true);
  assert.equal(hasImperialUnits('weighs 3 lb.'), true);
  assert.equal(hasImperialUnits('a number of feet'), false);
  assert.equal(hasImperialUnits('within 9 meters'), false);
});
