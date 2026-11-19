# Relatório da última importação dos catálogos

Gerado por `import-spells.mjs` e `import-items.mjs`. Cada importador reescreve só a sua seção.

<!-- section:items -->
## Itens (SRD 5.2.1, 5e-bits)

Fonte: `5e-bits/5e-srd-api` no commit `c33bc0aab99716eb60b094222533d5531405eef4`, pasta `packages/5e-database/src/2024/en`.

Resultado: **PASS**, `backend/src/Companion.Infrastructure/Persistence/Seed/items.srd52.json` escrito (458678 bytes).

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

Tradução: 461 entradas, juntadas de 14 lotes.

Validação: 461/461 entradas válidas, 0 erros.
<!-- /section:items -->
