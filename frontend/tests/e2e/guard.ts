// Spec 011: the e2e specs of this feature create real campaigns. They must never run
// against the stack that is serving the table (port 3636), so they refuse to start
// unless E2E_BASE_URL points somewhere else.
export const LIVE_STACK_PORT = '3636'

export function unsafeBaseUrlReason(baseUrl: string | undefined): string | null {
  if (!baseUrl || !baseUrl.trim()) return 'Defina E2E_BASE_URL para a instância de validação (ex.: http://127.0.0.1:3611).'
  let url: URL
  try {
    url = new URL(baseUrl)
  } catch {
    return `E2E_BASE_URL inválido: ${baseUrl}`
  }
  if (url.port === LIVE_STACK_PORT) return `E2E_BASE_URL aponta para a stack em uso (:${LIVE_STACK_PORT}). Use a instância de validação.`
  return null
}

export function assertSafeBaseUrl(baseUrl: string | undefined = process.env.E2E_BASE_URL): void {
  const reason = unsafeBaseUrlReason(baseUrl)
  if (reason) throw new Error(reason)
}
