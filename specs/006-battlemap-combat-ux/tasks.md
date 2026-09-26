---

description: "Task list for feature implementation"
---

# Tasks: Aprimoramentos de Combate no Battlemap

**Input**: Design documents from `specs/006-battlemap-combat-ux/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/battlemap-api.md, quickstart.md

**Tests**: Inclusas — a spec exige regras de autorização/validação testáveis (FR-002-style dono-ou-mestre, FR-009a de privacidade de HP, FR-011/FR-013 de intervalos válidos) e o projeto mantém suítes xUnit/Vitest/Playwright para as features equivalentes já implementadas (battlemap, áreas de ataque).

**Organization**: Tarefas agrupadas por user story (US1–US6, conforme spec.md) para permitir implementação e teste independentes.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Pode rodar em paralelo (arquivos diferentes, sem dependência)
- **[Story]**: US1–US6 conforme spec.md
- Caminhos de arquivo exatos em cada descrição

## Path Conventions

Web app existente: `backend/src/Companion.{Domain,Application,Infrastructure,Api}/`, `backend/tests/Companion.{UnitTests,IntegrationTests}/`, `frontend/app/`, `frontend/tests/`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Confirmar pontos de extensão antes de tocar qualquer entidade/serviço

- [X] T001 Revisar `backend/src/Companion.Application/Battlemaps/BattlemapService.cs`, `backend/src/Companion.Api/Endpoints/BattlemapEndpoints.cs`, `frontend/app/components/battlemap/BattlemapBoard.vue`, `frontend/app/components/battlemap/BattlemapToken.vue` e `frontend/app/components/battlemap/AttackAreaPanel.vue` (sem alterar nada ainda), anotando os pontos exatos de extensão descritos em `plan.md`/`data-model.md` (ex.: `ResolveTokenForMutation`, `ToSnapshot`, `tokenOrigin`, `stackOffset`).
- [X] T002 [P] Rodar a suíte atual como baseline: `dotnet test backend/tests/Companion.UnitTests` (60/60), `dotnet test backend/tests/Companion.IntegrationTests` (24/24) e `vitest run` no frontend (67/67), confirmando que tudo passa antes de qualquer mudança. Nota: exigiu corrigir `global.json` (rollForward, ver commit separado) e gerar `.nuxt/` local (`nuxt prepare`) neste worktree.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Nesta feature, nenhuma tarefa bloqueia *todas* as seis user stories ao mesmo tempo — cada uma introduz sua própria fatia de dados (turno é só leitura de dados já existentes; HP, tamanho, condições e marcadores usam colunas/tabelas independentes). As dependências reais entre stories específicas estão documentadas na seção "Dependencies & Execution Order" no final deste arquivo.

**⚠️ Nenhuma tarefa nesta fase — prossiga direto para as user stories.**

---

## Phase 3: User Story 1 - Ver de quem é o turno no battlemap (Priority: P1) 🎯 MVP

**Goal**: O token do combatente da vez exibe um destaque visual, calculado a partir de dados de turno já existentes (`Encounter.CurrentTurnIndex`), sem nenhuma mudança de backend.

**Independent Test**: Com combate ativo e iniciativa rolada, avançar o turno e confirmar que o destaque muda de token imediatamente, em todas as telas conectadas.

### Tests for User Story 1

- [X] T003 [P] [US1] Teste de componente em `frontend/tests/battlemap.spec.ts`: dado um `battlemap.tokens` e um `activeEncounter.combatants` com um `isCurrent: true`, a função/computed de destaque de turno identifica o token correto (para token de personagem via `participantId`, e para token de criatura via `combatant.id === token.participantId`, conforme `research.md`); retorna nenhum destaque quando `activeEncounter.status !== 'Active'` ou quando não há `activeEncounter`.

### Implementation for User Story 1

- [X] T004 [US1] Adicionar uma função/computed `currentTurnTokenId` (ou equivalente) em `frontend/app/components/battlemap/BattlemapBoard.vue`, cruzando `store.snapshot?.activeEncounter?.combatants` (campos `isCurrent`, `participantId`, `id`) com `battlemap.tokens[].participantId`, respeitando a semântica dupla desse campo (participante ou id de combatente/criatura) e retornando `null` fora do estado `Active`.
- [X] T005 [US1] Passar uma prop `is-current-turn` para `BattlemapTokenMarker` em `BattlemapBoard.vue` (a partir de T004); em `frontend/app/components/battlemap/BattlemapToken.vue`, renderizar um anel/destaque distinto (ex.: `<circle class="token-turn-ring">` com stroke/animação) quando a prop for verdadeira, sem interferir nas classes já existentes (`selected`, `pending`, `token-color-N`).
- [X] T006 [P] [US1] Estilizar o destaque de turno em `frontend/app/assets/css/battlemap.css` (ou `<style>` de `BattlemapToken.vue`), garantindo contraste visível tanto contra o token selecionado/pendente quanto em qualquer `colorKey`, e legibilidade em células pequenas (mobile/zoom reduzido).

**Checkpoint**: US1 funcional e testável de forma independente — nenhuma dependência de backend ou de outra story.

---

## Phase 4: User Story 2 - Ver a vida acima de cada token (Priority: P1)

**Goal**: Barra de vida semi-transparente acima de cada token com vida máxima definida, com HP exato visível a todos os participantes (FR-009a).

**Independent Test**: Definir vida máxima/atual na tela inicial (jogador) e ao inserir uma criatura (mestre); confirmar que a barra aparece com a proporção correta e é atualizada ao aplicar dano, em ambas as telas.

### Tests for User Story 2

- [X] T007 [P] [US2] Teste de domínio em `backend/tests/Companion.UnitTests/BattlemapTests.cs` (ou novo arquivo `CombatantTests.cs`): `Participant.UpdateCombatProfile`/`Combatant` (construtor e `Update`) aceitam `MaxHitPoints` entre 0 e 99999 e rejeitam valores fora do intervalo, espelhando a validação já existente de `CurrentHitPoints`.
- [X] T008 [P] [US2] Teste de integração — implementado em `backend/tests/Companion.IntegrationTests/EncounterApiTests.cs` (não em `BattlemapApiTests.cs`, já que HP não está mais em `BattlemapTokenDto`; ver T015): o snapshot do encontro (`EncounterDto.Combatants[]`, entregue via `CampaignSnapshot`/`GET encounters/{id}`) inclui `currentHitPoints`/`maxHitPoints` de todo combatente para **qualquer** participante autenticado, inclusive quando o observador não é o dono nem o mestre (prova de que a filtragem por observador foi removida — FR-009a). Note: `BattlemapTokenDto` propositalmente não carrega HP (ver data-model.md) — este teste cobre o snapshot do encontro, fonte real da vida.
- [X] T009 [P] [US2] Teste de componente em `frontend/tests/battlemap.spec.ts`: dado um `battlemap.tokens` e um `activeEncounter.combatants`/`participants` com HP definido, a função de resolução de vida (T015) retorna `currentHitPoints/maxHitPoints` corretos para o token (por combatente ligado, ou por participante como baseline); `BattlemapToken.vue` renderiza a barra proporcional a esse resultado, saturada entre 0% e 100%, e não renderiza nenhuma barra quando a resolução não encontra `maxHitPoints`.

### Implementation for User Story 2

- [X] T010 [P] [US2] Adicionar `MaxHitPoints` (int?, 0–99999) a `backend/src/Companion.Domain/Models/Participant.cs` (`UpdateCombatProfile`) e a `backend/src/Companion.Domain/Models/Combatant.cs` (construtor e `Update`), com a mesma validação de `CurrentHitPoints`.
- [X] T011 [US2] Atualizar `backend/src/Companion.Application/Encounters/EncounterService.cs`: `AddCombatantAsync`/`UpdateCombatantAsync` passam a aceitar e repassar `MaxHitPoints`; `RollInitiativeAsync` copia `participant.MaxHitPoints` para o novo `Combatant` (mesmo ponto onde `CurrentHitPoints` já é copiado, linha ~58-59).
- [X] T012 [US2] Atualizar `backend/src/Companion.Application/Campaigns/CampaignService.cs`: `UpdateCombatProfileAsync` aceita e persiste `MaxHitPoints` (tela inicial/configuração).
- [X] T013 [US2] Adicionar `MaxHitPoints` a `AddCombatantRequest`, `UpdateCombatantRequest`, `UpdateCombatProfileRequest`, `ParticipantDto` e `CombatantDto` em `backend/src/Companion.Application/Contracts/Dtos.cs`.
- [X] T014 [US2] Em `backend/src/Companion.Application/Contracts/DtoMapper.cs`, remover a condicional de HP por observador em `ToDto(Encounter, Guid? observerParticipantId, bool includeAllHitPoints)`: `CurrentHitPoints` (e o novo `MaxHitPoints`) passam a ser sempre incluídos no `CombatantDto`, para qualquer observador (FR-009a).
- [X] T015 [US2] **Não** tocar `BattlemapTokenDto`/`ToSnapshot` para HP (achado da análise pré-implementação: mutações de HP disparam `EncounterInvalidated`, não `BattlemapInvalidated` — duplicar HP no DTO do battlemap o deixaria dessincronizado). Em vez disso, adicionar uma função `healthFor(token)` em `frontend/app/components/battlemap/BattlemapBoard.vue`, que resolve `{ currentHitPoints, maxHitPoints } | null` para um token cruzando `store.snapshot.activeEncounter?.combatants` (por `participantId`/`id`, mesma semântica de T004) e, se não encontrado, `store.snapshot.participants` como baseline — nunca casando para tokens de marcador.
- [X] T016 [P] [US2] Adicionar `maxHitPoints` às interfaces `Combatant` e `Participant` em `frontend/app/types/campaign.ts` (sem adicionar HP a `BattlemapToken` — ver T015).
- [X] T017 [US2] Passar o resultado de `healthFor(token)` (T015) como props `current-hit-points`/`max-hit-points` para `BattlemapTokenMarker` em `BattlemapBoard.vue`; renderizar a barra de vida semi-transparente acima do token em `frontend/app/components/battlemap/BattlemapToken.vue` (grupo SVG com fundo e preenchimento, largura escalada pelo tamanho/footprint do token, opacidade ~0.7, preenchimento clampado 0–100%); ocultar completamente quando `maxHitPoints` for `null`/ausente (FR-009).
- [X] T018 [US2] Confirmado: `InitiativeTracker.vue` já exibia `combatant.currentHitPoints` incondicionalmente para não-donos (só o backend filtrava); a correção de T014 já basta. Além disso, foi adicionado `hpText()` (formato "atual/máxima") e um campo de vida máxima no editor do mestre e na tela inicial (`InitiativeModifier.vue`/`EncounterSetup.vue`), cobrindo FR-004/FR-005 de ponta a ponta.

**Checkpoint**: US2 funcional e testável de forma independente — sem dependência de US1 ou das demais.

---

## Phase 5: User Story 3 - Escolher o tamanho do token (Priority: P2)

**Goal**: Tamanho do token (1x1–5x5) configurável no menu de interação já existente, persistido numa nova entidade por-token.

**Independent Test**: Selecionar um token controlável, escolher "3x3" no menu, confirmar que ele passa a ocupar 3x3 células e que a mudança aparece para os demais participantes.

### Tests for User Story 3

- [X] T019 [P] [US3] Teste de domínio em `backend/tests/Companion.UnitTests/BattlemapTests.cs` (ou novo `BattlemapTokenAppearanceTests.cs`): `FootprintSize` aceita 1–5 e rejeita valores fora do intervalo.
- [X] T020 [P] [US3] Implementado em `BattlemapApiTests.cs` (autorização owner/master/other coberta na mesma classe, não em `BattlemapSecurityApiTests.cs`): `PUT tokens/{tokenId}/appearance` define `footprintSize` e ele é refletido no snapshot; jogador que não é dono nem mestre recebe `403`; validação de intervalo/condição inválida também coberta.
- [X] T021 [P] [US3] Teste de componente: raio proporcional em `frontend/tests/battlemap.spec.ts`; seleção de tamanho e preservação de condições em `frontend/tests/attack-area.spec.ts` (arquivo já existente do painel, mais coeso que criar um novo).

### Implementation for User Story 3

- [X] T022 [US3] Criar `backend/src/Companion.Domain/Models/BattlemapTokenAppearance.cs` (Id, BattlemapId, ParticipantId?, CombatantId?, MarkerId?, FootprintSize 1–5, StatusConditions, Version), incluindo desde já a coluna `MarkerId` e a lista `StatusConditions` na forma final (reaproveitada por US4/US5 sem nova migration), conforme `data-model.md`.
- [X] T023 [US3] Criar `backend/src/Companion.Domain/Models/StatusCondition.cs` (enum: `Stunned`, `Restrained`, `Blinded`, `Poisoned`, `Invisible`, `Prone`) — usado pela coluna criada em T022, mesmo que a UI de condições só seja ligada em US4.
- [X] T024 [US3] Adicionar mapeamento EF Core de `BattlemapTokenAppearance` em `backend/src/Companion.Infrastructure/Persistence/ModelConfigurations.cs` (índices únicos parciais por `ParticipantId`/`CombatantId`/`MarkerId`) e `DbSet<BattlemapTokenAppearance>` em `CompanionDbContext.cs`; gerar migration `AddBattlemapTokenAppearance` em `backend/src/Companion.Infrastructure/Persistence/Migrations/`.
- [X] T025 [US3] Adicionar `SetTokenAppearanceRequest` e os campos de aparência em `BattlemapTokenDto` em `Dtos.cs`, conforme `contracts/battlemap-api.md`.
- [X] T026 [US3] Implementar `SetTokenAppearanceAsync` em `backend/src/Companion.Application/Battlemaps/BattlemapService.cs` (reaproveitando a autorização de `ResolveTokenForMutation`), incluindo `FootprintSize` em `ToSnapshot`; adicionar rota `PUT tokens/{tokenId}/appearance` em `backend/src/Companion.Api/Endpoints/BattlemapEndpoints.cs`.
- [X] T027 [P] [US3] Adicionar `footprintSize`, `statusConditions: string[]` à interface `BattlemapToken` e criar `SetTokenAppearanceRequest` em `frontend/app/types/campaign.ts`.
- [X] T028 [US3] Criar `frontend/app/composables/useTokenAppearance.ts` (estado local reativo, `setAppearance(tokenId, { footprintSize, statusConditions })`, preview otimista, mesmo padrão de `useAttackAreas.ts`); adicionar uma seção de seleção de tamanho (botões 1x1–5x5) em `frontend/app/components/battlemap/AttackAreaPanel.vue`, ligada a esse composable a partir de `BattlemapBoard.vue`.
- [X] T029 [US3] Ajustar a renderização em `BattlemapToken.vue`/`BattlemapBoard.vue` para que o raio/área do token escale com `footprintSize` (N células), mantendo a âncora consistente ao mover o token (FR-012), reaproveitando `tokenOrigin`/`stackOffset` em `BattlemapBoard.vue`.

**Checkpoint**: US3 funcional e testável de forma independente — sem dependência de US1/US2.

---

## Phase 6: User Story 4 - Marcar condições de status (Priority: P2)

**Goal**: Condições de status (atordoado, imobilizado, cego, etc.) marcáveis no mesmo menu, exibidas como símbolos junto ao token.

**Independent Test**: Marcar "atordoado" no menu de um token controlável, confirmar que o símbolo aparece para todos; desmarcar e confirmar que some.

**Dependência**: Depende de US3 (T022–T024) já ter criado `BattlemapTokenAppearance` e o endpoint de aparência — não introduz uma nova migration, apenas liga a coluna/UI de condições já reservada.

### Tests for User Story 4

- [X] T030 [P] [US4] Já coberto por `BattlemapTokenAppearanceTests.cs` (T019, criado junto com a entidade): `Accepts_multiple_distinct_status_conditions`/`Clearing_status_conditions_empties_the_list`.
- [X] T031 [P] [US4] Já coberto por `BattlemapApiTests.cs` (T020/backend de T026, que já implementou os dois campos juntos): `Owner_and_master_can_set_token_appearance_but_other_player_cannot` persiste/retorna `statusConditions`; `Token_appearance_rejects_footprint_size_outside_one_to_five` cobre nome de condição desconhecido → 400.
- [X] T032 [P] [US4] Teste de componente em `frontend/tests/battlemap.spec.ts`: a lista de condições em `AttackAreaPanel.vue` alterna marcação/desmarcação e emite o payload correto; `BattlemapToken.vue` renderiza um ícone por condição ativa sem sobrepor a barra de vida (T017) nem o anel de turno (T005).

### Implementation for User Story 4

- [X] T033 [US4] Já implementado junto com T026 (o endpoint foi construído desde o início com os dois campos, `footprintSize` e `statusConditions`, para não exigir uma segunda migration).
- [X] T034 [US4] Adicionada seção de condições (múltipla escolha) em `AttackAreaPanel.vue`, reaproveitando `useTokenAppearance.ts` (T028) para enviar `statusConditions` junto com `footprintSize`.
- [X] T035 [US4] Renderizados badges de condição junto ao token em `BattlemapToken.vue` (abreviação de 3 letras em círculo, uma por `StatusCondition` ativo), posicionados abaixo do token para não cobrir a barra de vida nem o anel de turno.

**Checkpoint**: US4 funcional e testável de forma independente, assumindo US3 já entregue.

---

## Phase 7: User Story 5 - Tokens representativos do jogador (Priority: P2)

**Goal**: Jogador insere, reposiciona, configura (área/tamanho/condições) e remove tokens próprios adicionais (marcadores), distintos do pino principal.

**Independent Test**: Como jogador, inserir um token representativo, movê-lo, configurar uma área de alcance nele, e confirmar que outro participante o vê; confirmar que outro jogador não pode movê-lo/removê-lo.

**Dependência**: Depende de US3 (T022) para a tabela `BattlemapTokenAppearance` já existir (o `MarkerId` é adicionado a ela e a `AttackArea`/`BattlemapTokenPosition` nesta fase).

### Tests for User Story 5

- [ ] T036 [P] [US5] Teste de domínio em `backend/tests/Companion.UnitTests/BattlemapTests.cs` (ou novo `BattlemapMarkerTests.cs`): `BattlemapMarker.Label` aceita 1–40 caracteres e rejeita fora do intervalo.
- [ ] T037 [P] [US5] Teste de integração em `backend/tests/Companion.IntegrationTests/BattlemapApiTests.cs`: `POST markers` cria um token no snapshot (`isMarker: true`, sem HP); `DELETE markers/{markerId}` remove o marcador e sua posição/área/aparência associadas; em `BattlemapSecurityApiTests.cs`: mover/configurar/remover um marcador de outro dono (sem ser mestre) retorna `403`.
- [ ] T038 [P] [US5] Teste de componente em `frontend/tests/battlemap.spec.ts`: um token de marcador é renderizado como os demais (posição/tamanho/condições) mas nunca exibe barra de vida; o controle "Inserir token" cria um novo token na reserva pertencente ao participante atual.

### Implementation for User Story 5

- [ ] T039 [US5] Criar `backend/src/Companion.Domain/Models/BattlemapMarker.cs` (Id, BattlemapId, OwnerParticipantId, Label, ColorKey, CreatedAt), conforme `data-model.md`.
- [ ] T040 [US5] Adicionar mapeamento EF Core + `DbSet<BattlemapMarker>` em `ModelConfigurations.cs`/`CompanionDbContext.cs`; adicionar coluna `MarkerId` (Guid?) a `backend/src/Companion.Domain/Models/BattlemapTokenPosition.cs` e `backend/src/Companion.Domain/Models/AttackArea.cs` (domínio + mapeamento EF); gerar migration `AddBattlemapMarkers`.
- [ ] T041 [US5] Estender `ResolveTokenForMutation`/`MoveAsync`/`SetAttackAreaAsync`/`RemoveAttackAreaAsync`/`SetTokenAppearanceAsync` em `BattlemapService.cs` para resolver um `tokenId` também como marcador (autorização: `OwnerParticipantId` ou mestre) como terceiro caso ao lado de participante/combatente; incluir marcadores em `ToSnapshot` (`isMarker: true`, `ownerParticipantId`, sem campos de HP).
- [ ] T042 [US5] Implementar `CreateMarkerAsync`/`RemoveMarkerAsync` em `BattlemapService.cs` (remoção em cascata de posição/área/aparência associadas ao `MarkerId`, FR-019); adicionar rotas `POST markers`/`DELETE markers/{markerId}` em `BattlemapEndpoints.cs`.
- [ ] T043 [P] [US5] Adicionar `isMarker`/`ownerParticipantId` à interface `BattlemapToken` e criar os tipos de request/response de marcador em `frontend/app/types/campaign.ts`.
- [ ] T044 [US5] Criar `frontend/app/composables/useBattlemapMarkers.ts` (criar/mover/remover marcador, estado pendente/otimista, mesmo padrão de `useAttackAreas.ts`).
- [ ] T045 [US5] Adicionar um controle "Inserir token" em `frontend/app/components/battlemap/BattlemapBoard.vue` (próximo à reserva), chamando `useBattlemapMarkers`; garantir que `controllable(token)` em `BattlemapBoard.vue` trate o dono de um marcador (ou o mestre) como controlador, e qualquer outro participante como não-controlador.

**Checkpoint**: US5 funcional e testável de forma independente, assumindo US3 já entregue.

---

## Phase 8: User Story 6 - Clique/toque único em qualquer dispositivo (Priority: P3)

**Goal**: Garantir que o menu de interação expandido (área + tamanho + condições) continue abrindo com um único clique esquerdo (PC) ou toque único (mobile), sem gestos novos.

**Independent Test**: Em desktop, um único clique esquerdo sobre um token controlável abre o menu; em viewport mobile, um único toque faz o mesmo, com todos os controles usáveis por toque.

**Dependência**: Validação de regressão sobre US3/US4 (menu expandido) — deve rodar depois delas, mas não depende de US1/US2/US5.

### Tests for User Story 6

- [ ] T046 [P] [US6] Teste de componente em `frontend/tests/battlemap.spec.ts` (ou novo `battlemap-token-menu-gesture.spec.ts`): um único `pointerdown`+`pointerup` (sem deslocamento) sobre um token controlável abre o painel de interação já com as seções de tamanho/condições (US3/US4), sem exigir um segundo clique, clique direito ou pressão longa.
- [ ] T047 [P] [US6] Teste Playwright estendendo `frontend/tests/e2e/battlemap.spec.ts`: em viewport mobile, um único toque abre o painel com os controles de tamanho/condições operáveis por toque, sem necessidade de zoom manual (SC-007).

### Implementation for User Story 6

- [ ] T048 [US6] Auditar `startTokenDrag`/`selectToken`/`pointerUp` em `BattlemapBoard.vue` após as mudanças de US3–US5: confirmar que nenhum gesto novo (clique direito, duplo clique, pressão longa) foi introduzido para abrir o menu expandido; corrigir se alguma regressão for encontrada.
- [ ] T049 [US6] Verificar/ajustar o tamanho dos alvos de toque na seção de tamanho e na lista de condições de `AttackAreaPanel.vue` para viewports mobile (SC-007: sem zoom manual nem rolagem lateral).

**Checkpoint**: As seis user stories funcionam de forma independente; gesto único confirmado intacto sobre o menu expandido.

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Ajustes finais que atravessam todas as stories

- [ ] T050 [P] Rodar `dotnet test backend/tests/Companion.UnitTests` e `dotnet test backend/tests/Companion.IntegrationTests`, corrigindo eventuais falhas relacionadas às mudanças desta feature.
- [ ] T051 [P] Rodar `pnpm --dir frontend test` (Vitest), corrigindo eventuais falhas relacionadas às novas specs.
- [ ] T052 Rodar `pnpm --dir frontend test:e2e -- battlemap` (Playwright), corrigindo eventuais falhas.
- [ ] T053 Executar os cenários 1–5 de `quickstart.md` manualmente com duas sessões (mestre + jogador), confirmando todos os resultados "Esperado".
- [ ] T054 Validar responsividade mobile de ponta a ponta do `AttackAreaPanel.vue` expandido (formato/raio existente + tamanho + condições novas), confirmando SC-003 (já existente) e SC-007 (nova) juntas.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Sem dependências.
- **Foundational (Phase 2)**: Vazia — nenhuma tarefa bloqueia todas as stories (ver nota na própria fase).
- **US1 (Phase 3)**: Depende apenas do Setup; sem dependência de backend ou de outra story.
- **US2 (Phase 4)**: Depende apenas do Setup; independente de US1 e das demais.
- **US3 (Phase 5)**: Depende apenas do Setup; independente de US1/US2.
- **US4 (Phase 6)**: Depende de US3 (T022–T024, tabela `BattlemapTokenAppearance` e endpoint de aparência já existentes).
- **US5 (Phase 7)**: Depende de US3 (T022, tabela `BattlemapTokenAppearance` já existente, para receber a coluna `MarkerId`).
- **US6 (Phase 8)**: Validação de regressão sobre o menu expandido — deve rodar depois de US3 e US4, mas não depende de US1/US2/US5.
- **Polish (Phase 9)**: Depende de todas as stories desejadas estarem completas.

### Parallel Opportunities

- T001 e T002 podem rodar em paralelo (arquivos/ações diferentes).
- US1 (Phase 3) e US2 (Phase 4) podem ser feitas inteiramente em paralelo por desenvolvedores diferentes — não compartilham arquivo nem entidade.
- US3 (Phase 5) pode começar em paralelo com US1/US2; US4 e US5 só podem começar depois que T022–T024 (dentro de US3) estiverem prontos, mas podem então rodar em paralelo entre si (arquivos diferentes: US4 mexe em condições, US5 em marcadores/posição/área).
- Dentro de cada story, tarefas marcadas [P] (testes e arquivos distintos) podem rodar em paralelo.

---

## Parallel Example: User Story 2

```bash
# Testes em paralelo:
Task: "Teste de domínio MaxHitPoints em Participant/Combatant (T007)"
Task: "Teste de integração HP sempre visível no snapshot (T008)"
Task: "Teste de componente da barra de vida em BattlemapToken.vue (T009)"
```

---

## Implementation Strategy

### MVP First (User Story 1 e 2)

1. Phase 1 (Setup) → Phase 3 (US1) e Phase 4 (US2) em paralelo → **validar independentemente cada uma** → já é um MVP demonstrável (destaque de turno + barra de vida no battlemap), cobrindo as duas prioridades P1 da spec.
2. Deploy/demo se pronto.

### Incremental Delivery

1. Setup → base pronta (sem Foundational bloqueante nesta feature).
2. US1 → demo (destaque de turno).
3. US2 → demo (barra de vida, HP visível a todos).
4. US3 → demo (tamanho de token).
5. US4 → demo (condições de status) — depende de US3.
6. US5 → demo (tokens representativos) — depende de US3.
7. US6 → validação de regressão do gesto único, após US3/US4 existirem.

### Parallel Team Strategy

Com múltiplos desenvolvedores:

1. Time completa o Setup junto.
2. A partir daí:
   - Desenvolvedor A: US1 (frontend puro, sem backend)
   - Desenvolvedor B: US2 (backend + frontend, HP)
   - Desenvolvedor C: US3 (backend + frontend, tamanho) — depois libera US4/US5 para outros dois desenvolvedores
3. US6 fica para o final, como auditoria de regressão sobre o trabalho de US3/US4.

---

## Notes

- [P] tasks = arquivos diferentes, sem dependência.
- [Story] label mapeia a tarefa para a user story correspondente, para rastreabilidade.
- Cada user story deve ser completável e testável de forma independente (respeitando as dependências reais documentadas acima).
- Verificar que os testes falham antes de implementar.
- Commitar após cada tarefa ou grupo lógico de tarefas.
- Parar em qualquer checkpoint para validar a story isoladamente.
- Evitar: tarefas vagas, conflito no mesmo arquivo entre tarefas [P], dependências entre stories que quebrem a independência sem necessidade real.
