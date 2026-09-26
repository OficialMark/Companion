# Quickstart: Validação da Área de Ataque no Battlemap

## Pré-requisitos

- Backend rodando (`dotnet run` no projeto `Companion.Api`) com banco de dados migrado (inclui a migration da nova tabela `AttackAreas`).
- Frontend rodando (`pnpm dev` em `frontend/`).
- Uma campanha com um encontro (`Encounter`) em modo de combate, com pelo menos dois participantes conectados (dois navegadores/abas, um como mestre e outro como jogador) e um battlemap configurado com tokens posicionados.

## Cenário 1 — Jogador configura área no próprio personagem (User Story 1)

1. Como jogador, abra a campanha e entre no modo de combate.
2. Clique no token do seu personagem no battlemap.
3. **Esperado**: um painel compacto aparece com 4 opções de formato (círculo, quadrado, cone, cilindro).
4. Escolha "círculo", arraste o slider de raio para 2.
5. **Esperado**: um círculo semi-transparente de raio 2 blocos é desenhado ao redor do token, atualizando em tempo real conforme o slider se move.
6. Toque fora do painel.
7. **Esperado**: o painel de configuração desaparece; o círculo permanece desenhado no mapa.

## Cenário 2 — Reconfigurar e remover (User Story 2)

1. Clique sobre o círculo desenhado no Cenário 1.
2. **Esperado**: o painel reabre com "círculo" e raio 2 já selecionados.
3. Altere para "cone", ajuste distância para 4 e largura para 2, gire a direção.
4. **Esperado**: o desenho muda para um cone/leque na direção escolhida.
5. Acione "remover".
6. **Esperado**: a área desaparece do mapa.

## Cenário 3 — Mestre configura para qualquer token (User Story 3)

1. Na segunda aba (mestre), clique no token de uma criatura/NPC do mestre.
2. **Esperado**: o painel de formato abre normalmente (mesmo fluxo do jogador).
3. Clique no token do personagem do jogador (não controlado pelo mestre).
4. **Esperado**: o painel também abre (mestre pode configurar para qualquer token).
5. Volte à aba do jogador e clique no token de outro jogador (hipotético) ou do NPC do mestre.
6. **Esperado**: o painel NÃO abre para o jogador nesse token de terceiros.

## Cenário 4 — Sincronização em tempo real (User Story 4)

1. Com as duas abas lado a lado (jogador e mestre, mesma campanha/encontro), repita o Cenário 1 na aba do jogador.
2. **Esperado**: em até ~3 segundos, o mesmo círculo aparece na aba do mestre, sem recarregar a página.
3. Remova a área na aba do mestre (mestre pode remover qualquer área).
4. **Esperado**: a área some também na aba do jogador em poucos segundos.

## Cenário 5 — Correção do bug de HP/iniciativa em combate (User Story 5)

1. Na tela inicial (modo de configuração/setup), confirme que os campos de edição de HP e iniciativa dos combatentes aparecem editáveis normalmente.
2. Ative o modo de combate (`GameModeToggle`).
3. **Esperado**: os campos de edição de HP/iniciativa deixam de ser exibidos como editáveis (somente leitura ou ocultos); os valores de HP/iniciativa continuam visíveis.
4. Volte ao modo de configuração.
5. **Esperado**: os campos de edição voltam a aparecer normalmente.

## Validação automatizada

- Backend: `dotnet test backend/tests/Companion.UnitTests` (inclui `AttackAreaTests.cs` — regras de forma/raio/largura/direção e autorização).
- Frontend: `pnpm test` em `frontend/` (inclui `attack-area.spec.ts` e `initiative-tracker-combat-mode.spec.ts`).
- E2E (opcional, cobre sincronização entre duas sessões): `pnpm test:e2e`.
