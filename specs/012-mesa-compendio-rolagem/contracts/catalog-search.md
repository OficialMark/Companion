# Contrato: busca do catálogo (mudança)

As rotas existentes têm o mesmo formato de entrada:

- `GET /api/campaigns/{campaignId}/catalog/items?q=&category=&rarity=&mundane=&page=&pageSize=`
- `GET /api/campaigns/{campaignId}/catalog/spells?q=&level=&school=&class=&ritual=&concentration=&page=&pageSize=`

## Comportamento de `q`

- O termo é normalizado: sem acento, maiúsculas e espaços colapsados.
- Uma linha casa quando:
  - o termo está no nome PT ou no nome EN; ou
  - o termo tem 3 ou mais caracteres e está na descrição.
- Ordem: primeiro quem casou por nome, depois quem casou só pela descrição. Dentro de cada grupo vale a ordem de hoje: itens por nome; magias por nível e depois nome.
- A paginação e o `total` contam as duas faixas juntas.

## Mudança aditiva na resposta

- `CatalogSpellSummaryDto` e `CatalogSpellDto` ganham `nameEn: string | null`.
- Os itens já expõem `nameEn`.
