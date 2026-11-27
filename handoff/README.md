# Handoff para o Codex: acabamento do layout (spec 017)

Estado em 2026-11-27. Tudo abaixo já está na `main` (PRs #17 e #18) e no ar na porta 3636.

## Regras da casa (leia antes)

- `CLAUDE.md` na raiz: triagem antes de trabalhar, uma worktree por sessão (nunca edite `C:\Github\Companion` direto), testes junto com a mudança, PR para a `main`.
- Marcos quer: português, direto, sem travessão; merge na `main` depois de verde; deploy com `git -C C:/Github/Companion pull --ff-only origin main` e `docker compose up -d --build api frontend` a partir de `C:\Github\Companion` (o Docker Desktop às vezes está parado: inicie antes).
- Features novas passam pelo speckit (`specs/NNN-*`: spec, plan, research, quickstart, tasks). O acabamento abaixo entra em `specs/017-layout-responsivo/tasks.md` como tarefas novas (T030+).

## O que foi feito

1. **Spec 016, tokens do compêndio e de itens** (`specs/016-tokens-compendio-itens/`): qualquer participante põe no mapa uma criatura (nome, imagem, tamanho) ou um item (desenhado como quadrado). Rota `GET /api/campaigns/{id}/creatures/token-options` (jogador vê só o compêndio público, sem ficha). `BattlemapMarker` ganhou `CreatureId`/`CatalogItemId` (migration `AddMarkerSources` nos 3 provedores). Seletor: `frontend/app/components/battlemap/TokenSourcePicker.vue`. O mestre põe criaturas no combate pelo painel Combatentes na preparação; com o combate em andamento aparece o switch "Reforço no combate" no seletor.
2. **Spec 017, layout responsivo** (`specs/017-layout-responsivo/`):
   - Mesa em 3 zonas em `frontend/app/pages/index.vue` (`.table-people`, `.table-map`, `.table-dice`); grid em `frontend/app/assets/css/main.css` (bloco "Mesa: three zones"): 1 coluna < 1100 px (ordem: mapa, rolagem, combate, participantes, histórico), 2 colunas 1100–1599, 3 colunas ≥ 1600.
   - Colunas laterais fixas com rolagem própria; altura ajustada à janela por `frontend/app/composables/useFitToViewport.ts` (variável `--fit-height`). Rodapés fixos opacos: Rolar + último resultado (jogador) e "Rolar iniciativas"/"Começar combate" (mestre).
   - Celular (≤ 680 px): abas viram barra inferior fixa, resumo da ficha acoplado a ela, barra superior em 2 linhas; `scroll-padding` evita que foco caia sob barras fixas.
   - Mapa: `frontend/app/composables/useBattlemapViewport.ts` ajusta ao palco (célula 16–80 px) até o primeiro toque (`hold()`).
   - Ficha em 2 colunas a partir de 1440 px (`SheetCoreTab.vue` + `character.css`).
   - Estados: desabilitado tracejado; "Rolar" desabilitado mostra dica; último resultado do jogador vem do histórico se a página recarregar (`PlayerRollPanel.vue`).
   - Store da ficha (`frontend/app/stores/character.ts`): `remoteChangePending` + `reconcile()` (eco do SignalR não apaga "Salvo"; mudança remota durante gravação é recarregada).

## Como validar (obrigatório antes de PR)

Roteiro completo em `docs/testes-e2e.md`. Resumo (PowerShell):

```powershell
# API de validação (terminal 1). Testing é obrigatório: sem ele o limitador de entrada derruba os e2e.
$env:Database__Provider='Sqlite'; $env:ConnectionStrings__Companion="Data Source=$env:TEMP\companion-e2e.db;Foreign Keys=True"; $env:ASPNETCORE_URLS='http://127.0.0.1:5316'; $env:ASPNETCORE_ENVIRONMENT='Testing'; dotnet run --project backend/src/Companion.Api --no-launch-profile
```

```powershell
# Front estático com proxy de WebSocket (terminal 2; nuxt dev NÃO repassa o SignalR)
cd frontend; npx nuxt generate; node tests/e2e/serve-validation.mjs 3616 http://127.0.0.1:5316
```

```powershell
# Testes (terminal 3, em frontend/)
npx vitest run
npx nuxi typecheck
$env:E2E_BASE_URL='http://127.0.0.1:3616'; npx playwright test --grep-invert "layout audit"
$env:LAYOUT_AUDIT_DIR="$env:TEMP\layout-audit"; npx playwright test layout-audit sticky-columns --project=desktop
```

Estado no fim desta sessão: vitest 529/529, typecheck limpo, e2e 36/36 (1 a 2 testes oscilam quando a suíte roda inteira com 4 workers no SQLite; passam isolados), auditor de layout verde. Depois de cada mudança de CSS rode `npx nuxt generate` de novo: o servidor de validação serve `.output/public`.

O auditor (`frontend/tests/e2e/layout-audit.spec.ts`) é o gate: rolagem horizontal, controle coberto, fora da tela, texto cortado, aba escondida, nome da campanha < 100 px, alvo < 44 px no celular, contraste < 4.5:1, largura < 80% em 1920/2560. Capturas e `summary.txt` ficam em `LAYOUT_AUDIT_DIR`. Não afrouxe o auditor para passar; corrija o layout.

## O que falta (acabamento apontado pelo crítico cego, rodada 8)

Prioridade de cima para baixo. Nenhum é bloqueador; todos são P1–P6 da rubrica (hierarquia, contraste, proximidade, alinhamento, uso da tela larga, celular).

1. **Histórico do jogador no desktop** (1280/1920): hoje só aparece rolando a coluna da direita, enquanto a coluna esquerda do jogador tem ~400–500 px vazios abaixo da Iniciativa. Ideia: para o jogador (sem `.encounter-setup`), ≥ 1100 px, levar o Histórico para a coluna de pessoas. Cuidado: duas tentativas anteriores falharam. Dividir a coluna de dados entre rolagem e histórico achatou o painel de rolagem a 1280×800 (rodada 6); a coluna de dados precisa continuar rolando inteira, com "Rolar" e o último resultado fixos no pé. Mantenha `tests/e2e/sticky-columns.spec.ts` verde e acrescente nele a exigência do histórico visível.
2. **Faixas pretas ao lado do mapa** em telas largas (mapa 4:3 num palco mais largo): palco com `aspect-ratio` do mapa limitado pela altura disponível, ou fundo do palco na cor do cartão (`.battlemap-board` em `battlemap.css`).
3. **"Rolar" desabilitado mais apagado que "Salvar como rolagem"**: o primário desabilitado deve continuar o elemento mais forte da linha (`.button.primary:disabled` em `main.css`), sem parecer habilitado só pela cor.
4. **Rodapé fixo das colunas cortado reto**: cantos inferiores e borda do cartão quando o rodapé está colado no pé da coluna.
5. **"Desvantagem" encostado na borda** em colunas de ~340 px: `padding-inline` mínimo de 8 px; abaixo disso, só ícone com `aria-label`.
6. **Campanha** (`CampaignNotebook.vue`): três ações de criar ("Novo NPC", "Novo registro", "Criar o primeiro registro"); deixar um primário no cabeçalho; estado vazio do detalhe centralizado com `max-width` ~720 px.
7. **Coluna lateral estreita em 2560**: `clamp(360px, 20vw, 520px)`; combatentes em linha única.
8. **"Salv..." truncado** nos cartões de atributo no celular (`SheetCoreTab.vue`).
9. **Compêndio no celular**: "Minhas criaturas" e "Página 1 de 31" quebram em 2 linhas; criaturas sem imagem aparecem como círculo vazio (usar iniciais sempre).
10. **Seletor do compêndio no celular**: botão de adicionar só com ícone; pôr rótulo curto ("Pôr"); a lista com rolagem interna prende o polegar.
11. **Perícias** na Ficha em ordem por coluna (`grid-auto-flow: column`), não por linha.

Ao terminar: rode o auditor e os e2e, gere pares A/B com o script do crítico se quiser repetir a review cega (antes = capturas atuais), atualize `specs/017-layout-responsivo/tasks.md`, abra PR, mergeie e faça o deploy.

## Armadilhas já conhecidas

- `nuxt dev` com `NUXT_API_TARGET` não repassa WebSocket: a mesa fica "Sem conexão". Use `serve-validation.mjs`.
- Testes de `index.vue` (`tests/game-mode.spec.ts`) não têm auto-import do Nuxt: composables novos usados em `index.vue` precisam de `import` explícito.
- Mudar a altura de algo acima do catálogo de criaturas faz e2e clicarem na linha errada; os e2e localizam linhas por `Ver ficha de Goblin` exato.
- `scroll-padding` (barras fixas) e `hold()` do mapa existem por causa de regressões reais em e2e (`no-browser-context-menu.spec.ts`, `battlemap.spec.ts`); não remova.
- O `node_modules` das worktrees costuma ser junção para `C:\Github\Companion\frontend\node_modules`: desfaça a junção (`cmd /c rmdir`) antes de apagar uma pasta de worktree.
