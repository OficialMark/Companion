import { expect, test } from '@playwright/test'

const tinyPng = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=',
  'base64'
)

test('master publishes a fixed battlemap and player moves own token', async ({ page, browser }) => {
  await page.goto('/')
  await page.getByLabel('Nome da campanha').fill(`Battlemap ${Date.now()}`)
  await page.getByLabel('Seu nome').fill('Mestre do mapa')
  await page.getByRole('button', { name: 'Criar e entrar' }).click()
  const playerCode = await page.locator('.created-code').first().innerText()
  await page.getByRole('button', { name: 'Entrar como mestre' }).click()
  await page.getByRole('button', { name: 'Iniciar preparação' }).click()

  await page.locator('input[type="file"]').setInputFiles({
    name: 'dungeon.png',
    mimeType: 'image/png',
    buffer: tinyPng
  })
  await page.getByLabel('Colunas do grid').fill('100')
  await page.getByLabel('Linhas do grid').fill('100')
  await page.getByRole('button', { name: 'Publicar battlemap' }).click()
  await expect(page.locator('[data-board-surface]')).toBeVisible()
  const desktopHeight = await page.locator('[data-board-surface]').evaluate(element => element.getBoundingClientRect().height)
  expect(desktopHeight).toBeGreaterThanOrEqual(360)
  await page.getByRole('button', { name: 'Ajustar mapa' }).click()
  await page.getByRole('button', { name: 'Escala 40 px' }).click()

  const playerContext = await browser.newContext()
  const player = await playerContext.newPage()
  await player.goto('/')
  await player.getByRole('button', { name: 'Entrar com código' }).click()
  await player.getByLabel('Código da campanha').fill(playerCode)
  await player.getByLabel('Seu nome').fill('Exploradora')
  await player.getByRole('button', { name: 'Entrar na campanha' }).click()
  await expect(player.locator('[data-board-surface]')).toBeVisible()
  await player.getByRole('button', { name: /Exploradora/ }).click()
  await player.locator('[data-board-surface]').click({ position: { x: 120, y: 120 } })
  await expect(player.getByText(/Arraste seu pino/)).toBeVisible()
  await player.locator('[data-board-surface]').focus()
  await player.keyboard.press('ArrowRight')
  await player.keyboard.press('ArrowDown')
  await expect(player.locator('body')).not.toHaveCSS('overflow-x', 'scroll')
  await playerContext.close()
})

test('single click/tap opens the token interaction panel with size and condition controls usable without zoom (SC-004, SC-007)', async ({ page, browser }) => {
  await page.goto('/')
  await page.getByLabel('Nome da campanha').fill(`Battlemap Gesture ${Date.now()}`)
  await page.getByLabel('Seu nome').fill('Mestre do gesto')
  await page.getByRole('button', { name: 'Criar e entrar' }).click()
  const playerCode = await page.locator('.created-code').first().innerText()
  await page.getByRole('button', { name: 'Entrar como mestre' }).click()
  await page.getByRole('button', { name: 'Iniciar preparação' }).click()

  await page.locator('input[type="file"]').setInputFiles({
    name: 'dungeon.png',
    mimeType: 'image/png',
    buffer: tinyPng
  })
  await page.getByLabel('Colunas do grid').fill('20')
  await page.getByLabel('Linhas do grid').fill('20')
  await page.getByRole('button', { name: 'Publicar battlemap' }).click()
  await expect(page.locator('[data-board-surface]')).toBeVisible()

  // Only players get a battlemap pin (the master does not), so the token
  // used here belongs to a joined player, not the master.
  const playerContext = await browser.newContext()
  const player = await playerContext.newPage()
  await player.goto('/')
  await player.getByRole('button', { name: 'Entrar com código' }).click()
  await player.getByLabel('Código da campanha').fill(playerCode)
  await player.getByLabel('Seu nome').fill('Jogador do gesto')
  await player.getByRole('button', { name: 'Entrar na campanha' }).click()
  await expect(player.locator('[data-board-surface]')).toBeVisible()

  // Placing the pin: select the reserve token, then a cell (two clicks, two
  // different targets — not the gesture under test). The regression this
  // test guards is what happens next: a SINGLE click on the now-placed token.
  await player.getByRole('button', { name: /Jogador do gesto/ }).click()
  await player.locator('[data-board-surface]').click({ position: { x: 80, y: 80 } })

  const token = player.locator('[data-battlemap-token]').first()
  await token.click()

  const panel = player.locator('.attack-area-panel')
  await expect(panel).toBeVisible()
  await expect(panel.locator('.attack-area-shape')).toHaveCount(4)
  await expect(panel.locator('.token-size-option')).toHaveCount(5)
  await expect(panel.locator('.token-condition-option')).toHaveCount(6)

  // SC-007: touch targets stay usable without manual zoom or horizontal scroll.
  const sizeButtonBox = await panel.locator('.token-size-option').first().boundingBox()
  expect(sizeButtonBox?.height ?? 0).toBeGreaterThanOrEqual(40)
  await expect(page.locator('body')).not.toHaveCSS('overflow-x', 'scroll')

  await panel.locator('.token-size-option').nth(2).click()
  await panel.locator('[data-condition="Stunned"]').click()
  await expect(panel.locator('[data-condition="Stunned"]')).toHaveClass(/selected/)
})
