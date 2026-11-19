// last-run-report.md holds one section per importer, so running one importer keeps the other's
// last result. Sections are delimited by HTML comments.

import { readFile, writeFile } from 'node:fs/promises';

const HEADER = '# Relatório da última importação dos catálogos\n\nGerado por `import-spells.mjs` e `import-items.mjs`. Cada importador reescreve só a sua seção.\n';
const ORDER = ['spells', 'items'];

export function replaceSection(doc, name, body) {
  const sections = {};
  const re = /<!-- section:([a-z]+) -->\n([\s\S]*?)<!-- \/section:\1 -->/g;
  let m;
  while ((m = re.exec(doc))) sections[m[1]] = m[2];
  sections[name] = body.endsWith('\n') ? body : body + '\n';
  const names = [...ORDER.filter((n) => n in sections), ...Object.keys(sections).filter((n) => !ORDER.includes(n))];
  return HEADER + names.map((n) => `\n<!-- section:${n} -->\n${sections[n]}<!-- /section:${n} -->\n`).join('');
}

export async function updateReportSection(file, name, body) {
  let doc = '';
  try {
    doc = await readFile(file, 'utf8');
  } catch {
    /* first run */
  }
  await writeFile(file, replaceSection(doc, name, body), 'utf8');
}
