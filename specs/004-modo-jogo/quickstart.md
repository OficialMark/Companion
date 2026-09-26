# Quickstart: Modo Jogo (Visão Focada de Gameplay)

Guia de validação manual/automatizada end-to-end para confirmar que a feature funciona,
sem repetir os detalhes de implementação (ver [data-model.md](./data-model.md) e
[contracts/README.md](./contracts/README.md)).

## Pré-requisitos

- Backend (`backend/`) e frontend (`frontend/`) rodando localmente (ver `docker-compose`
  do repositório) com uma campanha e um encontro (`Encounter`) ativos contendo ao menos um
  combatente.
- Uma sessão de participante autenticada (mestre ou jogador) na página de sessão
  (`frontend/app/pages/index.vue`).

## Cenário 1 — Entrar e sair do Modo Jogo (US1)

1. Abrir a página de sessão com a visão completa.
2. Clicar no botão "Modo Jogo" na barra superior.
3. **Esperado**: apenas os blocos Rolagem de Dados, Combate, Mapa e Histórico ficam
   visíveis; os demais painéis (configuração de encontro, participantes) desaparecem.
4. Clicar no botão para sair do Modo Jogo.
5. **Esperado**: a visão completa volta a ser exibida.

Validação automatizada: `frontend/tests/*.spec.ts` (novo teste de componente para
`index.vue`/`GameModeToggle.vue` verificando quais painéis são renderizados em cada
estado).

## Cenário 2 — Rolagem rápida com modificador (US2)

1. No Modo Jogo, incrementar o modificador para +3 usando o botão "+".
2. Clicar no botão d8.
3. **Esperado**: resultado exibido é o valor bruto do d8 retornado pelo servidor + 3, e a
   expressão enviada ao backend foi equivalente a `1d8+3`.
4. Clicar no botão d20 em seguida, sem alterar o modificador.
5. **Esperado**: a rolagem de d20 também aplica +3 automaticamente.

Validação automatizada: `frontend/tests/dice.spec.ts` (novo caso cobrindo montagem da
expressão com modificador e reuso do mesmo modificador entre rolagens consecutivas).

## Cenário 3 — Fluxo de ataque → dano → alvo (US3)

1. No Modo Jogo, com um encontro ativo e ao menos um combatente, clicar no botão d20.
2. **Esperado**: aparece um modal "Acertou?" junto ao resultado da rolagem.
3a. Não responder por 10 segundos.
   **Esperado**: o modal fecha sozinho; nenhum modal de dano é aberto.
3b. Clicar em "Sim".
   **Esperado**: abre o modal de rolagem de dano.
4. Rolar o dano (ex. clicar em rolar no modal de dano, seguindo as mesmas regras de
   `CombatAttack.vue`).
5. Selecionar um combatente da lista de alvos e confirmar.
6. **Esperado**: o dano é aplicado ao combatente escolhido (via `resolveAttack`), o
   encontro é atualizado (`store.updateActiveEncounter`) e uma mensagem de confirmação é
   exibida.

Validação automatizada: `frontend/tests/attack.spec.ts` (estendido com o caminho iniciado
pela rolagem rápida do Modo Jogo, reaproveitando os mocks de `resolveAttack` já
existentes); cenário e2e opcional em `frontend/tests/e2e` cobrindo o caminho feliz
completo.

## Cenário 4 — Correção do bug de dado não convencional (US4)

1. Fora do Modo Jogo (ou usando a expressão livre, se exposta), rolar a expressão `1d27`.
2. **Esperado**: uma animação de dado aparece (visual genérico), em vez de nenhuma
   animação.
3. Rolar a expressão `1d20+1d27`.
4. **Esperado**: dois dados animam (um com o visual padrão de d20, outro com o visual
   genérico), e o total final soma corretamente ambos os termos.

Validação automatizada: `frontend/tests/dice.spec.ts` (novo caso testando
`diceForAnimation('1d27')` e `diceForAnimation('1d20+1d27')` diretamente sobre o utilitário
`frontend/app/utils/diceAnimation.ts`, como teste de regressão do bug).

## Critério de conclusão

Todos os quatro cenários acima passam manualmente (ou via testes automatizados
equivalentes) antes de considerar a feature pronta para revisão, conforme o Princípio IV
da constituição (cada user story deve ser validável de forma independente).
