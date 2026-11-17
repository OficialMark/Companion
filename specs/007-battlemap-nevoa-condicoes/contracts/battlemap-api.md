# Contratos: spec 007

Todas as rotas exigem `X-Participant-Token`. Erros seguem o formato existente (`AppException` → 400/403/404/409).

## Névoa

### `PUT /api/campaigns/{campaignId}/encounters/{encounterId}/battlemap/fog`

Mestre apenas.

```json
{ "mask": "AAAA//8A..." }
```

- `mask`: base64 do bitset (ver data-model) ou `null` para revelar tudo.
- 200: `BattlemapSnapshotDto` (visão do mestre).
- 400: base64 inválido ou tamanho diferente de `ceil(columns*rows/8)`.
- 403: jogador.
- 404: encontro sem battlemap.
- Efeito: persiste e emite `BattlemapInvalidated` (cada cliente refaz o GET com o próprio token).

### `GET .../battlemap` (alterado)

Resposta ganha `fogMask: string | null`. Para jogadores, `tokens` omite criaturas e marcadores do mestre cujas células ocupadas estão todas cobertas.

## Visibilidade de HP

### `PUT /api/campaigns/{campaignId}/encounters/{encounterId}/combatants/{combatantId}/hit-points-visibility`

Mestre apenas. Corpo `{ "hidden": true }`. 200: `EncounterDto` (visão do mestre). 400 se o combatente é personagem de jogador. 404 se não existe. Emite `EncounterInvalidated`.

### `PUT /api/campaigns/{campaignId}/encounters/{encounterId}/hit-points-visibility`

Mestre apenas. Corpo `{ "hidden": true }`. Aplica a todas as criaturas do encontro. 200: `EncounterDto`. Emite `EncounterInvalidated`.

### `EncounterDto` (alterado)

`combatants[].isHitPointsHidden: boolean`. Para jogadores, criaturas ocultas vêm com `currentHitPoints: null`, `maxHitPoints: null`, `isDefeated` correto.

## Condições

`PUT .../battlemap/tokens/{tokenId}/appearance` (existente) aceita os 18 nomes de `StatusCondition` (case-insensitive). Nomes desconhecidos: 400.
