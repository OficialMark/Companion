import { useCompanionApi } from '~/composables/useCompanionApi'

// Error messages shown to the user when the API refuses a request.
vi.stubGlobal('useRuntimeConfig', () => ({ public: { apiBase: '/api' } }))

function respond(status: number, body: unknown) {
  vi.stubGlobal('fetch', vi.fn(() => Promise.resolve(new Response(body === null ? null : JSON.stringify(body), { status }))))
}

describe('useCompanionApi error messages', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('a 429 from the join limiter (empty body) says to wait, not a generic failure', async () => {
    vi.stubGlobal('useRuntimeConfig', () => ({ public: { apiBase: '/api' } }))
    respond(429, null)
    await expect(useCompanionApi().joinCampaign('1234', 'Lia')).rejects.toMatchObject({
      message: 'Muitas tentativas em pouco tempo. Aguarde um minuto e tente de novo.', status: 429
    })
  })

  it('the admin limiter\'s 429 gets the same message', async () => {
    vi.stubGlobal('useRuntimeConfig', () => ({ public: { apiBase: '/api' } }))
    respond(429, null)
    await expect(useCompanionApi().listAdminCampaigns('secret')).rejects.toMatchObject({ status: 429, message: expect.stringContaining('Aguarde um minuto') })
  })

  it('the server\'s own detail still wins when present', async () => {
    vi.stubGlobal('useRuntimeConfig', () => ({ public: { apiBase: '/api' } }))
    respond(404, { title: 'Not Found', detail: 'Campanha não encontrada.' })
    await expect(useCompanionApi().joinCampaign('9999', 'Lia')).rejects.toMatchObject({ message: 'Campanha não encontrada.', status: 404 })
  })

  it('other empty errors keep the generic message', async () => {
    vi.stubGlobal('useRuntimeConfig', () => ({ public: { apiBase: '/api' } }))
    respond(500, null)
    await expect(useCompanionApi().joinCampaign('1234', 'Lia')).rejects.toMatchObject({ message: 'Não foi possível concluir a operação.' })
  })
})
