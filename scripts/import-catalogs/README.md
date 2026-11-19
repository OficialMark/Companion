# Importadores dos catálogos (magias e itens)

Geram os seeds globais da ficha do jogador (spec 011, US7):

- `backend/src/Companion.Infrastructure/Persistence/Seed/spells.magias5e.json`: 361 magias em PT-BR.
- `backend/src/Companion.Infrastructure/Persistence/Seed/items.srd52.json`: 459 itens do SRD 5.2.1 traduzidos para PT-BR
  (só é gerado depois da tradução congelada, T057).

Node.js puro (ESM, Node 20+), só `fetch` e módulos nativos, nenhuma dependência npm. Mesmo padrão de
`scripts/import-creatures/`.

| Arquivo | Papel |
| --- | --- |
| `import-spells.mjs` | Baixa (com cache), analisa e grava o seed de magias se todas as checagens passarem |
| `parse-spells.mjs` | Funções puras: texto de `spells.js` e `spells-by-class.js` para `CatalogSpell` |
| `import-items.mjs` | Baixa os JSONs do SRD, mapeia, grava `translations/source.en.json`, junta os lotes, valida e grava o seed de itens |
| `map-items.mjs` | Funções puras: registros do 5e-bits para `CatalogItem` (sem texto PT) |
| `validate-translation.mjs` | Validador da tradução (função pura e CLI) |
| `glossary.mjs` | Tabelas fixas EN para PT-BR de todos os termos mecânicos |
| `units.mjs` | Conversão pés para metros e libras para kg, em números e dentro de texto em inglês |
| `fetch-cache.mjs` | Download educado com cache em `.cache/` |
| `sources.mjs` | URLs das fontes e o commit fixado do SRD |
| `report.mjs` | Mantém uma seção por importador em `last-run-report.md` |
| `fixtures/` | Recortes reais das fontes usados pelos testes |
| `translations/` | `source.en.json`, `batch-plan.json`, `batches/NNN.json` e `items.pt-BR.json` |
| `last-run-report.md` | Relatório da última execução de cada importador |

## Como rodar

Na raiz do repositório:

```bash
node scripts/import-catalogs/import-spells.mjs                 # magias: baixa, valida, grava o seed
node scripts/import-catalogs/import-items.mjs --source-only     # itens: só gera translations/source.en.json
node scripts/import-catalogs/import-items.mjs                   # itens: junta os lotes, valida, grava o seed
node scripts/import-catalogs/validate-translation.mjs           # valida a tradução sem gravar nada
node --test scripts/import-catalogs/                            # testes
```

Opções:

| Opção | Importador | Efeito |
| --- | --- | --- |
| `--offline` | ambos | Não acessa a rede; falha se um arquivo não estiver em `.cache/` |
| `--cache-dir <dir>` | ambos | Outro diretório de cache (padrão `scripts/import-catalogs/.cache/`) |
| `--out <arquivo>` | ambos | Outro destino para o seed |
| `--report <arquivo>` | ambos | Outro destino para o relatório |
| `--source-only` | itens | Para depois de gravar `source.en.json` e `batch-plan.json`; sai com 0 se a fonte bateu |
| `--translations-dir <dir>` | itens | Outra pasta de traduções |

Os dois importadores saem com código 1 e **não escrevem o seed** quando qualquer checagem falha.
O seed existente fica intacto.

## Rede, cache e segurança

- No máximo 2 requisições simultâneas e pelo menos 300 ms entre o início de duas requisições.
- User-Agent genérico `CompanionCatalogImporter/1.0`, sem dados pessoais.
- 429, 5xx e erros de rede: até 5 tentativas com espera exponencial; `Retry-After` é respeitado.
- Tudo que passa na validação vai para `scripts/import-catalogs/.cache/` (ignorado pelo git) e nunca é
  baixado de novo. Para atualizar, apague o arquivo do cache.
- O conteúdo baixado é tratado só como texto. O `spells.js` nunca é executado nem avaliado: o importador
  recorta o literal de `document.write(\`...\`)` e recusa o arquivo se ele tiver `${...}`. As listas de
  `spells-by-class.js` são lidas por expressão regular.

## Política de fontes

### Magias: magias5e

- Fonte: <https://avribacki.gitlab.io/magias5e/spells.js> e `spells-by-class.js` (2 requisições).
- O autor autorizou o uso e pediu **nenhum crédito** no app. Por isso o seed tem `"attribution": null`,
  não tem `sourceUrl`, e nenhuma tela deve citar a fonte. O campo `source` é só a chave do seed
  (`SeedVersion` `spells.magias5e`).

### Itens: SRD 5.2.1 via 5e-bits

- Fonte: repositório `5e-bits/5e-srd-api`, pasta `packages/5e-database/src/2024/en`, fixado no commit
  **`c33bc0aab99716eb60b094222533d5531405eef4`** (main em 2026-11-19). Arquivos: `5e-SRD-Equipment.json`,
  `5e-SRD-Magic-Items.json`, `5e-SRD-Weapon-Properties.json`, `5e-SRD-Weapon-Mastery-Properties.json`.
- Licença do conteúdo: CC-BY-4.0. O seed leva o aviso obrigatório em `attribution`, e o catálogo de itens
  **precisa exibi-lo**:

  > This work includes material from the System Reference Document 5.2.1 ("SRD 5.2.1") by Wizards of the
  > Coast LLC, available at https://www.dndbeyond.com/srd. The SRD 5.2.1 is licensed under the Creative
  > Commons Attribution 4.0 International License, available at
  > https://creativecommons.org/licenses/by/4.0/legalcode.

- Para trocar de commit: mude `SRD_SHA` em `sources.mjs`, rode `--source-only`, confira as contagens em
  `EXPECTED_SOURCE` (`import-items.mjs`) e os testes, e retraduza só os índices cujo texto mudou.

## Magias: o que é extraído

Cada cartão `div.spell-item[data-name][data-level]` vira um `CatalogSpell`:

| Campo | Regra |
| --- | --- |
| `slug` | `data-name` (igual às listas por classe) |
| `name`, `level` | primeiro `<p>` de `.name`; `data-level`, conferido com o número de `p.school` |
| `school` | texto de `p.school` sem o número final |
| `ritual` | ícone `img/ritual.svg` dentro de `.name` |
| `castingTime`, `range`, `components`, `duration` | `.meta-item` identificado pelo ícone `cast`, `range`, `components`, `duration` |
| `concentration` | ícone `img/concentration.svg` no item de duração |
| `descriptionHtml` | `.description` com só `p`, `strong`, `em`, `ul`, `li`, `br`, sem atributos; tabelas viram um `<p>` por linha com células separadas por ` \| ` (cabeçalho em `<strong>`) |
| `higherLevels` | texto a partir de "Em Níveis Superiores", fora da descrição; `null` se não houver |
| `classes` | classes de `spells-by-class.js` que listam o slug |
| `suggestedDamageDice` | primeiro `NdM` ou `dM` da descrição, normalizado (`um d8` vira `1d8`) |
| `suggestedDamageType` | tipo nomeado depois de "dano" na mesma frase do dado, como chave do glossário (`fire`, `cold`...); aceita as formas antigas "concussão", "energia", "frio" |
| `attackKind`, `saveAbility` | `attack` com "ataque (corpo-a-corpo / à distância) com magia"; `save` com "teste de resistência de <Atributo>" (o que aparecer primeiro); senão `none`. Atributo em `FOR DES CON INT SAB CAR` |

Checagens que bloqueiam a gravação (`EXPECTED` em `parse-spells.mjs`): 361 magias; por nível 0 a 9:
27, 63, 59, 49, 35, 42, 32, 20, 18, 16; 32 rituais; 154 de concentração; as 8 classes (bardo, bruxo,
clérigo, druida, feiticeiro, mago, paladino, patrulheiro); 100% dos nomes das listas por classe
encontrados; nenhum cartão com campo faltando. Se a fonte mudar de verdade, atualize `EXPECTED` junto
com o novo seed.

## Itens: o que é mapeado

| Fonte | Categoria | Observação |
| --- | --- | --- |
| Armas (38) | `Weapon` | bloco `weapon`: dado, tipo de dano, dado versátil, `melee`/`ranged`, propriedades, alcance em metros (de `range` para armas à distância, de `throw_range` para arremesso), maestria |
| Armaduras (12) | `Armor` | bloco `armor`: CA base; tipo pelo limite de DES (sem limite = leve, 2 = média, sem DES = pesada); força mínima; desvantagem em Furtividade |
| Escudo | `Shield` | `{ acBonus: 2 }` |
| Ferramentas e equipamento | `Gear` | ferramentas trazem atributo, usos (CD) e o que fabricam no texto |
| Pergaminhos de magia | `Misc` | |
| Poções | `Potion` | |
| Itens mágicos | conforme o tipo | anel, cajado, varinha, bastão e item maravilhoso viram `Gear` (equipáveis, para os efeitos valerem) |

- **Raridade**: `common`, `uncommon`, `rare`, `very-rare`, `legendary`, `artifact`, `varies`. "Rarity Varies"
  e textos com várias raridades viram `varies`.
- **+1/+2/+3**: o 5e-bits já traz cada variante como registro próprio (`weapon-1`, `armor-2`, `shield-3`,
  `ammunition-1`...). O item pai com `variants` não entra no catálogo; cada variante entra com a raridade
  do seu nível e `magicBonus`.
- **Bônus mágico**: lido do texto exato do SRD ("+N bonus to attack rolls and damage rolls made with this
  weapon", "+N bonus to Armor Class while wearing this armor", o equivalente do escudo); munição pelo nome.
- **Arma ou armadura base**: quando a linha de tipo nomeia uma só base (`Weapon (Dagger)`,
  `Armor (Plate Armor)`), o bloco e o peso da base são copiados e `baseItem` aponta para ela. Itens genéricos
  ("Any Simple or Martial", "Longsword or Shortsword") ficam sem bloco; o app pede a base ao adicionar.
- **Tabelas expandidas**: Cinto de Força do Gigante vira 5 itens (FOR `min` 21, 23, 25, 27, 29). Poções de
  Cura viram 3 itens (maior, superior, suprema) com `healingDice`; a linha comum é mesclada na Poção de Cura
  do equipamento (`rarity: common`, `healingDice: 2d4+2`). Esses itens só traduzem o nome
  (`textFrom` aponta para o texto do pai).
- **Efeitos** (`KNOWN_EFFECTS`, só os óbvios, cada um preso a uma frase do texto, que se mudar faz o
  importador falhar): Anel e Manto de Proteção (`armorClass add 1`, `saveBonus all add 1`), Manoplas de
  Força do Ogro (FOR `min` 19), Amuleto da Saúde (CON `min` 19), Tiara do Intelecto (INT `min` 19),
  Varinha do Mago de Guerra +N (`attackBonus spell add N`), cintos acima. Poções de cura não têm efeito.
- **Peso** em kg (1 lb = 0,5 kg), **custo** como texto em PT (`15 PO`).
- Aviso conhecido: o 5e-bits classifica Hide Armor como leve, mas o limite de DES 2 a faz média (regra 2024);
  o importador usa média e registra o aviso no relatório.

## Glossário (`glossary.mjs`)

Termos mecânicos nunca passam por tradução livre: o código usa a tabela, e o tradutor tem de usar as
mesmas formas no texto.

- Propriedades: Acuidade (Finesse), Leve (Light), Pesada (Heavy), Duas Mãos (Two-Handed), Arremesso
  (Thrown), Munição (Ammunition), Recarga (Loading), Alcance (Reach), Versátil (Versatile), Distância (Range).
- Maestrias: Trespassar (Cleave), Raspão (Graze), Talho (Nick), Empurrão (Push), Enfraquecer (Sap),
  Lentidão (Slow), Derrubar (Topple), Afligir (Vex). **Escolha do projeto**: são os termos definidos para
  esta feature; não foi possível conferir contra o Livro do Jogador 2024 da Galápagos (a busca não achou
  fonte pública confiável). Se o livro usar outros, troque aqui e rode os testes; a tradução dos itens
  segue o glossário.
- Tipos de dano: ácido, contundente, cortante, elétrico, fogo, força, gélido, necrótico, perfurante,
  psíquico, radiante, trovejante, veneno.
- Raridades: Comum, Incomum, Raro, Muito Raro, Lendário, Artefato, Varia.
- Armaduras: leve, média, pesada. Moedas: PC, PP, PE, PO, PL.
- Termos de texto (`TEXT_TERMS`): teste de resistência, jogada de ataque, ponto de vida, Classe de Armadura,
  vantagem, desvantagem, sintonização, bônus de proficiência, ação bônus, reação, ação de Magia, CD,
  descanso curto, descanso longo e os seis atributos.

## Unidades (`units.mjs`)

Regra dos livros em PT: 5 pés = 1,5 m (pés x 0,3, uma casa decimal), 1 milha = 1,5 km, 1 polegada = 2,5 cm,
1 libra = 0,5 kg; área e volume derivam de 1 pé = 0,3 m. Galões, quartilhos e onças **não** são convertidos
(a fonte às vezes dá a quantidade em dados, "1d6 + 1 ounces"); só a palavra é traduzida.

`source.en.json` já sai com as unidades convertidas ("within 9 meters"), então o tradutor só troca palavras
e todo número do inglês reaparece no português.

## Tradução dos itens (T057)

1. `import-items.mjs --source-only` grava `translations/source.en.json`: `{ stats, items: { index: { name, text } } }`
   na ordem da fonte (461 entradas, 39.921 palavras) e `translations/batch-plan.json` (lotes de até 40 itens
   ou cerca de 4.000 palavras).
2. Cada lote vira `translations/batches/NNN.json` com o formato `{ "index": { "name": "...", "text": "..." } }`,
   cobrindo exatamente os índices do plano. Lotes que rodam em paralelo usam `isolation: "worktree"`, e a
   junção é sequencial.
3. Regras para quem traduz:
   - todo número e todo dado do inglês aparece igual no português (`2d4 + 2`, `+1`, `CD 15`, `1,5`); use
     vírgula decimal; não escreva por extenso um número que está em algarismo, nem o contrário;
   - medidas sem número ("half a pound", "a cubic foot") são traduzidas sem inventar número;
   - termos mecânicos só nas formas do glossário;
   - nenhuma palavra em inglês de regra (`ENGLISH_BLACKLIST`), nenhuma unidade imperial;
   - `text` vazio ou `null` quando o original não tem texto (entradas com `textFrom` só traduzem o nome);
   - tabelas markdown continuam tabelas, com as mesmas linhas.
4. Valide um lote: `node scripts/import-catalogs/validate-translation.mjs scripts/import-catalogs/translations/batches/007.json`.
5. Rode `import-items.mjs` sem `--source-only`: ele junta todos os lotes em `translations/items.pt-BR.json`
   (índice repetido entre lotes é erro), valida tudo e só então grava `items.srd52.json`.

**Refazer um lote**: apague ou reescreva `batches/NNN.json` (os índices estão em `batch-plan.json`),
valide o arquivo e rode o importador de novo. Os outros lotes não mudam.

Regras do validador (`validate-translation.mjs`), por índice: tradução presente e nome não vazio; texto
presente se e só se o original tem texto; multiconjunto de números e notações de dados igual ao do inglês
(depois da conversão de unidades), separado para nome e texto; toda expressão de regra do inglês aparece
na forma do glossário (singular ou plural); nenhuma palavra da lista negra e nenhuma unidade imperial.

## Contagens esperadas

| Catálogo | Contagem |
| --- | --- |
| Magias | 361 (por nível: 27, 63, 59, 49, 35, 42, 32, 20, 18, 16; 32 rituais; 154 de concentração) |
| Fonte de itens | 182 equipamentos (38 armas, 13 armaduras com o escudo, 90 itens de aventura, 36 ferramentas) e 277 itens mágicos (24 poções) |
| Catálogo de itens | 459 (Gear 322, Weapon 71, Armor 26, Potion 27, Shield 10, Misc 3) |
| Tradução | 461 entradas (459 itens mais 2 textos-pai), 1.171 palavras em nomes e 38.750 em textos |
