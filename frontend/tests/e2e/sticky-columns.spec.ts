import { expect, test, type Page } from '@playwright/test'
import { assertSafeBaseUrl } from './guard'

// Spec 017: on a laptop the player's "Rolar" and their last result stay inside the window at every
// scroll position (the dice column ends at the window bottom wherever it starts and its action row is
// stuck to its foot), and fitting the column never makes the page height jump while scrolling.
test('the player keeps "Rolar" on screen while the Mesa scrolls (1280x800)', async ({ browser }, info) => {
  test.skip(info.project.name !== 'desktop', 'widths are set explicitly')
  assertSafeBaseUrl()
  const master = await browser.newPage({ viewport: { width: 1280, height: 800 } })
  await master.goto('/')
  await master.getByLabel('Nome da campanha').fill(`Colunas ${Date.now()}`)
  await master.getByLabel('Seu nome').fill('Mestre')
  await master.getByRole('button', { name: 'Criar e entrar' }).click()
  const code = await master.locator('.created-code').first().innerText()
  await master.getByRole('button', { name: 'Entrar como mestre' }).click()
  await master.getByRole('button', { name: 'Iniciar preparação' }).click()
  const png = await master.evaluate(() => {
    const canvas = document.createElement('canvas'); canvas.width = 1200; canvas.height = 900
    const g = canvas.getContext('2d')!; g.fillStyle = '#3c3a2f'; g.fillRect(0, 0, 1200, 900)
    return canvas.toDataURL('image/png').split(',')[1]!
  })
  await master.getByLabel('Imagem do battlemap').setInputFiles({ name: 'm.png', mimeType: 'image/png', buffer: Buffer.from(png, 'base64') })
  await master.getByRole('button', { name: 'Publicar battlemap' }).click()
  await master.locator('[data-board-surface]').waitFor()

  for (const displayName of ['Lia', 'Caio', 'Bia', 'Rui']) {
    const join = await master.request.post('/api/campaigns/join', { data: { code, displayName } })
    expect(join.ok()).toBeTruthy()
  }

  const page = await browser.newPage({ viewport: { width: 1280, height: 800 } })
  await page.goto('/')
  await page.getByRole('tab', { name: 'Entrar com código' }).click()
  await page.getByLabel('Código da campanha').fill(code)
  await page.getByLabel('Seu nome').fill('Ayla')
  await page.getByRole('button', { name: 'Entrar na campanha' }).click()
  await page.locator('[data-roll-go]').waitFor()
  await expect(page.locator('.table-participants .participant-list > li')).toHaveCount(6)
  await page.locator('[data-die="d20"]').first().click()
  await page.locator('[data-roll-go]').click()
  await page.locator('[data-roll-last-chip]').waitFor({ timeout: 10_000 })
  await page.evaluate(() => window.scrollTo(0, 0))
  await page.waitForTimeout(400)

  const history = page.getByRole('region', { name: 'Histórico', exact: true })
  const firstRoll = history.locator('.roll-list > li').first()
  await expectDesktopColumns(page)
  await expect(page.locator('.table-people').getByRole('region', { name: 'Histórico', exact: true })).toHaveCount(1)
  await expect(firstRoll).toBeInViewport({ ratio: 0.9 })

  const heights: number[] = []
  for (const y of [0, 120, 240, 400, 700, 1200]) {
    await page.evaluate(top => window.scrollTo(0, top), y)
    await page.waitForTimeout(120)
    const state = await page.evaluate(() => {
      const button = document.querySelector('[data-roll-go]')!.getBoundingClientRect()
      const chip = document.querySelector('[data-roll-last-chip]')?.getBoundingClientRect()
      return { top: button.top, bottom: button.bottom, chipBottom: chip?.bottom ?? null, height: document.documentElement.scrollHeight, vh: innerHeight }
    })
    expect(state.bottom, `Rolar fora da tela com scrollY=${y}`).toBeLessThanOrEqual(state.vh)
    expect(state.top, `Rolar acima da tela com scrollY=${y}`).toBeGreaterThanOrEqual(0)
    // The player's own last result rides with "Rolar" in the stuck footer (review round 6).
    expect(state.chipBottom, `último resultado ausente com scrollY=${y}`).not.toBeNull()
    expect(state.chipBottom!, `último resultado fora da tela com scrollY=${y}`).toBeLessThanOrEqual(state.vh)
    heights.push(state.height)
  }
  expect(new Set(heights).size, `altura da página mudou ao rolar: ${heights.join(', ')}`).toBe(1)
  await page.setViewportSize({ width: 1920, height: 1080 })
  await page.evaluate(() => window.scrollTo(0, 0))
  await expectDesktopColumns(page)
  await master.setViewportSize({ width: 1920, height: 1080 })
  await expectDesktopColumns(master)
  await expect(page.locator('.table-people').getByRole('region', { name: 'Histórico', exact: true })).toHaveCount(1)
  await expect(firstRoll).toBeInViewport({ ratio: 0.9 })

  await page.setViewportSize({ width: 390, height: 844 })
  const mobileParticipants = await page.locator('.table-participants').boundingBox()
  const mobileHistory = await history.boundingBox()
  expect(mobileHistory!.y).toBeGreaterThanOrEqual(mobileParticipants!.y + mobileParticipants!.height)
  await expect(history).toHaveCount(1)
  await expect(firstRoll).toContainText('Ayla')
  await master.close()
  await page.close()
})

async function expectDesktopColumns(page: Page) {
  const roll = (await page.locator('.table-roll').boundingBox())!
  const map = (await page.locator('.table-map').boundingBox())!
  const participants = (await page.locator('.table-participants').boundingBox())!
  const history = (await page.getByRole('region', { name: 'Histórico', exact: true }).boundingBox())!
  expect(roll.x + roll.width).toBeLessThanOrEqual(map.x)
  expect(map.x + map.width).toBeLessThanOrEqual(participants.x)
  expect(history.x).toBeCloseTo(participants.x, 0)
  expect(history.y).toBeGreaterThanOrEqual(participants.y + participants.height)
}
