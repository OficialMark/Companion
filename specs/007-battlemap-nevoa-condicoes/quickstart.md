# Quickstart: validar a spec 007

## Pré-requisitos

- Docker (SQL Server de desenvolvimento) e `docker compose up -d` na raiz, ou backend rodando localmente em `:8080`.
- `cd frontend && pnpm install && pnpm dev` (proxy `/api` e `/hubs` para `:8080`).
- Dois navegadores (ou uma janela anônima): um como mestre, outro como jogador na mesma mesa.

## Testes automatizados

```bash
cd backend && dotnet test
```

```bash
cd frontend && pnpm vitest run
```

```bash
cd frontend && pnpm nuxt typecheck
```

## Cenários manuais

1. **Menu do token (US1)**: no desktop, clique e solte com o botão esquerdo num token 1x1, num 2x2 (clicando na metade inferior direita) e num token empilhado. O painel deve continuar aberto e o token não deve se mover. Arraste o token para outra célula: ele se move. Clique direito: abre o painel sem menu de contexto do navegador e sem arraste.
2. **Condições (US2)**: no painel, marque Envenenado, Sangrando, Paralisado e Caído. No mapa aparecem 3 selos com ícones e um "+1". Passe o mouse no token: a dica lista as quatro. O jogador vê o mesmo.
3. **HP (US3)**: com combate ativo, o mestre oculta o HP de uma criatura (painel do token ou ícone de olho na iniciativa). O jogador deixa de ver a barra e os números dela; o HP dos personagens continua visível. Use "Ocultar HP das criaturas" e confira todas. No DevTools do jogador, a resposta de `GET /api/campaigns/{id}` não tem `currentHitPoints` da criatura.
4. **Névoa (US4)**: o mestre abre "Névoa", "Cobrir tudo", troca para "Revelar" + "Retângulo" e arrasta sobre uma sala; troca para pincel 1x1 e revela mais células. O jogador vê só o revelado; uma criatura numa célula coberta não aparece. Revele a célula: ela aparece em até 3 s. Recarregue a página do jogador: estado igual.
5. **Responsividade**: repita 1, 2 e 4 em 360x740 (celular) e 768x1024 (tablet). Nenhuma rolagem horizontal; botões com pelo menos 44 px; painel e barra de névoa não escondem o token selecionado.

## Limites conhecidos

- A imagem do mapa é baixada inteira pelo jogador; a névoa é opaca na interface, não na imagem.
- Névoa é manual (sem linha de visão automática).
