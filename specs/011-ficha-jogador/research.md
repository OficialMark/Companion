# Research: Ficha pessoal, inventário e rolagens do jogador

Cada decisão traz o motivo e as alternativas rejeitadas. Fatos do código citam arquivo e linha.

## R1. Onde calcular os valores derivados

- **Decision**: um módulo de domínio puro, `Companion.Domain.Characters.CharacterRules.Compute(sheet, items, spells) → CharacterStats`, roda **só no servidor**. Toda escrita da ficha devolve `stats` recalculado; o frontend apenas exibe e monta expressões a partir de `stats` (`parts` rotulados).
- **Rationale**: o pedido exige regra única (FR-018, SC-003) e a constituição (princípio II) põe o servidor como autoridade. Com um só cálculo, não existe paridade a manter. A gravação automática com 400 ms de espera mais a ida e volta numa rede local dá atualização perceptivelmente imediata; enquanto espera, a tela marca o valor como desatualizado em vez de inventar um número.
- **Alternatives considered**: (a) espelhar as regras em TypeScript com testes de paridade sobre os mesmos casos JSON. Dá resposta em 0 ms, mas duplica cerca de 400 linhas de regra e cria a divergência que o pedido proíbe; rejeitado. (b) calcular só no frontend: viola a constituição; rejeitado.

## R2. Gramática de vantagem, desvantagem e repetição

- **Decision**: estender `DiceExpression` (`backend/src/Companion.Domain/Dice/DiceExpression.cs`) de regex para um analisador recursivo pequeno, com `N#[…]` (repetição), `2#(…) maior|menor` (seleção) e constantes rotuladas `(FOR) 3`. A sintaxe legada continua aceita e com o mesmo `Normalized`. Contrato completo em `contracts/dice-expression.md`.
- **Rationale**: o pedido usa `2#(…)` para vantagem, mas `#` já é repetição (`3#d20+5`). Sem marcador, "2 repetições" e "vantagem" teriam a mesma escrita, e uma delas seria aplicada ou eliminada em silêncio, o que o pedido proíbe. Colchetes para repetição e "maior"/"menor" para seleção deixam as duas explícitas e componíveis (`3#[2#(…) maior]`). O rótulo na expressão faz o histórico mostrar de onde vem cada número, como no exemplo `(char) 3`.
- **Alternatives considered**: (a) notação `kh1`/`kl1` (Roll20): aplicaria a seleção só a um dado, contra a regra explícita de envolver a expressão inteira; rejeitado. (b) vantagem como flag fora da expressão, no corpo da requisição: a expressão mostrada deixaria de ser o que roda (o pedido exige que coincidam); rejeitado. (c) biblioteca de dados pronta (ex.: `DiceNotation`, `d20.NET`): nenhuma tem seleção sobre expressão composta com rótulos e todas trariam uma sintaxe diferente da atual, quebrando o legado; o analisador próprio tem cerca de 150 linhas e é coberto por casos; Layer 1 não se aplica.

## R3. Conta por nome e várias sessões

- **Decision**:
  - Nova tabela `ParticipantSessions(TokenHash único)`. A autorização (`ParticipantAuthorization.cs:11-44`) passa a buscar pela sessão, com índice, em vez de varrer os participantes da campanha.
  - `Participant.NameKey` com índice único filtrado `(CampaignId, Role, NameKey) WHERE NameKey IS NOT NULL`.
  - `JoinAsync` (`CampaignService.cs:33-50`) procura a conta pelo `NameKey` e cria só uma sessão nova. Se a inserção bater no índice único, relê a conta e usa a vencedora.
- **Rationale**:
  - O índice único fecha a corrida de dois joins simultâneos.
  - O filtro `NOT NULL` aceita os nomes repetidos que já existem (a migration `AllowDuplicateParticipantNames` permitiu).
  - Copiar o `TokenHash` atual para uma sessão por participante mantém conectado todo navegador que já está na mesa.
  - Comparar pelo `NameKey` em maiúsculas invariantes ignora maiúsculas e minúsculas, mas diferencia acentos ("Joao" ≠ "João"), como a spec decidiu.
- **Alternatives considered**: (a) trocar o token do participante a cada join: derruba o outro aparelho, contra o FR-043; rejeitado. (b) índice único sem filtro: a migration falharia nas campanhas com nomes repetidos; rejeitado. (c) tabela de contas fora da campanha: o pedido diz "na campanha" e a identidade atual é por campanha; rejeitado.
- **Risco aceito pelo Marcos**: quem sabe o código de jogador e o nome entra na conta. O rate limit "access" de 12 por minuto por IP (`Program.cs:34-41`) continua.

## R4. Normalização do nome

- **Decision**: `PersonName.Normalize(string)` no domínio.
  - Remove espaços nas pontas e reduz espaços repetidos a um.
  - Divide por espaço e hífen; em cada parte, aplica minúsculas e depois maiúscula na primeira letra, com a cultura `pt-BR`.
  - O apóstrofo não divide palavra ("D'artagnan").
  - Limite de 40 caracteres, aplicado depois da normalização.
- **Rationale**: é a regra literal do Marcos ("primeira letra maiúscula e restantes minúsculas"). Hífen divide porque "Jean-luc" sairia errado para nomes compostos comuns.
- **Alternatives considered**: `TextInfo.ToTitleCase`, rejeitado porque mantém palavras inteiras em maiúsculas ("VONN" continuaria "VONN") e o comportamento muda por cultura.

## R5. Backfill dos participantes existentes

- **Decision**: a migration cria colunas e tabelas e copia os tokens com SQL puro. A normalização de nomes e a atribuição de `NameKey` rodam num `ParticipantNameBackfill` no startup, logo depois de `MigrateAsync` e antes do seeder (`Program.cs:131-147`). O passo é idempotente (só pega quem tem `NameKey` nulo) e uma falha é logada sem derrubar a API, como o seeder.
- **Rationale**: o T-SQL não tem conversão confiável de maiúsculas por palavra em pt-BR. Em C#, a mesma `PersonName.Normalize` serve ao join e ao backfill, então a regra é uma só.
- **Alternatives considered**: função T-SQL de title case, rejeitada por duplicar a regra em outra linguagem.

## R6. Fonte do catálogo de itens

- **Decision**: usar os dados do SRD 5.2.1 em inglês do repositório `5e-bits/5e-srd-api`, fixado num commit.
  - Arquivos: `packages/5e-database/src/2024/en/5e-SRD-Equipment.json` (182 entradas: 38 armas, 13 armaduras incluindo o escudo, 90 itens de aventura, 36 ferramentas) e `5e-SRD-Magic-Items.json` (277 itens, 24 poções).
  - Licença: MIT para o código; o conteúdo segue o CC-BY-4.0 do SRD.
  - O repositório recebeu commit em 2026-11-19 e tem 654 estrelas.
  - O aviso de atribuição exigido é a frase da página 1 do SRD 5.2.1 ("This work includes material from the System Reference Document 5.2.1…"), exibida no catálogo de itens.
- **Rationale**:
  - A Wizards publicou o SRD 5.2.1 só em EN, DE, ES, FR e IT. Os PDFs `PT_`, `PTBR_` e `BR_` respondem 403.
  - Não há tradução comunitária com licença aberta: as buscas no GitHub voltam vazias; o 5e-bits tem pt-BR só para três tabelas de 2024; o Pocket DM é "direitos reservados" e a autorização dele cobre só monstros.
  - O 5e-bits já é JSON estruturado: dano, propriedades, maestria, CA, limite de DES, força mínima e furtividade. Raspar HTML ou PDF seria mais frágil.
- **Alternatives considered**:
  - Open5e v2 (`srd-2024`): não traz raridade nos itens mágicos e o licenciamento do repositório é "NOASSERTION"; rejeitado.
  - PDF do SRD: exigiria parse de PDF; rejeitado.
  - Raspar a D&D Beyond: risco de termos de uso, já descartado pelo Marcos.

## R7. Tradução dos itens para PT-BR

- **Decision**: a tradução é feita uma única vez na implementação e congelada em `scripts/import-catalogs/translations/items.pt-BR.json`, chaveado pelo `index` do 5e-bits. Volume: cerca de 459 nomes e 38 mil palavras.
  - **Autoria**: subagentes do Claude Code em lotes, cada lote com um glossário fixo.
  - **Termos mecânicos**: propriedades, maestrias, tipos de dano, raridades, categorias e unidades ficam numa tabela determinística no código (`glossary.mjs`) e nunca passam por tradução livre.
  - **Validação**: o importador recusa um item cujo texto traduzido não preserve, como multiconjunto, todos os números e todas as notações de dados do original (`2d4 + 2`, `+1`, `18`). Também recusa se faltar tradução para algum `index`.
  - **Unidades**: pés viram metros pela regra do livro em PT (5 pés = 1,5 m) e libras viram kg (1 lb = 0,5 kg), aplicadas por código, não pelo tradutor. Essa conversão vem antes da checagem de números.
- **Rationale**: tradução é trabalho do espaço latente, mas a saída vira dado versionado e o script passa a vigiá-la para sempre. O app nunca chama LLM em tempo de execução, então a regra "LLM pelo Claude Code local" não precisa de um serviço `services/llm` nesta feature: não existe chamada de LLM no software entregue.
- **Alternatives considered**:
  - Serviço LLM chamado pelo importador a cada execução: não é determinístico e reimportar mudaria o texto; rejeitado.
  - Pedir ao Pocket DM a tradução dos itens: depende de terceiros e eles têm 1223 itens com escopo diferente; fica como opção futura.

## R8. Fonte e parse das magias

- **Decision**: `scripts/import-catalogs/import-spells.mjs` baixa `spells.js` (855 KB) e `spells-by-class.js`, retira o `document.write(\`…\`)` e analisa o HTML. Seletores: `div.spell-item[data-name][data-level]`, `.name`, `img/ritual.svg`, `p.school`, os ícones `cast`, `range`, `components` e `duration` dentro de `.meta-item`, `img/concentration.svg` e `.description`.
  - O dano sugerido é o primeiro `NdM` da descrição, mais o tipo de dano na mesma frase.
  - O tipo de rolagem sai do texto: "jogada de ataque" vira ataque; "teste de resistência de X" vira salvaguarda com o atributo X.
  - "Em Níveis Superiores" vai para `HigherLevels`.
  - O HTML passa por uma lista de tags permitidas.
  - Sem crédito na interface, a pedido do autor.
- **Rationale**: é o mesmo padrão de `scripts/import-creatures`: Node sem dependências, cache em disco, fixtures, `node:test` e relatório. O site é estático, então duas requisições bastam.
- **Contagens a conferir no relatório**: 361 magias. Por nível: 27, 63, 59, 49, 35, 42, 32, 20, 18, 16. As listas por classe batem 100% com o `data-name`.
- **Alternatives considered**: um parser de HTML do npm, como `node-html-parser`. Rejeitado porque o importador de criaturas já evita dependências e a estrutura é regular; se o parse por regex e tokenização ficar frágil nos testes, esta decisão é revista.

## R9. Carga dos catálogos no app

- **Decision**: `ICatalogSeedSource` × 2 (`EmbeddedItemCatalogSeedSource`, `EmbeddedSpellCatalogSeedSource`) e um `CatalogSeeder` idempotente por SHA-256, com as chaves `items.srd52` e `spells.magias5e` em `SeedVersions`. O comportamento repete o `CompendiumSeeder.cs:34-79`: upsert por `Slug`, marca o que sumiu e nunca derruba a API.
- **Rationale**: é o mesmo padrão comprovado em produção. A indisponibilidade da fonte só afeta quem roda o importador (FR-021, SC-008).

## R10. Telas e navegação

- **Decision**:
  - Renomear `MasterViewNav.vue` para `ViewNav.vue` e `useMasterView.ts` para `useAppView.ts`, com as opções escolhidas pelo papel.
  - A tela Ficha entra como mais um ramo em `pages/index.vue:170-176`.
  - O painel do jogador substitui o `QuickDiceRoller` no Modo Jogo só quando o participante é jogador (`index.vue:196`).
  - O fluxo "Acertou?" e de dano é reaproveitado.
- **Rationale**: o pedido manda reaproveitar a navegação do Compêndio. Só existe uma página, e a spec 009 já fixou o estado em `?tela=`.
- **Alternatives considered**: criar uma rota `/ficha` separada. Rejeitado porque perderia o topbar, a conexão SignalR e o padrão da 009.

## R11. Alvos e limites

- O `DiceRoll.Expression` vai de 128 para 256 caracteres. A expressão rotulada mais longa realista (3 repetições, vantagem, arma, 3 dados, 4 rótulos) tem cerca de 110 caracteres; 256 deixa folga e o servidor recusa acima disso.
- A gravação automática espera 400 ms. A ficha completa, com 50 itens e 40 magias, cabe numa resposta de cerca de 60 KB.

## R12. Validação sem tocar no ambiente em uso

- **Decision**: os testes de integração usam Testcontainers (`CompanionApiFactory.cs:15,23-24`). A validação manual e o e2e rodam numa instância com nomes, portas e banco próprios (`companion-011-sql`, `:14333`, `:5311`, `:3611`), com `E2E_BASE_URL` sempre definido. A troca dos containers em produção é feita pelo Marcos.
- **Rationale**: o pedido proíbe derrubar, reiniciar ou recriar containers. O `playwright.config.ts` aponta por padrão para a 3636 do ambiente em uso, por isso `E2E_BASE_URL` é obrigatório. Essa restrição prevalece sobre a "Autorização operacional local" da constituição durante esta feature.
