import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'

// Spec 012, FR-008: group titles of the native selects (e.g. the roll source) are readable on a tone
// darker than the options. The numbers come from the stylesheet, so a later edit is checked too.
const css = readFileSync(resolve(__dirname, '../app/assets/css/main.css'), 'utf8')

function rule(selector: string) {
  const match = css.match(new RegExp(`${selector}\\s*\\{([^}]*)\\}`))
  if (!match?.[1]) throw new Error(`regra ${selector} ausente`)
  const value = (prop: string) => match[1]!.match(new RegExp(`(?:^|;)\\s*${prop}:\\s*(#[0-9a-f]{6})`, 'i'))![1]!
  return { background: value('background'), color: value('color') }
}

function luminance(hex: string) {
  const channels = [1, 3, 5].map(index => parseInt(hex.slice(index, index + 2), 16) / 255)
    .map(c => (c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4))
  return 0.2126 * channels[0]! + 0.7152 * channels[1]! + 0.0722 * channels[2]!
}
const contrast = (a: string, b: string) => {
  const [hi, lo] = [luminance(a), luminance(b)].sort((x, y) => y - x)
  return (hi! + 0.05) / (lo! + 0.05)
}

describe('native select colors', () => {
  it('group titles are darker than options and readable (WCAG AA 4.5:1)', () => {
    const option = rule('select option')
    const group = rule('select optgroup')
    expect(luminance(group.background)).toBeLessThan(luminance(option.background))
    expect(contrast(group.color, group.background)).toBeGreaterThanOrEqual(4.5)
    expect(contrast(option.color, option.background)).toBeGreaterThanOrEqual(4.5)
  })

  it('the sheet picker of the master is a dark field, not the white default of the browser', () => {
    const panel = readFileSync(resolve(__dirname, '../app/assets/css/roll-panel.css'), 'utf8')
    expect(panel).toMatch(/\.master-roll-source select\s*\{[^}]*background:\s*var\(--panel-high\)[^}]*color:\s*var\(--ink\)/)
  })
})
