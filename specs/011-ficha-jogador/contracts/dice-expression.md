# Contrato: gramática e resultado das expressões de dados

Executada só no servidor (`Companion.Domain.Dice.DiceExpression`). O frontend só **gera** expressões (`frontend/app/utils/rollExpression.ts`); a forma gerada é exatamente a forma canônica que o servidor devolve em `Normalized`. Um arquivo de casos compartilhado (`backend/tests/Fixtures/dice-expressions.cases.json`) é lido pelos testes dos dois lados.

## Gramática (EBNF, espaços ignorados na leitura)

```
expressao   = repeticao | selecao | soma ;
repeticao   = INT "#" "[" ( selecao | soma ) "]"      (* nova, 1..20 *)
            | INT "#" soma ;                           (* legado: 3#d20+5 *)
selecao     = "2" "#" "(" soma ")" ( "maior" | "menor" ) ;
soma        = termo { ( "+" | "-" ) termo } ;          (* primeiro termo pode ter sinal *)
termo       = dado | rotulado | INT ;
dado        = [ INT ] "d" INT ;
rotulado    = "(" ROTULO ")" INT ;
ROTULO      = 1..16 caracteres de letras (com acento), dígitos, espaço, "+", "." ou "'" ;
```

- `maior` e `menor` aceitam qualquer combinação de maiúsculas na leitura; a forma canônica é minúscula.
- `selecao` só existe com 2 execuções (vantagem/desvantagem). Outros números antes de `#(` são erro.
- `repeticao` com colchetes pode conter uma `selecao`; uma `selecao` não pode conter `repeticao` nem outra `selecao`.
- Um termo rotulado negativo é escrito `- (FOR) 1`.

## Limites (mesma mensagem de hoje, estendida)

- Repetições: 1..20. Faces: 2..1000. Quantidade por termo: 1..100.
- **Dados executados no total** (`dados por execução × 2 se houver seleção × repetições`) ≤ 100.
- Soma das constantes por execução entre −10000 e 10000.
- Expressão canônica com até 256 caracteres.

## Forma canônica (`Normalized`)

- Expressão só com a sintaxe legada (sem rótulo, sem colchetes, sem seleção): **inalterada**, compacta, como hoje (`3#3d5+4`). Garante que nenhum teste ou histórico atual mude.
- Expressão com qualquer construção nova: termos separados por ` + ` / ` - `, dados antes de constantes na ordem em que vieram, dado único sem o `1` (`d20`), rótulo como `(FOR) 3`, seleção como `2#(…) maior`, repetição como `N#[…]`.

| Entrada | Canônica |
|---|---|
| `d20+5+2d6` | `1d20+2d6+5` (legado, regra atual de hoje) |
| `2#(d20 + 5 + 2d6) maior` | `2#(d20 + 2d6 + 5) maior` |
| `3#[d20+(FOR)3+(prof)3]` | `3#[d20 + (FOR) 3 + (prof) 3]` |
| `3#[2#(d20+(DES)4)MENOR]` | `3#[2#(d20 + (DES) 4) menor]` |
| `2#(d20+5)` | erro: seleção sem `maior`/`menor` |
| `4#(d20) maior` | erro: seleção só com 2 |
| `2#[2#[d20]]` | erro: repetição aninhada |

> A ordem "dados antes de constantes" reproduz o comportamento do `Normalized` legado. O gerador do frontend já emite nessa ordem, então tela e servidor mostram a mesma string.

## Execução

1. Para cada repetição `r` em 1..N:
   1. Se houver seleção: executa a `soma` completa duas vezes (todos os dados rolados de novo, todas as constantes somadas nas duas). Total de `r` = maior (ou menor) dos dois totais. Empate: a primeira execução é a escolhida.
   2. Sem seleção: executa a `soma` uma vez.
2. `DiceRoll.Total` continua sendo o total da primeira repetição (como hoje).

## Resultado (`DiceRollResultDto`, campos novos opcionais)

```jsonc
{
  "sequence": 1,
  "terms": [ /* termos da execução escolhida, como hoje */ ],
  "modifier": 7,                       // soma das constantes da execução escolhida
  "total": 21,                         // total escolhido
  "modifiers": [                       // novo, opcional: constantes com rótulo
    { "label": "FOR", "value": 3 }, { "label": "prof", "value": 3 }, { "label": null, "value": 1 }
  ],
  "selection": "maior",                // novo, opcional: "maior" | "menor" | null
  "executions": [                      // novo, opcional: presente só com seleção
    { "terms": [...], "modifier": 7, "total": 21 },
    { "terms": [...], "modifier": 7, "total": 12 }
  ],
  "selectedExecution": 0               // novo, opcional
}
```

Clientes antigos leem `terms`, `modifier` e `total` como antes e veem a execução escolhida. O histórico novo mostra as duas execuções e marca a escolhida.
