export type CampaignKnowledgeKind = 'Npc' | 'Map' | 'Clue' | 'Journal'
export interface CampaignKnowledgeSummary {
  id: string; kind: CampaignKnowledgeKind; title: string; summary: string | null; isPlayerVisible: boolean
  author: string; createdAt: string; updatedAt: string; hasImage: boolean; pointCount: number
}
export interface CampaignKnowledgeContribution {
  id: string; body: string; author: string; authorId: string; createdAt: string; updatedAt: string
}
export interface CampaignMapPoint {
  id: string; title: string; description: string | null; x: number; y: number; author: string; authorId: string; createdAt: string
}
export interface CampaignKnowledgeDetail extends CampaignKnowledgeSummary {
  sharedDescription: string | null; version: string; masterNote: string | null
  contributions: CampaignKnowledgeContribution[]; points: CampaignMapPoint[]
}
export interface CampaignKnowledgePage { items: CampaignKnowledgeSummary[]; total: number; page: number; pageSize: number }
export interface SaveCampaignKnowledge {
  kind: CampaignKnowledgeKind; title: string; summary: string | null; sharedDescription: string | null
  isPlayerVisible?: boolean; expectedVersion?: string | null
}
export const CAMPAIGN_KNOWLEDGE_KINDS: Array<{ key: CampaignKnowledgeKind; label: string; icon: string }> = [
  { key: 'Npc', label: 'NPCs', icon: 'lucide:users-round' },
  { key: 'Map', label: 'Mapas', icon: 'lucide:map' },
  { key: 'Clue', label: 'Pistas', icon: 'lucide:search' },
  { key: 'Journal', label: 'Diário', icon: 'lucide:notebook-pen' }
]
