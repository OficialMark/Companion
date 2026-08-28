# Data Model: Battlemap com Grid Interativo

## Overview

O battlemap é um recurso único do encontro. A imagem e a geometria do grid são persistidas em `Battlemap`; cada posição confirmada é persistida em `BattlemapTokenPosition`. Um pino em reserva não exige registro próprio: ele é projetado a partir de um jogador da campanha sem posição correspondente.

## Battlemap

Representa a configuração vigente do tabuleiro para um encontro.

| Field | Type | Rules |
|---|---|---|
| `Id` | UUID | Opaco, obrigatório e imutável |
| `EncounterId` | UUID | Obrigatório; único, garantindo no máximo um battlemap por encontro |
| `ImageContent` | Binary | Obrigatório; 1 byte a 20 MB; conteúdo PNG, JPEG ou WebP validado |
| `ImageMediaType` | String | Obrigatório; `image/png`, `image/jpeg` ou `image/webp` |
| `ImageFileName` | String | Nome de exibição sanitizado, máximo de 255 caracteres |
| `ImageRevision` | UUID | Alterado somente quando a imagem/configuração é substituída; compõe URL/ETag da imagem |
| `Columns` | Integer | Obrigatório; entre 1 e 100 |
| `Rows` | Integer | Obrigatório; entre 1 e 100 |
| `CreatedAt` | UTC timestamp | Definido na primeira publicação |
| `UpdatedAt` | UTC timestamp | Atualizado em cada substituição |
| `Version` | Row version | Concorrência otimista da configuração |

### Invariants

- `EncounterId` deve apontar para encontro da mesma campanha usada na autorização.
- A imagem, seu tipo e dimensões são publicados atomicamente.
- Substituição preserva o identificador do battlemap, troca `ImageRevision` e incrementa `Version`.
- Após redução do grid, nenhuma posição pode permanecer fora de `0 <= column < Columns` e `0 <= row < Rows`.

## BattlemapTokenPosition

Representa a célula confirmada de um pino. A ausência desta entidade para um jogador significa que o pino está na reserva.

| Field | Type | Rules |
|---|---|---|
| `Id` | UUID | Opaco, obrigatório e imutável |
| `BattlemapId` | UUID | Obrigatório; referência ao battlemap vigente |
| `ParticipantId` | UUID | Obrigatório; deve ser jogador da campanha do encontro |
| `Column` | Integer | Obrigatório; zero-based e dentro das colunas vigentes |
| `Row` | Integer | Obrigatório; zero-based e dentro das linhas vigentes |
| `UpdatedAt` | UTC timestamp | Momento da última posição confirmada |
| `Version` | Row version | Concorrência otimista entre jogador e mestre |

### Keys and indexes

- Unique `(BattlemapId, ParticipantId)`: exatamente uma posição por pino no battlemap.
- Index `BattlemapId`: carrega todas as posições do snapshot.
- Não há unicidade para `(BattlemapId, Column, Row)`: vários pinos podem ocupar a mesma célula.

### Invariants

- Somente o participante dono ou o mestre da campanha pode alterar a posição.
- O participante deve ter papel Player e pertencer à campanha do encontro.
- Uma coordenada é validada contra as dimensões vigentes no momento da persistência.
- Alterações de escala, viewport ou orientação não alteram `Column` ou `Row`.

## Participant projection

Os dados do pino são projetados, não duplicados:

| Field | Source | Meaning |
|---|---|---|
| `ParticipantId` | `Participant.Id` | Identidade e dono do pino |
| `DisplayName` | `Participant.DisplayName` | Rótulo acessível |
| `Initials` | Derived | Identificação visual compacta |
| `ColorKey` | Derived from opaque ID | Cor estável sem persistência adicional |
| `Row/Column` | Optional position | Nulos quando o pino está na reserva |
| `PositionVersion` | Optional position | `0` quando em reserva |

## Relationships

```text
Campaign 1 ── * Participant
Campaign 1 ── * Encounter
Encounter 1 ── 0..1 Battlemap
Battlemap 1 ── * BattlemapTokenPosition
Participant 1 ── * BattlemapTokenPosition
```

## State transitions

### Battlemap

```text
Absent
  └─ publish by master ─> Published
Published
  └─ replace by master with matching revision ─> Published (new image revision)
Published
  └─ stale replacement ─> Unchanged + conflict response
```

### Token

```text
Reserve (no position)
  └─ valid move ─> Positioned(row, column, version 1)
Positioned
  └─ valid move with current version ─> Positioned(new row, new column, version + 1)
Positioned
  └─ grid shrinks beyond coordinate ─> Reserve
Positioned
  └─ stale move or invalid coordinate ─> Unchanged + conflict/validation response
```

## Deletion and replacement behavior

- Encerrar um encontro não apaga o battlemap; ele permanece recuperável como histórico do encontro conforme o ciclo de vida já adotado.
- Excluir uma campanha remove encounters, battlemaps, imagens e posições por cascade dentro da exclusão do agregado.
- Substituir o mapa não cria histórico de binários nesta versão.
- Redimensionamento remove somente posições fora dos novos limites; posições ainda válidas permanecem na mesma coordenada lógica.
