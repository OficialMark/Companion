import { expect, test, type Browser, type Locator, type Page } from '@playwright/test'
import { assertSafeBaseUrl } from './guard'

// Spec 011 end to end (quickstart, steps 1 to 8): account by name, the player's sheet, the bag
// changing the AC, the roll panel in game mode with advantage and repetitions, a saved roll, and
// the layout rules (44 px targets, no horizontal scroll). Runs only against the validation
// instance, never against the stack serving the table. SHOTS=<dir> saves screenshots.
test.beforeAll(() => assertSafeBaseUrl())

const shots = process.env.SHOTS
async function shot(page: Page, name: string) {
  if (shots) await page.screenshot({ path: `${shots}/${test.info().project.name}-${page.viewportSize()!.width}-${name}.png`, fullPage: true })
}

/** Visible interactive controls smaller than 44 × 44 px (links inside sentences are exempt, WCAG 2.5.8). */
async function smallTargets(root: Locator) {
  return root.evaluate(element => [...element.querySelectorAll<HTMLElement>('button, input:not([type="file"]):not([type="checkbox"]):not([type="radio"]), select, textarea, a[href], [role="button"], [role="tab"]')]
    .filter(node => !(node.tagName === 'A' && node.closest('p')))
    .filter(node => node.closest('.sr-only') === null && getComputedStyle(node).display !== 'none' && getComputedStyle(node).visibility !== 'hidden' && node.getClientRects().length > 0)
    .map(node => ({ node, box: node.getBoundingClientRect() }))
    .filter(({ box }) => box.width < 43.5 || box.height < 43.5)
    .map(({ node, box }) => `${node.tagName}.${node.className} "${(node.getAttribute('aria-label') || node.textContent || '').trim().slice(0, 24)}" ${Math.round(box.width)}x${Math.round(box.height)}`))
}

async function noHorizontalScroll(page: Page) {
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)
  expect(overflow, 'rolagem horizontal').toBeLessThanOrEqual(1)
}

async function createCampaign(page: Page, master: string) {
  await page.goto('/')
  await page.getByLabel('Nome da campanha').fill(`Ficha ${Date.now()}`)
  await page.getByLabel('Seu nome').fill(master)
  await page.getByRole('button', { name: 'Criar e entrar' }).click()
  const playerCode = (await page.locator('.created-code').first().innerText()).replace(/\D/g, '')
  await page.getByRole('button', { name: 'Entrar como mestre' }).click()
  await expect(page.locator('[data-master-nav]')).toBeVisible()
  return playerCode
}

async function join(browser: Browser, code: string, name: string, viewport: { width: number, height: number }) {
  const context = await browser.newContext({ viewport })
  const page = await context.newPage()
  await page.goto('/')
  await page.getByRole('tab', { name: 'Entrar com código' }).click()
  await page.getByLabel('Código da campanha').fill(code)
  await page.getByLabel('Seu nome').fill(name)
  await page.getByRole('button', { name: 'Entrar na campanha' }).click()
  await expect(page.locator('[data-player-nav]')).toBeVisible()
  return page
}

async function setNumber(page: Page, field: string, value: number) {
  const input = page.locator(`[data-field="${field}"]`)
  await input.fill(String(value))
  await expect(page.locator('[data-save-state="saved"]')).toBeVisible({ timeout: 10_000 })
}

async function addFromCatalog(page: Page, query: string, name: RegExp) {
  await page.getByRole('button', { name: 'Do catálogo' }).click()
  const dialog = page.getByRole('dialog')
  await dialog.getByRole('searchbox').or(dialog.locator('input[type="search"]')).first().fill(query)
  const row = dialog.locator('li, article').filter({ hasText: name }).first()
  await row.getByRole('button', { name: 'Adicionar' }).click()
  await expect(page.locator('[data-inventory]')).toContainText(name)
  if (await dialog.isVisible()) await dialog.getByRole('button', { name: /Fechar/ }).first().click()
}

for (const viewport of [{ width: 1280, height: 900 }, { width: 375, height: 812 }]) {
  test(`ficha do jogador de ponta a ponta em ${viewport.width}px`, async ({ page, browser }) => {
    test.setTimeout(180_000)
    await page.setViewportSize(viewport)

    // 1. Master: normalized name, Mesa | Compêndio, no Pocket DM credit.
    const code = await createCampaign(page, 'mestre maria')
    await expect(page.locator('.welcome-row h1')).toHaveText('Mestre Maria')
    await page.locator('[data-view="compendio"]').click()
    await expect(page.locator('.creature-credit a[href*="pocketdm"]')).toHaveCount(0)
    await page.locator('[data-view="mesa"]').click()

    // 2. Player account by name.
    const player = await join(browser, code, 'loren b. vonn neuman', viewport)
    await expect(player.locator('[data-view="compendio"]')).toHaveCount(0)
    await player.locator('[data-view="ficha"]').click()
    await expect(player.locator('[data-character-view]')).toBeVisible()
    await expect(player.getByRole('heading', { level: 1 })).toContainText('Loren B. Vonn Neuman')

    // 3. Base values -> derived values from the server.
    await setNumber(player, 'dexterity', 16)
    await setNumber(player, 'level', 5)
    await setNumber(player, 'strength', 16)
    await expect(player.locator('[data-derived="initiative"] [data-value]')).toHaveText('+3')
    await expect(player.locator('[data-derived="proficiencyBonus"] [data-value]')).toHaveText('+3')
    await expect(player.locator('[data-derived="armorClass"] [data-value]')).toHaveText('13')
    await shot(player, 'ficha')
    expect(await smallTargets(player.locator('[data-character-view]')), 'alvos pequenos na ficha').toEqual([])
    await noHorizontalScroll(player)

    // General proficiencies are the starting value of each new item's own flag (FR-030).
    await player.locator('[data-armor-prof="Heavy"]').click()
    await player.locator('[data-shield-prof]').click()
    await player.locator('[data-martial-prof]').click()
    await expect(player.locator('[data-save-state="saved"]')).toBeVisible({ timeout: 10_000 })

    // 4. Bag: catalog chain mail + shield + longsword; AC goes to 18.
    await player.locator('[data-tab="mochila"]').click()
    await addFromCatalog(player, 'cota de malha', /Cota de Malha/)
    await addFromCatalog(player, 'escudo', /^Escudo$|Escudo/)
    await addFromCatalog(player, 'espada longa', /Espada Longa/)
    const item = (name: RegExp) => player.locator('[data-item-id]').filter({ hasText: name }).first()
    await item(/Cota de Malha/).locator('[data-equip]').click()
    await item(/Escudo/).locator('[data-equip]').click()
    await item(/Espada Longa/).locator('[data-equip]').click()
    await player.locator('[data-tab="ficha"]').click()
    await expect(player.locator('[data-derived="armorClass"] [data-value]')).toHaveText('18')
    await player.locator('[data-tab="mochila"]').click()
    await shot(player, 'mochila')
    expect(await smallTargets(player.locator('[data-character-view]')), 'alvos pequenos na mochila').toEqual([])
    await noHorizontalScroll(player)

    // 5. Same name, another device: same account and sheet; the first device stays connected.
    const second = await join(browser, code, 'LOREN B. VONN NEUMAN', viewport)
    await second.locator('[data-view="ficha"]').click()
    await expect(second.locator('[data-derived="armorClass"] [data-value]')).toHaveText('18')
    await player.reload()
    await expect(player.locator('[data-player-nav]')).toBeVisible()

    // 6. Game mode: Espada Longa, attack, advantage, 2 repetitions.
    await player.locator('[data-view="mesa"]').click()
    await player.getByRole('button', { name: 'Modo Jogo' }).click()
    const panel = player.locator('[data-player-roll-panel]')
    await expect(panel).toBeVisible()
    const select = panel.locator('[data-source-select]')
    const swordValue = await select.locator('option', { hasText: 'Espada Longa' }).first().getAttribute('value')
    await select.selectOption(swordValue!)
    await panel.locator('[data-kind="attack"]').click()
    await panel.locator('[data-advantage="advantage"]').click()
    await panel.locator('[data-repetitions]').fill('2')
    await expect(panel.locator('[data-expression]')).toHaveText('2#[2#(d20 + (FOR) 3 + (prof) 3) maior]')
    await panel.locator('[data-roll-go]').click()
    const history = player.locator('[data-selected-execution]').first()
    await expect(history).toBeVisible({ timeout: 15_000 })
    await shot(player, 'modo-jogo')
    expect(await smallTargets(panel), 'alvos pequenos no painel').toEqual([])
    await noHorizontalScroll(player)

    // 7. Saved roll follows the sheet.
    await panel.locator('[data-save-roll]').click()
    const editor = player.getByRole('dialog')
    await editor.getByLabel(/Nome/).fill('Ataque com espada')
    await editor.getByRole('button', { name: /Salvar/ }).last().click()
    await expect(panel.locator('[data-saved-roll]').filter({ hasText: 'Ataque com espada' })).toBeVisible()

    // 8. Master tools untouched.
    await page.getByRole('button', { name: 'Modo Jogo' }).click()
    await expect(page.locator('[data-player-roll-panel]')).toHaveCount(0)
  })
}
