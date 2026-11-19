// Imperial -> metric conversion with the rules of the PT-BR books (research.md R7):
//   1 foot  = 0.3 m   (5 ft = 1.5 m)        1 mile  = 1.5 km
//   1 inch  = 2.5 cm                         1 pound = 0.5 kg
//   areas and volumes follow from 1 ft = 0.3 m (1 sq ft = 0.09 m², 1 cu ft = 0.027 m³)
// Liquid measures (gallon, pint, ounce) are NOT converted: the source often gives them as dice
// ("1d6 + 1 ounces"), so they keep their number and only the word is translated.
// Pure functions; applied by code, never by the translator.

const round = (n, places) => {
  const f = 10 ** places;
  return Math.round((n + Number.EPSILON) * f) / f;
};

export const feetToMeters = (ft) => round(ft * 0.3, 1);
export const milesToKm = (mi) => round(mi * 1.5, 1);
export const inchesToCm = (inch) => round(inch * 2.5, 1);
export const poundsToKg = (lb) => round(lb * 0.5, 2);
export const squareFeetToSquareMeters = (sqft) => round(sqft * 0.09, 2);
export const cubicFeetToCubicMeters = (cuft) => round(cuft * 0.027, 3);

/** PT number text: decimal comma, no thousands separator, no trailing zeros. 1.5 -> "1,5". */
export const formatPt = (n) => String(n).replace('.', ',');

/** "9 m", "1,5 m" from a value in feet. */
export const metersTextPt = (ft) => `${formatPt(feetToMeters(ft))} m`;
/** "1,5 kg" from a value in pounds. */
export const kgTextPt = (lb) => `${formatPt(poundsToKg(lb))} kg`;

// English number: 1,000 | 1000 | 1.5
const NUM = String.raw`(\d{1,3}(?:,\d{3})+|\d+(?:\.\d+)?)`;
const parseEn = (s) => Number(s.replace(/,/g, ''));

function keepCase(sample, word) {
  return sample[0] === sample[0].toUpperCase() && sample[0] !== sample[0].toLowerCase()
    ? word[0].toUpperCase() + word.slice(1)
    : word;
}

/** Unit word for the converted value: hyphenated compounds stay singular ("a 3-meter radius"). */
function unitWord(value, hyphen, singular, plural, sample) {
  return keepCase(sample, hyphen || value === 1 ? singular : plural);
}

/**
 * Rewrites the imperial measures inside an English text as metric ones, so a translator only
 * has to translate words and every number in the result can be checked 1:1 against the PT text.
 * Idempotent: running it on its own output changes nothing.
 */
export function convertUnitsInText(text) {
  let s = text;
  // "range 20/60 feet"
  s = s.replace(new RegExp(String.raw`${NUM}\/${NUM}(\s+)(feet|foot|ft\.)`, 'gi'), (_m, a, b, sp, u) =>
    u.toLowerCase() === 'ft.'
      ? `${feetToMeters(parseEn(a))}/${feetToMeters(parseEn(b))}${sp}m`
      : `${feetToMeters(parseEn(a))}/${feetToMeters(parseEn(b))}${sp}${keepCase(u, 'meters')}`,
  );
  // "3/4 of an inch"
  s = s.replace(new RegExp(String.raw`\b(\d+)\/(\d+)(\s+of\s+an)?\s+inch(es)?\b`, 'gi'), (_m, a, b) =>
    `${inchesToCm(Number(a) / Number(b))} centimeters`,
  );
  // "64 cubic feet", "1-cubic-foot", "1 square foot"
  s = s.replace(
    new RegExp(String.raw`${NUM}(\s*-\s*|\s+)(cubic|square)([- ])(feet|foot)\b`, 'gi'),
    (_m, n, sep, kind, sep2, u) => {
      const hyphen = sep.includes('-');
      const v = kind.toLowerCase() === 'cubic' ? cubicFeetToCubicMeters(parseEn(n)) : squareFeetToSquareMeters(parseEn(n));
      return `${v}${hyphen ? '-' : ' '}${kind}${sep2}${unitWord(v, hyphen, 'meter', 'meters', u)}`;
    },
  );
  // "a number of feet equal to 5 times ..." (the multiplier is a distance in feet)
  s = s.replace(/\b(feet)( equal to )(\d+)( times)/gi, (_m, u, mid, n, tail) => `${keepCase(u, 'meters')}${mid}${feetToMeters(Number(n))}${tail}`);
  // "30 feet", "10-foot pole", "15- foot radius", "30 ft."
  s = s.replace(new RegExp(String.raw`${NUM}(\s*-\s*|\s+)(feet|foot|ft\.)`, 'gi'), (_m, n, sep, u) => {
    const v = feetToMeters(parseEn(n));
    const hyphen = sep.includes('-');
    if (u.toLowerCase() === 'ft.') return `${v}${hyphen ? '-' : ' '}m`;
    return `${v}${hyphen ? '-' : ' '}${unitWord(v, hyphen, 'meter', 'meters', u)}`;
  });
  // miles
  s = s.replace(new RegExp(String.raw`${NUM}(\s*-\s*|\s+)(miles?)\b`, 'gi'), (_m, n, sep, u) => {
    const v = milesToKm(parseEn(n));
    const hyphen = sep.includes('-');
    return `${v}${hyphen ? '-' : ' '}${unitWord(v, hyphen, 'kilometer', 'kilometers', u)}`;
  });
  // inches
  s = s.replace(new RegExp(String.raw`${NUM}(\s*-\s*|\s+)(inch(?:es)?)\b`, 'gi'), (_m, n, sep, u) => {
    const v = inchesToCm(parseEn(n));
    const hyphen = sep.includes('-');
    return `${v}${hyphen ? '-' : ' '}${unitWord(v, hyphen, 'centimeter', 'centimeters', u)}`;
  });
  // pounds, "200 lb."
  s = s.replace(new RegExp(String.raw`${NUM}(\s*-\s*|\s+)(pounds?|lbs?\.?)(?![a-z])`, 'gi'), (_m, n, sep, u) => {
    const v = poundsToKg(parseEn(n));
    const hyphen = sep.includes('-');
    if (/^lb/i.test(u)) return `${v}${hyphen ? '-' : ' '}kg`;
    return `${v}${hyphen ? '-' : ' '}${unitWord(v, hyphen, 'kilogram', 'kilograms', u)}`;
  });
  return s;
}

/** True when the text still has an imperial length or weight measure with a number. */
export function hasImperialUnits(text) {
  return new RegExp(String.raw`\d\s*-?\s*(?:(?:cubic|square)[- ])?(?:feet|foot|ft\.|miles?|inch(?:es)?|pounds?|lbs?\.?)(?![a-z])`, 'i').test(text);
}
