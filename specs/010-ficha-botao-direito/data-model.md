# Data Model: Ficha pelo botão direito e resultado da rolagem visível por 2 s

Nenhuma entidade persistida nova ou alterada. Esta feature usa a ficha de criatura, o vínculo token → criatura e o preset de rolagem por token das specs 008 e 009. O que segue é o estado de interface.

## Ficha de um token (`sheetFor`)

| Quem vê | Token | Resultado |
|---|---|---|
| Mestre | com criatura vinculada | ficha da criatura (id da criatura) |
| Mestre | jogador ou marcador | nenhuma: o botão direito abre o menu |
| Jogador | qualquer | nenhuma: o botão direito abre o menu (a ficha de jogador é futura) |

## Exibição da rolagem (`data-roll-showing`)

| Estado | Significado | Dura |
|---|---|---|
| `rolling` | dados girando, aguardando o servidor | até a revelação (~0,8 s) |
| `result` | dados assentados e total revelado | 2 s (`RESULT_HOLD_MS`) |
| *(ausente)* | sem animação; "Último resultado" visível se já houve rolagem | até a próxima rolagem |

**Transições**
- clique em Rolar: o estado vai para `rolling` e o timer anterior é cancelado;
- revelação: o estado vai para `result`, o histórico e o preset são atualizados, o botão é liberado e o timer de 2 s começa;
- fim do timer: o estado fica ausente;
- erro: o estado fica ausente na hora;
- fechar a ficha: o timer é cancelado.
