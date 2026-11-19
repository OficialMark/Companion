# Contratos: spec 008

Todas as rotas usam `X-Participant-Token`. "Mestre" = 403 para jogador.

## Biblioteca (mestre)

| Método | Rota | Corpo / Query | Resposta |
|---|---|---|---|
| GET | `/api/campaigns/{c}/creatures` | `q, size, type, crMin, crMax, origin, page (1..), pageSize (≤60)` | `CreaturePageDto` |
| GET | `/api/campaigns/{c}/creatures/types` | | `string[]` (tipos distintos, para o filtro) |
| GET | `/api/campaigns/{c}/creatures/{id}` | | `CreatureDto` (versão da mesa se existir) |
| POST | `/api/campaigns/{c}/creatures` | `SaveCreatureRequest` | 201 `CreatureDto` (Custom) |
| PUT | `/api/campaigns/{c}/creatures/{id}` | `SaveCreatureRequest` | `CreatureDto`. Se `id` é do compêndio, cria/atualiza a versão da mesa. 409 se `ExpectedRevision` diverge |
| DELETE | `/api/campaigns/{c}/creatures/{id}` | | 204. Custom: apaga; Edited: restaura o original; compêndio: 400 |
| PUT | `/api/campaigns/{c}/creatures/{id}/image` | corpo binário, `Content-Type` image/png, image/jpeg ou image/webp, ≤ 1 MB | `CreatureDto` (cria versão da mesa se for do compêndio) |
| DELETE | `/api/campaigns/{c}/creatures/{id}/image` | | `CreatureDto` |

## Imagem (qualquer participante da mesa)

`GET /api/campaigns/{c}/creatures/{id}/image?rev=...`: 200 imagem, `Cache-Control: private, max-age=31536000, immutable`; 404 se não houver imagem ou a busca remota falhar. Só criaturas globais ou da própria mesa.

## Combate (mestre)

`POST /api/campaigns/{c}/encounters/{e}/creatures` `{ creatureId, count }` (1-20) → `EncounterDto`. Em `Preparing` entra como combatente comum; em `Active` entra como reforço com iniciativa rolada, sem mudar o turno; em `Ended` responde 400. Emite `EncounterInvalidated` e `BattlemapInvalidated` (se há mapa).

## Battlemap

- `PUT .../battlemap` (multipart, existente) + campo opcional `fogMask` (base64; vazio = sem névoa). Inválido para o grid: 400. Aplicado na mesma transação.
- `PUT .../battlemap/tokens/{tokenId}/roll-preset` `{ count, die, ability, bonus }` → `BattlemapSnapshotDto`. Quem controla o token.
- `BattlemapTokenDto` + `imageUrl?`, `creatureId?`, `rollPreset?`.

## Rolagens

`POST /api/campaigns/{c}/dice-rolls` + `label?` (≤ 80, sem quebras de linha). `DiceRollDto.label?`.
