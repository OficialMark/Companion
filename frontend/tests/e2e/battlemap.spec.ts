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
