# Quickstart: Validar Aprimoramentos de Combate no Battlemap

## Pré-requisitos

- Backend rodando (`dotnet run --project backend/src/Companion.Api`) com banco atualizado (`dotnet ef database update` após a nova migration).
- Frontend rodando (`pnpm --dir frontend dev`).
- Duas abas/navegadores conectados à mesma campanha: um como mestre, um como jogador (ver `specs/003-battlemap-grid/quickstart.md` ou README para como criar uma campanha e entrar com os dois códigos).
- Um battlemap já publicado (imagem + grid) na campanha, com um encontro criado.

## Cenário 1 — Destaque de turno (User Story 1)

1. Como jogador, na tela inicial, informe vida máxima e atual do personagem.
2. Como mestre, adicione o jogador à iniciativa ("Rolar iniciativas") e inicie o combate ("Começar combate").
3. Posicione o pino do jogador no battlemap (arrastando ou tocando + selecionando célula).
4. **Esperado**: o token do combatente da vez exibe um destaque visual (contorno/brilho) — confira em ambas as telas (mestre e jogador).
5. Como mestre, clique em "Próximo turno".
6. **Esperado**: o destaque some do token anterior e aparece no próximo, em até 3s, nas duas telas.
7. Como mestre, clique em "Encerrar".
8. **Esperado**: nenhum token exibe destaque de turno.

## Cenário 2 — Barra de vida (User Story 2)

1. Com o combate ativo do cenário 1, como mestre, insira uma criatura no encontro informando vida máxima e atual.
2. Posicione o token da criatura no battlemap.
3. **Esperado**: uma barra de vida semi-transparente aparece acima do token da criatura e do token do jogador, proporcional a atual/máxima, visível em ambas as telas (jogador vê a vida exata da criatura — FR-009a).
4. Aplique dano à criatura (fluxo de dano já existente).
5. **Esperado**: a barra de vida da criatura é atualizada em até 3s em ambas as telas.

## Cenário 3 — Tamanho do token e condições de status (User Stories 3 e 4)

1. Como mestre (ou jogador, no próprio token), clique uma única vez (clique esquerdo/toque) no token da criatura.
2. **Esperado**: o menu de interação abre (mesmo painel de área/projétil, agora com seções extras de "Tamanho" e "Condições").
3. Escolha o tamanho "3x3".
4. **Esperado**: o token passa a ocupar visualmente 3x3 células, em ambas as telas.
5. Marque a condição "Atordoado".
6. **Esperado**: um símbolo de atordoado aparece junto ao token, em ambas as telas.
7. Como jogador, tente abrir o menu de tamanho/condições de um token que não é seu (nem é o mestre).
8. **Esperado**: a opção não fica disponível / a ação é bloqueada.

## Cenário 4 — Token representativo do jogador (User Story 5)

1. Como jogador, acione "Inserir token" (novo controle no battlemap).
2. **Esperado**: um novo token próprio aparece (na reserva ou em posição padrão), distinto do pino principal.
3. Posicione esse token em uma célula do grid.
4. Configure uma área de alcance para ele pelo menu de interação.
5. **Esperado**: outro participante conectado vê o novo token, sua posição e sua área.
6. Como outro jogador, tente mover ou remover esse token.
7. **Esperado**: ação bloqueada (não é o dono nem o mestre).
8. Como o próprio jogador, remova o token.
9. **Esperado**: o token e sua área desaparecem do battlemap para todos.

## Cenário 5 — Gesto único em qualquer dispositivo (User Story 6)

1. Em uma viewport de desktop, clique uma única vez com o botão esquerdo do mouse sobre um token controlável.
2. **Esperado**: o menu de interação abre.
3. Repita em uma viewport de celular (emulada ou real) com um único toque.
4. **Esperado**: o mesmo menu abre, com todos os controles (formato/raio, tamanho, condições) utilizáveis por toque, sem exigir zoom manual.

## Testes automatizados a rodar

```bash
dotnet test backend/tests/Companion.UnitTests
dotnet test backend/tests/Companion.IntegrationTests
pnpm --dir frontend test
pnpm --dir frontend test:e2e -- battlemap
```
