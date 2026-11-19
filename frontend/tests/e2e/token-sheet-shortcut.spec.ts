import { expect, test, type Page } from '@playwright/test'

// Spec 010 end to end, in a real browser: right-click on a creature token opens its sheet (no
// token menu, no browser menu), and a roll from the sheet keeps its result on screen ~2 s.
const shots = process.env.SHOTS

async function mapPng(page: Page) {
  const base64 = await page.evaluate(() => {
    const canvas = document.createElement('canvas'); canvas.width = 400; canvas.height = 400
    const g = canvas.getContext('2d')!; g.fillStyle = '#2f6b4a'; g.fillRect(0, 0, 400, 400)
    return canvas.toDataURL('image/png').split(',')[1]!
  })
  return Buffer.from(base64, 'base64')
}

test('right-click on a creature token opens its sheet; the roll result stays 2 s', async ({ page }) => {
  test.skip(test.info().project.name !== 'desktop', 'right-click is a mouse gesture')
  test.setTimeout(90_000)
  await page.setViewportSize({ width: 1280, height: 900 })
  await page.goto('/')
  await page.getByLabel('Nome da campanha').fill(`Botão direito ${Date.now()}`)
  await page.getByLabel('Seu nome').fill('Mestre')
  await page.getByRole('button', { name: 'Criar e entrar' }).click()
  await page.getByRole('button', { name: 'Entrar como mestre' }).click()

  // A goblin on a published (unfogged) map.
  await page.getByRole('button', { name: 'Iniciar preparação' }).click()
  await page.getByLabel('Buscar criatura no catálogo').first().fill('goblin')
  await page.locator('.encounter-setup .creature-picker-row').filter({ hasText: /^Goblin/ }).first().getByRole('button', { name: 'Adicionar' }).click()
  await expect(page.getByText('Goblin adicionado ao combate.')).toBeVisible()
  await page.getByLabel('Imagem do battlemap').setInputFiles({ name: 'mapa.png', mimeType: 'image/png', buffer: await mapPng(page) })
  await page.getByLabel('Colunas do grid').fill('10')
  await page.getByLabel('Linhas do grid').fill('10')
  await page.locator('[data-fog-reveal-all]').click()
  await page.getByRole('button', { name: 'Publicar battlemap' }).click()
  await expect(page.locator('[data-board-surface]')).toBeVisible()
  await page.locator('.battlemap-reserve').getByRole('button', { name: /Goblin/ }).click()
  await page.locator('[data-board-surface]').focus()
  await page.keyboard.press('Enter') // places it at the cursor cell (the menu covers part of the board)
  const token = page.locator('[data-battlemap-token]').first()
  await expect(token).toBeVisible()
  await expect(page.locator('.battlemap-status')).toContainText('Botão direito (ou Shift+F10) numa criatura abre a ficha.')

  // Right-click: straight to the sheet, labelled with the token, no token menu.
  await token.click({ button: 'right' })
  await expect(page.locator('[data-creature-sheet]')).toBeVisible()
  await expect(page.locator('#creature-sheet-title')).toHaveText('Goblin')
  await expect(page.locator('.creature-sheet .eyebrow')).toContainText('Goblin')
  await expect(page.locator('.attack-area-panel')).toHaveCount(0)
  if (shots) await page.screenshot({ path: `${shots}/010-right-click-sheet.png` })

  // Roll: the settled dice and total stay ~2 s, then the "Último resultado" line takes over.
  const panel = page.locator('.creature-roll')
  // As a user opening the sheet: scrolled to the top, and a click that does not auto-scroll.
  await page.locator('.creature-sheet-body').evaluate(body => { body.scrollTop = 0 })
  await page.locator('[data-roll-go]').dispatchEvent('click')
  await expect(panel).toHaveAttribute('data-roll-showing', 'result', { timeout: 5_000 })
  const shownAt = Date.now()
  // The dice and total are inside the sheet's visible scroll area, not cut off below the footer.
  await expect.poll(() => page.locator('[data-roll-stage]').evaluate(stage => {
    const area = stage.closest('.creature-sheet-body')!.getBoundingClientRect(), box = stage.getBoundingClientRect()
    return Math.max(0, Math.min(area.bottom, box.bottom) - Math.max(area.top, box.top)) / box.height
  }), { timeout: 1_500 }).toBeGreaterThanOrEqual(0.95)
  if (shots) await page.screenshot({ path: `${shots}/010-roll-result.png` })
  await expect(panel.locator('[data-roll-go]')).toBeEnabled() // a new roll can interrupt the hold
  await page.waitForTimeout(Math.max(0, 1_400 - (Date.now() - shownAt))) // relative to when it appeared, not to the screenshot
  await expect(panel).toHaveAttribute('data-roll-showing', 'result') // still on screen ~1.4 s after it appeared
  await expect(page.locator('.roll-last')).toBeVisible({ timeout: 2_000 })
  const heldFor = Date.now() - shownAt
  expect(heldFor).toBeGreaterThanOrEqual(1_700)
  expect(heldFor).toBeLessThan(3_500)
  await expect(panel).not.toHaveAttribute('data-roll-showing', /.+/)

  // Closing the sheet keeps the master on the table, and left click still opens the token menu.
  await page.getByRole('button', { name: 'Fechar ficha' }).click()
  await token.click()
  await expect(page.locator('.attack-area-panel')).toBeVisible()
})
