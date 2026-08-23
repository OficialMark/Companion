import { expect, test } from '@playwright/test'

test('rerolls initiative before and during combat', async ({ page }) => {
  await page.goto('/')
  await page.getByLabel('Nome da campanha').fill(`Iniciativa ${Date.now()}`)
  await page.getByLabel('Seu nome').fill('Mestre da iniciativa')
  await page.getByRole('button', { name: 'Criar e entrar' }).click()

  await page.getByRole('button', { name: 'Iniciar preparação' }).click()
  await page.getByLabel('Nome').fill('Guerreiro')
  await page.getByLabel('Modificador').fill('3')
  await page.getByRole('button', { name: 'Adicionar' }).click()

  await page.getByRole('button', { name: 'Rolar iniciativas', exact: true }).click()
  await expect(page.getByRole('button', { name: 'Rolar iniciativas novamente' })).toBeVisible()
  await page.getByRole('button', { name: 'Rolar iniciativas novamente' }).click()

  await page.getByRole('button', { name: 'Começar combate' }).click()
  await expect(page.getByText('Rodada 1')).toBeVisible()
  await expect(page.getByText('Turno', { exact: true })).toBeVisible()
  await page.getByRole('button', { name: 'Rolar todas novamente' }).click()
  await expect(page.getByText('Turno', { exact: true })).toBeVisible()
  await page.getByRole('button', { name: 'Rolar iniciativa novamente para Guerreiro' }).click()
  await expect(page.getByText('Turno', { exact: true })).toBeVisible()
})
