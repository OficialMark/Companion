import { expect, test, type Page } from '@playwright/test'

// Chrome opens its own context menu for any `contextmenu` event the page does not cancel. On
// Windows that event fires when the right button goes UP, after our right-click already opened the
// sheet (or a token menu) on top of the board, so the board's own `@contextmenu.prevent` never sees
// it. These tests record every real `contextmenu` event and whether the page cancelled it.

async function recordContextMenus(page: Page) {
  await page.evaluate(() => {
    const seen: Array<{ target: string; prevented: boolean }> = []
    ;(window as unknown as { __menus: typeof seen }).__menus = seen
    // Bubble phase on window: runs after every page handler, so it sees the final decision.
    window.addEventListener('contextmenu', event => {
      const el = event.target as Element
      seen.push({ target: `${el.tagName}.${el.getAttribute('class') ?? ''}`, prevented: event.defaultPrevented })
    })
  })
}
const menus = (page: Page) => page.evaluate(() => (window as unknown as { __menus: Array<{ target: string; prevented: boolean }> }).__menus)

async function mapPng(page: Page) {
  const base64 = await page.evaluate(() => {
    const canvas = document.createElement('canvas'); canvas.width = 400; canvas.height = 400
    const g = canvas.getContext('2d')!; g.fillStyle = '#2f6b4a'; g.fillRect(0, 0, 400, 400)
    return canvas.toDataURL('image/png').split(',')[1]!
  })
  return Buffer.from(base64, 'base64')
}

/** Right press and release as two steps, with the sheet/menu given time to appear in between,
 *  the way a person clicks (and the way Windows Chrome fires `contextmenu` on release). */
async function slowRightClick(page: Page, x: number, y: number, upAt?: { x: number; y: number }) {
  await page.mouse.move(x, y)
  await page.mouse.down({ button: 'right' })
  await page.waitForTimeout(600) // the sheet / token menu has time to open on top of the board
  if (upAt) await page.mouse.move(upAt.x, upAt.y, { steps: 5 })
  const before = (await menus(page)).length
  await page.mouse.up({ button: 'right' })
  await page.waitForTimeout(100)
  // Windows Chrome fires `contextmenu` on release (Linux/Mac on press). If this browser did not fire
  // one on release, fire it the way Windows would, at whatever is under the pointer now.
  if ((await menus(page)).length === before) {
    const point = upAt ?? { x, y }
    await page.evaluate(({ px, py }) => {
      const target = document.elementFromPoint(px, py) ?? document.body
      target.dispatchEvent(new MouseEvent('contextmenu', { bubbles: true, cancelable: true, button: 2, clientX: px, clientY: py }))
    }, { px: point.x, py: point.y })
  }
}

test('right-click on the board never leaves the browser menu free to open', async ({ page }) => {
  test.skip(test.info().project.name !== 'desktop', 'right-click is a mouse gesture')
  test.setTimeout(90_000)
  await page.setViewportSize({ width: 1280, height: 900 })
  await page.goto('/')
  await page.getByLabel('Nome da campanha').fill(`Menu ${Date.now()}`)
  await page.getByLabel('Seu nome').fill('Mestre')
  await page.getByRole('button', { name: 'Criar e entrar' }).click()
  await page.getByRole('button', { name: 'Entrar como mestre' }).click()
  await page.getByRole('button', { name: 'Iniciar preparação' }).click()
  await page.getByLabel('Buscar criatura no catálogo').first().fill('goblin')
  await page.locator('.encounter-setup .creature-picker-row').filter({ hasText: /^Goblin/ }).first().getByRole('button', { name: 'Adicionar' }).click()
  await expect(page.getByText('Goblin adicionado ao combate.')).toBeVisible()
  await page.getByLabel('Imagem do battlemap').setInputFiles({ name: 'mapa.png', mimeType: 'image/png', buffer: await mapPng(page) })
  await page.getByLabel('Colunas do grid').fill('10')
  await page.getByLabel('Linhas do grid').fill('10')
  await page.locator('[data-fog-reveal-all]').click()
  await page.getByRole('button', { name: 'Publicar battlemap' }).click()
  await page.locator('.battlemap-reserve').getByRole('button', { name: /Goblin/ }).click()
  await page.locator('[data-board-surface]').focus()
  await page.keyboard.press('Enter')
  const token = page.locator('[data-battlemap-token]').first()
  await expect(token).toBeVisible()
  await recordContextMenus(page)

  // 1. Right-click on a creature: the sheet opens and no browser menu is left uncancelled.
  const box = (await token.boundingBox())!
  await slowRightClick(page, box.x + box.width / 2, box.y + box.height / 2)
  await expect(page.locator('[data-creature-sheet]')).toBeVisible()
  expect((await menus(page)).filter(menu => !menu.prevented), JSON.stringify(await menus(page))).toEqual([])
  await page.getByRole('button', { name: 'Fechar ficha' }).click()

  // 2. Right-drag (pan) released outside the board: still no browser menu.
  await page.locator('[data-board-surface]').evaluate(el => el.scrollIntoView({ block: 'center' }))
  const surface = (await page.locator('[data-board-surface]').boundingBox())!
  const start = { x: surface.x + surface.width - 30, y: surface.y + surface.height / 2 }
  expect(await page.evaluate(({ x, y }) => !!document.elementFromPoint(x, y)?.closest('[data-board-surface]'), start), 'press starts on the board').toBe(true)
  await slowRightClick(page, start.x, start.y, { x: surface.x + surface.width + 120, y: start.y })
  expect((await menus(page)).filter(menu => !menu.prevented), JSON.stringify(await menus(page))).toEqual([])

  // 3. Outside the board the page keeps the normal browser menu (we do not hijack it everywhere).
  const before = (await menus(page)).length
  const heading = page.getByRole('heading', { name: 'Combatentes' })
  await heading.click({ button: 'right' })
  const after = await menus(page)
  expect(after.length).toBeGreaterThan(before)
  expect(after.at(-1)!.prevented).toBe(false)
})
