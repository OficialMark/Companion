import type { Session } from '~/types/campaign'
import type {
  CatalogItem, CatalogItemQuery, CatalogItemSummary, CatalogPage, CatalogSpell, CatalogSpellQuery, CatalogSpellSummary,
  Character, CharacterSheetData, CharacterSpellData, InventoryItemData, SavedRollData
} from '~/types/character'

/**
 * HTTP calls of the player's character sheet and the catalogs (spec 011, contracts/character-api.md).
 * No call carries a participant id: the server always uses the caller's own session.
 */
export function useCharacterApi() {
  const { request } = useCompanionApi()
  const base = (session: Session) => `/campaigns/${session.campaignId}/participants/me/character`
  const json = (method: string, body?: unknown): RequestInit => ({ method, body: body === undefined ? undefined : JSON.stringify(body) })
  const send = <T>(session: Session, path: string, init: RequestInit = {}) => request<T>(path, init, session.participantToken)

  function query(params: Record<string, unknown>) {
    const search = new URLSearchParams()
    for (const [key, value] of Object.entries(params)) {
      if (value === undefined || value === null || value === '') continue
      search.set(key, String(value))
    }
    const text = search.toString()
    return text ? `?${text}` : ''
  }

  return {
    /**
     * The caller's character. By default the server creates an empty sheet on the first read; with
     * `ifExists` it answers 404 instead (FR-010: the combat profile only reads an existing sheet).
     */
    getCharacter: (session: Session, options: { ifExists?: boolean } = {}) =>
      send<Character>(session, `${base(session)}${options.ifExists ? '?ifExists=true' : ''}`),
    saveSheet: (session: Session, sheet: CharacterSheetData, version: string) =>
      send<Character>(session, base(session), json('PUT', { data: sheet, version })),

    addItem: (session: Session, item: InventoryItemData) => send<Character>(session, `${base(session)}/items`, json('POST', { data: item })),
    addItemFromCatalog: (session: Session, catalogItemId: string, quantity = 1) =>
      send<Character>(session, `${base(session)}/items/from-catalog`, json('POST', { catalogItemId, quantity })),
    saveItem: (session: Session, itemId: string, item: InventoryItemData, version: string) =>
      send<Character>(session, `${base(session)}/items/${itemId}`, json('PUT', { data: item, version })),
    equipItem: (session: Session, itemId: string, equipped: boolean) =>
      send<Character>(session, `${base(session)}/items/${itemId}/equip`, json('POST', { equipped })),
    removeItem: (session: Session, itemId: string) => send<Character>(session, `${base(session)}/items/${itemId}`, json('DELETE')),

    addSpell: (session: Session, spell: CharacterSpellData) => send<Character>(session, `${base(session)}/spells`, json('POST', { data: spell })),
    addSpellFromCatalog: (session: Session, catalogSpellId: string) =>
      send<Character>(session, `${base(session)}/spells/from-catalog`, json('POST', { catalogSpellId })),
    saveSpell: (session: Session, spellId: string, spell: CharacterSpellData) =>
      send<Character>(session, `${base(session)}/spells/${spellId}`, json('PUT', { data: spell })),
    removeSpell: (session: Session, spellId: string) => send<Character>(session, `${base(session)}/spells/${spellId}`, json('DELETE')),

    addRoll: (session: Session, roll: SavedRollData) => send<Character>(session, `${base(session)}/rolls`, json('POST', { data: roll })),
    saveRoll: (session: Session, rollId: string, roll: SavedRollData) =>
      send<Character>(session, `${base(session)}/rolls/${rollId}`, json('PUT', { data: roll })),
    duplicateRoll: (session: Session, rollId: string) => send<Character>(session, `${base(session)}/rolls/${rollId}/duplicate`, json('POST')),
    removeRoll: (session: Session, rollId: string) => send<Character>(session, `${base(session)}/rolls/${rollId}`, json('DELETE')),

    listCatalogItems: (session: Session, params: CatalogItemQuery) =>
      send<CatalogPage<CatalogItemSummary>>(session, `/campaigns/${session.campaignId}/catalog/items${query({ ...params })}`),
    getCatalogItem: (session: Session, id: string) => send<CatalogItem>(session, `/campaigns/${session.campaignId}/catalog/items/${id}`),
    listCatalogSpells: (session: Session, params: CatalogSpellQuery) =>
      send<CatalogPage<CatalogSpellSummary>>(session, `/campaigns/${session.campaignId}/catalog/spells${query({ ...params })}`),
    getCatalogSpell: (session: Session, id: string) => send<CatalogSpell>(session, `/campaigns/${session.campaignId}/catalog/spells/${id}`)
  }
}
