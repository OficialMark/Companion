import { expect, test, type Locator, type Page } from '@playwright/test'

// Spec 009 end to end: the Compendium is a top-bar screen that survives a reload, the master can
// filter and create creatures there, and the sheet (view and edit) has no overlapping text, no
// control under 44 px and no horizontal scroll. SHOTS=<dir> also saves screenshots for review.
const shots = process.env.SHOTS
async function shot(page: Page, name: string) {
  if (shots) await page.screenshot({ path: `${shots}/${test.info().project.name}-${page.viewportSize()!.width}-${name}.png` })
}

/** Pairs of visible text boxes inside `root` that overlap (neither contains the other). */
async function overlaps(root: Locator) {
  return root.evaluate(element => {
    const nodes = [...element.querySelectorAll<HTMLElement>('h1, h2, h3, p, dt, dd, legend, label, strong, small, span, button, input, select, textarea, output')]
      .filter(node => {
        const style = getComputedStyle(node)
        const box = node.getBoundingClientRect()
        return style.visibility !== 'hidden' && style.display !== 'none' && box.width > 2 && box.height > 2 && node.closest('.sr-only') === null
      })
    // What the user actually sees of a node: one box per rendered line (inline text that wraps is
    // several boxes, not one rectangle), each cut by every ancestor that clips (scroll areas).
    type Box = { left: number, top: number, right: number, bottom: number }
    function visible(node: HTMLElement): Box[] {
      return [...node.getClientRects()].map(rect => {
        let { left, top, right, bottom } = rect
        for (let parent = node.parentElement; parent; parent = parent.parentElement) {
          if (getComputedStyle(parent).overflow === 'visible') continue
          const box = parent.getBoundingClientRect()
          left = Math.max(left, box.left); top = Math.max(top, box.top); right = Math.min(right, box.right); bottom = Math.min(bottom, box.bottom)
        }
        return { left, top, right, bottom }
      }).filter(box => box.right - box.left > 2 && box.bottom - box.top > 2)
    }
    const hit = (ra: Box, rb: Box) => Math.min(ra.right, rb.right) - Math.max(ra.left, rb.left) > 2 && Math.min(ra.bottom, rb.bottom) - Math.max(ra.top, rb.top) > 2
    const boxes = new Map(nodes.map(node => [node, visible(node)]))
    const found: string[] = []
    for (let i = 0; i < nodes.length; i++) {
      for (let j = i + 1; j < nodes.length; j++) {
        const a = nodes[i]!, b = nodes[j]!
        if (a.contains(b) || b.contains(a)) continue
        const la = boxes.get(a)!, lb = boxes.get(b)!
        if (la.some(ra => lb.some(rb => hit(ra, rb)))) found.push(`${a.tagName}.${a.className}("${a.textContent?.trim().slice(0, 20)}") × ${b.tagName}.${b.className}("${b.textContent?.trim().slice(0, 20)}")`)
      }
    }
    return found
  })
}

/** Visible interactive controls smaller than 44 × 44 px. */
async function smallTargets(root: Locator) {
  return root.evaluate(element => [...element.querySelectorAll<HTMLElement>('button, input:not([type="file"]), select, textarea, a[href], [role="button"]')]
    // WCAG 2.5.8 exempts links inside a sentence (the Pocket DM credit) from the target size.
    .filter(node => !(node.tagName === 'A' && node.closest('p')))
    .filter(node => node.closest('.sr-only') === null && getComputedStyle(node).display !== 'none' && node.getClientRects().length > 0)
    .map(node => ({ node, box: node.getBoundingClientRect() }))
    .filter(({ box }) => box.width < 43.5 || box.height < 43.5)
    .map(({ node, box }) => `${node.tagName}.${node.className} "${(node.getAttribute('aria-label') || node.textContent || '').trim().slice(0, 24)}" ${Math.round(box.width)}x${Math.round(box.height)}`))
}

async function noHorizontalScroll(page: Page) {
  const overflow = await page.evaluate(() => {
    const areas = [...document.querySelectorAll('.creature-sheet-body, .creature-editor-fields')]
    return Math.max(document.documentElement.scrollWidth - document.documentElement.clientWidth, ...areas.map(area => area.scrollWidth - area.clientWidth))
  })
  expect(overflow).toBeLessThanOrEqual(1)
}

/**
 * Editor grid rules (spec 009, D5): no row ends in an empty cell, and on phones (<= 680 px) only
 * numbers sit side by side; every text field or select takes the whole row.
 */
async function gridProblems(page: Page) {
  return page.locator('.creature-editor').evaluate(editor => {
    const problems: string[] = []
    const phone = window.innerWidth <= 680
    for (const fieldset of editor.querySelectorAll<HTMLElement>('.creature-fieldset')) {
      const legend = fieldset.querySelector(':scope > legend')
      const top = fieldset.getBoundingClientRect().top + parseFloat(getComputedStyle(fieldset).borderTopWidth)
      if (legend && legend.getBoundingClientRect().top < top + 4) problems.push(`legend "${legend.textContent}" sits on the border`)
    }
    for (const label of editor.querySelectorAll<HTMLElement>('.creature-grid > label')) {
      const field = label.querySelector('input, select')
      const range = document.createRange()
      if (label.firstChild) range.selectNodeContents(label.firstChild)
      if (field && label.firstChild && range.getBoundingClientRect().bottom > field.getBoundingClientRect().top - 4) problems.push(`label "${label.firstChild.textContent?.trim()}" is not above its field`)
    }
    for (const grid of editor.querySelectorAll<HTMLElement>('.creature-grid')) {
      const style = getComputedStyle(grid)
      const box = grid.getBoundingClientRect()
      const left = box.left + parseFloat(style.paddingLeft) + parseFloat(style.borderLeftWidth)
      const right = box.right - parseFloat(style.paddingRight) - parseFloat(style.borderRightWidth)
      const labels = [...grid.querySelectorAll<HTMLElement>(':scope > label')]
      const rows = new Map<number, HTMLElement[]>()
      for (const label of labels) {
        const top = Math.round(label.getBoundingClientRect().top)
        rows.set(top, [...(rows.get(top) ?? []), label])
      }
      for (const row of rows.values()) {
        const last = Math.max(...row.map(label => label.getBoundingClientRect().right))
        if (right - last > 2) problems.push(`${grid.querySelector('legend')?.textContent}: row "${row.map(label => label.firstChild?.textContent?.trim()).join(' | ')}" ends ${Math.round(right - last)}px early`)
      }
      if (!phone) continue
      for (const label of labels) {
        const field = label.querySelector('input, select')
        const isNumber = field instanceof HTMLInputElement && field.type === 'number' || label.textContent?.trim() === 'ND'
        const width = label.getBoundingClientRect()
        if (!isNumber && (width.left - left > 2 || right - width.right > 2)) problems.push(`text field "${label.firstChild?.textContent?.trim()}" is not full width on a phone`)
      }
    }
    return problems
  })
}

/** Walks the whole scroll area, checking every screenful. */
async function checkSheetEverywhere(page: Page, label: string) {
  // While editing only the fields scroll (the action bar is a fixed row); otherwise the whole body.
  const body = (await page.locator('.creature-editor-fields').count()) ? page.locator('.creature-editor-fields') : page.locator('.creature-sheet-body')
  const { scrollHeight, clientHeight } = await body.evaluate(element => ({ scrollHeight: element.scrollHeight, clientHeight: element.clientHeight }))
  for (let top = 0, step = 0; top < scrollHeight; top += clientHeight * 0.8, step++) {
    await body.evaluate((element, value) => { element.scrollTop = value }, top)
    if (step < 2) await shot(page, `${label}-${step}`)
    expect(await overlaps(page.locator('.creature-sheet')), `${label} overlap at ${top}px (scroll ${scrollHeight}/${clientHeight})`).toEqual([])
  }
  expect(await smallTargets(page.locator('.creature-sheet')), `${label} small targets`).toEqual([])
  if (await page.locator('.creature-editor').count()) expect(await gridProblems(page), `${label} editor grid`).toEqual([])
  await noHorizontalScroll(page)
}

async function master(page: Page, width: number) {
  const campaign = `Compêndio ${width} ${Date.now()}`
  await page.goto('/')
  await page.getByLabel('Nome da campanha').fill(campaign)
  await page.getByLabel('Seu nome').fill('Mestre')
  await page.getByRole('button', { name: 'Criar e entrar' }).click()
  const playerCode = await page.locator('.created-code').first().innerText()
  await page.getByRole('button', { name: 'Entrar como mestre' }).click()
  await expect(page.locator('[data-master-nav]')).toBeVisible()
  return { playerCode, campaign }
}

async function journey(page: Page, width: number) {
  const { playerCode, campaign } = await master(page, width)
  await shot(page, '0-topbar-mesa')
  // C9: a labelled navigation landmark; C2: the campaign name stays visible at every width.
  await expect(page.getByRole('navigation', { name: 'Áreas do mestre' })).toBeVisible()
  await expect(page.locator('.topbar').getByText(campaign, { exact: true }).locator('visible=true')).toHaveCount(1)
  expect(await smallTargets(page.locator('.topbar')), 'topbar small targets').toEqual([])
  expect(await overlaps(page.locator('.topbar')), 'topbar overlap').toEqual([])

  // US3: one click to the Compendium, full screen, survives a reload.
  await page.getByRole('button', { name: 'Compêndio' }).click()
  await expect(page).toHaveURL(/tela=compendio/)
  await expect(page.getByRole('heading', { level: 1, name: 'Compêndio' })).toBeVisible()
  await expect(page.locator('.master-code-panel')).toHaveCount(0)
  await expect(page.locator('.creature-card').first()).toBeVisible()
  await page.reload()
  await expect(page.getByRole('heading', { level: 1, name: 'Compêndio' })).toBeVisible()
  await expect(page.getByRole('button', { name: 'Compêndio' })).toHaveAttribute('aria-current', 'page')
  await shot(page, '1-compendio')
  if (shots) await page.screenshot({ path: `${shots}/${test.info().project.name}-${width}-1-compendio-full.png`, fullPage: true })
  expect(await smallTargets(page.locator('[data-creature-library]')), 'library small targets').toEqual([])
  await noHorizontalScroll(page)
  // C5: pagination and the Pocket DM credit are part of the screen.
  await expect(page.getByRole('navigation', { name: 'Páginas' })).toBeVisible()
  await expect(page.locator('.creature-credit a[href*="pocketdm"]')).toBeVisible()
  // C4: the five filter selects share each row evenly (same width within a row).
  const rows = await page.locator('.creature-filters select').evaluateAll(selects => {
    const byRow = new Map<number, number[]>()
    for (const select of selects) {
      const box = select.getBoundingClientRect()
      byRow.set(Math.round(box.top), [...(byRow.get(Math.round(box.top)) ?? []), Math.round(box.width)])
    }
    return [...byRow.values()]
  })
  for (const widths of rows.filter(row => row.length > 1)) expect(Math.max(...widths) - Math.min(...widths), `filter widths ${widths}`).toBeLessThanOrEqual(2)
  const edges = await page.locator('.creature-filters').evaluate(filters => ({
    search: Math.round(filters.querySelector('.creature-search')!.getBoundingClientRect().right),
    select: Math.round(Math.max(...[...filters.querySelectorAll('select')].map(select => select.getBoundingClientRect().right)))
  }))
  expect(Math.abs(edges.search - edges.select), `filters end at ${edges.select}, search at ${edges.search}`).toBeLessThanOrEqual(1)

  // "Minhas criaturas" starts empty, then the master creates one and finds it there.
  await page.locator('[data-mine]').click()
  await expect(page.locator('[data-mine-empty]')).toBeVisible()
  await page.locator('[data-new-creature]').click()
  await page.waitForTimeout(250) // open animation
  await checkSheetEverywhere(page, '2-new')
  await page.locator('[data-field="name"]').fill('Lobo das Cinzas')
  await page.locator('[data-save-creature]').click()
  await expect(page.locator('[data-edit-creature]')).toBeVisible()
  await page.getByRole('button', { name: 'Fechar ficha' }).click()
  await expect(page.locator('.creature-card', { hasText: 'Lobo das Cinzas' })).toBeVisible()

  // US1 + US2: a long sheet, read and edited.
  await page.locator('[data-mine]').click()
  await page.getByLabel('Buscar criatura').fill('dragão vermelho adulto')
  await page.locator('.creature-card', { hasText: 'Dragão Vermelho Adulto' }).click()
  await expect(page.locator('[data-edit-creature]')).toBeVisible()
  await page.waitForTimeout(250) // open animation
  await checkSheetEverywhere(page, '3-view')
  await page.locator('[data-edit-creature]').click()
  await checkSheetEverywhere(page, '4-edit')
  await page.getByRole('button', { name: 'Cancelar' }).click()
  await page.getByRole('button', { name: 'Fechar ficha' }).click()

  // Back to the Mesa in one click; a player never gets the Compendium.
  await page.getByRole('button', { name: 'Mesa', exact: true }).click()
  await expect(page).not.toHaveURL(/tela=compendio/)
  await expect(page.locator('.master-code-panel')).toBeVisible()

  const playerContext = await page.context().browser()!.newContext({ viewport: page.viewportSize()! })
  const player = await playerContext.newPage()
  await player.goto(new URL('/?tela=compendio', page.url()).toString())
  await player.getByRole('tab', { name: 'Entrar com código' }).click()
  await player.getByLabel('Código da campanha').fill(playerCode)
  await player.getByLabel('Seu nome').fill('Jogadora')
  await player.getByRole('button', { name: 'Entrar na campanha' }).click()
  await expect(player.getByText('Bem-vindo à mesa')).toBeVisible()
  await expect(player.locator('[data-master-nav]')).toHaveCount(0)
  await expect(player.locator('[data-creature-library]')).toHaveCount(0)
  await playerContext.close()
}

for (const width of [1280, 768, 375]) {
  test(`compendium screen and readable sheet (${width}px)`, async ({ page }) => {
    test.skip(test.info().project.name !== 'desktop', 'widths are set explicitly')
    test.setTimeout(120_000)
    await page.setViewportSize({ width, height: width === 375 ? 812 : 900 })
    await journey(page, width)
  })
}

test('compendium screen and readable sheet (Android)', async ({ page }) => {
  test.skip(test.info().project.name !== 'android', 'Android device project only')
  test.setTimeout(120_000)
  await journey(page, page.viewportSize()!.width)
})
