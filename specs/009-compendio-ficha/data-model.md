# Data Model: Compêndio na barra superior e ficha de criatura legível

Esta feature não cria nem altera entidades persistidas. Ela usa `Creature` e o filtro de origem da spec 008 ([../008-biblioteca-criaturas/data-model.md](../008-biblioteca-criaturas/data-model.md)). O que segue é o estado de interface.

## MasterView (estado de interface)

| Campo | Tipo | Origem | Regra |
|---|---|---|---|
| `view` | `'mesa' \| 'compendio'` | `route.query.tela` | `'compendio'` só quando a sessão é de mestre **e** `tela === 'compendio'`. Qualquer outro caso é `'mesa'`. |

**Transições**
- `mesa → compendio`: o clique em "Compêndio" grava `tela=compendio` no endereço, preserva os outros parâmetros e rola ao topo.
- `compendio → mesa`: o clique em "Mesa" remove `tela` do endereço e rola ao topo.
- O clique na tela já aberta não faz nada.
- Ao perder o papel de mestre, a tela volta a ser `'mesa'`, porque a regra deriva do papel, e a ficha aberta fecha.

## Filtro "Minhas criaturas"

| Estado | Equivale a | Efeito |
|---|---|---|
| ligado | `query.origin === 'Custom'` | Lista só as criaturas criadas na mesa, a partir da página 1. |
| desligado | `query.origin === ''` | Lista todas as origens. |

O select "Origem" e o atalho mostram o mesmo estado: escolher "Criadas" no select acende o atalho.

## Grade do editor (spans por largura)

| Seção | Desktop (6 colunas) | Até 680 px (6 colunas) |
|---|---|---|
| Identidade | Nome 3, Nome EN 3; Tamanho 2, Tipo 2, Alinhamento 2; ND 2, XP 2, Proficiência 2 | Nome, EN, Tamanho, Tipo e Alinhamento 6 cada; ND 2, XP 2, Proficiência 2 |
| Combate | CA 2, PV 2, Iniciativa 2; Dados de vida 2, Nota da CA 4; Deslocamento 6 | CA 2, PV 2, Iniciativa 2; Dados de vida, Nota da CA e Deslocamento 6 cada |
| Detalhes (8 campos) | 3 + 3 por linha | 6 cada |

Toda linha soma 6, então nenhuma termina com célula vazia. No celular, só números ficam lado a lado (trios ND/XP/Proficiência e CA/PV/Iniciativa); todo campo de texto ocupa a linha inteira. Antes, "dragão (cromático)" aparecia cortado num campo de meia largura, como o crítico apontou.
