import { expect, test } from '@playwright/test'

test('create campaign and expose the shared table', async ({ page, browser }) => {
  await page.goto('/')
  await page.getByLabel('Nome da campanha').fill(`Mesa ${Date.now()}`)
  await page.getByLabel('Seu nome').fill('Mestre E2E')
  await page.getByRole('button', { name: 'Criar e entrar' }).click()
  await expect(page.getByText('Bem-vindo à mesa')).toBeVisible()
  const code = await page.locator('.code-badge').innerText()

  const playerContext = await browser.newContext()
  const player = await playerContext.newPage()
  await player.goto('/')
  await player.getByRole('button', { name: 'Entrar com código' }).click()
  await player.getByLabel('Código da campanha').fill(code)
  await player.getByLabel('Seu nome').fill('Jogador E2E')
  await player.getByRole('button', { name: 'Entrar na campanha' }).click()
  await expect(player.getByText('Bem-vindo à mesa')).toBeVisible()

  await page.getByRole('button', { name: 'D20', exact: true }).click()
  await expect(player.locator('.roll-list li')).toHaveCount(1)
  await playerContext.close()
})

test('rolls repeated and compound expressions without native randomUUID', async ({ page }) => {
  await page.addInitScript(() => {
    Object.defineProperty(Crypto.prototype, 'randomUUID', { configurable: true, value: undefined })
  })
  await page.goto('/')
  await page.getByLabel('Nome da campanha').fill(`Dados avançados ${Date.now()}`)
  await page.getByLabel('Seu nome').fill('Mestre dos dados')
  await page.getByRole('button', { name: 'Criar e entrar' }).click()

  const input = page.getByLabel('Expressão de dados')
  await input.fill('3#3d5+4')
  await page.getByRole('button', { name: 'Rolar', exact: true }).click()
  await expect(page.locator('.roll-list li').first().locator('.roll-attempt')).toHaveCount(3)

  await input.fill('2d3+4+2d8+3')
  await page.getByRole('button', { name: 'Rolar', exact: true }).click()
  const latest = page.locator('.roll-list li').first()
  await expect(latest).toContainText('2d3+2d8+7')
  await expect(latest.locator('.term-result')).toHaveCount(2)
})
