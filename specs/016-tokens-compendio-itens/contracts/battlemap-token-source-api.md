# Contrato: origem de tokens (spec 016)

Todas as rotas exigem `X-Participant-Token`.

## POST /api/campaigns/{campaignId}/encounters/{encounterId}/battlemap/markers

```json
{ "label": "string | null", "creatureId": "guid | null", "itemId": "guid | null" }
```

- Sem origem: `label` obrigatório (1–40).
- Com origem: `label` opcional; ausente usa o nome da entrada (`LabelFrom`).
- `201` com `BattlemapSnapshotDto`; evento `BattlemapInvalidated` após persistir.
- `400`: `creatureId` e `itemId` juntos; label vazio ou > 40.
- `404`: criatura fora do alcance do participante ("Criatura não encontrada no compêndio.") ou item fora do catálogo da mesa.

## GET /api/campaigns/{campaignId}/creatures/token-options?q=&page=&pageSize=

Qualquer participante. `pageSize` padrão 12, máximo 60.

```json
{ "items": [{ "id": "guid", "name": "Ogro", "size": "Large", "type": "gigante", "imageUrl": "/api/...|null" }],
  "total": 730, "page": 1, "pageSize": 12 }
```

Sem PV, CA, atributos ou ações.

## BattlemapTokenDto (aditivo)

`sourceKind: "creature" | "item" | null`, `sourceId: guid | null`; `imageUrl` passa a vir também em marcadores de criatura.
