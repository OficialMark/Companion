---

description: "Task list for feature implementation"
---

# Tasks: Exibição de Área de Ataque no Battlemap

**Input**: Design documents from `specs/005-attack-area-display/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/attack-area-api.md, quickstart.md

**Tests**: Inclusas — a spec exige regras de autorização/validação testáveis (FR-002, SC-004) e o projeto já mantém suítes xUnit/Vitest/Playwright para funcionalidades equivalentes (battlemap, iniciativa).

**Organization**: Tarefas agrupadas por user story (US1–US5, conforme spec.md) para permitir implementação e teste independentes.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Pode rodar em paralelo (arquivos diferentes, sem dependência)
- **[Story]**: US1–US5 conforme spec.md
- Caminhos de arquivo exatos em cada descrição

## Path Conventions

Web app existente: `backend/src/Companion.{Domain,Application,Infrastructure,Api}/`, `backend/tests/Companion.UnitTests/`, `frontend/app/`, `frontend/tests/`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Preparar terreno comum antes de qualquer entidade/serviço novo

- [X] T001 Confirmar convenção de rota/DI: revisar `backend/src/Companion.Api/Endpoints/BattlemapEndpoints.cs` e `backend/src/Companion.Application/Battlemaps/BattlemapService.cs` para reaproveitar o mesmo `MapGroup`, padrão de autorização por `X-Participant-Token` e injeção de `BattlemapService` (sem criar novo serviço/arquivo ainda — apenas anotar pontos de extensão).
- [X] T002 [P] Criar arquivo de enum `AttackAreaShape` em `backend/src/Companion.Domain/Models/AttackAreaShape.cs` (`Circle`, `Square`, `Cone`, `Cylinder`).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Entidade de domínio, persistência e infraestrutura de sincronização que TODAS as user stories (exceto US5, que é isolada) dependem.

**⚠️ CRITICAL**: Nenhuma user story de área de ataque (US1–US4) pode começar antes desta fase.

- [X] T003 Criar entidade `AttackArea` em `backend/src/Companion.Domain/Models/AttackArea.cs` com campos e invariantes descritos em `data-model.md` (validação de `Radius` 1–100, `Width`/`DirectionDegrees` obrigatórios apenas para `Cone`/`Cylinder`, nulos para `Circle`/`Square`; construtor + métodos `Update(...)`).
- [X] T004 Adicionar mapeamento EF Core de `AttackArea` em `backend/src/Companion.Infrastructure/Persistence/ModelConfigurations.cs` (chave, FK para `Battlemap`, índice único por `CombatantId` para garantir no máximo uma área ativa por token) e registrar `DbSet<AttackArea>` em `backend/src/Companion.Infrastructure/Persistence/CompanionDbContext.cs`.
- [X] T005 Gerar migration EF Core `AddAttackAreas` em `backend/src/Companion.Infrastructure/Persistence/Migrations/` (via `dotnet ef migrations add AddAttackAreas`, seguindo o padrão das migrations `AddBattlemap*` existentes).
- [X] T006 [P] Adicionar `AttackAreaDto` em `backend/src/Companion.Application/Contracts/Dtos.cs` e `SetAttackAreaRequest` em `backend/src/Companion.Application/Contracts/Abstractions.cs` (ou arquivo correspondente de requests), conforme `contracts/attack-area-api.md`.
- [X] T007 Implementar `SetAttackAreaAsync`/`RemoveAttackAreaAsync`/inclusão de `attackAreas` no snapshot em `backend/src/Companion.Application/Battlemaps/BattlemapService.cs`, reaproveitando a checagem de autorização já usada em `MoveAsync` (dono do token via `ParticipantId`, ou mestre) e persistindo via repositório/DbContext.
- [X] T008 Adicionar broadcast dos eventos `AttackAreaChanged`/`AttackAreaRemoved` no notifier realtime (`backend/src/Companion.Api/Realtime/SignalRCampaignNotifier.cs` e sua interface em `Companion.Application`), disparado somente após a persistência ser confirmada em T007.
- [X] T009 [P] Criar composable `frontend/app/composables/useAttackAreas.ts`: estado local reativo das áreas do battlemap atual, funções `setAttackArea(tokenId, config)`/`removeAttackArea(tokenId)` (chamando os novos endpoints) e assinatura dos eventos `AttackAreaChanged`/`AttackAreaRemoved` via `useCampaignRealtime`/`store.applyEvent`.

**Checkpoint**: Backend persiste e sincroniza `AttackArea`; frontend tem estado reativo pronto para ser consumido pela UI das próximas fases.

---

## Phase 3: User Story 1 - Configurar área de ataque no próprio personagem (Priority: P1) 🎯 MVP

**Goal**: Jogador clica no próprio token, escolhe formato, ajusta sliders e vê a área desenhada no mapa.

**Independent Test**: Em uma sessão de combate, selecionar o próprio token, escolher "círculo", ajustar raio para 2 e confirmar que o círculo é desenhado corretamente ao redor do token.

### Tests for User Story 1

- [X] T010 [P] [US1] Teste de domínio em `backend/tests/Companion.UnitTests/AttackAreaTests.cs`: validação de `Radius`/`Width`/`DirectionDegrees` (aceita valores válidos, rejeita fora do intervalo, rejeita `Width`/`DirectionDegrees` para `Circle`/`Square`, exige ambos para `Cone`/`Cylinder`).
- [X] T011 [P] [US1] Teste de componente em `frontend/tests/attack-area-panel.spec.ts`: seleção de formato "círculo", ajuste do slider de raio, emissão do payload correto para `useAttackAreas.setAttackArea`.

### Implementation for User Story 1

- [X] T012 [US1] Criar componente `frontend/app/components/battlemap/AttackAreaPanel.vue`: seletor compacto dos 4 formatos (ícones), sliders de raio/distância (`Circle`/`Square`) e de distância+largura+direção (`Cone`/`Cylinder`), emitindo evento de mudança em tempo real e evento de "concluir".
- [X] T013 [US1] Integrar `AttackAreaPanel.vue` ao `frontend/app/components/battlemap/BattlemapBoard.vue`: ao clicar em um token controlável (reaproveitando `controllable(token)` já existente), abrir o painel posicionado próximo ao token; ao fechar/concluir, chamar `useAttackAreas.setAttackArea`.
- [X] T014 [US1] Renderizar o desenho da área (SVG) em `BattlemapBoard.vue` a partir do estado de `useAttackAreas`, convertendo blocos→pixels com `useBattlemapViewport.ts` (`cellSize`, `pan`), para os 4 formatos (retângulo girado para cone/cilindro, leque/setor para cone se aplicável, círculo, quadrado).
- [X] T015 [US1] Fechar o painel automaticamente ao concluir a configuração (clique fora do popover), mantendo apenas o desenho da área visível, em `AttackAreaPanel.vue`/`BattlemapBoard.vue`.

**Checkpoint**: User Story 1 funcional e testável de forma independente (MVP).

---

## Phase 4: User Story 2 - Reconfigurar uma área já desenhada (Priority: P2)

**Goal**: Clicar em uma área desenhada reabre o painel com os valores atuais, permitindo ajustar ou remover.

**Independent Test**: Com uma área já criada (US1), clicar sobre o desenho e confirmar que os valores atuais aparecem pré-carregados; alterar e remover funcionam.

### Tests for User Story 2

- [X] T016 [P] [US2] Teste de componente em `frontend/tests/attack-area-panel.spec.ts` (mesma suíte de T011, casos adicionais): reabertura do painel com valores pré-carregados a partir de uma área existente, e emissão do evento de remoção.

### Implementation for User Story 2

- [X] T017 [US2] Adicionar handler de clique sobre o desenho da área (não apenas sobre o token) em `BattlemapBoard.vue`, reabrindo `AttackAreaPanel.vue` com os valores atuais da `AttackArea` correspondente.
- [X] T018 [US2] Adicionar botão/ação "remover" em `AttackAreaPanel.vue`, chamando `useAttackAreas.removeAttackArea(tokenId)`.
- [X] T019 [P] [US2] Teste de backend em `backend/tests/Companion.UnitTests/AttackAreaTests.cs`: `RemoveAttackAreaAsync` remove a área existente e é idempotente quando já não existe.

**Checkpoint**: User Stories 1 e 2 funcionam juntas e de forma independente.

---

## Phase 5: User Story 3 - Mestre configura áreas para qualquer personagem/criatura (Priority: P2)

**Goal**: O mestre pode abrir o painel de configuração em qualquer token (seu, de jogadores, ou criaturas), enquanto jogadores continuam restritos aos próprios tokens.

**Independent Test**: Logado como mestre, clicar em um token de jogador e em uma criatura do mestre — painel abre em ambos; logado como jogador, clicar em token de terceiro — painel não abre.

### Tests for User Story 3

- [X] T020 [P] [US3] Teste de backend em `backend/tests/Companion.UnitTests/AttackAreaTests.cs`: `SetAttackAreaAsync` permite mestre configurar qualquer token e rejeita (`403`/exceção de autorização) jogador configurando token de terceiro.
- [X] T021 [P] [US3] Teste de componente em `frontend/tests/battlemap-setup.spec.ts` ou novo `frontend/tests/attack-area-authorization.spec.ts`: `controllable(token)` (já existente) segue controlando corretamente se o clique abre `AttackAreaPanel` para mestre vs. jogador.

### Implementation for User Story 3

- [X] T022 [US3] Confirmar/ajustar em `backend/src/Companion.Application/Battlemaps/BattlemapService.cs` que `SetAttackAreaAsync`/`RemoveAttackAreaAsync` usam exatamente a mesma regra de autorização de `MoveAsync` (mestre = qualquer token; jogador = apenas `participantId` próprio), cobrindo FR-002/SC-004.
- [X] T023 [US3] Confirmar em `BattlemapBoard.vue` que a abertura do `AttackAreaPanel` (T013) reutiliza `controllable(token)` sem exceções para tokens de criaturas do mestre.

**Checkpoint**: US1, US2 e US3 funcionam juntas; autorização coberta em ambas as camadas (frontend UX + backend autoridade).

---

## Phase 6: User Story 4 - Áreas visíveis para todos os participantes em tempo real (Priority: P1)

**Goal**: Criação/ajuste/remoção de áreas por um participante aparece automaticamente para os demais.

**Independent Test**: Duas sessões de navegador na mesma campanha; criar área em uma, verificar que aparece na outra em poucos segundos sem recarregar.

### Tests for User Story 4

- [ ] T024 [P] [US4] Teste de integração/E2E em `frontend/tests/attack-area-sync.spec.ts` (Playwright): duas sessões simuladas, criação/remoção de área em uma refletida na outra.

### Implementation for User Story 4

- [X] T025 [US4] Confirmar que `GET /battlemap` (snapshot) retorna `attackAreas` atualizado (extensão de T007) para reidratação ao entrar/reconectar, em `backend/src/Companion.Application/Battlemaps/BattlemapService.cs`.
- [X] T026 [US4] Confirmar que `useAttackAreas.ts` (T009) processa `AttackAreaChanged`/`AttackAreaRemoved` recebidos por `useCampaignRealtime.ts` e atualiza o estado reativo consumido por `BattlemapBoard.vue` sem necessidade de refresh manual.
- [X] T027 [US4] Ao reconectar (evento de reconexão do SignalR já tratado por `useCampaignRealtime.ts`), disparar reidratação completa das áreas via GET em `useAttackAreas.ts`, descartando estado local otimista pendente.

**Checkpoint**: Sincronização em tempo real validada; US1–US4 entregam o MVP completo da funcionalidade de área de ataque.

---

## Phase 7: User Story 5 - Corrigir exibição indevida do editor de vida/iniciativa em combate (Priority: P3)

**Goal**: Ocultar os campos editáveis de HP/iniciativa durante o modo de combate, mantendo-os apenas no modo de configuração.

**Independent Test**: Entrar no modo de combate e confirmar que os campos de HP/iniciativa não aparecem editáveis; voltar ao setup e confirmar que reaparecem.

**Independência**: Esta user story não depende de nenhuma das anteriores (US1–US4) nem da Fase 2 — pode ser implementada e entregue isoladamente a qualquer momento.

### Tests for User Story 5

- [X] T028 [P] [US5] Teste de componente em `frontend/tests/initiative-tracker-combat-mode.spec.ts`: com `gameMode.active = true`, os inputs editáveis de nome/modificador/HP/iniciativa não são renderizados (apenas texto somente-leitura); com `gameMode.active = false`, continuam renderizados como hoje.

### Implementation for User Story 5

- [X] T029 [US5] Em `frontend/app/components/InitiativeTracker.vue` (linhas ~17-18), envolver o `combatant-editor` (mestre) e o input de HP do próprio jogador com uma condição adicional `!gameMode.active`, substituindo por uma exibição somente-leitura (nome, modificador, HP, iniciativa) quando `gameMode.active` for verdadeiro.
- [X] T030 [US5] Revisar `frontend/app/pages/index.vue` (linhas ~140-141) para confirmar que `InitiativeTracker`/`CombatAttack` continuam renderizados em ambos os modos, mas que o estado somente-leitura de T029 é o único efeito visível da alternância de modo (sem esconder o componente inteiro, já que ele deve mostrar HP/iniciativa em modo leitura durante o combate).

**Checkpoint**: Bug corrigido e coberto por teste, entregável de forma independente das demais stories.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Ajustes finais que atravessam todas as stories

- [X] T031 [P] Rodar `dotnet test backend/tests/Companion.UnitTests` e corrigir eventuais falhas relacionadas a `AttackArea`.
- [X] T032 [P] Rodar `pnpm test` (Vitest) em `frontend/` e corrigir eventuais falhas relacionadas às novas specs.
- [ ] T033 Validar responsividade mobile do `AttackAreaPanel.vue` (SC-003: painel ≤ 1/3 da altura da tela) manualmente ou via teste de snapshot em viewport mobile.
- [ ] T034 Executar os cenários de `quickstart.md` manualmente (ou via Playwright) para validar US1–US5 de ponta a ponta.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Sem dependências.
- **Foundational (Phase 2)**: Depende do Setup — BLOQUEIA US1, US2, US3, US4 (não bloqueia US5, que é independente).
- **US1 (Phase 3)**: Depende de Phase 2.
- **US2 (Phase 4)**: Depende de Phase 2 e reutiliza componentes de US1 (T012–T014), mas é testável de forma independente assim que US1 existir.
- **US3 (Phase 5)**: Depende de Phase 2; reutiliza T013, mas a regra de autorização (T022) é independente e pode ser testada isoladamente no backend.
- **US4 (Phase 6)**: Depende de Phase 2 (T007–T009); reutiliza o desenho de US1 para validação visual, mas a sincronização em si é independente.
- **US5 (Phase 7)**: Sem dependência de nenhuma outra fase — pode ser feita a qualquer momento, inclusive em paralelo com as demais.
- **Polish (Phase 8)**: Depende de todas as stories desejadas estarem completas.

### Parallel Opportunities

- T002 pode rodar em paralelo com T001.
- T006 e T009 podem rodar em paralelo com T003–T005 (arquivos diferentes), embora T007/T008 dependam de T003–T006.
- US5 (Phase 7, T028–T030) pode ser feita totalmente em paralelo com Phases 2–6 por outro desenvolvedor, pois não compartilha arquivos.
- Dentro de cada story, tarefas marcadas [P] (testes e arquivos distintos) podem rodar em paralelo.

---

## Parallel Example: User Story 1

```bash
# Testes em paralelo:
Task: "Teste de domínio AttackAreaTests.cs (validação de forma/raio/largura/direção)"
Task: "Teste de componente attack-area-panel.spec.ts (seleção de formato e sliders)"
```

---

## Implementation Strategy

### MVP First

1. Phase 1 (Setup) → Phase 2 (Foundational) → Phase 3 (US1) → **validar independentemente** → já é um MVP demonstrável (jogador vê sua própria área de ataque).
2. Phase 6 (US4, sincronização) deve entrar logo em seguida para o MVP fazer sentido em grupo (conforme priorização P1 no spec).

### Incremental Delivery

1. Setup + Foundational → base pronta.
2. US1 → demo (jogador configura área própria).
3. US4 → demo (sincronização entre participantes) — combinar com US1 para o primeiro incremento "completo" em grupo.
4. US2 → demo (reconfiguração/remoção).
5. US3 → demo (mestre configura qualquer token).
6. US5 (bug fix) → pode ser entregue a qualquer momento em paralelo, inclusive antes de tudo o mais, por não ter dependências.

---

## Notes

- [P] = arquivos diferentes, sem dependência.
- Rótulo [Story] mapeia a tarefa à user story correspondente da spec.md.
- US5 é a correção de bug, independente das demais — pode ser priorizada isoladamente se o usuário quiser o fix mais rápido.
- Commitar após cada tarefa ou grupo lógico de tarefas.
- Parar em cada checkpoint para validar a story isoladamente antes de seguir.
