# Quickstart: Ataques Tipados e Caderno da Campanha

## Pré-requisitos

- Ambiente local Companion iniciado com API, frontend e SQL Server.
- Uma campanha com uma sessão de mestre e duas sessões de jogador em navegadores distintos.
- Fichas com a mesma arma; um registro de mapa de referência com imagem.

## Jornada 1: ataque com dano misto

1. No Compêndio, edite uma arma de campanha e confirme pelo menos um tipo físico; adicione uma opção adicional se a arma permitir mais de um tipo.
2. Na ficha de jogador, crie um ataque salvo associado à arma e inclua um componente adicional, como `1d4 gélido`.
3. Abra o Modo Jogo em dois navegadores e use o ataque salvo.
4. Confirme que o resultado mostra subtotal por componente, tipo de dano e total somado.
5. Reabra a ficha e confirme que a configuração continua salva. Repita com uma rolagem antiga sem componentes adicionais.

## Jornada 2: caderno colaborativo

1. Abra **Campanha** como mestre e crie um NPC, um mapa com imagem, uma pista e uma entrada de diário.
2. Como jogador, crie outra pista, acrescente uma contribuição a um NPC e consulte o diário.
3. Atualize e reconecte as sessões. Confirme que o conteúdo compartilhado e sua autoria são recuperados.
4. Busque pelo nome de uma pista e filtre pelo tipo.

## Jornada 3: privacidade

1. Como mestre, adicione nota pessoal ao NPC e oculte uma pista e um mapa.
2. Como jogador, busque a pista, consulte contagens, tente abrir o mapa pelo identificador e observe eventos durante mudanças na campanha.
3. Confirme que nenhum resultado revela título, nota, imagem, ponto ou existência do registro oculto.
4. Como mestre, confirme que as notas e registros continuam disponíveis; torne a pista visível e confirme que jogadores só a veem depois da confirmação.

## Jornada 4: pontos de interesse responsivos

1. Em um mapa visível, crie dois pontos de interesse com nome e descrição em celular e desktop.
2. Redimensione a janela e confirme que os pontos permanecem ancorados à imagem.
3. Abra a campanha em outro participante e após reconexão; confirme a posição e conteúdo.

## Validação automatizada executada

- Backend: `dotnet test backend/Companion.slnx --no-restore` — 533 testes unitários e 145 testes de integração aprovados, incluindo migrações em SQL Server/Testcontainers.
- Frontend: `pnpm test` — 499 testes Vitest aprovados.
- Frontend: `pnpm typecheck` — aprovado.
- Backend: `dotnet build backend/Companion.slnx --no-restore` — aprovado.

Antes da publicação, revisar visualmente as jornadas desktop e Android e confirmar os fluxos com duas sessões reais da mesma campanha.
