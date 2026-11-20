import { expect, test, type Browser, type Locator, type Page } from '@playwright/test'
import { assertSafeBaseUrl } from './guard'

// Spec 013 end to end (quickstart): the master creates an item and edits a compendium spell for the table,
// the player of that table finds and adds them, deleting the edit brings the original back; the player's
// features with a use counter survive a reload and follow the rests. Validation instance only.
test.beforeAll(() => assertSafeBaseUrl())

const shots = process.env.SHOTS
async function shot(page: Page, name: string) {
  if (shots) await page.screenshot({ path: `${shots}/${test.info().project.name}-${page.viewportSize()!.width}-${name}.png`, fullPage: true })
}

async function noHorizontalScroll(page: Page) {
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)
  expect(overflow, 'rolagem horizontal').toBeLessThanOrEqual(1)
}

/** Visible interactive controls smaller than 44 × 44 px (checkboxes and links in sentences are exempt). */
async function smallTargets(root: Locator) {
  return root.evaluate(element => [...element.querySelectorAll<HTMLElement>('button, input:not([type="checkbox"]), select, textarea')]
    .filter(node => getComputedStyle(node).display !== 'none' && node.getClientRects().length > 0)
    .map(node => ({ node, box: node.getBoundingClientRect() }))
    .filter(({ box }) => box.width < 43.5 || box.height < 43.5)
    .map(({ node, box }) => `${node.tagName} "${(node.getAttribute('aria-label') || node.textContent || '').trim().slice(0, 24)}" ${Math.round(box.width)}x${Math.round(box.height)}`))
}

async function createCampaign(page: Page, master: string) {
  await page.goto('/')
  await page.getByLabel('Nome da campanha').fill(`Compêndio 013 ${Date.now()}`)
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

/** Exactly "Bola de Fogo" (Fireball), not "Bola de Fogo Retardada". */
const FIREBALL = /^\s*Bola de Fogo\s*Fireball\s*$/

async function search(root: Locator, text: string) {
  await root.locator('[data-catalog-search]').fill(text)
}

for (const viewport of [{ width: 1280, height: 900 }, { width: 375, height: 812 }]) {
  test(`mestre edita o compêndio e jogador usa características em ${viewport.width}px`, async ({ page, browser }) => {
    const tag = `${Date.now()}`.slice(-6)
    await page.setViewportSize(viewport)
    const code = await createCampaign(page, `Mestre ${tag}`)

    // US1, item: the master creates "Corpo Fênix" in Compêndio > Itens.
    await page.locator('[data-view="compendio"]').click()
    await page.locator('[data-compendium-tab="itens"]').click()
    const items = page.locator('[data-catalog-picker="items"]')
    await items.locator('[data-catalog-new]').click()
    const itemEditor = page.locator('[data-item-editor]')
    await itemEditor.locator('[data-item-field="name"]').fill(`Corpo Fênix ${tag}`)
    await itemEditor.locator('[data-item-field="category"]').selectOption('Armor')
    await itemEditor.locator('[data-item-field="baseAc"]').fill('18')
    await itemEditor.locator('[data-item-field="armorType"]').selectOption('Heavy')
    await itemEditor.locator('[data-item-field="magicBonus"]').selectOption('1')
    await itemEditor.locator('[data-item-field="rarity"]').fill('Raro')
    await shot(page, 'item-editor')
    expect(await smallTargets(itemEditor)).toEqual([])
    await itemEditor.locator('[data-item-save]').click()
    await expect(itemEditor).toHaveCount(0)
    await expect(items.locator('[data-catalog-status]')).toContainText(`Corpo Fênix ${tag}`)
    await search(items, `fenix ${tag}`)
    await expect(items.locator('.catalog-row').filter({ hasText: `Corpo Fênix ${tag}` }).locator('.origin-badge')).toHaveText('Personalizada')

    // US1, spell: editing "Bola de Fogo" creates the table's version (one row, badge Editada).
    await page.locator('[data-compendium-tab="magias"]').click()
    const spells = page.locator('[data-catalog-picker="spells"]')
    await search(spells, 'fireball')
    const fireball = spells.locator('.catalog-row').filter({ has: page.locator('.catalog-row-name', { hasText: FIREBALL }) }).first()
    await fireball.locator('[data-catalog-detail-toggle]').click()
    await fireball.locator('[data-catalog-edit]').click()
    const spellEditor = page.locator('[data-spell-editor]')
    await expect(spellEditor.locator('[data-catalog-copy-hint]')).toBeVisible()
    await spellEditor.locator('[data-spell-field="damageDice"]').fill('10d6')
    await spellEditor.locator('[data-spell-save]').click()
    await expect(spellEditor).toHaveCount(0)
    await search(spells, 'bola de fogo')
    await expect(spells.locator('.catalog-row').filter({ has: page.locator('.catalog-row-name', { hasText: FIREBALL }) })).toHaveCount(1)
    await expect(spells.locator('.catalog-row').filter({ has: page.locator('.catalog-row-name', { hasText: FIREBALL }) }).locator('.origin-badge')).toHaveText('Editada')
    await shot(page, 'compendium-edited')
    await noHorizontalScroll(page)

    // The player of this table adds both and sees the table's numbers.
    const player = await join(browser, code, `Vash ${tag}`, viewport)
    await player.locator('[data-view="ficha"]').click()
    await player.locator('[data-tab="magias"]').click()
    await player.getByRole('button', { name: 'Do catálogo' }).click()
    const picker = player.getByRole('dialog')
    await picker.locator('[data-catalog-search]').fill('bola de fogo')
    await picker.locator('.catalog-row').filter({ has: player.locator('.catalog-row-name', { hasText: FIREBALL }) }).first().getByRole('button', { name: /Adicionar/ }).click()
    await expect(player.locator('[data-spell-list]')).toContainText('10d6')
    await picker.getByRole('button', { name: 'Fechar' }).click()

    // Deleting the edit restores the original; the player's copy keeps 10d6. The saved row stayed open.
    const edited = spells.locator('.catalog-row').filter({ has: page.locator('.catalog-row-name', { hasText: FIREBALL }) }).first()
    await expect(edited.locator('[data-catalog-delete]')).toHaveText(/Restaurar original/)
    await edited.locator('[data-catalog-delete]').click()
    await edited.locator('[data-catalog-delete]').click()
    await expect(spells.locator('[data-catalog-status]')).toContainText('voltou ao original')
    await expect(spells.locator('.catalog-row').filter({ has: page.locator('.catalog-row-name', { hasText: FIREBALL }) }).locator('.origin-badge')).toHaveCount(0)
    await player.reload()
    await player.locator('[data-tab="magias"]').click()
    await expect(player.locator('[data-spell-list]')).toContainText('10d6')

    // US2: features with a counter, rests, reload. Five sheet tabs, no label cut by an ellipsis.
    await player.locator('[data-tab="caracteristicas"]').click()
    const cut = await player.locator('.character-tab span').evaluateAll(spans =>
      spans.filter(span => span.scrollWidth > span.clientWidth + 1).map(span => span.textContent))
    expect(cut, 'rótulos de aba cortados').toEqual([])
    const list = player.locator('[data-feature-list]')
    for (const [name, max, recovery] of [['Sentido Divino', '5', 'LongRest'], ['Canalizar Divindade', '1', 'ShortRest']] as const) {
      await list.locator('[data-new-feature]').click()
      const editor = player.locator('[data-feature-editor]')
      await editor.locator('[data-feature-field="name"]').fill(name)
      await editor.locator('[data-feature-field="source"]').fill('Paladino 5')
      await editor.locator('[data-feature-field="hasCounter"]').check()
      await editor.locator('[data-feature-field="maxUses"]').fill(max)
      await editor.locator('[data-feature-field="recovery"]').selectOption(recovery)
      await editor.locator('[data-feature-save]').click()
      await expect(editor).toHaveCount(0)
    }
    const sense = list.locator('[data-feature-id]').filter({ hasText: 'Sentido Divino' })
    const channel = list.locator('[data-feature-id]').filter({ hasText: 'Canalizar Divindade' })
    await expect(sense.locator('[data-feature-uses]')).toHaveText('5/5')
    await sense.locator('[data-spend]').click()
    await sense.locator('[data-spend]').click()
    await channel.locator('[data-spend]').click()
    await expect(sense.locator('[data-feature-uses]')).toHaveText('3/5')
    await expect(channel.locator('[data-feature-uses]')).toHaveText('0/1')
    await list.locator('[data-rest="Short"]').click()
    await expect(channel.locator('[data-feature-uses]')).toHaveText('1/1')
    await expect(sense.locator('[data-feature-uses]')).toHaveText('3/5')
    await shot(player, 'features')
    expect(await smallTargets(list)).toEqual([])
    await noHorizontalScroll(player)
    await player.reload()
    await player.locator('[data-tab="caracteristicas"]').click()
    await expect(player.locator('[data-feature-id]').filter({ hasText: 'Sentido Divino' }).locator('[data-feature-uses]')).toHaveText('3/5')
    await player.locator('[data-rest="Long"]').click()
    await expect(player.locator('[data-feature-id]').filter({ hasText: 'Sentido Divino' }).locator('[data-feature-uses]')).toHaveText('5/5')
    await player.context().close()
  })
}
