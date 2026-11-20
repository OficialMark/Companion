// The permanent "how-to-work" skill must stay identical to CLAUDE.md, and must stay wired in:
// the SessionStart hook loads it and CLAUDE.md requires it. Run: node --test scripts/how-to-work.test.mjs
import { test } from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { join } from 'node:path'

const root = join(fileURLToPath(new URL('.', import.meta.url)), '..')
const read = path => readFileSync(join(root, path), 'utf8').replace(/\r\n/g, '\n')

test('a skill tem o mesmo texto do CLAUDE.md', () => {
  const skill = read('.claude/skills/how-to-work/SKILL.md')
  const claude = read('CLAUDE.md')
  assert.match(skill, /^---\nname: how-to-work\ndescription: .+\n---\n/)
  assert.ok(skill.endsWith(claude), 'SKILL.md deve terminar com o CLAUDE.md inteiro; regenere a skill')
})

test('o CLAUDE.md exige a skill e as seções obrigatórias continuam lá', () => {
  const claude = read('CLAUDE.md')
  assert.match(claude, /invoque a skill `how-to-work`/)
  for (const section of ['## How to work', '## Task sizing', '## Branching', '## Fan-out + harsh critic', '## Self-rating', '## Safety'])
    assert.ok(claude.includes(section), `seção ausente: ${section}`)
})

test('o hook SessionStart carrega a skill em toda sessão e depois de compactar', () => {
  const settings = JSON.parse(read('.claude/settings.json'))
  const entries = settings.hooks?.SessionStart ?? []
  const entry = entries.find(item => item.hooks?.some(hook => hook.command?.includes('how-to-work')))
  assert.ok(entry, 'hook SessionStart com a skill how-to-work ausente')
  for (const source of ['startup', 'resume', 'clear', 'compact']) assert.match(entry.matcher, new RegExp(source))
})
