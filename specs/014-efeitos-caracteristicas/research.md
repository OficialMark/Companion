# Research: Efeitos de características

## Decisão 1 — Reutilizar `ItemEffect`

- **Decision**: `CharacterFeatureData` recebe uma coleção de `ItemEffect`, validada pelos mesmos limites e alvos dos itens.
- **Rationale**: A regra já cobre bônus de ataque/dano por alvo, atributos, CA, perícias, salvaguardas e efeitos textuais; duplicar o modelo criaria semânticas divergentes.
- **Alternatives considered**: Criar `FeatureEffect` equivalente; reconhecer características conhecidas pelo nome. Ambas foram rejeitadas por duplicação ou regras ocultas.

## Decisão 2 — Fontes genéricas no cálculo

- **Decision**: Generalizar o efeito ativo em `CharacterRules` para conter identificador, nome da fonte e efeito, mantendo a supressão de duplicados exclusivamente na coleta de itens e acrescentando depois os efeitos das características.
- **Rationale**: Preserva integralmente a regra existente de itens e permite que a composição exiba "Dueling" como origem.
- **Alternatives considered**: Transformar características em itens sintéticos. Rejeitada porque mistura conceitos e poderia acionar equipagem/supressão indevidamente.

## Decisão 3 — Compatibilidade de JSON

- **Decision**: O novo parâmetro `Effects` é opcional na desserialização, normalizado para lista vazia ao validar/mapear.
- **Rationale**: Registros existentes não têm a propriedade e precisam ser carregados sem backfill manual ou migration de tabela.
- **Alternatives considered**: Migration SQL para reescrever cada JSON. Rejeitada por custo e risco sem benefício funcional.

## Decisão 4 — Preservar o rascunho no autosave

- **Decision**: A resposta bem-sucedida do autosave atualiza o snapshot/estatísticas e a versão, mas não substitui o rascunho local pela versão textual normalizada. Reload explícito continua carregando o valor normalizado.
- **Rationale**: Evita apagar espaços e mover o cursor enquanto mantém a normalização no servidor.
- **Alternatives considered**: Parar de normalizar Traços no servidor. Rejeitada porque armazenaria conteúdo vazio inútil e mudaria a política de todos os campos.

