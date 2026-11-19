import { expect, test, type Page } from '@playwright/test'

// Spec 008 end to end: the master finds the catalog everywhere it is needed (tab, combat
// preparation, board), publishes a map that is already fogged, and adds reinforcements mid-combat.
// SHOTS=<dir> also saves screenshots for the responsiveness review.
const shots = process.env.SHOTS
async function shot(page: Page, name: string) {
  if (shots) await page.screenshot({ path: `${shots}/${test.info().project.name}-${page.viewportSize()!.width}-${name}.png`, fullPage: false })
}
async function noHorizontalScroll(page: Page) {
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)
  expect(overflow).toBeLessThanOrEqual(1)
}

async function mapPng(page: Page) {
  // A real-looking 600x400 image so the fog preview has something to cover.
  const base64 = await page.evaluate(async () => {
    const canvas = document.createElement('canvas'); canvas.width = 600; canvas.height = 400
    const g = canvas.getContext('2d')!; g.fillStyle = '#2f6b4a'; g.fillRect(0, 0, 600, 400); g.fillStyle = '#b33'; g.fillRect(380, 220, 160, 120)
    return canvas.toDataURL('image/png').split(',')[1]!
  })
  return Buffer.from(base64, 'base64')
}

for (const width of [1280, 1024, 375]) {
  test(`catalog in preparation and on the board, fogged publish, reinforcements (${width}px)`, async ({ page, browser }) => {
    test.skip(test.info().project.name !== 'desktop', 'widths are set explicitly')
    await page.setViewportSize({ width, height: width === 375 ? 812 : 900 })
    await page.goto('/')
    await page.getByLabel('Nome da campanha').fill(`Catálogo ${width} ${Date.now()}`)
    await page.getByLabel('Seu nome').fill('Mestre')
    await page.getByRole('button', { name: 'Criar e entrar' }).click()
    const playerCode = await page.locator('.created-code').first().innerText()
    await page.getByRole('button', { name: 'Entrar como mestre' }).click()

    // 1. The Criaturas tab exists for the master.
    await expect(page.getByRole('tab', { name: 'Criaturas' })).toBeVisible()
    await page.getByRole('tab', { name: 'Criaturas' }).click()
    await expect(page.locator('.creature-card').first()).toBeVisible()
    await shot(page, '1-tab')
    await noHorizontalScroll(page)
    await page.getByRole('tab', { name: 'Mesa' }).click()

    // 2. The combat preparation form opens on the catalog.
    await page.getByRole('button', { name: 'Iniciar preparação' }).click()
    await expect(page.getByRole('button', { name: 'Do catálogo' }).last()).toHaveAttribute('aria-pressed', 'true')
    await page.getByLabel('Buscar criatura no catálogo').first().fill('goblin')
    const goblinRow = page.locator('.encounter-setup .creature-picker-row').filter({ hasText: /^Goblin/ }).first()
    await expect(goblinRow).toBeVisible()
    await goblinRow.getByRole('button', { name: 'Mais uma' }).click()
    await goblinRow.getByRole('button', { name: 'Adicionar' }).click()
    await expect(page.getByText('2 × Goblin adicionados ao combate.')).toBeVisible()
    await goblinRow.scrollIntoViewIfNeeded()
    await shot(page, '2-preparation')
    await noHorizontalScroll(page)

    // 3. Uploading a map opens the fog editor with everything covered before publishing.
    await page.getByLabel('Imagem do battlemap').setInputFiles({ name: 'masmorra.png', mimeType: 'image/png', buffer: await mapPng(page) })
    await expect(page.locator('[data-fog-reveal-all]')).toBeVisible()
    await page.locator('[data-fog-reveal-all]').scrollIntoViewIfNeeded()
    await shot(page, '3-fog-before-publish')
    await noHorizontalScroll(page)
    await page.getByRole('button', { name: 'Publicar battlemap' }).click()
    await expect(page.locator('[data-board-surface]')).toBeVisible()

    // The player's first view of the map is fully fogged.
    const playerContext = await browser.newContext({ viewport: { width, height: width === 375 ? 812 : 900 } })
    const player = await playerContext.newPage()
    await player.goto(new URL('/', page.url()).toString())
    await player.getByRole('tab', { name: 'Entrar com código' }).click()
    await player.getByLabel('Código da campanha').fill(playerCode)
    await player.getByLabel('Seu nome').fill('Exploradora')
    await player.getByRole('button', { name: 'Entrar na campanha' }).click()
    await expect(player.locator('[data-board-surface]')).toBeVisible()
    await expect(player.locator('[data-board-surface] [class*="fog"]').first()).toBeAttached()
    await player.locator('[data-board-surface]').scrollIntoViewIfNeeded()
    await shot(player, '4-player-fogged')
    // Combat only starts when everyone has HP: the player fills in their profile.
    await player.getByLabel('Modificador de iniciativa').fill('3')
    await player.locator('#own-hit-points').fill('24')
    await player.getByRole('button', { name: 'Salvar', exact: true }).click()
    await expect(player.getByText('Perfil de combate salvo.')).toBeVisible()

    // 4. Start combat, then add a reinforcement from the board's catalog.
    await page.getByRole('button', { name: 'Rolar iniciativas', exact: true }).click()
    await page.getByRole('button', { name: 'Começar combate' }).click()
    await page.locator('[data-board-catalog]').click()
    await expect(page.locator('[data-picker-running]')).toBeVisible()
    await page.locator('.battlemap-catalog').getByLabel('Buscar criatura no catálogo').fill('ogro')
    const ogreRow = page.locator('.battlemap-catalog .creature-picker-row').filter({ hasText: /^Ogro/ }).first()
    await ogreRow.getByRole('button', { name: 'Adicionar' }).click()
    await expect(page.locator('.battlemap-catalog').getByText(/Ogro.* adicionado ao combate\./)).toBeVisible()
    await page.locator('.battlemap-catalog').scrollIntoViewIfNeeded()
    await shot(page, '5-board-catalog-in-combat')
    await noHorizontalScroll(page)
    await expect(page.getByRole('textbox', { name: 'Nome de Ogro', exact: true })).toBeVisible()
    await playerContext.close()
  })
}
