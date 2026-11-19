import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { SAVED_ROLL_ICONS, SAVED_ROLL_ICON_LABEL, isSavedRollIcon } from '~/utils/savedRollIcons'

// Spec 011, FR-033 and T052: the frontend icon list is the backend's SavedRollIcons.All, same order.
// Vitest runs in frontend/ (pnpm --dir frontend test); under jsdom import.meta.url is not file://.
const csharp = readFileSync(resolve(process.cwd(), '../backend/src/Companion.Domain/Characters/SavedRollData.cs'), 'utf8')

function backendIcons() {
  const block = csharp.match(/class\s+SavedRollIcons[\s\S]*?All\s*=\s*\[([\s\S]*?)\];/)
  if (!block) throw new Error('SavedRollIcons.All not found in SavedRollData.cs')
  return [...block[1]!.matchAll(/"(lucide:[a-z0-9-]+)"/g)].map(match => match[1])
}

describe('saved roll icons', () => {
  it('match SavedRollIcons.All in the backend, in the same order', () => {
    const icons = backendIcons()
    expect(icons).toHaveLength(16)
    expect([...SAVED_ROLL_ICONS]).toEqual(icons)
  })

  it('every icon has a PT-BR name and the guard accepts only the list', () => {
    for (const icon of SAVED_ROLL_ICONS) expect(SAVED_ROLL_ICON_LABEL[icon]).toBeTruthy()
    expect(isSavedRollIcon('lucide:sword')).toBe(true)
    expect(isSavedRollIcon('lucide:rocket')).toBe(false)
  })
})
