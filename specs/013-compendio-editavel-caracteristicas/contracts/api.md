# API: spec 013

Todas sob `/api/campaigns/{campaignId}`, header `X-Participant-Token`.

## Catálogo (escrita só do mestre; 403 para jogador)

| Método | Rota | Corpo | Resposta |
|---|---|---|---|
| GET | `/catalog/items?origin=Compendium\|Edited\|Custom` | (filtro novo, opcional) | página; cada item com `origin` |
| GET | `/catalog/items/{id}` | | `CatalogItemDto` + `origin`, `baseId` (id global substituído resolve para a cópia) |
| POST | `/catalog/items` | `SaveCatalogItemRequest` | 200 `CatalogItemDto` (Custom) |
| PUT | `/catalog/items/{id}` | `SaveCatalogItemRequest` | 200 `CatalogItemDto` (global → cria/atualiza cópia Edited) |
| DELETE | `/catalog/items/{id}` | | 200 `CatalogItemDto` da original (Edited) ou 204 (Custom); 400 em global |
| GET/POST/PUT/DELETE | `/catalog/spells...` | `SaveCatalogSpellRequest` | idem para magias |

```text
SaveCatalogItemRequest { template: InventoryItemData, nameEn?, subcategory?, rarity?, requiresAttunement, cost? }
SaveCatalogSpellRequest { template: CharacterSpellData, classes?: string[], higherLevels?, nameEn? }
```

`template.weapon` nulo em categoria Weapon (ou `armor` nulo em Armor) = item genérico que pede item base.

## Características (dono da ficha; resposta sempre `CharacterDto` com `features`)

| Método | Rota | Corpo |
|---|---|---|
| POST | `/participants/me/character/features` | `{ data: CharacterFeatureData }` |
| PUT | `/participants/me/character/features/{id}` | `{ data: CharacterFeatureData }` |
| DELETE | `/participants/me/character/features/{id}` | |
| POST | `/participants/me/character/features/{id}/uses` | `{ delta: int }` (-999..999) |
| POST | `/participants/me/character/features/{id}/restore` | |
| POST | `/participants/me/character/features/{id}/move` | `{ direction: -1 \| 1 }` |
| POST | `/participants/me/character/rest` | `{ kind: "Short" \| "Long" }` |

```text
CharacterFeatureData { name, source?, description?, maxUses?, currentUses, recovery: "None"|"ShortRest"|"LongRest" }
CharacterFeatureDto { id, sortOrder, data }
```
