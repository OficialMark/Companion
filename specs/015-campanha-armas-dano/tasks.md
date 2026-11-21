# Tasks: Ataques Tipados e Caderno da Campanha

**Input**: Design documents in `/specs/015-campanha-armas-dano/`\
**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/`

## Phase 1: Setup

**Purpose**: Preparar os pontos de extensão já existentes; não há novos projetos ou dependências.

- [X] T001 Confirmar módulos e rotas existentes que serão estendidos em `backend/src/Companion.Api/Program.cs` e `frontend/app/pages/index.vue`
- [X] T002 Confirmar compatibilidade dos dados seed e formato de `WeaponData` em `backend/src/Companion.Domain/Characters/CharacterData.cs` e `backend/src/Companion.Application/Catalog/CatalogSeeder.cs`

## Phase 2: Foundational

**Purpose**: Persistência e autorização comum que bloqueiam as histórias do caderno.

- [X] T003 [P] Criar entidades de registro, contribuição, nota privada e ponto em `backend/src/Companion.Domain/Campaigns/CampaignKnowledge.cs`
- [X] T004 Registrar as entidades no contrato de persistência e DbContext em `backend/src/Companion.Application/Contracts/Abstractions.cs` e `backend/src/Companion.Infrastructure/Persistence/CompanionDbContext.cs`
- [X] T005 Configurar índices, relacionamentos, limites, rowversion e exclusões em `backend/src/Companion.Infrastructure/Persistence/ModelConfigurations.cs`
- [X] T006 Criar migration aditiva de SQL Server para registros, notas e pontos em `backend/src/Companion.Infrastructure/Persistence/Migrations/`
- [X] T007 Adicionar validação compartilhada de tipos, imagem, campos e coordenadas em `backend/src/Companion.Domain/Campaigns/CampaignKnowledge.cs`
- [X] T008 Adicionar eventos de invalidação do caderno sem payload sensível em `backend/src/Companion.Application/Contracts/Abstractions.cs` e `backend/src/Companion.Api/Realtime/SignalRCampaignNotifier.cs`

## Phase 3: User Story 1 - Configurar dano tipado em ataques (Priority: P1) 🎯 MVP

**Goal**: Configurar e rolar componentes independentes de dano, com tipo de dano do catálogo de armas.\
**Independent Test**: O jogador salva uma arma cortante com componente adicional gélido, rola, consulta subtotais e total e reabre a ficha para confirmar persistência.

### Implementation

- [X] T009 [US1] Estender `WeaponData` para tipos selecionáveis e compatibilidade com `DamageType` antigo em `backend/src/Companion.Domain/Characters/CharacterData.cs`
- [X] T010 [US1] Estender dados e validação de rolagem salva com componentes adicionais limitados em `backend/src/Companion.Domain/Characters/SavedRollData.cs`
- [X] T011 [US1] Preservar desserialização e normalizar tipos de dano de armas seedadas em `backend/src/Companion.Application/Catalog/CatalogSeeder.cs` e `backend/src/Companion.Infrastructure/Persistence/Seed/`
- [X] T012 [US1] Expor tipos múltiplos de dano em `DamageRoll` e no cálculo de armas em `backend/src/Companion.Domain/Characters/CharacterStats.cs` e `CharacterRules.cs`
- [X] T013 [US1] Atualizar a importação de armas para associar os tipos físicos de dano em `scripts/import-catalogs/`
- [X] T014 [P] [US1] Estender os tipos da ficha, arma, rolagem salva e resultado detalhado em `frontend/app/types/character.ts`
- [X] T015 [US1] Atualizar serialização e construção de configuração de rolagem tipada em `frontend/app/utils/playerRoll.ts`
- [X] T016 [US1] Adicionar seleção de tipos de arma e componentes extras no editor de rolagem em `frontend/app/components/character/SavedRollEditor.vue` e `frontend/app/components/character/RollConfigFields.vue`
- [X] T017 [US1] Persistir e apresentar subtotais e tipos de dano na resolução e no histórico da rolagem em `DiceRollService`, `RollHistory.vue` e `DamageRollModal.vue`
- [X] T018 [US1] Permitir editar tipos disponíveis no editor de item de catálogo em `frontend/app/components/character/ItemEditorModal.vue` e `frontend/app/components/character/CatalogDetail.vue`

**Checkpoint**: ataques legados e novos ataques tipados são configuráveis e rolam pelo fluxo atual.

## Phase 4: User Story 2 - Consultar e construir o caderno compartilhado (Priority: P1)

**Goal**: Expor seção Campanha, páginas para os quatro tipos de conteúdo e contribuições atribuídas.\
**Independent Test**: Um participante cria NPC, mapa, pista e diário; outras sessões consultam o conteúdo após atualização e reconexão.

### Implementation

- [X] T019 [US2] Criar DTOs de lista/detalhe e filtros de campanha em `backend/src/Companion.Application/Campaigns/CampaignKnowledgeService.cs`
- [X] T020 [US2] Implementar consulta paginada, busca, criação, edição e remoção de conteúdo compartilhado em `backend/src/Companion.Application/Campaigns/CampaignKnowledgeService.cs`
- [X] T021 [US2] Implementar adição e edição atribuída de contribuições em `backend/src/Companion.Application/Campaigns/CampaignKnowledgeService.cs`
- [X] T022 [US2] Mapear as rotas REST de registros e contribuições em `backend/src/Companion.Api/Endpoints/CampaignKnowledgeEndpoints.cs` e registrar em `backend/src/Companion.Api/Program.cs`
- [X] T023 [US2] Implementar cliente REST e modelos de domínio de interface em `frontend/app/composables/useCampaignKnowledgeApi.ts` e `frontend/app/types/campaignKnowledge.ts`
- [X] T024 [US2] Implementar busca e invalidação/reconsulta após atualização em tempo real com a revisão `campaignKnowledgeRevision` em `frontend/app/stores/campaign.ts`
- [X] T025 [US2] Criar tela Campanha com abas acessíveis para NPCs, Mapas, Pistas e Diário em `frontend/app/components/campaign/CampaignNotebook.vue`
- [X] T026 [US2] Criar editor responsivo com estados vazios, autoria e contribuições em `CampaignNotebook.vue`
- [X] T027 [US2] Adicionar entrada Campanha na navegação superior e na tela principal em `frontend/app/components/ViewNav.vue` e `frontend/app/pages/index.vue`

**Checkpoint**: jogadores e mestre podem consultar e adicionar informação pública nos quatro tipos de página.

## Phase 5: User Story 3 - Guardar segredos e controlar visibilidade (Priority: P1)

**Goal**: Restringir dados ocultos e notas ao mestre, tanto nas consultas quanto nos eventos.
**Independent Test**: O mestre oculta NPC/mapa/pista e adiciona notas; um jogador não recebe itens, notas ou pistas de existência, inclusive por busca direta.

### Implementation

- [X] T028 [US3] Filtrar registros por papel antes de projetar listas e detalhes e limitar exclusão/visibilidade ao mestre em `backend/src/Companion.Application/Campaigns/CampaignKnowledgeService.cs`
- [X] T029 [US3] Implementar leitura e gravação isolada de notas pessoais do mestre em `backend/src/Companion.Application/Campaigns/CampaignKnowledgeService.cs` e `backend/src/Companion.Api/Endpoints/CampaignKnowledgeEndpoints.cs`
- [X] T030 [US3] Garantir eventos genéricos sem identificadores e emissão após persistência em `backend/src/Companion.Api/Realtime/SignalRCampaignNotifier.cs`
- [X] T031 [US3] Adicionar edição de notas pessoais e controle de visibilidade apenas para mestre em `frontend/app/components/campaign/CampaignNotebook.vue`
- [X] T032 [US3] Diferenciar visualmente conteúdo reservado ao mestre e conteúdo compartilhado em `frontend/app/assets/css/campaign-knowledge.css` e `frontend/app/components/campaign/`

**Checkpoint**: nenhum campo ou registro privado aparece em respostas ou eventos de jogador.

## Phase 6: User Story 4 - Marcar pontos de interesse nos mapas (Priority: P2)

**Goal**: Persistir mapas de referência e pontos que ficam ancorados à imagem.
**Independent Test**: Dois participantes criam e consultam pontos; posições permanecem estáveis ao redimensionar e reconectar.

### Implementation

- [X] T033 [US4] Validar MIME, tamanho, nome e acesso a imagens de mapas da campanha em `backend/src/Companion.Application/Campaigns/CampaignKnowledgeService.cs` e `backend/src/Companion.Api/Endpoints/CampaignKnowledgeEndpoints.cs`
- [X] T034 [US4] Implementar criar, atualizar, listar e remover pontos em coordenadas normalizadas em `backend/src/Companion.Application/Campaigns/CampaignKnowledgeService.cs` e `backend/src/Companion.Api/Endpoints/CampaignKnowledgeEndpoints.cs`
- [X] T035 [US4] Implementar carregamento de imagem de detalhe sem carregar bytes na listagem em `frontend/app/composables/useCampaignKnowledgeApi.ts`
- [X] T036 [US4] Criar visualizador responsivo de mapa com marcadores acessíveis por toque e teclado em `frontend/app/components/campaign/CampaignNotebook.vue`
- [X] T037 [US4] Integrar edição de imagem, pontos, estado de gravação e atualização em tempo real ao detalhe do mapa em `CampaignNotebook.vue`

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T038 [P] Atualizar documentação do produto com navegação Campanha e ataques tipados em `README.md`
- [X] T039 [P] Rever espaçamento, contraste, foco, alvos de toque e breakpoints da nova área em `frontend/app/assets/css/campaign-knowledge.css` e folhas de estilo da ficha
- [X] T040 Revisar endpoints, projeções por papel e erros de concorrência contra os contratos em `specs/015-campanha-armas-dano/contracts/`
- [X] T041 Registrar a validação automatizada executada e passos manuais no relatório e `specs/015-campanha-armas-dano/quickstart.md`

## Dependencies & Execution Order

### Phase Dependencies

- Setup precede Foundation; Foundation bloqueia as histórias do caderno.
- US1 depende apenas de Setup e pode ser entregue de forma independente.
- US2 depende de Foundation.
- US3 depende do modelo e serviços de US2; a privacidade é pré-requisito de lançamento do caderno.
- US4 depende do modelo e detalhe de mapa de US2 e da projeção segura de US3.
- Polish depende das histórias implementadas.

### Parallel Opportunities

- T003 e T008 podem ser trabalhadas em paralelo após T001–T002 por arquivos separados; T004–T006 dependem de T003.
- Em US1, T014 pode avançar em paralelo com tarefas de domínio do backend; tarefas de UI dependem dos contratos estabilizados.
- Em US2, tipos e cliente (T023) podem começar após contratos; navegação e componentes dependem da tela base.
- Em US4, editor de marcador e validação de imagem são separáveis depois de T019–T020.

## Implementation Strategy

Entregar primeiro US1 como incremento independente. Para a área Campanha, implementar Foundation e US2/US3 como uma única entrega segura, pois conteúdo compartilhado sem privacidade completa não deve ser exposto; concluir os pontos de mapa em seguida. Não marcar testes ou critérios automatizados como validados nesta execução.
