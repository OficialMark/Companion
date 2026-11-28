import { mkdirSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'
import { expect, test, type Browser, type Page } from '@playwright/test'
import { assertSafeBaseUrl } from './guard'

/**
 * Layout audit (visual hierarchy, contrast, proximity, alignment). Walks every main screen of the
 * master and of a player at six widths and measures, in the browser, what breaks a layout:
 * horizontal page scroll, controls covered by other elements, controls pushed off-screen, text cut
 * without ellipsis, touch targets under 44 px on phones, low-contrast text, and how much of a wide
 * screen the content uses. Screenshots and a JSON report go to LAYOUT_AUDIT_DIR.
 *
 * The run is a gate: the per-screen limits at the bottom must hold. LAYOUT_AUDIT_ONLY=1 collects the
 * report without failing (used for the "before" baseline).
 *
 *   $env:E2E_BASE_URL='http://127.0.0.1:3616'; $env:LAYOUT_AUDIT_DIR='…'; npx playwright test layout-audit --project=desktop
 */
const OUT = process.env.LAYOUT_AUDIT_DIR ?? join(process.cwd(), 'test-results', 'layout-audit')
const AUDIT_ONLY = process.env.LAYOUT_AUDIT_ONLY === '1'
const WIDTHS = [
  { name: 'phone-360', width: 360, height: 740, mobile: true },
  { name: 'phone-390', width: 390, height: 844, mobile: true },
  { name: 'tablet-768', width: 768, height: 1024, mobile: true },
  { name: 'tablet-1024', width: 1024, height: 768, mobile: true },
  { name: 'laptop-1280', width: 1280, height: 800, mobile: false },
  { name: 'desktop-1920', width: 1920, height: 1080, mobile: false },
  { name: 'wide-2560', width: 2560, height: 1440, mobile: false }
] as const

type Session = Record<string, string>
interface Finding { kind: string; detail: string }
interface ScreenReport {
  screen: string
  viewport: string
  horizontalOverflow: number
  covered: Finding[]
  offscreen: Finding[]
  clipped: Finding[]
  smallTargets: Finding[]
  lowContrast: Finding[]
  contentWidthRatio: number
  contentWidth: number
  marginDifference: number
  columns: number
}

test.describe.configure({ mode: 'serial' })
test.use({ actionTimeout: 10_000 })

test('layout audit across widths', async ({ browser }, info) => {
  test.skip(info.project.name !== 'desktop', 'one browser drives every viewport')
  test.setTimeout(900_000)
  assertSafeBaseUrl()
  mkdirSync(OUT, { recursive: true })

  const { master, player } = await seedTable(browser)
  const reports: ScreenReport[] = []

  for (const viewport of WIDTHS) {
    for (const role of ['master', 'player'] as const) {
      const context = await browser.newContext({
        viewport: { width: viewport.width, height: viewport.height },
        isMobile: viewport.mobile && viewport.width < 768,
        hasTouch: viewport.mobile,
        deviceScaleFactor: 1
      })
      const session = role === 'master' ? master : player
      await context.addInitScript(entries => {
        for (const [key, value] of Object.entries(entries)) localStorage.setItem(key, value)
      }, session)
      const page = await context.newPage()
      await page.goto('/')
      await page.locator('.dashboard').waitFor()
      await page.waitForTimeout(400)

      const screens = role === 'master'
        ? [['mesa', null], ['mesa-compendio', 'picker'], ['compendio', 'compendio'], ['campanha', 'campanha']] as const
        : [['mesa', null], ['mesa-compendio', 'picker'], ['ficha', 'ficha'], ['campanha', 'campanha']] as const
      for (const [screen, action] of screens) {
        await open(page, action)
        const name = `${role}-${screen}`
        reports.push({ screen: name, viewport: viewport.name, ...(await measure(page, viewport.mobile && viewport.width < 768)) })
        await page.screenshot({ path: join(OUT, `${viewport.name}__${name}.png`), fullPage: true })
        // What the user actually sees on arrival: full-page shots paint fixed bars mid-page.
        await page.screenshot({ path: join(OUT, `${viewport.name}__${name}__view.png`) })
        console.log(`[audit] ${viewport.name} ${name}`)
      }
      await context.close()
    }
  }

  writeFileSync(join(OUT, 'report.json'), JSON.stringify(reports, null, 2))
  writeFileSync(join(OUT, 'summary.txt'), summarize(reports))
  if (AUDIT_ONLY) return

  for (const report of reports) {
    const where = `${report.viewport} ${report.screen}`
    expect.soft(report.horizontalOverflow, `${where}: rolagem horizontal`).toBeLessThanOrEqual(0)
    expect.soft(report.covered, `${where}: controles cobertos`).toEqual([])
    expect.soft(report.offscreen, `${where}: controles fora da tela`).toEqual([])
    expect.soft(report.clipped, `${where}: texto cortado sem reticências`).toEqual([])
    expect.soft(report.smallTargets, `${where}: alvos de toque < 44 px`).toEqual([])
    expect.soft(report.lowContrast, `${where}: contraste < 4.5:1`).toEqual([])
    if (report.screen.endsWith('-mesa') || report.screen.endsWith('-mesa-compendio')) {
      const desktop = ['laptop-1280', 'desktop-1920', 'wide-2560'].includes(report.viewport)
      expect.soft(report.columns, `${where}: número de colunas na Mesa`).toBe(desktop ? 3 : 1)
    }
    if (report.viewport === 'desktop-1920' || report.viewport === 'wide-2560') {
      if (report.screen === 'master-compendio' || report.screen.endsWith('-campanha')) {
        expect.soft(report.contentWidth, `${where}: largura máxima`).toBe(1380)
        expect.soft(report.marginDifference, `${where}: diferença entre margens`).toBeLessThanOrEqual(1)
      } else {
        expect.soft(report.contentWidthRatio, `${where}: largura usada`).toBeGreaterThanOrEqual(0.8)
      }
    }
  }
})

async function open(page: Page, action: string | null) {
  const nav = async (view: string) => {
    // Through the DOM: an unclickable nav item is itself a finding (reported as covered/offscreen),
    // it must not stop the audit of the other screens.
    await page.locator(`[data-view="${view}"]`).first().evaluate(element => (element as HTMLElement).click())
    await page.waitForTimeout(500)
    await page.evaluate(() => window.scrollTo(0, 0))
  }
  if (action === null) await nav('mesa')
  else if (action === 'picker') {
    await nav('mesa')
    const toggle = page.locator('[data-board-catalog]')
    if (await toggle.count() && (await toggle.getAttribute('aria-expanded')) !== 'true') await toggle.evaluate(element => (element as HTMLElement).click())
    await page.locator('[data-token-source-picker] li').first().waitFor({ timeout: 8000 }).catch(() => {})
  } else await nav(action)
  // The map image is fetched with the participant token: let it paint before measuring.
  await page.locator('[data-board-surface] image').first().waitFor({ timeout: 4000 }).catch(() => {})
  await page.waitForTimeout(500)
}

/** Creates a table with a map, tokens and a player, and returns both sessions' localStorage. */
async function seedTable(browser: Browser): Promise<{ master: Session; player: Session }> {
  const context = await browser.newContext({ viewport: { width: 1280, height: 900 } })
  const page = await context.newPage()
  await page.goto('/')
  await page.getByLabel('Nome da campanha').fill('Ruínas de Valdren')
  await page.getByLabel('Seu nome').fill('Mestre Auditor')
  await page.getByRole('button', { name: 'Criar e entrar' }).click()
  const playerCode = await page.locator('.created-code').first().innerText()
  await page.getByRole('button', { name: 'Entrar como mestre' }).click()
  await page.getByRole('button', { name: 'Iniciar preparação' }).click()

  // A realistic 1600x1200 map drawn in the page (no binary fixture in the repo).
  const map = await page.evaluate(() => {
    const canvas = document.createElement('canvas')
    canvas.width = 1600
    canvas.height = 1200
    const g = canvas.getContext('2d')!
    g.fillStyle = '#3c3a2f'
    g.fillRect(0, 0, 1600, 1200)
    for (let i = 0; i < 40; i++) {
      g.fillStyle = `hsl(${30 + (i * 7) % 40}, 25%, ${20 + (i * 13) % 25}%)`
      g.fillRect((i * 173) % 1500, (i * 97) % 1100, 120 + (i * 31) % 200, 80 + (i * 17) % 160)
    }
    return canvas.toDataURL('image/png').split(',')[1]!
  })
  await page.getByLabel('Imagem do battlemap').setInputFiles({ name: 'ruinas.png', mimeType: 'image/png', buffer: Buffer.from(map, 'base64') })
  await page.getByLabel('Colunas do grid').fill('24')
  await page.getByLabel('Linhas do grid').fill('18')
  // Without fog: the player's screenshots must show the map, so the critic judges layout, not fog.
  const fog = page.locator('[data-fog-publish-toggle]')
  if (await fog.isChecked().catch(() => false)) await fog.uncheck({ force: true })
  await page.getByRole('button', { name: 'Publicar battlemap' }).click()
  await page.locator('[data-board-surface]').waitFor()
  await page.locator('[data-new-marker-label]').fill('Lobo invocado')
  await page.locator('[data-new-marker-submit]').click()
  // A few rolls so the history panel has content (the master panel has quick-die buttons).
  for (const die of ['D20', 'D8', 'D6']) {
    await page.getByRole('button', { name: die, exact: true }).first().click({ timeout: 3000 }).catch(() => {})
    await page.waitForTimeout(300)
  }
  const master = await page.evaluate(() => Object.fromEntries(Object.entries(localStorage)))

  const playerContext = await browser.newContext({ viewport: { width: 1280, height: 900 } })
  const playerPage = await playerContext.newPage()
  await playerPage.goto('/')
  await playerPage.getByRole('tab', { name: 'Entrar com código' }).click()
  await playerPage.getByLabel('Código da campanha').fill(playerCode)
  await playerPage.getByLabel('Seu nome').fill('Ayla Ventoleste')
  await playerPage.getByRole('button', { name: 'Entrar na campanha' }).click()
  await playerPage.locator('.dashboard').waitFor()
  // One roll of the player's own, so the shots show its result right under "Rolar".
  await playerPage.locator('[data-die="d20"]').first().click({ timeout: 5000 }).catch(() => {})
  await playerPage.locator('[data-roll-go]').first().click({ timeout: 5000 }).catch(() => {})
  await playerPage.locator('[data-roll-last]').first().waitFor({ timeout: 8000 }).catch(() => {})
  const player =await playerPage.evaluate(() => Object.fromEntries(Object.entries(localStorage)))
  await playerContext.close()
  await context.close()
  return { master, player }
}

async function measure(page: Page, phone: boolean) {
  return page.evaluate(phoneWidth => {
    const W = window.innerWidth
    const label = (el: Element) => {
      const text = (el.getAttribute('aria-label') || (el as HTMLElement).innerText || el.getAttribute('placeholder') || '').trim().replace(/\s+/g, ' ').slice(0, 40)
      const cls = typeof el.className === 'string' ? el.className.split(' ').filter(Boolean).slice(0, 2).join('.') : ''
      return `${el.tagName.toLowerCase()}${cls ? '.' + cls : ''} "${text}"`
    }
    const visible = (el: Element) => {
      // Content of a closed <details> keeps a layout box in Chromium but is not rendered.
      const closed = el.closest('details:not([open])')
      if (closed && !el.closest('summary')) return false
      const style = getComputedStyle(el)
      if (style.visibility === 'hidden' || style.display === 'none' || Number(style.opacity) === 0) return false
      const rect = el.getBoundingClientRect()
      return rect.width > 1 && rect.height > 1
    }
    /** Ancestor that clips horizontally (scroll containers are fine: the control is reachable). */
    const clippedByAncestor = (el: Element) => {
      for (let node = el.parentElement; node && node !== document.body; node = node.parentElement) {
        const overflowX = getComputedStyle(node).overflowX
        if (overflowX !== 'visible') return true
      }
      return false
    }
    const inBoard = (el: Element) => Boolean(el.closest('svg, [data-board-surface], .battlemap-viewport'))
    const interactive = [...document.querySelectorAll('button, a[href], input:not([type=hidden]), select, textarea, [role=button], [role=tab], summary')]
      .filter(el => visible(el) && !inBoard(el) && !(el as HTMLInputElement).disabled || (visible(el) && !inBoard(el)))
      .filter((el, index, all) => all.indexOf(el) === index)
      .filter(el => !(el instanceof HTMLInputElement && ['radio', 'checkbox'].includes(el.type) && getComputedStyle(el).opacity === '0'))

    const covered: { kind: string; detail: string }[] = []
    const offscreen: { kind: string; detail: string }[] = []
    const smallTargets: { kind: string; detail: string }[] = []
    for (const el of interactive) {
      const rect = el.getBoundingClientRect()
      if (rect.right > W + 1 || rect.left < -1) {
        if (!clippedByAncestor(el)) offscreen.push({ kind: 'offscreen', detail: `${label(el)} x=${Math.round(rect.left)}..${Math.round(rect.right)}` })
        continue
      }
      // Only judge what is on screen right now (sticky bars, modals) and inside the viewport vertically.
      if (rect.bottom < 0 || rect.top > window.innerHeight) continue
      // Scrolled out of its own scroll box (sticky side columns): reachable by scrolling it, not covered.
      let hiddenInScroller = false
      for (let node = el.parentElement; node && node !== document.body; node = node.parentElement) {
        const style = getComputedStyle(node)
        if (style.overflowY === 'visible' && style.overflowX === 'visible') continue
        const box = node.getBoundingClientRect()
        if (rect.top + rect.height / 2 > box.bottom || rect.top + rect.height / 2 < box.top) { hiddenInScroller = true; break }
      }
      if (hiddenInScroller) continue
      // Probe the center and two points 25% in from opposite corners: corners of pills and
      // rounded buttons belong to the parent, so probing the very corner would be a false alarm.
      const dx = rect.width / 4, dy = rect.height / 4
      const points = [[rect.left + rect.width / 2, rect.top + rect.height / 2], [rect.left + dx, rect.top + dy], [rect.right - dx, rect.bottom - dy]]
      /** A sticky/fixed bar (top bar, sheet summary) over scrolled content is expected; inside the bar it is a bug. */
      const pinned = (node: Element | null) => {
        for (let current = node; current; current = current.parentElement) {
          const position = getComputedStyle(current).position
          if (position === 'sticky' || position === 'fixed') return current
        }
        return null
      }
      const blockers = points.map(([x, y]) => {
        if (x! < 0 || y! < 0 || x! >= W || y! >= window.innerHeight) return null
        const top = document.elementFromPoint(x!, y!)
        if (!top || top === el || el.contains(top) || top.contains(el)) return null
        const bar = pinned(top)
        if (bar && !bar.contains(el)) return null
        if (top.closest('.modal-overlay') && !el.closest('.modal-overlay')) return null
        return top
      }).filter(Boolean) as Element[]
      if (blockers.length >= 2) covered.push({ kind: 'covered', detail: `${label(el)} under ${label(blockers[0]!)}` })
      if (phoneWidth && (rect.width < 43.5 || rect.height < 43.5)) {
        const inline = el.tagName === 'A' && getComputedStyle(el).display === 'inline'
        if (!inline) smallTargets.push({ kind: 'small', detail: `${label(el)} ${Math.round(rect.width)}x${Math.round(rect.height)}` })
      }
    }

    const clipped: { kind: string; detail: string }[] = []
    const lowContrast: { kind: string; detail: string }[] = []
    const parse = (color: string) => {
      const match = color.match(/rgba?\(([^)]+)\)/)
      if (!match) return null
      const [r, g, b, a = '1'] = match[1]!.split(/[ ,/]+/).filter(Boolean)
      return [Number(r), Number(g), Number(b), Number(a)] as const
    }
    const luminance = ([r, g, b]: readonly number[]) => {
      const c = [r, g, b].map(v => { const s = v! / 255; return s <= .03928 ? s / 12.92 : ((s + .055) / 1.055) ** 2.4 })
      return .2126 * c[0]! + .7152 * c[1]! + .0722 * c[2]!
    }
    const backgroundOf = (el: Element) => {
      // Composite translucent layers bottom-up over the page background.
      const layers: (readonly number[])[] = []
      for (let node: Element | null = el; node; node = node.parentElement) {
        const style = getComputedStyle(node)
        if (style.backgroundImage !== 'none' && node !== document.body && node !== document.documentElement) return null // gradient/image: skip
        const bg = parse(style.backgroundColor)
        if (bg && bg[3] > 0) { layers.push(bg); if (bg[3] >= 1) break }
      }
      let base: number[] = [23, 18, 22]
      for (const layer of layers.reverse()) base = base.map((v, i) => v * (1 - layer[3]!) + layer[i]! * layer[3]!)
      return base
    }
    const seen = new Set<string>()
    for (const el of document.querySelectorAll('body *')) {
      if (inBoard(el) || !visible(el)) continue
      const own = [...el.childNodes].some(node => node.nodeType === 3 && node.textContent!.trim().length > 1)
      if (!own) continue
      const style = getComputedStyle(el)
      const rect = el.getBoundingClientRect()
      if (rect.bottom < 0 || rect.top > document.documentElement.scrollHeight) continue
      const html = el as HTMLElement
      if (style.overflow !== 'visible' && style.textOverflow !== 'ellipsis' && style.webkitLineClamp === 'none' && html.scrollWidth > html.clientWidth + 2 && style.whiteSpace === 'nowrap') {
        clipped.push({ kind: 'clipped', detail: `${label(el)} ${html.scrollWidth}>${html.clientWidth}` })
      }
      const fg = parse(style.color)
      const bg = backgroundOf(el)
      if (!fg || !bg) continue
      const blended = [0, 1, 2].map(i => fg[i]! * fg[3] + bg[i]! * (1 - fg[3]))
      const l1 = luminance(blended), l2 = luminance(bg)
      const ratio = (Math.max(l1, l2) + .05) / (Math.min(l1, l2) + .05)
      const size = parseFloat(style.fontSize)
      const large = size >= 24 || (size >= 18.66 && Number(style.fontWeight) >= 700)
      if (ratio < (large ? 3 : 4.5)) {
        const key = `${label(el)}`
        if (!seen.has(key)) { seen.add(key); lowContrast.push({ kind: 'contrast', detail: `${key} ${ratio.toFixed(2)}:1` }) }
      }
    }

    const dashboard = document.querySelector('.dashboard')?.getBoundingClientRect()
    const grid = document.querySelector('.dashboard-grid, .compendium-layout, .sheet-layout, .knowledge-layout')
    const columns = grid ? getComputedStyle(grid).gridTemplateColumns.split(' ').filter(Boolean).length : 1
    // scrollWidth alone misses content that sticks out while html/body clip it (the full-page shot
    // still widens): also take the right edge of every element no ancestor clips.
    let widest = 0
    let widestLabel = ''
    for (const el of document.querySelectorAll('body *')) {
      if (inBoard(el) || !visible(el) || clippedByAncestor(el)) continue
      const right = el.getBoundingClientRect().right
      if (right > widest) { widest = right; widestLabel = label(el) }
    }
    // A tab scrolled out of its own tab list is a navigation item nobody sees (Ficha "Rola..." bug).
    for (const list of document.querySelectorAll('[role=tablist]')) {
      if (!visible(list)) continue
      const box = list.getBoundingClientRect()
      for (const tab of list.querySelectorAll('[role=tab]')) {
        const rect = tab.getBoundingClientRect()
        if (rect.right > box.right + 1 || rect.left < box.left - 1) clipped.push({ kind: 'tab-hidden', detail: `${label(tab)} fora da lista de abas` })
      }
    }
    // The campaign name is the page's context: squeezed to a sliver it is as good as gone (1280 px bug).
    const names = [...document.querySelectorAll('.topbar .campaign-name')].filter(visible)
    const nameWidth = names.length ? Math.round(Math.max(...names.map(el => el.getBoundingClientRect().width))) : 0
    if (nameWidth < 100) clipped.push({ kind: 'campaign-name', detail: `nome da campanha com ${nameWidth}px na barra` })
    const overflow = Math.max(document.documentElement.scrollWidth - W, Math.round(widest - W))
    if (overflow > 0) offscreen.push({ kind: 'overflow', detail: `widest ${widestLabel} right=${Math.round(widest)}` })
    return {
      horizontalOverflow: overflow,
      covered, offscreen, clipped, smallTargets, lowContrast,
      contentWidthRatio: dashboard ? Number((dashboard.width / W).toFixed(2)) : 0,
      contentWidth: dashboard ? Math.round(dashboard.width) : 0,
      marginDifference: dashboard ? Math.abs(dashboard.left - (document.documentElement.clientWidth - dashboard.right)) : Infinity,
      columns
    }
  }, phone)
}

function summarize(reports: ScreenReport[]) {
  const lines = ['viewport | screen | hOverflow | covered | offscreen | clipped | small | contrast | width% | cols']
  for (const r of reports) {
    lines.push(`${r.viewport} | ${r.screen} | ${r.horizontalOverflow} | ${r.covered.length} | ${r.offscreen.length} | ${r.clipped.length} | ${r.smallTargets.length} | ${r.lowContrast.length} | ${Math.round(r.contentWidthRatio * 100)} | ${r.columns}`)
  }
  return lines.join('\n')
}
