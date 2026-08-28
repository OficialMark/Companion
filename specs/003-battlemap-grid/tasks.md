---

description: "Tarefas de implementação do battlemap com grid interativo"
---

# Tasks: Battlemap com Grid Interativo

**Input**: Design documents from `/specs/003-battlemap-grid/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/openapi.yaml, quickstart.md

**Tests**: Obrigatórios pela constituição do projeto. Em cada história, escrever os testes indicados e confirmar a falha esperada antes da implementação.

**Organization**: Tarefas agrupadas por história para permitir entrega e validação incremental.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Pode ser executada em paralelo por alterar arquivos distintos e não depender de tarefa incompleta.
- **[Story]**: Mapeia a tarefa para US1, US2 ou US3.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Preparar limites de upload, contratos compartilhados e fixtures reutilizáveis.

- [ ] T001 Configurar limite de upload de 20 MB e opções de battlemap em `backend/src/Companion.Api/appsettings.json` e `backend/src/Companion.Api/Program.cs`
- [ ] T002 [P] Adicionar tipos compartilhados `BattlemapSnapshot`, `BattlemapToken` e comandos em `frontend/app/types/campaign.ts`
- [ ] T003 [P] Criar fixtures PNG/JPEG/WebP válidas e arquivos inválidos para testes em `backend/tests/Companion.IntegrationTests/Fixtures/Battlemaps/` e `frontend/tests/fixtures/battlemaps/`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Criar modelo, persistência, projeções e invalidação compartilhados por todas as histórias.

**⚠️ CRITICAL**: Nenhuma história começa antes desta fase.

- [ ] T004 [P] Escrever testes unitários inicialmente falhos para criação, substituição, limites do grid e coordenadas em `backend/tests/Companion.UnitTests/BattlemapTests.cs`
- [ ] T005 Implementar entidades e invariantes `Battlemap` e `BattlemapTokenPosition` em `backend/src/Companion.Domain/Models/Battlemap.cs` e `backend/src/Companion.Domain/Models/BattlemapTokenPosition.cs`
- [ ] T006 Integrar DbSets, mapeamentos, índices, cascades e row versions em `backend/src/Companion.Application/Contracts/Abstractions.cs`, `backend/src/Companion.Infrastructure/Persistence/CompanionDbContext.cs` e `backend/src/Companion.Infrastructure/Persistence/ModelConfigurations.cs`
- [ ] T007 Gerar e revisar migration EF Core de battlemap/imagem/posições em `backend/src/Companion.Infrastructure/Persistence/Migrations/*_AddBattlemap.cs` e `backend/src/Companion.Infrastructure/Persistence/Migrations/CompanionDbContextModelSnapshot.cs`
- [ ] T008 [P] Definir requests, snapshots, resultado de substituição e projeções de pino em `backend/src/Companion.Application/Contracts/Dtos.cs` e `backend/src/Companion.Application/Contracts/DtoMapper.cs`
- [ ] T009 [P] Acrescentar contrato e implementação de `BattlemapInvalidatedAsync` em `backend/src/Companion.Application/Contracts/Abstractions.cs` e `backend/src/Companion.Api/Realtime/SignalRCampaignNotifier.cs`
- [ ] T010 Preparar estado de battlemap, revisão, carregamento e limpeza de sessão em `frontend/app/stores/campaign.ts`

**Checkpoint**: Modelo persistente, projeções e canal de invalidação estão disponíveis; testes unitários passam.

---

## Phase 3: User Story 1 - Configurar o battlemap (Priority: P1) 🎯 MVP

**Goal**: Permitir que o mestre visualize a prévia, publique/substitua imagem e grid e que jogadores autorizados recuperem o mapa sem poder editá-lo.

**Independent Test**: Mestre publica PNG com grid 20×15, altera para 10×10 e recarrega; jogador vê o mapa e recebe 403 ao configurar; arquivo/dimensão inválidos são rejeitados.

### Tests for User Story 1

- [ ] T011 [P] [US1] Escrever testes de integração inicialmente falhos para GET/PUT multipart, imagem com ETag, autorização, validação, revisão e retorno à reserva em `backend/tests/Companion.IntegrationTests/BattlemapApiTests.cs`
- [ ] T012 [P] [US1] Escrever testes Vitest inicialmente falhos da prévia, dimensões, recorte e permissões em `frontend/tests/battlemap-setup.spec.ts`
- [ ] T013 [P] [US1] Escrever jornada Playwright inicialmente falha de publicação/substituição por mestre e visualização por jogador nos projetos desktop/Android em `frontend/tests/e2e/battlemap.spec.ts`

### Implementation for User Story 1

- [ ] T014 [US1] Implementar publicação, substituição transacional, snapshot e leitura autorizada do binário em `backend/src/Companion.Application/Battlemaps/BattlemapService.cs`
- [ ] T015 [US1] Implementar endpoints GET/PUT multipart e GET da imagem conforme `contracts/openapi.yaml` em `backend/src/Companion.Api/Endpoints/BattlemapEndpoints.cs`
- [ ] T016 [US1] Registrar `BattlemapService`, mapear endpoints, aplicar limite de request e emitir invalidação após persistência em `backend/src/Companion.Api/Program.cs` e `backend/src/Companion.Api/Endpoints/BattlemapEndpoints.cs`
- [ ] T017 [P] [US1] Implementar cliente de snapshot, upload multipart e carregamento versionado da imagem em `frontend/app/composables/useCompanionApi.ts`
- [ ] T018 [P] [US1] Criar formulário do mestre com validação e prévia de recorte/grid em `frontend/app/components/battlemap/BattlemapSetup.vue`
- [ ] T019 [P] [US1] Criar renderização SVG somente leitura de imagem e linhas O(rows+columns) em `frontend/app/components/battlemap/BattlemapBoard.vue`
- [ ] T020 [US1] Integrar carregamento, estado vazio, configuração do mestre e visualização do jogador em `frontend/app/app.vue` e `frontend/app/stores/campaign.ts`
- [ ] T021 [US1] Estilizar formulário, visualizador base e estados de erro/carregamento responsivos em `frontend/app/assets/css/main.css`

**Checkpoint**: US1 funciona isoladamente e entrega um battlemap publicado/recuperável, ainda sem movimentação.

---

## Phase 4: User Story 2 - Posicionar e mover o próprio pino (Priority: P2)

**Goal**: Projetar um pino por jogador e confirmar movimentos válidos por arraste ou seleção, preservando permissões e convergência.

**Independent Test**: Com battlemap semeado e dois jogadores, cada um posiciona o próprio pino; tentativa sobre pino alheio falha; mestre move qualquer pino; dois pinos coexistem na mesma célula.

### Tests for User Story 2

- [ ] T022 [P] [US2] Estender testes unitários inicialmente falhos para reserva, movimento, versão concorrente e redução de grid em `backend/tests/Companion.UnitTests/BattlemapTests.cs`
- [ ] T023 [P] [US2] Estender testes de integração inicialmente falhos para projeção tardia, dono/mestre, coordenadas inválidas, concorrência e persistência antes do evento em `backend/tests/Companion.IntegrationTests/BattlemapApiTests.cs`
- [ ] T024 [P] [US2] Escrever testes Vitest inicialmente falhos para arraste, seleção toque/teclado, rollback, pilha de pinos e bloqueio de terceiros em `frontend/tests/battlemap.spec.ts`
- [ ] T025 [P] [US2] Estender Playwright com dois jogadores, arbitragem do mestre e célula compartilhada em desktop/Android em `frontend/tests/e2e/battlemap.spec.ts`

### Implementation for User Story 2

- [ ] T026 [US2] Implementar projeção de todos os jogadores, criação/atualização de posição e concorrência otimista em `backend/src/Companion.Application/Battlemaps/BattlemapService.cs`
- [ ] T027 [US2] Implementar PUT de posição com autorização de dono/mestre e resposta de snapshot em `backend/src/Companion.Api/Endpoints/BattlemapEndpoints.cs`
- [ ] T028 [P] [US2] Implementar pino focável, identificação, estado pendente e agrupamento em célula compartilhada em `frontend/app/components/battlemap/BattlemapToken.vue`
- [ ] T029 [US2] Implementar hit testing, snap, arraste Pointer Events e fluxo selecionar-pino/selecionar-célula em `frontend/app/components/battlemap/BattlemapBoard.vue`
- [ ] T030 [US2] Implementar comando otimista seguro, rollback, revisão de posição e mensagens de conflito em `frontend/app/stores/campaign.ts` e `frontend/app/composables/useCompanionApi.ts`
- [ ] T031 [US2] Consumir `battlemap.invalidated`, refazer snapshot e preservar a imagem quando `imageRevision` não mudar em `frontend/app/composables/useCampaignRealtime.ts` e `frontend/app/stores/campaign.ts`
- [ ] T032 [US2] Estilizar reserva, pinos, seleção, célula realçada, pilha e estados pendente/proibido em `frontend/app/assets/css/main.css`

**Checkpoint**: US2 funciona contra battlemap semeado e, integrada à US1, permite posicionamento compartilhado completo.

---

## Phase 5: User Story 3 - Navegar pelo mapa em desktop e celular (Priority: P3)

**Goal**: Manter visualizador estável e permitir alcançar qualquer célula com escala-alvo de 40 px, pan/zoom, teclado e recuperação de conexão.

**Independent Test**: Alternar 1×1, 20×15 e 100×100 em desktop/Android sem mudar a área reservada nem gerar rolagem horizontal; navegar aos quatro cantos, mover por teclado e recuperar snapshot após desconexão.

### Tests for User Story 3

- [ ] T033 [P] [US3] Escrever testes Vitest inicialmente falhos para cálculo 36–44 px, limites de pan/zoom, ajuste total e preservação de coordenadas em `frontend/tests/battlemap-viewport.spec.ts`
- [ ] T034 [P] [US3] Estender testes de realtime inicialmente falhos para desconexão, comando bloqueado, reentrada e refetch sem baixar imagem inalterada em `frontend/tests/realtime.spec.ts`
- [ ] T035 [P] [US3] Estender Playwright para grids 1×1/100×100, ausência de overflow, orientação, pan/zoom, teclado e reconexão em `frontend/tests/e2e/battlemap.spec.ts`

### Implementation for User Story 3

- [ ] T036 [P] [US3] Implementar matriz de viewport, escala inicial alvo, clamp de pan/zoom, resize e comando ajustar mapa em `frontend/app/composables/useBattlemapViewport.ts`
- [ ] T037 [US3] Integrar pan por fundo, pinch/wheel, zoom por botões, navegação de teclado e anúncio acessível em `frontend/app/components/battlemap/BattlemapBoard.vue`
- [ ] T038 [US3] Bloquear movimentos offline, manter último snapshot seguro e sincronizar após reconexão em `frontend/app/stores/campaign.ts` e `frontend/app/composables/useCampaignRealtime.ts`
- [ ] T039 [US3] Definir altura estável por breakpoint, overflow interno, touch-action, controles de 44 px e orientação móvel em `frontend/app/assets/css/main.css`
- [ ] T040 [US3] Integrar controles de zoom/ajuste, status de coordenada e instruções por dispositivo em `frontend/app/components/battlemap/BattlemapBoard.vue` e `frontend/app/app.vue`

**Checkpoint**: Todas as histórias funcionam em desktop e Android, inclusive após desconexão/reconexão.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Fechar segurança, desempenho, documentação e validação integral.

- [ ] T041 [P] Adicionar casos de segurança para MIME/assinatura divergentes, nome malicioso, payload acima de 20 MB e acesso cruzado em `backend/tests/Companion.IntegrationTests/BattlemapApiTests.cs`
- [ ] T042 [P] Adicionar testes de regressão de renderização máxima com 202 linhas e 20 pinos em `frontend/tests/battlemap.spec.ts`
- [ ] T043 Atualizar instruções e escopo funcional do battlemap em `README.md`
- [ ] T044 Executar e registrar todos os cenários de `specs/003-battlemap-grid/quickstart.md`, corrigindo falhas antes de marcar a feature concluída

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: inicia imediatamente.
- **Phase 2 (Foundational)**: depende da Phase 1 e bloqueia todas as histórias.
- **Phase 3 (US1)**: depende da fundação e entrega o MVP publicável.
- **Phase 4 (US2)**: depende da fundação; testes de domínio/API podem usar battlemap semeado, mas a jornada integrada depende da US1.
- **Phase 5 (US3)**: depende da fundação e dos componentes de US1/US2 que serão navegados.
- **Phase 6 (Polish)**: depende das histórias escolhidas para a entrega.

### User Story Dependency Graph

```text
Setup ──> Foundation ──> US1 (configuração/MVP) ──> US2 (pinos) ──> US3 (viewport/resiliência) ──> Polish
                         └──────── testes de serviço US2 podem iniciar com fixture ────────┘
```

### Within Each User Story

1. Escrever os testes e confirmar que falham pelo comportamento ainda ausente.
2. Implementar domínio/persistência antes do serviço.
3. Implementar serviço antes dos endpoints.
4. Implementar cliente/estado antes da integração visual final.
5. Executar o teste independente e somente então avançar ao próximo checkpoint.

### Parallel Opportunities

- T002 e T003 podem avançar em paralelo após T001.
- T004, T008 e T009 alteram arquivos distintos e podem iniciar em paralelo.
- Em US1, T011–T013 são testes paralelos; T017–T019 podem ser implementados em paralelo após os contratos.
- Em US2, T022–T025 são paralelos; T028 pode avançar em paralelo com T026–T027.
- Em US3, T033–T035 são paralelos; T036 pode avançar enquanto T038 trata reconexão.
- T041 e T042 são validações finais paralelas.

## Parallel Example: User Story 1

```text
T011: integração REST/imagem em backend/tests/Companion.IntegrationTests/BattlemapApiTests.cs
T012: componente de configuração em frontend/tests/battlemap-setup.spec.ts
T013: jornada ponta a ponta em frontend/tests/e2e/battlemap.spec.ts

Após contratos compartilhados:
T017: cliente HTTP em frontend/app/composables/useCompanionApi.ts
T018: formulário em frontend/app/components/battlemap/BattlemapSetup.vue
T019: SVG base em frontend/app/components/battlemap/BattlemapBoard.vue
```

## Parallel Example: User Story 2

```text
T022: regras de domínio em backend/tests/Companion.UnitTests/BattlemapTests.cs
T023: autorização/concorrência em backend/tests/Companion.IntegrationTests/BattlemapApiTests.cs
T024: interações do board em frontend/tests/battlemap.spec.ts
T025: jornada multiusuário em frontend/tests/e2e/battlemap.spec.ts
```

## Parallel Example: User Story 3

```text
T033: matemática do viewport em frontend/tests/battlemap-viewport.spec.ts
T034: recuperação realtime em frontend/tests/realtime.spec.ts
T035: responsividade ponta a ponta em frontend/tests/e2e/battlemap.spec.ts
```

## Implementation Strategy

### MVP First

1. Concluir Setup e Foundational.
2. Concluir US1.
3. Validar publicação, substituição, autorização, persistência e visualização após reload.
4. Demonstrar o mapa compartilhado antes de adicionar movimentação.

### Incremental Delivery

1. **US1**: mestre publica mapa e grid; participantes visualizam.
2. **US2**: jogadores posicionam pinos e mestre arbitra.
3. **US3**: grids extremos, celular, acessibilidade e reconexão ficam completos.
4. **Polish**: segurança, regressão, documentação e quickstart integral.

## Notes

- Todas as tarefas têm checkbox, ID sequencial e caminho explícito.
- Tarefas de história usam exatamente [US1], [US2] ou [US3].
- O marcador [P] aparece somente quando os arquivos e dependências permitem execução simultânea.
- Alterações existentes no workspace devem ser preservadas e integradas, não sobrescritas.
