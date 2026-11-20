import { expect, test, type Page } from '@playwright/test'
import { assertSafeBaseUrl } from './guard'

// Spec 014 end to end: a feature with an effect changes the roll. Dueling (+2 damage, one-handed melee)
// shows up in the damage expression of a Warhammer and leaves the Maul (two-handed) alone; removing the
// feature takes it back out. Validation instance only.
test.beforeAll(() => assertSafeBaseUrl())

const shots = process.env.SHOTS
async function shot(page: Page, name: string) {
  if (shots) await page.screenshot({ path: `${shots}/${test.info().project.name}-${page.viewportSize()!.width}-${name}.png`, fullPage: true })
}

async function createCampaign(page: Page, master: string) {
  await page.goto('/')
  await page.getByLabel('Nome da campanha').fill(`Efeitos 014 ${Date.now()}`)
  await page.getByLabel('Seu nome').fill(master)
  await page.getByRole('button', { name: 'Criar e entrar' }).click()
  const playerCode = (await page.locator('.created-code').first().innerText()).replace(/\D/g, '')
  await page.getByRole('button', { name: 'Entrar como mestre' }).click()
  await expect(page.locator('[data-master-nav]')).toBeVisible()
  return playerCode
}

/** `row` matches the catalog row exactly ("Malho", not "Malho Leve"); `shown` is the name in the bag. */
async function addFromCatalog(player: Page, query: string, row: RegExp, shown: string) {
  await player.getByRole('button', { name: 'Do catálogo' }).click()
  const dialog = player.getByRole('dialog')
  await dialog.locator('[data-catalog-search]').fill(query)
  await dialog.locator('.catalog-row').filter({ has: player.locator('.catalog-row-name', { hasText: row }) }).first()
    .getByRole('button', { name: /Adicionar/ }).click()
  await expect(player.locator('[data-inventory]')).toContainText(shown)
  await dialog.getByRole('button', { name: 'Fechar' }).click()
}

for (const viewport of [{ width: 1280, height: 900 }, { width: 375, height: 812 }]) {
  test(`Duelismo entra na rolagem de dano em ${viewport.width}px`, async ({ page, browser }) => {
    const tag = `${Date.now()}`.slice(-6)
    await page.setViewportSize(viewport)
    const code = await createCampaign(page, `Mestre ${tag}`)

    const context = await browser.newContext({ viewport })
    const player = await context.newPage()
    await player.goto('/')
    await player.getByRole('tab', { name: 'Entrar com código' }).click()
    await player.getByLabel('Código da campanha').fill(code)
    await player.getByLabel('Seu nome').fill(`Vash ${tag}`)
    await player.getByRole('button', { name: 'Entrar na campanha' }).click()
    await expect(player.locator('[data-player-nav]')).toBeVisible()

    // A versatile one-handed weapon and a two-handed one, both equipped.
    await player.locator('[data-view="ficha"]').click()
    await player.locator('[data-tab="mochila"]').click()
    await addFromCatalog(player, 'martelo de guerra', /^\s*Martelo de Guerra\s*Warhammer\s*$/, 'Martelo de Guerra')
    await addFromCatalog(player, 'malho', /^\s*Malho\s*Maul\s*$/, 'Malho')
    for (const name of [/Martelo de Guerra/, /Malho/])
      await player.locator('[data-item-id]').filter({ hasText: name }).first().locator('[data-equip]').click()

    // The feature with the Dueling effect.
    await player.locator('[data-tab="caracteristicas"]').click()
    await player.locator('[data-new-feature]').click()
    const editor = player.locator('[data-feature-editor]')
    await editor.locator('[data-feature-field="name"]').fill('Duelismo')
    await editor.locator('[data-feature-field="source"]').fill('Paladino 2')
    await editor.locator('[data-add-effect]').click()
    await editor.locator('[data-effect-type]').selectOption('DamageBonus')
    await editor.locator('[data-effect-target]').selectOption('melee-one-handed')
    await editor.locator('[data-effect-value]').fill('2')
    await editor.locator('[data-feature-save]').click()
    await expect(editor).toHaveCount(0)
    const card = player.locator('[data-feature-id]').filter({ hasText: 'Duelismo' })
    await expect(card.locator('[data-feature-effects]')).toHaveText('Bônus de dano +2 (corpo a corpo de uma mão)')
    await shot(player, 'feature-effect')

    // The roll panel: the Warhammer damage carries "(Duelismo) 2"; the Maul does not.
    await player.locator('[data-view="mesa"]').click()
    await player.getByRole('button', { name: 'Modo Jogo' }).click()
    const panel = player.locator('[data-player-roll-panel]')
    await expect(panel).toBeVisible()
    const select = panel.locator('[data-source-select]')
    const source = async (name: string) => select.selectOption((await select.locator('option', { hasText: name }).first().getAttribute('value'))!)
    await source('Martelo de Guerra')
    await panel.locator('[data-kind="damage"]').click()
    await expect(panel.locator('[data-expression]')).toContainText('(Duelismo) 2')
    await shot(player, 'roll-duelismo')
    // Two hands (versatile 1d10): Dueling does not apply.
    await panel.locator('[data-versatile]').check()
    await expect(panel.locator('[data-expression]')).toContainText('1d10')
    await expect(panel.locator('[data-expression]')).not.toContainText('Duelismo')
    await panel.locator('[data-versatile]').uncheck()
    await expect(panel.locator('[data-expression]')).toContainText('(Duelismo) 2')
    await source('Malho')
    await panel.locator('[data-kind="damage"]').click()
    await expect(panel.locator('[data-expression]')).not.toContainText('Duelismo')

    // Removing the feature takes the bonus back out.
    await player.getByRole('button', { name: 'Voltar à visão completa' }).click()
    await player.locator('[data-view="ficha"]').click()
    await player.locator('[data-tab="caracteristicas"]').click()
    await card.locator('[data-remove-feature]').click()
    await card.locator('[data-confirm-remove]').click()
    await expect(player.locator('[data-feature-empty]')).toBeVisible()
    await player.locator('[data-view="mesa"]').click()
    await player.getByRole('button', { name: 'Modo Jogo' }).click()
    await source('Martelo de Guerra')
    await panel.locator('[data-kind="damage"]').click()
    await expect(panel.locator('[data-expression]')).not.toContainText('Duelismo')
    await context.close()
  })
}
