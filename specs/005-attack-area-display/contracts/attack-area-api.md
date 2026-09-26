# Contract: Attack Area API & Realtime Events

Segue o mesmo padrão de `BattlemapEndpoints.cs` (grupo `/api/campaigns/{campaignId:guid}/encounters/{encounterId:guid}/battlemap`, autenticação via header `X-Participant-Token`).

## REST Endpoints

### `PUT /api/campaigns/{campaignId}/encounters/{encounterId}/battlemap/tokens/{tokenId}/attack-area`

Cria ou substitui a área de ataque associada ao token `tokenId`.

**Autorização**: participante dono do token, ou mestre da campanha. Caso contrário → `403 Forbidden`.

**Request body** (`SetAttackAreaRequest`):
```json
{
  "shape": "Circle | Square | Cone | Cylinder",
  "radius": 3,
  "width": 1,
  "directionDegrees": 90
}
```
- `radius`: inteiro, 1–100 (obrigatório).
- `width`: inteiro, 1–100 (obrigatório se `shape` for `Cone`/`Cylinder`; ignorado/deve ser omitido para `Circle`/`Square`).
- `directionDegrees`: inteiro, 0–359 (obrigatório se `shape` for `Cone`/`Cylinder`; ignorado/deve ser omitido para `Circle`/`Square`).

**Response `200 OK`** (`AttackAreaDto`):
```json
{
  "id": "guid",
  "battlemapId": "guid",
  "combatantId": "guid",
  "shape": "Cone",
  "radius": 3,
  "width": 1,
  "directionDegrees": 90,
  "updatedAt": "2026-09-12T12:00:00Z"
}
```

**Errors**: `400` (validação — raio/largura/direção fora do intervalo, ou campos direcionais ausentes/presentes indevidamente), `403` (token não pertence ao participante e ele não é mestre), `404` (token/battlemap inexistente).

### `DELETE /api/campaigns/{campaignId}/encounters/{encounterId}/battlemap/tokens/{tokenId}/attack-area`

Remove a área de ataque do token, se existir. Mesma autorização do PUT. Response `204 No Content`. Idempotente (remover quando já não existe também retorna `204`).

### `GET /api/campaigns/{campaignId}/encounters/{encounterId}/battlemap` (endpoint existente, estendido)

O snapshot do battlemap (já retornado por este endpoint) passa a incluir `attackAreas: AttackAreaDto[]` com todas as áreas ativas, para reidratação ao entrar/reconectar na sessão.

## Evento Realtime (SignalR — grupo `campaign:{campaignId}`)

Reaproveita o canal de eventos já usado por `SignalRCampaignNotifier` (mesmo padrão de `ParticipantsChanged`/eventos de battlemap).

- **`AttackAreaChanged`**: disparado após um PUT bem-sucedido. Payload: `AttackAreaDto` (o estado atual da área).
- **`AttackAreaRemoved`**: disparado após um DELETE bem-sucedido (ou remoção em cascata por remoção de token/encerramento de combate/reset do battlemap). Payload: `{ battlemapId, tokenId }` (combatantId).

Clientes atualizam seu estado local (`useAttackAreas`) a partir desses eventos; ao reconectar, descartam qualquer estado local e reidratam via GET.
