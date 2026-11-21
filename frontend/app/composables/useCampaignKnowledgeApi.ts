import type { Session } from '~/types/campaign'
import type { CampaignKnowledgeDetail, CampaignKnowledgePage, CampaignKnowledgeSummary, SaveCampaignKnowledge, CampaignMapPoint } from '~/types/campaignKnowledge'

export function useCampaignKnowledgeApi() {
  const companion = useCompanionApi()
  const config = useRuntimeConfig()
  const base = (session: Session) => `/campaigns/${session.campaignId}/knowledge`
  const json = (method: string, body?: unknown) => ({ method, body: body === undefined ? undefined : JSON.stringify(body) })
  return {
    list: (session: Session, kind: string, q: string, page = 1) => companion.request<CampaignKnowledgePage>(`${base(session)}?kind=${encodeURIComponent(kind)}&q=${encodeURIComponent(q)}&page=${page}`, {}, session.participantToken),
    get: (session: Session, id: string) => companion.request<CampaignKnowledgeDetail>(`${base(session)}/${id}`, {}, session.participantToken),
    create: (session: Session, body: SaveCampaignKnowledge) => companion.request<CampaignKnowledgeDetail>(base(session), json('POST', body), session.participantToken),
    save: (session: Session, id: string, body: SaveCampaignKnowledge) => companion.request<CampaignKnowledgeDetail>(`${base(session)}/${id}`, json('PUT', body), session.participantToken),
    remove: (session: Session, id: string) => companion.request<void>(`${base(session)}/${id}`, json('DELETE'), session.participantToken),
    contribute: (session: Session, id: string, body: string) => companion.request(`${base(session)}/${id}/contributions`, json('POST', { body }), session.participantToken),
    saveNote: (session: Session, id: string, body: string) => companion.request<CampaignKnowledgeDetail>(`${base(session)}/${id}/master-note`, json('PUT', { body }), session.participantToken),
    addPoint: (session: Session, id: string, point: { title: string; description: string | null; x: number; y: number }) => companion.request<CampaignMapPoint>(`${base(session)}/${id}/points`, json('POST', point), session.participantToken),
    savePoint: (session: Session, id: string, pointId: string, point: { title: string; description: string | null; x: number; y: number }) => companion.request<CampaignMapPoint>(`${base(session)}/${id}/points/${pointId}`, json('PUT', point), session.participantToken),
    deletePoint: (session: Session, id: string, pointId: string) => companion.request<void>(`${base(session)}/${id}/points/${pointId}`, json('DELETE'), session.participantToken),
    uploadImage: (session: Session, id: string, file: File) => {
      const form = new FormData(); form.append('image', file)
      return companion.request<CampaignKnowledgeDetail>(`${base(session)}/${id}/image`, { method: 'POST', body: form }, session.participantToken)
    },
    image: async (session: Session, id: string) => {
      const response = await fetch(`${config.public.apiBase}${base(session)}/${id}/image`, { headers: { 'X-Participant-Token': session.participantToken } })
      if (!response.ok) throw new Error('Não foi possível carregar o mapa.')
      return URL.createObjectURL(await response.blob())
    }
  }
}
