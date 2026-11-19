import { describe, expect, it } from 'vitest'
import { assertSafeBaseUrl, unsafeBaseUrlReason } from './e2e/guard'

describe('guarda do e2e da spec 011', () => {
  it('recusa quando E2E_BASE_URL não está definido', () => {
    expect(unsafeBaseUrlReason(undefined)).toMatch(/Defina E2E_BASE_URL/)
    expect(unsafeBaseUrlReason('  ')).toMatch(/Defina E2E_BASE_URL/)
  })

  it('recusa a porta da stack em uso', () => {
    expect(unsafeBaseUrlReason('http://127.0.0.1:3636')).toMatch(/stack em uso/)
    expect(unsafeBaseUrlReason('http://192.168.0.10:3636/')).toMatch(/stack em uso/)
    expect(() => assertSafeBaseUrl('http://localhost:3636')).toThrow()
  })

  it('recusa endereço inválido', () => {
    expect(unsafeBaseUrlReason('nao-e-url')).toMatch(/inválido/)
  })

  it('aceita a instância de validação', () => {
    expect(unsafeBaseUrlReason('http://127.0.0.1:3611')).toBeNull()
    expect(() => assertSafeBaseUrl('http://127.0.0.1:3611')).not.toThrow()
  })
})
