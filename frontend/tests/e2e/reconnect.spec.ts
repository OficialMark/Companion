import { expect, test } from '@playwright/test'

test('reconnects, resynchronizes and reloads without duplicate rolls', async ({ page, request }) => {
  await page.goto('/')
  await page.getByLabel('Nome da campanha').fill(`Reconexão ${Date.now()}`)
  await page.getByLabel('Seu nome').fill('Mestre Reconexão')
  await page.getByRole('button', { name: 'Criar e entrar' }).click()
  await page.getByRole('button', { name: 'Entrar como mestre' }).click()
  await expect(page.getByText('Bem-vindo à mesa')).toBeVisible()

  const session = await page.evaluate(() =>
    JSON.parse(localStorage.getItem('companion.session.v1') ?? 'null') as {
      campaignId: string
      participantToken: string
    }
  )

  await page.context().setOffline(true)
  await expect(page.locator('.connection.offline')).toBeVisible()

  const response = await request.post(`/api/campaigns/${session.campaignId}/dice-rolls`, {
    headers: {
      'X-Participant-Token': session.participantToken,
      'Idempotency-Key': crypto.randomUUID()
    },
    data: { expression: 'd20' }
  })
  expect(response.ok()).toBeTruthy()

  await page.context().setOffline(false)
  await expect(page.locator('.connection.online')).toBeVisible({ timeout: 20_000 })
  await expect(page.locator('.roll-list li')).toHaveCount(1)

  await page.getByRole('button', { name: 'D20', exact: true }).click()
  await expect(page.locator('.roll-list li')).toHaveCount(2)
  await page.reload()
  await expect(page.getByText('Bem-vindo à mesa')).toBeVisible()
  await expect(page.locator('.roll-list li')).toHaveCount(2)
})
