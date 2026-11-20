// Entry point for `node --test scripts/import-catalogs/` on Node 22+, which resolves the directory
// argument as a module through package.json "main". Node 20 scans the directory for *.test.mjs
// instead; this file does not match that pattern, so no test runs twice on either version.
import './parse-spells.test.mjs';
import './glossary.test.mjs';
import './units.test.mjs';
import './map-items.test.mjs';
import './validate-translation.test.mjs';
import './report.test.mjs';
import './spell-names.test.mjs';
