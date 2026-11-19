# Contrato de interface: ficha pelo botão direito e espera do resultado

A API HTTP não muda.

## Token no tabuleiro

| Entrada | Token com ficha | Token sem ficha |
|---|---|---|
| Botão direito (`pointerdown`, button 2) | emite `open-creature-sheet(tokenId, creatureId, nome)`; fecha o menu desse token se estava aberto | abre o menu do token (como antes) |
| Shift+F10 / tecla Menu no token com foco | igual ao botão direito | igual ao botão direito |
| Clique esquerdo | abre o menu do token (sem mudança) | abre o menu do token (sem mudança) |
| Modo névoa ativo | nada (sem mudança) | nada (sem mudança) |

- O menu de contexto do navegador nunca aparece sobre o tabuleiro.
- A barra de status (`.battlemap-status`) termina com "Botão direito numa criatura abre a ficha." quando há ao menos um token com ficha.

## Painel de rolagem da ficha

- `section.creature-roll[data-roll-showing]` vale `rolling` ou `result` durante a animação e fica ausente fora dela.
- `[data-roll-stage]` envolve a animação. Ao rolar e ao revelar, ela entra na área visível da ficha.
- Depois da revelação, `[data-roll-go]` fica habilitado. Uma nova rolagem substitui a exibição e reinicia os 2 s.
- Ao fim de `RESULT_HOLD_MS` (2000 ms), aparece `.roll-last` com "Último resultado".
