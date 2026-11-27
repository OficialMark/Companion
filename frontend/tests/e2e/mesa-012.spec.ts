import { expect, test, type Page } from '@playwright/test'
import { assertSafeBaseUrl } from './guard'

// Spec 012 end to end (quickstart): master's code in the top bar, Compêndio tabs with the broad catalog
// search, the master's roll by creature, rolls scrolling to the result, the player's roll panel in the
// full view and the sheet overview with damage and healing. Validation instance only.
test.beforeAll(() => assertSafeBaseUrl())

const shots = process.env.SHOTS
async function shot(page: Page, name: string) {
  if (shots) await page.screenshot({ path: `${shots}/${test.info().project.name}-${name}.png`, fullPage: false })
}

async function createCampaign(page: Page) {
  await page.goto('/')
  await page.getByLabel('Nome da campanha').fill(`Mesa 012 ${Date.now()}`)
  await page.getByLabel('Seu nome').fill('Mestre')
  await page.getByRole('button', { name: 'Criar e entrar' }).click()
  const playerCode = (await page.locator('.created-code').first().innerText()).replace(/\D/g, '')
  await page.getByRole('button', { name: 'Entrar como mestre' }).click()
  await expect(page.locator('[data-master-nav]')).toBeVisible()
  return playerCode
}

async function inViewport(page: Page, selector: string) {
  return page.locator(selector).first().evaluate((element) => {
    const box = element.getBoundingClientRect()
    return box.top >= 0 && box.bottom <= window.innerHeight
  })
}

test('mestre: código na barra, compêndio em abas com busca ampla e rolagem por criatura', async ({ page }) => {
  await createCampaign(page)

  // US4: the reserved code is masked in the top bar, and the eye toggles it.
  const code = page.locator('[data-master-code-value]')
  await expect(code).toContainText('•')
  await page.getByRole('button', { name: 'Mostrar código do mestre' }).click()
  await expect(code).not.toHaveText(/•/)
  await page.getByRole('button', { name: 'Mostrar código do mestre' }).click()
  await expect(code).toContainText('•')

  // US2: Compêndio tabs; the English name and the description find spells and items.
  await page.getByRole('button', { name: 'Compêndio' }).click()
  await expect(page.getByRole('heading', { level: 1, name: 'Compêndio' })).toBeVisible()
  await page.getByRole('tab', { name: 'Magias' }).click()
  await expect(page).toHaveURL(/aba=magias/)
  await page.locator('[data-catalog-search]').fill('fireball')
  const first = page.locator('.catalog-row').first()
  await expect(first).toContainText('Bola de Fogo')
  await expect(first).toContainText('Fireball')
  await first.getByRole('button', { name: /Detalhes de Bola de Fogo/ }).click()
  await expect(first.locator('[data-catalog-detail]')).toContainText('3º círculo')
  await shot(page, 'compendio-magias')
  await page.locator('[data-catalog-search]').fill('invisível')
  await expect(page.locator('.catalog-row').first()).toBeVisible()
  await page.getByRole('tab', { name: 'Itens' }).click()
  await page.locator('[data-catalog-search]').fill('longsword')
  await expect(page.locator('.catalog-row').first()).toContainText('Espada Longa')
  await page.reload()
  await expect(page.getByRole('tab', { name: 'Itens' })).toHaveAttribute('aria-selected', 'true')

  // US1: a goblin on the table; the master rolls with its numbers and the page scrolls to the dice.
  await page.getByRole('button', { name: 'Mesa', exact: true }).click()
  await page.getByRole('button', { name: 'Iniciar preparação' }).click()
  await page.getByLabel('Buscar criatura no catálogo').first().fill('goblin')
  await page.locator('.encounter-setup .creature-picker-row').filter({ has: page.getByRole('button', { name: 'Ver ficha de Goblin', exact: true }) }).getByRole('button', { name: 'Adicionar' }).click()
  await expect(page.getByText('Goblin adicionado ao combate.')).toBeVisible()
  const source = page.locator('[data-master-roll-source]')
  await expect(source.locator('option')).toHaveCount(2)
  await source.selectOption({ index: 1 })
  const panel = page.locator('[data-master-roll]')
  await expect(panel.locator('[data-master-roll-actions]')).toBeVisible()
  await panel.locator('[data-shortcut="hit"]').first().click()
  await page.evaluate(() => window.scrollTo(0, document.body.scrollHeight))
  await panel.locator('[data-roll-go]').click()
  await expect(panel.locator('[data-roll-stage]')).toBeVisible()
  await expect.poll(() => inViewport(page, '[data-master-roll] [data-roll-stage]')).toBe(true)
  await shot(page, 'mestre-rolagem')
  await expect(page.locator('.history-panel')).toContainText('Goblin', { timeout: 10_000 })
})

test('jogador: rolagem pela ficha na visão completa e overview com dano e cura', async ({ page, browser }) => {
  const code = await createCampaign(page)
  const context = await browser.newContext({ viewport: page.viewportSize()! })
  const player = await context.newPage()
  await player.goto('/')
  await player.getByRole('tab', { name: 'Entrar com código' }).click()
  await player.getByLabel('Código da campanha').fill(code)
  await player.getByLabel('Seu nome').fill(`Iara ${Date.now() % 100000}`)
  await player.getByRole('button', { name: 'Entrar na campanha' }).click()

  // US1: the sheet's roll panel in the full view; the source groups are readable (US4).
  const rollPanel = player.locator('[data-player-roll-panel]')
  await expect(rollPanel).toBeVisible()
  await expect(rollPanel).toContainText('Sua ficha')
  const groupBackground = await rollPanel.locator('optgroup').first().evaluate(element => getComputedStyle(element).backgroundColor)
  expect(groupBackground).toBe('rgb(23, 18, 26)')

  // A max of 30 HP, set on the Ficha.
  await player.getByRole('button', { name: 'Ficha', exact: true }).click()
  await player.locator('[data-field="baseMaxHitPoints"]').fill('30')
  await player.locator('[data-field="currentHitPoints"]').fill('30')
  await expect(player.locator('[data-save-state="saved"]')).toBeVisible({ timeout: 10_000 })
  await player.getByRole('button', { name: 'Mesa', exact: true }).click()

  // US3: the summary at the foot opens the overview; damage and healing go to the sheet and the table.
  await expect(player.locator('[data-summary-hp]')).toHaveText('30/30')
  await player.locator('[data-open-overview]').click()
  const overview = player.locator('[data-sheet-overview]')
  await expect(overview).toBeVisible()
  await overview.locator('[data-hp-amount]').fill('7')
  await overview.locator('[data-hp-damage]').click()
  await expect(overview.locator('[data-overview-hp]')).toHaveText('23')
  await overview.locator('[data-hp-amount]').fill('5')
  await overview.locator('[data-hp-heal]').click()
  await expect(overview.locator('[data-overview-hp]')).toHaveText('28')
  await expect(overview.locator('[data-sheet-save-state="saved"]')).toBeVisible({ timeout: 10_000 })
  await shot(player, 'overview')
  await player.keyboard.press('Escape')
  await expect(overview).toBeHidden()
  await expect(player.locator('[data-open-overview]')).toBeFocused()
  await expect(player.locator('[data-summary-hp]')).toHaveText('28/30')

  // The table's copy of the HP follows the sheet on the server (spec 011 FR-010, integration tests).

  // Rolling from the sheet scrolls to the result.
  const ability = await rollPanel.locator('option[data-source^="ability:"]').first().getAttribute('value')
  await rollPanel.locator('.roll-source select').selectOption(ability!)
  await player.evaluate(() => window.scrollTo(0, document.body.scrollHeight))
  await rollPanel.locator('[data-roll-go]').click()
  await expect.poll(() => inViewport(player, '[data-player-roll-panel] [data-roll-stage], [data-player-roll-panel] [data-roll-last]')).toBe(true)
  await context.close()
})
