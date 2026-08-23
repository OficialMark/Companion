# Tasks: MVP de Campanha D&D

**Input**: Design documents from `specs/001-campaign-mvp/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Obrigatórios pela constituição para regras de domínio, persistência, autorização, SignalR e jornadas críticas.

**Organization**: Tasks are grouped by user story so every capability remains independently understandable and modifiable.

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Pode executar em paralelo quando arquivos e dependências não conflitam.
- **[Story]**: Mapeia a tarefa para uma user story do `spec.md`.

## Phase 1: Setup

**Purpose**: Estrutura dos projetos e ferramentas compartilhadas.

- [X] T001 Criar solução .NET 10 e os projetos Domain, Application, Infrastructure, Api, UnitTests e IntegrationTests em `backend/Companion.slnx`
- [X] T002 [P] Criar aplicação Nuxt 4 SPA/PWA e configuração TypeScript em `frontend/package.json` e `frontend/nuxt.config.ts`
- [X] T003 [P] Criar arquivos de exclusão e configuração-base do repositório em `.gitignore`, `.dockerignore` e `global.json`

---

## Phase 2: Foundational

**Purpose**: Infraestrutura obrigatória compartilhada por todas as jornadas.

- [X] T004 Definir entidades, enums e invariantes compartilhadas em `backend/src/Companion.Domain/Models/`
- [X] T005 [P] Definir DTOs, erros e contratos de serviços em `backend/src/Companion.Application/Contracts/`
- [X] T006 Configurar `CompanionDbContext`, mapeamentos SQL Server e migration inicial em `backend/src/Companion.Infrastructure/Persistence/`
- [X] T007 Implementar emissão, hash e validação de token opaco em `backend/src/Companion.Infrastructure/Security/ParticipantTokenService.cs`
- [X] T008 Implementar presença e grupos autorizados SignalR em `backend/src/Companion.Api/Realtime/CampaignHub.cs`
- [X] T009 Configurar DI, Problem Details, health checks, migrations e endpoints em `backend/src/Companion.Api/Program.cs`
- [X] T010 [P] Criar tipos, cliente HTTP, armazenamento de sessão e cliente SignalR compartilhados em `frontend/app/`
- [X] T011 [P] Configurar bases de teste xUnit, Vitest e Playwright em `backend/tests/`, `frontend/vitest.config.ts` e `frontend/playwright.config.ts`

**Checkpoint**: Fundação pronta; histórias podem ser implementadas em ordem de prioridade.

---

## Phase 3: User Story 1 - Criar e entrar em uma campanha (Priority: P1)

**Goal**: Mestre cria campanha e jogadores entram por código com isolamento e presença.

**Independent Test**: Dois navegadores criam/entram na mesma campanha e recebem lista atualizada; código inválido e acesso cruzado são recusados.

### Tests for User Story 1

- [X] T012 [P] [US1] Criar testes unitários de validação de campanha e participante em `backend/tests/Companion.UnitTests/CampaignTests.cs`
- [X] T013 [P] [US1] Criar testes de integração para criação, entrada, token e isolamento em `backend/tests/Companion.IntegrationTests/CampaignApiTests.cs`
- [X] T014 [P] [US1] Criar testes de componentes para formulários de campanha em `frontend/tests/campaign.spec.ts`

### Implementation for User Story 1

- [X] T015 [US1] Implementar casos de uso de criar, entrar e obter snapshot em `backend/src/Companion.Application/Campaigns/CampaignService.cs`
- [X] T016 [US1] Mapear endpoints de campanha conforme OpenAPI em `backend/src/Companion.Api/Endpoints/CampaignEndpoints.cs`
- [X] T017 [P] [US1] Implementar tela inicial de criação e entrada em `frontend/app/components/CampaignAccess.vue`
- [X] T018 [US1] Implementar cabeçalho, participantes e integração da sessão na mesa em `frontend/app/pages/index.vue` e `frontend/app/components/ParticipantsPanel.vue`

**Checkpoint**: Campanhas e presença são utilizáveis sem dados ou combate.

---

## Phase 4: User Story 2 - Rolar dados publicamente (Priority: P1)

**Goal**: Participantes executam rolagens autoritativas, idempotentes e persistidas.

**Independent Test**: Uma rolagem `2d6+3` aparece igual em dois clientes e persiste após reload; entrada inválida não gera histórico.

### Tests for User Story 2

- [X] T019 [P] [US2] Criar testes unitários do parser, limites e totais de dados em `backend/tests/Companion.UnitTests/DiceExpressionTests.cs`
- [X] T020 [P] [US2] Criar testes de integração de autorização, idempotência e histórico em `backend/tests/Companion.IntegrationTests/DiceRollApiTests.cs`
- [X] T021 [P] [US2] Criar testes de componentes de rolagem e histórico em `frontend/tests/dice.spec.ts`

### Implementation for User Story 2

- [X] T022 [US2] Implementar parser e aleatoriedade substituível em `backend/src/Companion.Domain/Dice/` e `backend/src/Companion.Infrastructure/Randomness/CryptoDiceRandomizer.cs`
- [X] T023 [US2] Implementar caso de uso idempotente e consulta de histórico em `backend/src/Companion.Application/DiceRolls/DiceRollService.cs`
- [X] T024 [US2] Mapear endpoint de rolagem e evento pós-persistência em `backend/src/Companion.Api/Endpoints/DiceRollEndpoints.cs`
- [X] T025 [P] [US2] Implementar controles rápidos e expressão manual em `frontend/app/components/DiceRoller.vue`
- [X] T026 [US2] Implementar histórico deduplicado e atualização SignalR em `frontend/app/components/RollHistory.vue` e `frontend/app/stores/campaign.ts`

**Checkpoint**: O principal incremento do MVP funciona e é recuperável.

---

## Phase 5: User Story 5 - Disponibilizar o MVP na rede local (Priority: P1)

**Goal**: Executar frontend, API e SQL Server com um comando e uma única porta LAN.

**Independent Test**: Um dispositivo acessa `http://<ip-do-host>:3000`, cria campanha e rola dados; reinício preserva o histórico.

### Tests for User Story 5

- [X] T027 [P] [US5] Criar script de smoke test dos health checks e proxy em `scripts/smoke-test.ps1`
- [X] T028 [P] [US5] Criar teste E2E de criação, entrada e rolagem em viewports desktop/mobile em `frontend/tests/e2e/mvp.spec.ts`

### Implementation for User Story 5

- [X] T029 [US5] Criar Dockerfile multi-stage da API em `backend/Dockerfile`
- [X] T030 [P] [US5] Criar Dockerfile do frontend e proxy WebSocket/API em `frontend/Dockerfile` e `frontend/nginx.conf`
- [X] T031 [US5] Orquestrar frontend, API, SQL Server, health checks e volume em `docker-compose.yml` e `.env.example`
- [X] T032 [US5] Documentar inicialização, IP LAN, firewall e Hamachi/Radmin em `README.md`

**Checkpoint**: Campanhas e dados funcionam entre dispositivos da LAN.

---

## Phase 6: User Story 3 - Controlar iniciativa e turnos (Priority: P2)

**Goal**: Mestre cria combate, adiciona combatentes, rola iniciativa e controla rodadas.

**Independent Test**: Três combatentes percorrem uma rodada completa; outro cliente acompanha; jogador não altera combate.

### Tests for User Story 3

- [X] T033 [P] [US3] Criar testes unitários de ordenação, desempate e transições em `backend/tests/Companion.UnitTests/EncounterTests.cs`
- [X] T034 [P] [US3] Criar testes de integração de autorização e concorrência de turno em `backend/tests/Companion.IntegrationTests/EncounterApiTests.cs`
- [X] T035 [P] [US3] Criar testes de componentes do painel de iniciativa em `frontend/tests/encounter.spec.ts`

### Implementation for User Story 3

- [X] T036 [US3] Implementar casos de uso e concorrência do encontro em `backend/src/Companion.Application/Encounters/EncounterService.cs`
- [X] T037 [US3] Mapear endpoints e eventos de encontro conforme contratos em `backend/src/Companion.Api/Endpoints/EncounterEndpoints.cs`
- [X] T038 [P] [US3] Implementar painel de preparação e adição de combatentes em `frontend/app/components/EncounterSetup.vue`
- [X] T039 [US3] Implementar ordem, turno ativo e controles do mestre em `frontend/app/components/InitiativeTracker.vue` e `frontend/app/stores/campaign.ts`

**Checkpoint**: O combate básico está sincronizado e autorizado.

---

## Phase 7: User Story 4 - Recuperar a mesa após desconexão (Priority: P2)

**Goal**: Exibir conectividade, reconectar, reautorizar e substituir estado pelo snapshot atual.

**Independent Test**: Cliente offline perde rolagem/turno e, ao voltar, recupera ambos sem duplicação.

### Tests for User Story 4

- [X] T040 [P] [US4] Criar testes unitários da máquina de conexão e deduplicação em `frontend/tests/realtime.spec.ts`
- [X] T041 [P] [US4] Criar teste E2E de desconexão e ressincronização em `frontend/tests/e2e/reconnect.spec.ts`

### Implementation for User Story 4

- [X] T042 [US4] Implementar reconexão, reentrada no grupo e ressincronização em `frontend/app/composables/useCampaignRealtime.ts`
- [X] T043 [P] [US4] Implementar indicador acessível de conexão em `frontend/app/components/ConnectionStatus.vue`
- [X] T044 [US4] Integrar ciclo de vida e invalidação de sessão na mesa em `frontend/app/pages/index.vue` e `frontend/app/stores/campaign.ts`

**Checkpoint**: Suspensão de aba e oscilações de LAN/VPN são recuperadas com segurança.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Qualidade final, segurança, acessibilidade e documentação.

- [X] T045 [P] Revisar responsividade, alvos de toque, foco e contraste em `frontend/app/assets/css/main.css`
- [X] T046 [P] Adicionar logs estruturados e correlação sem credenciais em `backend/src/Companion.Api/Program.cs`
- [X] T047 Executar e documentar validações unitárias, integração, frontend, Compose e quickstart em `specs/001-campaign-mvp/quickstart.md`
- [X] T048 Validar contratos, ausência de segredos e consistência final da documentação em `specs/001-campaign-mvp/`

---

## Phase 9: Histórico temático e nova rolagem de iniciativa

**Purpose**: Refinar a apresentação do histórico e permitir nova rolagem antes ou durante o combate.

- [X] T049 [P] [US2] Estilizar a barra de rolagem do histórico conforme o tema em `frontend/app/assets/css/main.css`
- [X] T050 [P] [US3] Criar testes de nova rolagem e preservação do turno em `backend/tests/Companion.UnitTests/EncounterTests.cs` e `backend/tests/Companion.IntegrationTests/EncounterApiTests.cs`
- [X] T051 [US3] Permitir nova rolagem individual ou geral durante preparação e combate em `backend/src/Companion.Domain/Models/Encounter.cs`
- [X] T052 [US3] Expor controles acessíveis de nova rolagem na interface e testar o fluxo em `frontend/app/components/EncounterSetup.vue`, `frontend/app/components/InitiativeTracker.vue`, `frontend/tests/encounter.spec.ts` e `frontend/tests/e2e/initiative.spec.ts`
- [X] T053 [P] [US3] Atualizar requisitos, modelo e contrato da nova rolagem em `specs/001-campaign-mvp/spec.md`, `specs/001-campaign-mvp/data-model.md` e `specs/001-campaign-mvp/contracts/openapi.yaml`

---

## Phase 10: Compatibilidade Android e expressões avançadas de dados

**Purpose**: Remover a dependência de UUID nativo e representar repetições e termos compostos como um único teste rastreável.

- [X] T054 [P] [US2] Criar gerador UUID v4 compatível e adotá-lo no cliente HTTP em `frontend/app/utils/createUuid.ts` e `frontend/app/composables/useCompanionApi.ts`
- [X] T055 [P] [US2] Especificar e testar a gramática de `#` e múltiplos termos em `backend/src/Companion.Domain/Dice/DiceExpression.cs` e `backend/tests/Companion.UnitTests/DiceExpressionTests.cs`
- [X] T056 [US2] Persistir resultados agrupados com compatibilidade retroativa e ampliar a coluna de expressão em `backend/src/Companion.Application/DiceRolls/DiceRollService.cs`, `backend/src/Companion.Application/Campaigns/CampaignService.cs`, `backend/src/Companion.Infrastructure/Persistence/ModelConfigurations.cs` e `backend/src/Companion.Infrastructure/Persistence/Migrations/`
- [X] T057 [P] [US2] Atualizar DTOs, tipos e contrato dos resultados compostos em `backend/src/Companion.Application/Contracts/Dtos.cs`, `frontend/app/types/campaign.ts` e `specs/001-campaign-mvp/contracts/openapi.yaml`
- [X] T058 [US2] Adaptar entrada e histórico para repetições e múltiplos termos em `frontend/app/components/DiceRoller.vue`, `frontend/app/components/RollHistory.vue` e `frontend/app/assets/css/main.css`
- [X] T059 [P] [US2] Cobrir UUID ausente, resultados agrupados e fluxo desktop/Android em `frontend/tests/dice.spec.ts`, `frontend/tests/realtime.spec.ts`, `frontend/tests/e2e/mvp.spec.ts` e `backend/tests/Companion.IntegrationTests/DiceRollApiTests.cs`
- [X] T060 [P] [US2] Atualizar requisitos, modelo de dados e validações em `specs/001-campaign-mvp/spec.md`, `specs/001-campaign-mvp/data-model.md` e `specs/001-campaign-mvp/quickstart.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- Setup → Foundational → US1.
- US2 depende de US1 para campanha/autorização.
- US5 depende de US1 e US2 para seu smoke/E2E representativo.
- US3 depende apenas da fundação e US1, mas vem após o incremento LAN para entrega incremental.
- US4 depende do estado e eventos de US1, US2 e US3.
- Polish depende de todas as histórias.

### User Story Completion Order

```text
US1 Campaigns ──> US2 Dice ──> US5 LAN deployment
      └─────────> US3 Initiative ──> US4 Reconnection
```

### Parallel Opportunities

- T002 e T003 podem ocorrer paralelamente a T001.
- Testes unitários, integração e frontend de cada história podem ser escritos em paralelo.
- Componentes frontend marcados [P] podem avançar enquanto o caso de uso backend da mesma história é implementado contra o contrato.
- Dockerfiles de API e frontend podem ser criados em paralelo antes do Compose.

## Parallel Examples

### User Story 1

```text
T012 backend unit tests | T013 API integration tests | T014 frontend component tests
T015 campaign service   | T017 campaign access component
```

### User Story 2

```text
T019 dice unit tests | T020 dice API tests | T021 dice component tests
T023 dice service    | T025 dice controls
```

### User Story 3

```text
T033 encounter unit tests | T034 encounter API tests | T035 encounter UI tests
T036 encounter service    | T038 encounter setup UI
```

## Implementation Strategy

1. Entregar US1 e validar isolamento de campanha.
2. Entregar US2 como núcleo funcional de rolagens.
3. Empacotar US5 e validar uso real na LAN.
4. Adicionar US3 sem alterar contratos de campanha/dados.
5. Completar US4 sobre os snapshots e eventos existentes.
6. Executar todos os gates e concluir Polish.

## Notes

- Cada tarefa usa caminhos concretos e deve ser marcada `[X]` após validação.
- Migrations e dados persistentes pertencem ao backend; presença permanece efêmera.
- A remoção do volume SQL é uma operação deliberadamente fora dos comandos normais.

