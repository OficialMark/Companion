# Tasks: Acesso, Administração, Iniciativa e Vida em Combate

**Input**: Design documents from `/specs/002-campaign-access-initiative/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/openapi.yaml, quickstart.md

**Tests**: Obrigatórios pela constituição: domínio, integração, componentes e jornadas críticas E2E.

**Organization**: Tarefas agrupadas pelas cinco histórias da especificação. US1 e US5 preservam funcionalidades existentes por regressão; US2–US4 entregam o novo escopo de vida e ataques.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Pode ser executada em paralelo sem editar os mesmos arquivos.
- **[Story]**: História da especificação coberta pela tarefa.
- Todas as tarefas possuem caminho exato.

## Phase 1: Setup e baseline

**Purpose**: Confirmar o estado inicial e os arquivos de exclusão antes da mudança.

- [X] T001 Verificar padrões essenciais de .NET, Node, Docker e arquivos temporários em `.gitignore` e `.dockerignore`
- [X] T002 [P] Confirmar que o design-time do EF Core carrega `backend/src/Companion.Infrastructure/Companion.Infrastructure.csproj` e a solução `backend/Companion.slnx`
- [X] T003 [P] Executar o baseline de testes descrito em `frontend/package.json`, `backend/tests/Companion.UnitTests/Companion.UnitTests.csproj` e `backend/tests/Companion.IntegrationTests/Companion.IntegrationTests.csproj`

---

## Phase 2: Foundational — contratos compartilhados

**Purpose**: Definir os tipos e abstrações usados pelas histórias de vida, ataque e privacidade.

- [X] T004 Atualizar requests e DTOs de perfil, vida, ataque e projeção filtrada em `backend/src/Companion.Application/Contracts/Dtos.cs`
- [X] T005 Atualizar abstrações de persistência e notificação para ataques e invalidação de snapshot em `backend/src/Companion.Application/Contracts/Abstractions.cs`
- [X] T006 [P] Atualizar tipos TypeScript de participante, combatente, ataque e evento em `frontend/app/types/campaign.ts`

**Checkpoint**: Contratos compiláveis e prontos para as histórias.

---

## Phase 3: User Story 1 — Entrar como jogador ou mestre (Priority: P1)

**Goal**: Preservar códigos, papéis e ocultação de segredos enquanto os novos campos são adicionados aos snapshots.

**Independent Test**: Criar campanha e entrar pelos dois códigos; somente o código correto concede mestre e nenhum campo novo expõe segredo ou vida alheia.

### Tests for User Story 1

- [X] T007 [P] [US1] Estender regressões de criação, entrada e snapshot com os novos campos opcionais em `backend/tests/Companion.IntegrationTests/CampaignApiTests.cs`
- [X] T008 [P] [US1] Estender regressões de papel e sessão no frontend em `frontend/tests/campaign.spec.ts`

**Checkpoint**: Entrada por código continua funcional e independente.

---

## Phase 4: User Story 2 — Preparar iniciativa e vida (Priority: P1) 🎯 MVP do novo escopo

**Goal**: Jogador salva iniciativa/vida; mestre importa, edita e valida vidas na preparação.

**Independent Test**: Jogadores salvam valores, mestre importa todos sem duplicação, edita uma vida, rerrola sem sobrescrever e só inicia quando todas as vidas existem.

### Tests for User Story 2

- [X] T009 [P] [US2] Adicionar testes de limite, valor nulo, cópia e requisito de início em `backend/tests/Companion.UnitTests/EncounterTests.cs`
- [X] T010 [P] [US2] Adicionar testes de perfil, importação, edição e persistência em `backend/tests/Companion.IntegrationTests/EncounterApiTests.cs`
- [X] T011 [P] [US2] Atualizar testes dos componentes de perfil e preparação em `frontend/tests/initiative-modifier.spec.ts` e `frontend/tests/encounter.spec.ts`

### Implementation for User Story 2

- [X] T012 [P] [US2] Adicionar vida opcional validada ao participante em `backend/src/Companion.Domain/Models/Participant.cs`
- [X] T013 [US2] Adicionar vida, edição e validação de início ao combatente/encontro em `backend/src/Companion.Domain/Models/Combatant.cs` e `backend/src/Companion.Domain/Models/Encounter.cs`
- [X] T014 [US2] Implementar atualização do perfil e cópia de vida no encontro em `backend/src/Companion.Application/Campaigns/CampaignService.cs` e `backend/src/Companion.Application/Encounters/EncounterService.cs`
- [X] T015 [US2] Expor perfil de combate e aceitar vida nos comandos de combatente em `backend/src/Companion.Api/Endpoints/CampaignEndpoints.cs` e `backend/src/Companion.Api/Endpoints/EncounterEndpoints.cs`
- [X] T016 [US2] Mapear participante/combatente com vida e projeção por observador em `backend/src/Companion.Application/Contracts/DtoMapper.cs`
- [X] T017 [US2] Configurar colunas de vida e gerar migration `AddCombatHealthAndAttacks` em `backend/src/Companion.Infrastructure/Persistence/ModelConfigurations.cs` e `backend/src/Companion.Infrastructure/Persistence/Migrations/`
- [X] T018 [US2] Atualizar cliente HTTP para perfil e vida dos combatentes em `frontend/app/composables/useCompanionApi.ts`
- [X] T019 [US2] Transformar o formulário final em perfil de iniciativa e vida somente para jogadores em `frontend/app/components/InitiativeModifier.vue` e `frontend/app/pages/index.vue`
- [X] T020 [US2] Adicionar vida na inclusão/edição e bloqueio de início em `frontend/app/components/EncounterSetup.vue` e `frontend/app/components/InitiativeTracker.vue`

**Checkpoint**: US2 funciona sem depender de ataques.

---

## Phase 5: User Story 3 — Atacar um ou mais alvos (Priority: P1)

**Goal**: Jogador vinculado ou mestre usa uma rolagem de resultado único para causar dano atômico e idempotente a alvos distintos em qualquer turno.

**Independent Test**: Rolar dano, selecionar dois alvos, confirmar uma vez e verificar o mesmo desconto em ambos, sem alterar terceiro alvo nem duplicar dano no retry.

### Tests for User Story 3

- [X] T021 [P] [US3] Adicionar testes de dano, piso zero, alvos distintos e ataque fora do turno em `backend/tests/Companion.UnitTests/EncounterTests.cs`
- [X] T022 [P] [US3] Adicionar testes de autorização, rolagem válida, atomicidade e idempotência em `backend/tests/Companion.IntegrationTests/EncounterApiTests.cs`
- [X] T023 [P] [US3] Adicionar testes de seleção posterior, cancelamento e confirmação em `frontend/tests/attack.spec.ts`

### Implementation for User Story 3

- [X] T024 [P] [US3] Criar agregado e alvos de ataque em `backend/src/Companion.Domain/Models/CombatAttack.cs` e `backend/src/Companion.Domain/Models/CombatAttackTarget.cs`
- [X] T025 [US3] Implementar aplicação de dano e regras de alvo no domínio em `backend/src/Companion.Domain/Models/Encounter.cs` e `backend/src/Companion.Domain/Models/Combatant.cs`
- [X] T026 [US3] Persistir ataques/alvos, índices idempotentes e relacionamentos em `backend/src/Companion.Infrastructure/Persistence/CompanionDbContext.cs` e `backend/src/Companion.Infrastructure/Persistence/ModelConfigurations.cs`
- [X] T027 [US3] Implementar validação da rolagem, autorização, tratamento de concorrência e resolução atômica em `backend/src/Companion.Application/Encounters/AttackService.cs`
- [X] T028 [US3] Expor `POST /attacks` com Idempotency-Key em `backend/src/Companion.Api/Endpoints/EncounterEndpoints.cs` e registrar o serviço em `backend/src/Companion.Api/Program.cs`
- [X] T029 [US3] Adicionar chamada idempotente de ataque em `frontend/app/composables/useCompanionApi.ts`
- [X] T030 [US3] Permitir que a rolagem existente devolva o resultado ao fluxo chamador em `frontend/app/components/DiceRoller.vue`
- [X] T031 [US3] Criar jornada Atacar com rolagem, seleção múltipla, revisão e cancelamento em `frontend/app/components/CombatAttack.vue` e integrar em `frontend/app/pages/index.vue`

**Checkpoint**: US3 aplica ataques independentemente do avanço de turno.

---

## Phase 6: User Story 4 — Alterar e acompanhar vida em tempo real (Priority: P1)

**Goal**: Jogador edita/vê apenas sua vida; mestre edita/vê todas; mudanças chegam em tempo real e reconexão recupera snapshot correto.

**Independent Test**: Três navegadores confirmam que mestre e dono recebem uma mudança, terceiro jogador não recebe a vida, e reconexão restaura o valor autorizado.

### Tests for User Story 4

- [X] T032 [P] [US4] Adicionar testes de autorização e projeção de snapshot em `backend/tests/Companion.IntegrationTests/EncounterApiTests.cs`
- [X] T033 [P] [US4] Adicionar testes de invalidação/refetch e ausência de vazamento em `frontend/tests/realtime.spec.ts` e `frontend/tests/encounter.spec.ts`
- [X] T034 [P] [US4] Adicionar jornada desktop/Android de vida, privacidade e reconexão em `frontend/tests/e2e/health-attack.spec.ts`

### Implementation for User Story 4

- [X] T035 [US4] Implementar edição direta autorizada e retorno filtrado em `backend/src/Companion.Application/Encounters/EncounterService.cs`
- [X] T036 [US4] Expor endpoint de vida do combatente em `backend/src/Companion.Api/Endpoints/EncounterEndpoints.cs`
- [X] T037 [US4] Filtrar participantes e encontro do snapshot pelo observador em `backend/src/Companion.Application/Campaigns/CampaignService.cs` e `backend/src/Companion.Application/Contracts/DtoMapper.cs`
- [X] T038 [US4] Emitir invalidação sem vida após ataques/edições em `backend/src/Companion.Api/Realtime/SignalRCampaignNotifier.cs` e `backend/src/Companion.Application/Contracts/Abstractions.cs`
- [X] T039 [US4] Recarregar snapshot em eventos sensíveis e deduplicar refresh em `frontend/app/stores/campaign.ts` e `frontend/app/composables/useCampaignRealtime.ts`
- [X] T040 [US4] Mostrar/editar vida conforme papel no tracker em `frontend/app/components/InitiativeTracker.vue`

**Checkpoint**: US4 atende tempo real, privacidade e reconexão.

---

## Phase 7: User Story 5 — Administrar mesas existentes (Priority: P2)

**Goal**: Preservar listagem/exclusão e garantir remoção dos novos registros de combate.

**Independent Test**: Excluir campanha com ataque confirmado e verificar que códigos, sessões, ataques, alvos, rolagens e encontro deixam de existir.

### Tests and implementation for User Story 5

- [X] T041 [P] [US5] Estender regressão de exclusão com ataques e alvos em `backend/tests/Companion.IntegrationTests/AdminApiTests.cs`
- [X] T042 [US5] Incluir ataques e alvos na exclusão transacional em `backend/src/Companion.Infrastructure/Persistence/CompanionDbContext.cs`

**Checkpoint**: US5 continua funcional com o novo agregado.

---

## Phase 8: Polish e validação transversal

**Purpose**: Fechar acessibilidade, migração, documentação e todas as suítes.

- [X] T043 [P] Atualizar estilos responsivos e estados de erro/pendência em `frontend/app/assets/css/main.css`
- [X] T044 [P] Atualizar cobertura do contrato realizado e passos executáveis em `specs/002-campaign-access-initiative/contracts/openapi.yaml` e `specs/002-campaign-access-initiative/quickstart.md`
- [X] T045 Validar a migration contra banco vazio e banco no schema anterior usando `backend/src/Companion.Infrastructure/Persistence/Migrations/`
- [X] T046 Executar `dotnet test` para `backend/tests/Companion.UnitTests/Companion.UnitTests.csproj` e `backend/tests/Companion.IntegrationTests/Companion.IntegrationTests.csproj`
- [X] T047 Executar `pnpm test`, `pnpm typecheck` e `pnpm test:e2e` definidos em `frontend/package.json`
- [X] T048 Revisar aderência final a `specs/002-campaign-access-initiative/spec.md`, `plan.md` e `quickstart.md`
- [X] T049 Ajustar a grade responsiva do perfil de combate em `frontend/app/components/InitiativeModifier.vue` e `frontend/app/assets/css/main.css`
- [X] T050 Publicar e animar a quantidade de dano para todos sem expor vidas privadas em `backend/src/Companion.Application/Encounters/AttackService.cs`, `frontend/app/stores/campaign.ts` e `frontend/app/components/InitiativeTracker.vue`
- [X] T051 Separar visualmente nome e modificador na visão do jogador em `frontend/app/assets/css/main.css`
- [X] T052 Fazer a rerrolagem geral transferir o turno para a maior iniciativa, preservando a rerrolagem individual, em `backend/src/Companion.Domain/Models/Encounter.cs`
- [X] T053 Descartar o efeito de dano após a animação e cobrir a regressão de reordenação em `frontend/app/stores/campaign.ts` e `frontend/tests/e2e/health-attack.spec.ts`
- [X] T054 Expor o estado público de PV zero sem revelar o valor privado e aplicar tonalidade vermelha no tracker em `backend/src/Companion.Application/Contracts/DtoMapper.cs` e `frontend/app/components/InitiativeTracker.vue`

---

## Dependencies & Execution Order

### Phase Dependencies

- Phase 1 → Phase 2 → histórias.
- US1 é regressão independente após Foundation.
- US2 entrega o modelo de vida e bloqueia US3 e US4.
- US3 depende de US2 para vida dos alvos.
- US4 depende de US2 e integra eventos produzidos por US3.
- US5 depende do agregado criado em US3.
- Polish depende de todas as histórias.

### User Story Completion Order

```text
US1 (regressão) ───────────────┐
US2 (perfil/preparação) → US3 (ataque) → US4 (tempo real/privacidade) ─→ Polish
                           └────────────→ US5 (exclusão) ────────────────┘
```

### Parallel Opportunities

- T002 e T003 podem executar em paralelo; T006 pode avançar enquanto T004/T005 são preparados.
- Testes marcados `[P]` em cada história podem ser escritos em paralelo antes da implementação correspondente.
- US1 pode ser validada em paralelo com US2 após Foundation.
- T034 (E2E) pode ser preparado enquanto backend de US4 é implementado.
- T041 pode ser escrito após o modelo de US3 sem esperar a interface de US4.
- T043 e T044 podem executar em paralelo antes das validações finais.

## Parallel Examples

### User Story 2

```text
T009: testes de domínio em backend/tests/Companion.UnitTests/EncounterTests.cs
T010: testes HTTP em backend/tests/Companion.IntegrationTests/EncounterApiTests.cs
T011: testes Vue em frontend/tests/initiative-modifier.spec.ts e frontend/tests/encounter.spec.ts
```

### User Story 3

```text
T021: regras de dano no domínio
T022: contrato/autorização/idempotência HTTP
T023: fluxo do componente de ataque
```

### User Story 4

```text
T032: privacidade do snapshot no backend
T033: invalidação e refetch no store
T034: jornada E2E desktop/Android
```

## Implementation Strategy

### MVP do novo escopo

1. Completar Setup e Foundation.
2. Validar regressão US1.
3. Entregar US2: perfil de iniciativa/vida e preparação completa.
4. Validar US2 isoladamente antes de introduzir ataques.

### Incremental Delivery

1. US2 → vida preparada e editável pelo mestre.
2. US3 → dano por ataque confirmado.
3. US4 → privacidade e sincronização resiliente.
4. US5 → administração compatível com novos dados.
5. Polish → migração, acessibilidade e suíte completa.

## Notes

- Testes são escritos antes da implementação da história correspondente.
- Payload compartilhado em SignalR nunca contém vida.
- Reenvios usam a mesma Idempotency-Key; o cliente não inventa dano.
- Cada tarefa concluída deve ser marcada `[X]`.