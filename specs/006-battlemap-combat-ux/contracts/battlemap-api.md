# API Contract: Battlemap Combat UX

Todas as rotas abaixo pertencem ao grupo existente `MapGroup("/api/campaigns/{campaignId:guid}/encounters/{encounterId:guid}/battlemap")` em `BattlemapEndpoints.cs`, autenticadas pelo header `X-Participant-Token`, no mesmo formato de resposta (`BattlemapSnapshotDto`) já usado por `position`/`attack-area`.

## Novas rotas

### `PUT /tokens/{tokenId}/appearance`

Define tamanho e condições de status de um token (participante, criatura ou marcador).

Request body (`SetTokenAppearanceRequest`):
```json
{ "footprintSize": 3, "statusConditions": ["Stunned", "Blinded"] }
```

- `footprintSize`: inteiro 1–5 (FR-011). Fora do intervalo → `400 Validation`.
- `statusConditions`: lista de nomes do enum `StatusCondition` (`Stunned`, `Restrained`, `Blinded`, `Poisoned`, `Invisible`, `Prone`); nome desconhecido → `400 Validation`; lista vazia é válida (remove todas as condições).
- Autorização: dono do token (participante do próprio pino, ou `OwnerParticipantId` do marcador) ou mestre → senão `403 Forbidden` (mesma mensagem/padrão de `SetAttackAreaAsync`).
- Resposta: `200 OK` com `BattlemapSnapshotDto` atualizado (mesmo padrão de `attack-area`).
- Efeito colateral: chama `notifier.BattlemapInvalidatedAsync` como as demais mutações.

### `POST /markers`

Cria um novo token representativo (marcador) pertencente ao participante autenticado.

Request body (`CreateMarkerRequest`):
```json
{ "label": "Lobo invocado" }
```

- `label`: 1–40 caracteres (mesma regra de `Participant.DisplayName`); vazio/maior → `400 Validation`.
- Autorização: qualquer participante autenticado (jogador cria para si; mestre pode criar em nome de um jogador informando `ownerParticipantId` — ver nota abaixo) — sem restrição adicional de papel além de estar na campanha.
- Resposta: `201 Created` com `BattlemapSnapshotDto` incluindo o novo token (`isMarker: true`, sem posição — cai na "reserva" como qualquer pino novo, reaproveitando `reserveTokens` do frontend).

> Nota: o mestre pode remover/mover qualquer marcador (regra geral de autorização), mas a criação nesta versão é sempre em nome de quem chama o endpoint (`OwnerParticipantId = actor.Id`); o mestre criar um marcador "para" outro jogador fica fora do escopo desta versão (consistente com a spec, que descreve apenas o próprio jogador inserindo seus tokens representativos).

### `DELETE /markers/{markerId}`

Remove um marcador e tudo associado a ele (posição, área de ataque, aparência).

- Autorização: `OwnerParticipantId` do marcador, ou mestre → senão `403 Forbidden`.
- Resposta: `200 OK` com `BattlemapSnapshotDto` sem o token removido (FR-019).
- Marcador inexistente → `404 NotFound` (mesmo padrão de `RemoveAttackAreaAsync`, que é idempotente quando já não existe — aqui um `markerId` desconhecido é tratado como erro, já que, diferente de área, o marcador é a própria identidade do token).

## Rotas existentes reaproveitadas sem alteração de contrato

- `PUT /tokens/{tokenId}/position` — `tokenId` passa a poder ser também um `markerId` (resolução de tipo de token estendida em `ResolveTokenForMutation`/`MoveAsync`, ver data-model.md); formato de request/response inalterado.
- `PUT /tokens/{tokenId}/attack-area` / `DELETE /tokens/{tokenId}/attack-area` — mesma extensão: `tokenId` também pode ser um marcador.

## Alterações em rotas fora do grupo `battlemap` (Encounters/Campaigns)

- `POST /api/campaigns/{campaignId}/encounters/{encounterId}/combatants` (`AddCombatantRequest`) — + campo opcional `maxHitPoints` (int?).
- `PUT /api/campaigns/{campaignId}/encounters/{encounterId}/combatants/{combatantId}` (`UpdateCombatantRequest`) — + campo opcional `maxHitPoints` (int?).
- `PUT /api/campaigns/{campaignId}/combat-profile` (`UpdateCombatProfileRequest`, tela inicial) — + campo opcional `maxHitPoints` (int?).

## `BattlemapTokenDto` — forma final

```json
{
  "participantId": "guid (id do participante, criatura ou marcador — nome do campo mantido por compatibilidade)",
  "displayName": "string",
  "initials": "string",
  "colorKey": 0,
  "column": null,
  "row": null,
  "positionVersion": null,
  "isCreature": false,
  "isMarker": false,
  "ownerParticipantId": null,
  "footprintSize": 1,
  "statusConditions": [],
  "attackArea": null
}
```

> `BattlemapTokenDto` propositalmente **não** carrega `currentHitPoints`/`maxHitPoints` — ver data-model.md, seção "Resolução de vida do token". O cliente resolve a vida cruzando este token com `activeEncounter.combatants`/`participants`, que já chegam por outros canais de sincronização (`EncounterInvalidated`/`ParticipantsChanged`), evitando um segundo caminho de dados que ficaria dessincronizado do primeiro.

## `CombatantDto` / `ParticipantDto` — forma final (trecho alterado)

```json
{
  "currentHitPoints": 12,
  "maxHitPoints": 18
}
```

`currentHitPoints` deixa de ser `null` para observadores que não são o dono nem o mestre (FR-009a) — mesma alteração vale tanto para `EncounterDto.Combatants[]` (lista de iniciativa) quanto, por consequência, para `BattlemapTokenDto`.
