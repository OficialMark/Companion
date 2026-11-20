# Contrato HTTP: conta, ficha, inventário, magias, rolagens salvas e catálogos

Minimal APIs, mesmo estilo de `CreatureEndpoints`. Toda rota exige `X-Participant-Token`. Erros em ProblemDetails, com o mapeamento existente (400 validação, 401 sem sessão, 403 papel, 404, 409 concorrência). Enums como string.

**Regra de dono (FR-003):** nenhuma rota de ficha recebe `participantId`. O dono é sempre `ParticipantAuthorization.Require(campaignId, token)`. Itens, magias e rolagens são buscados por `id` **e** `ParticipantId = dono`; id de outro dono responde 404 (não 403, para não confirmar que existe).

**Papel:** rotas de ficha respondem 403 para o mestre ("Somente jogadores possuem ficha pessoal.").

**Resposta padrão de leitura e escrita da ficha (`CharacterDto`):**

```jsonc
{
  "participantId": "guid",
  "sheet": { /* CharacterSheetData */ },
  "version": "base64 (rowversion da ficha)",
  "items": [ { "id": "…", "sortOrder": 0, "catalogItemId": null, "version": "base64", "data": { /* InventoryItemData */ } } ],
  "spells": [ { "id": "…", "sortOrder": 0, "catalogSpellId": null, "data": { /* CharacterSpellData */ } } ],
  "savedRolls": [ { "id": "…", "sortOrder": 0, "incomplete": false, "data": { /* SavedRollData */ } } ],
  "stats": { /* CharacterStats, ver data-model.md */ },
  "notices": [ "Cota de Malha foi desequipado." ]
}
```

Corpos de escrita: `{ "data": …, "version": … }` para ficha e item (a versão do item é opcional), `{ "data": … }` para magia e rolagem. Todas as respostas são 200.

Toda escrita devolve o `CharacterDto` inteiro, já recalculado. Assim a tela nunca calcula regra e nunca fica com `stats` velho.

## Conta e sessão

| Método | Rota | Corpo | Resposta | Notas |
|---|---|---|---|---|
| POST | `/api/campaigns/join` | `{code, displayName}` | `SessionResponse` (inalterado) | **Comportamento novo:** normaliza o nome e procura a conta `(campanha, papel do código, NameKey)`. Existe: cria uma sessão nova para ela. Não existe: cria participante e sessão. Concorrência: violação do índice único → relê e usa a conta vencedora. `ParticipantJoined` só é emitido quando a conta é criada. Rate limit "access" inalterado. |
| POST | `/api/campaigns` | inalterado | inalterado | Nome do mestre normalizado; cria a sessão em `ParticipantSessions`. |
| DELETE | `/api/campaigns/{id}/participants/me/session` | — | 204 | Apaga só a sessão do token usado (FR-043). "Sair da campanha" passa a chamar esta rota antes de limpar o navegador. |

## Ficha

| Método | Rota | Corpo | Resposta |
|---|---|---|---|
| GET | `/api/campaigns/{id}/participants/me/character` | — | `CharacterDto`. Cria a ficha padrão se não existir (idempotente; corrida resolvida pela PK). |
| GET | `/api/campaigns/{id}/participants/me/character?ifExists=true` | — | `CharacterDto` se a ficha existe; 404 "Você ainda não tem ficha." sem criar a ficha (T073). |
| PUT | `/api/campaigns/{id}/participants/me/character` | `{ data: CharacterSheetData, version }` (ficha inteira) | `CharacterDto` · 409 se `version` velha |

**Concorrência (T072).** Toda escrita da ficha (salvar ficha, itens, equipar, magias, rolagens salvas) grava a linha `CharacterSheets` conferindo a versão: a que o cliente mandou no PUT da ficha, ou a lida no começo da requisição nas outras rotas. Duas escritas simultâneas do mesmo dono não passam juntas: a segunda recebe 409 e nada dela é gravado. Isso garante o teto de 200 itens e uma armadura e um escudo equipados mesmo com pedidos em paralelo. Toda escrita muda `version`, então o cliente usa sempre a versão da última resposta.

**Perfil de combate (T073, FR-010).** Com ficha, `PUT /api/campaigns/{id}/participants/me/combat-profile` e `PUT …/participants/me/initiative-modifier` respondem 409 "Iniciativa e vida vêm da sua ficha. Edite pela aba Ficha." e não mudam nada: iniciativa e PV da mesa vêm da ficha. Jogador sem ficha continua editando o perfil manual como antes.

## Inventário

| Método | Rota | Corpo | Resposta |
|---|---|---|---|
| POST | `…/me/character/items` | `InventoryItemInput` | `CharacterDto` (201) |
| POST | `…/me/character/items/from-catalog` | `{catalogItemId, quantity?}` | `CharacterDto` (201) |
| PUT | `…/me/character/items/{itemId}` | `InventoryItemInput` + `version` | `CharacterDto` |
| POST | `…/me/character/items/{itemId}/equip` | `{equipped: bool}` | `CharacterDto`, com `notices` quando outro item saiu |
| DELETE | `…/me/character/items/{itemId}` | — | `CharacterDto` |

Validação de `InventoryItemInput`: categoria exige o bloco correspondente (`weapon`, `armor`, `shield`); `damageDice` e `versatileDice` aceitam só `NdM` válidos pela gramática; `weapon.properties` só da lista fechada `finesse`, `light`, `heavy`, `two-handed`, `thrown`, `ammunition`, `loading`, `reach`, `versatile`, `range` (até 10); `weapon.mastery` e `weapon.damageType` até 30 caracteres; `effects` até 20 itens, cada um validado contra a tabela de `ItemEffect`.

Enums (T071): todo enum de entrada (ficha, item, efeito, magia, rolagem salva) precisa ser um valor definido. Um número fora da lista (por exemplo `"op": 5`) responde 400 com mensagem em PT, nunca 500 nem valor gravado.

## Magias do personagem

| Método | Rota | Corpo | Resposta |
|---|---|---|---|
| POST | `…/me/character/spells` | `CharacterSpellInput` | `CharacterDto` (201) |
| POST | `…/me/character/spells/from-catalog` | `{catalogSpellId}` | `CharacterDto` (201); repetir a mesma magia → 409 |
| PUT | `…/me/character/spells/{spellId}` | `CharacterSpellInput` | `CharacterDto` |
| DELETE | `…/me/character/spells/{spellId}` | — | `CharacterDto` |

## Rolagens salvas

| Método | Rota | Corpo | Resposta |
|---|---|---|---|
| POST | `…/me/character/rolls` | `SavedRollInput` | `CharacterDto` (201) |
| PUT | `…/me/character/rolls/{rollId}` | `SavedRollInput` | `CharacterDto` |
| POST | `…/me/character/rolls/{rollId}/duplicate` | — | `CharacterDto` (201) |
| DELETE | `…/me/character/rolls/{rollId}` | — | `CharacterDto` |

`SavedRollInput.versatile` (bool, padrão `false`, T074): rolagem de dano com a arma em duas mãos, usa o `versatileDice` da arma. Só vale com `kind = Damage` e `inventoryItemId`; fora disso → 400. Rolagens gravadas antes do campo leem `false`.

`SavedRollInput.icon` fora de `SavedRollIcons.All` → 400. `inventoryItemId` / `characterSpellId` de outro dono → 400 ("Item não encontrado na sua ficha.").

A execução de uma rolagem salva usa a rota de dados existente: o frontend monta a expressão com `stats` atuais e chama `POST /api/campaigns/{id}/dice-rolls` com `label = nome da rolagem`. A rota de dados aceita a gramática nova (ver `dice-expression.md`).

## Catálogos (qualquer participante da campanha, jogador ou mestre)

| Método | Rota | Query | Resposta |
|---|---|---|---|
| GET | `/api/campaigns/{id}/catalog/items` | `q`, `category`, `rarity`, `page` (1..100000, valores fora são trazidos para o limite), `pageSize` (≤50) | `{items: CatalogItemSummaryDto[], total, page, pageSize, attribution}` |
| GET | `/api/campaigns/{id}/catalog/items/{itemId}` | — | `CatalogItemDto` |
| GET | `/api/campaigns/{id}/catalog/spells` | `q`, `level`, `school`, `class`, `ritual`, `concentration`, `page`, `pageSize` | `{items: CatalogSpellSummaryDto[], total, page, pageSize}` |
| GET | `/api/campaigns/{id}/catalog/spells/{spellId}` | — | `CatalogSpellDto` |

`q` é comparado com `SearchKey` (sem acento, minúsculo), como no compêndio de criaturas. `attribution` só existe no catálogo de itens (texto CC-BY-4.0 do SRD 5.2). O catálogo de magias não tem crédito.

## Tempo real

| Evento | Payload | Quem recebe | Quando |
|---|---|---|---|
| `ParticipantUpdated` | `ParticipantDto` (inalterado) | grupo da campanha | PV ou iniciativa da ficha mudaram (FR-010) |
| `CharacterChanged` | `{participantId}` | grupo da campanha | qualquer escrita na ficha; só o cliente cujo `me.id` bate busca de novo (sincroniza aparelhos da mesma conta sem expor a ficha a ninguém) |
| `DiceRolled` | `DiceRollDto` (campos novos opcionais) | grupo da campanha | inalterado |
