import { expect, type Page } from '@playwright/test'

/**
 * The player's combat profile comes from the sheet since spec 011 (the manual form left in spec 012):
 * set Destreza (initiative = its modifier) and HP on the Ficha, then return to the Mesa. Dexterity 16
 * gives +3; `hp` is both the base maximum and the current hit points.
 */
export async function setSheetProfile(player: Page, { dexterity, hp }: { dexterity: number; hp: number }) {
  await player.getByRole('button', { name: 'Ficha', exact: true }).click()
  for (const [field, value] of [['dexterity', dexterity], ['baseMaxHitPoints', hp], ['currentHitPoints', hp]] as const) {
    await player.locator(`[data-field="${field}"]`).fill(String(value))
  }
  await expect(player.locator('[data-save-state="saved"]')).toBeVisible({ timeout: 10_000 })
  await player.getByRole('button', { name: 'Mesa', exact: true }).click()
  await expect(player.locator('[data-summary-hp]')).toHaveText(`${hp}/${hp}`, { timeout: 10_000 })
}
