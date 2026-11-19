# Relatório da última importação dos catálogos

Gerado por `import-spells.mjs` e `import-items.mjs`. Cada importador reescreve só a sua seção.

<!-- section:spells -->
## Magias (magias5e)

Resultado: **PASS**, `backend/src/Companion.Infrastructure/Persistence/Seed/spells.magias5e.json` escrito (545271 bytes).

| Verificação | Esperado | Obtido | Status |
| --- | --- | --- | --- |
| total | 361 | 361 | PASS |
| nível 0 | 27 | 27 | PASS |
| nível 1 | 63 | 63 | PASS |
| nível 2 | 59 | 59 | PASS |
| nível 3 | 49 | 49 | PASS |
| nível 4 | 35 | 35 | PASS |
| nível 5 | 42 | 42 | PASS |
| nível 6 | 32 | 32 | PASS |
| nível 7 | 20 | 20 | PASS |
| nível 8 | 18 | 18 | PASS |
| nível 9 | 16 | 16 | PASS |
| ritual | 32 | 32 | PASS |
| concentração | 154 | 154 | PASS |
| classes | bardo, bruxo, clérigo, druida, feiticeiro, mago, paladino, patrulheiro | bardo, bruxo, clérigo, druida, feiticeiro, mago, paladino, patrulheiro | PASS |
| nomes das listas por classe encontrados | 842 | 842 | PASS |

Informativo (sem valor esperado fixo):

- Magias com "Em Níveis Superiores": 101
- Com dado de dano sugerido: 133; com tipo de dano: 98
- Tipo de rolagem: ataque 22, salvaguarda 128, nenhum 211
- Entradas nas listas por classe: 842
- Magias sem nenhuma classe: 0
<!-- /section:spells -->

<!-- section:items -->
## Itens (SRD 5.2.1, 5e-bits)

Fonte: `5e-bits/5e-srd-api` no commit `c33bc0aab99716eb60b094222533d5531405eef4`, pasta `packages/5e-database/src/2024/en`.

Resultado: **SOURCE OK**, tradução pendente (modo `--source-only`, seed não escrito).

| Verificação da fonte | Esperado | Obtido | Status |
| --- | --- | --- | --- |
| equipment | 182 | 182 | PASS |
| weapons | 38 | 38 | PASS |
| armor | 13 | 13 | PASS |
| adventuringGear | 90 | 90 | PASS |
| tools | 36 | 36 | PASS |
| magicItems | 277 | 277 | PASS |
| potions | 24 | 24 | PASS |
| propriedades no glossário | 10 | 10 | PASS |
| maestrias no glossário | 8 | 8 | PASS |

Catálogo mapeado:

- Itens no catálogo: 459 (Gear 322, Weapon 71, Armor 26, Potion 27, Shield 10, Misc 3)
- Com efeitos mecânicos: 13; com bônus mágico (+N): 29
- Entradas a traduzir: 461 (390 com texto), 39921 palavras (1171 em nomes, 38750 em textos)
- Lotes planejados: 14 (até 40 itens ou cerca de 4000 palavras cada), em `translations/batch-plan.json`

Avisos do mapeamento:

- hide-armor: tipo medium pelo limite de DES, mas a fonte diz light-armor; usado medium

Tradução: pendente (T057). Rode sem `--source-only` depois que os lotes existirem.
<!-- /section:items -->
