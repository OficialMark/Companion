import { expect, test } from '@playwright/test'

test('admin secret lists and deletes a campaign after confirmation', async ({ page, request }) => {
  const adminKey = process.env.E2E_ADMIN_KEY
  test.skip(!adminKey, 'E2E_ADMIN_KEY is required for the destructive admin journey')

  const creation = await request.post('/api/campaigns', {
    data: { campaignName: `Admin E2E ${Date.now()}`, displayName: 'Mestre Admin E2E' }
  })
  expect(creation.ok()).toBeTruthy()
  const { playerCode } = await creation.json() as { playerCode: string }

  await page.goto('/admin/')
  await page.getByLabel('Credencial administrativa').fill(adminKey!)
  await page.getByRole('button', { name: 'Acessar mesas' }).click()
  const row = page.locator('tr', { hasText: playerCode })
  await expect(row).toBeVisible()
  page.once('dialog', dialog => dialog.accept())
  await row.getByRole('button', { name: 'Excluir' }).click()
  await expect(row).toHaveCount(0)
})
