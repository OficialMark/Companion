# Research: spec 013

## R1. Onde mora a edição do mestre

- **Decision**: linhas de campanha na mesma tabela do catálogo (`CampaignId`, `BaseId`), como `Creatures`.
- **Rationale**: o padrão já existe, foi testado na spec 008 e a lista/busca continua uma consulta só. O seeder só precisa filtrar `CampaignId == null`.
- **Alternatives**: editar a linha global (o seeder sobrescreve na próxima carga e afeta todas as campanhas); tabela separada de overrides (duplica a busca, a paginação e o detalhe).

## R2. Slug das linhas de campanha

- **Decision**: slug gerado `campanha-{id}`; índice único de slug passa a filtrado em `[CampaignId] IS NULL`.
- **Rationale**: o slug só serve ao seeder. Gerar evita colisão e mantém a coluna obrigatória.
- **Alternatives**: slug nulo (exigiria mudar a coluna para nula e todo código que lê slug).

## R3. Unicidade da cópia editada

- **Decision**: índice único filtrado `(CampaignId, BaseId)` onde ambos não são nulos. Salvar uma global procura a cópia existente antes de criar.
- **Rationale**: impede duas versões da mesma original na mesma campanha, mesmo com cliques duplos.

## R4. Resolução de id na campanha

- **Decision**: `ResolveForCampaign(campaignId, id)`: se `id` é global e substituído na campanha, devolve a cópia; se é de outra campanha, 404.
- **Rationale**: a lista do jogador pode estar aberta antes da edição; adicionar pelo id antigo deve trazer a versão da mesa (FR-003).

## R5. Contador de usos

- **Decision**: operações dedicadas no servidor (`uses` com delta, `restore`, `rest`), não PUT do JSON inteiro.
- **Rationale**: dois cliques rápidos em -1 somam dois gastos; o servidor limita entre 0 e o máximo (constituição II).
- **Alternatives**: autosave do rascunho como a ficha base (perderia gastos concorrentes e exigiria versão).

## R6. Reordenação

- **Decision**: `POST features/{id}/move` com `direction` (-1/+1): troca `SortOrder` com o vizinho.
- **Rationale**: "reordenação simples" da spec; funciona em toque sem arrastar.

## R7. Reaproveitar os editores

- **Decision**: `ItemEditorModal` e `SpellEditorModal` recebem prop opcional `catalog` (valores extras + função de salvar). Sem ela, o comportamento atual não muda.
- **Rationale**: um formulário só para item e um para magia; os campos de catálogo entram num bloco extra.
- **Item genérico** ("Arma +1"): no modo catálogo, a caixa "Pede item base ao adicionar" remove o bloco de arma/armadura do envio.
