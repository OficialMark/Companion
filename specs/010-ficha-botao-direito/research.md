# Research: Ficha pelo botão direito e resultado da rolagem visível por 2 s

Não havia NEEDS CLARIFICATION. As decisões abaixo resolvem as escolhas de interação.

## R1. Onde decidir se um token tem ficha

- **Decision**: uma função única no tabuleiro, `sheetFor(token)`. Ela devolve a ficha quando quem olha é o mestre e o token tem uma criatura vinculada; nos outros casos devolve nada. Usam a mesma função:
  - o botão direito;
  - o equivalente de teclado;
  - o botão "Ficha e rolagens" do menu;
  - a dica da barra de status.
- **Rationale**: o pedido antecipa fichas de jogador. Com um ponto só, a ficha de jogador entra ali sem caçar condições espalhadas.
- **Alternatives considered**: repetir `isMaster && creatureId` em cada lugar (como estava no botão do menu). Funciona hoje, mas multiplica os pontos de mudança.

## R2. O que o botão direito faz em cada token

- **Decision**:
  - **Token com ficha**: abre a ficha. Se o menu desse token estava aberto, ele fecha antes.
  - **Token sem ficha**: abre o menu, como antes.
  - **Modo névoa**: não faz nada, como antes.
  - **Arraste**: o botão direito nunca inicia um.
- **Rationale**: assim o gesto não perde utilidade em tokens sem ficha, e a ficha não abre por baixo de um menu aberto.
- **Alternatives considered**:
  - Usar o evento `contextmenu`: o tabuleiro já o suprime, e o gesto vem do `pointerdown` do token.
  - Botão direito sempre abrir o menu com um atalho para a ficha: é o caminho atual, que o pedido quer encurtar.

## R3. Equivalente de teclado

- **Decision**: Shift+F10 e a tecla Menu, num token com foco, emitem o evento `context` do token, que segue o mesmo caminho do botão direito.
- **Rationale**: são os atalhos padrão do sistema para o "menu de contexto". Assim o gesto novo não fica exclusivo do mouse.
- **Alternatives considered**: uma tecla própria (por exemplo "F"), que ninguém descobriria.

## R4. Resultado por 2 s

- **Decision**:
  - A constante `RESULT_HOLD_MS = 2000` fica no utilitário de rolagem de criatura.
  - Depois da revelação:
    - o histórico e o preset do token são atualizados na hora;
    - o botão é liberado;
    - um timer limpa a animação ao fim dos 2 s, e a linha "Último resultado" assume.
  - **Nova rolagem**: cancela o timer anterior e recomeça.
  - **Erro**: limpa na hora.
  - **Fechar a ficha**: cancela o timer.
- **Rationale**:
  - O rolador principal segura 1 s com o botão travado. Na ficha, o mestre pode precisar rolar dano logo depois do acerto, então travar o botão por 2 s atrapalharia.
  - Contar a partir da revelação garante 2 s de número final na tela, não de dados girando.
- **Alternatives considered**:
  - Travar o botão durante a exibição: atrasa rolagens seguidas.
  - Contar desde o clique: o número final ficaria menos tempo na tela.

## R5. Resultado visível dentro do modal

- **Decision**: ao rolar, e de novo na revelação, a área dos dados entra na vista da ficha (`scrollIntoView`, bloco `nearest`).
- **Rationale**: o painel de rolagem fica no meio da ficha, e a animação aparece abaixo do botão, cortada pelo rodapé. A primeira captura confirmou isso: a espera de 2 s funcionava, mas fora da vista.
  - Uma linha extra de "Resultado" foi testada e removida, porque a animação já mostra "TOTAL" e "RESULTADO DEFINIDO".
- **Alternatives considered**:
  - Overlay sobre a ficha: tapa os controles de rolagem.
  - Mover o painel para o topo da ficha: muda a hierarquia que a spec 009 acabou de acertar.

## R6. Como verificar

- **Decision**:
  - **Unidade**: gestos (botão direito, menu aberto, token sem ficha, jogador, teclado, dica) e timers falsos para os 2 s, a interrupção, o erro e o fechamento.
  - **E2E no navegador real**:
    - posiciona um goblin no mapa e clica com o botão direito;
    - rola com a ficha no topo, sem a rolagem automática do clique do Playwright;
    - mede que pelo menos 95% da área dos dados está dentro da parte visível da ficha;
    - mede o tempo de exibição entre 1,7 s e 3,5 s.
- **Rationale**: `toBeInViewport` mede contra a janela do navegador, não contra a área com rolagem do modal, e passava mesmo sem a correção. A mutação expôs isso. A medição dentro da ficha falha sem a rolagem (3 de 3 execuções) e passa com ela (3 de 3).
