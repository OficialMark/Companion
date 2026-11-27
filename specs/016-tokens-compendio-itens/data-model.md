# Data Model: Tokens do Compêndio e de Itens

## BattlemapMarker (alterada)

| Campo | Tipo | Regra |
|---|---|---|
| Id | Guid | PK |
| BattlemapId | Guid | FK Battlemap, cascade |
| OwnerParticipantId | Guid | FK Participant; quem criou |
| Label | string(40) | obrigatório, 1–40 após trim |
| CreatedAt | DateTimeOffset | |
| **CreatureId** | Guid? | novo; criatura do compêndio/mesa; sem FK |
| **CatalogItemId** | Guid? | novo; item do catálogo; sem FK |

Invariantes:
- `CreatureId` e `CatalogItemId` nunca preenchidos juntos.
- `LabelFrom(nome)` cabe em 40 caracteres, cortando em palavra quando o espaço está entre as posições 20 e 40.

Derivados na criação:
- Criatura Grande/Enorme/Imensa cria `BattlemapTokenAppearance` do marcador com footprint 2/3/4.

## Visibilidade

| Participante | Criaturas na busca e na criação | Itens |
|---|---|---|
| Jogador | `CampaignId == null && !IsRemovedFromCompendium` | `CatalogScope.Items` da mesa |
| Mestre | acima + `CampaignId == campanha` | idem |

## Snapshot (BattlemapTokenDto, marcador)

- `SourceKind`: `"creature"`, `"item"` ou `null`.
- `SourceId`: id da entrada ou `null`.
- `ImageUrl`: imagem da criatura quando ela ainda existe.
- `IsCreature` continua `false` e `CreatureId` continua `null` (o atalho de ficha do mestre é só para combatentes).
