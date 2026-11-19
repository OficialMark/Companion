# Tasks: Ficha pessoal, inventário e rolagens do jogador

**Input**: Design documents from `specs/011-ficha-jogador/`
**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/), [quickstart.md](quickstart.md)

**Tests**: obrigatórios. Exigidos pela constituição (IV), pelo CLAUDE.md (toda mudança de comportamento vem com teste) e pelos critérios SC-002 a SC-011. Nas tarefas de cada história, os testes vêm antes da implementação e têm que falhar antes dela.

**Ambiente**: nenhuma tarefa usa o `docker compose` do projeto nem toca `companion-frontend-1`, `companion-api-1` ou `companion-sqlserver-1`. Integração roda com Testcontainers; validação manual e e2e rodam na instância própria do quickstart (portas 14333, 5311 e 3611).

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Setup

- [X] T001 Bootstrap do worktree: `pnpm install` em `frontend/`, `dotnet restore backend/Companion.slnx`. Rodar `dotnet test backend/tests/Companion.UnitTests` e `pnpm --dir frontend test` e registrar a linha de base (quantos passam) em `specs/011-ficha-jogador/checklists/baseline.md`
- [X] T002 [P] Tornar o proxy de dev configurável: `NUXT_API_TARGET` (padrão `http://localhost:8080`) para `/api` e `/hubs` em `frontend/nuxt.config.ts`; documentar em `README.md`
- [X] T003 [P] Guarda do e2e: em `frontend/playwright.config.ts`, os specs novos (`tests/e2e/player-*.spec.ts`) falham na inicialização se `E2E_BASE_URL` não estiver definido ou apontar para a porta 3636. Teste em `frontend/tests/e2e-guard.spec.ts` (Vitest) para a função de checagem extraída em `frontend/tests/e2e/guard.ts`
- [X] T004 [P] Criar `backend/tests/Fixtures/` com `dice-expressions.cases.json` e `character-rules.cases.json` vazios (schema documentado no topo de cada arquivo) e copiar ambos para a saída dos testes em `backend/tests/Companion.UnitTests/Companion.UnitTests.csproj`

## Phase 2: Foundational (bloqueia todas as histórias)

### Gramática de dados (contracts/dice-expression.md)

- [X] T005 [P] Preencher `backend/tests/Fixtures/dice-expressions.cases.json` com pelo menos 40 casos: entrada, canônica, válido/erro e mensagem. Incluir todas as entradas legadas de `DiceExpressionTests.cs`, seleção, repetição com colchetes, as duas combinadas, rótulos com acento, rótulo negativo, limites (100 dados contando as duas execuções, 20 repetições, 256 caracteres) e os erros da tabela do contrato
- [X] T006 Testes `backend/tests/Companion.UnitTests/DiceExpressionGroupingTests.cs`, guiados pelo arquivo de casos (parse, `Normalized` e mensagens). Devem falhar
- [X] T007 Reescrever `backend/src/Companion.Domain/Dice/DiceExpression.cs` como analisador recursivo (repetição, seleção, termos rotulados), mantendo o `Normalized` legado byte a byte. `DiceExpressionTests.cs` passa **sem alteração**, e T006 passa
- [X] T008 Testes de execução com `IDiceRandomizer` determinístico em `backend/tests/Companion.UnitTests/DiceRollExecutionTests.cs`. Cobrir: vantagem rola todos os termos duas vezes e escolhe o maior; desvantagem escolhe o menor; empate fica com a primeira execução; 3#[…] com seleção gera 3 resultados de 2 execuções cada; `Total` é o da 1ª repetição
- [X] T009 Extrair a execução para `backend/src/Companion.Domain/Dice/DiceRoller.cs` (pura, recebe `IDiceRandomizer`) e usá-la em `backend/src/Companion.Application/DiceRolls/DiceRollService.cs`. Estender `DiceRollResultDto` com `modifiers`, `selection`, `executions` e `selectedExecution` opcionais em `backend/src/Companion.Application/Contracts/Dtos.cs`. Nota: o `DiceRoller` ficou em `backend/src/Companion.Application/DiceRolls/DiceRoller.cs`, porque `IDiceRandomizer` é declarado na Application e o Domain não depende dela
- [X] T010 Integração `backend/tests/Companion.IntegrationTests/DiceRollGroupingApiTests.cs`: POST de vantagem com randomizador falso registrado na factory; histórico do snapshot traz as execuções; expressão de 200 caracteres grava; legado `3#d20+5` devolve o mesmo JSON de antes (snapshot do formato). Nota: o caso de 200 caracteres está com `Skip` até T019 alargar `DiceRolls.Expression` para 256; tirar o `Skip` em T019
- [X] T011 [P] Gerador `frontend/app/utils/rollExpression.ts` (`buildExpression({dice, parts, extras, selection, repetitions})` → string canônica) e testes `frontend/tests/roll-expression.spec.ts` lendo `../backend/tests/Fixtures/dice-expressions.cases.json`. Para cada caso válido gerável, o gerador produz exatamente a canônica
- [X] T012 [P] `frontend/app/components/RollHistory.vue` mostra as execuções da seleção (escolhida com `[data-selected-execution]`, rótulo "maior"/"menor") e os modificadores rotulados `(FOR) 3`. Tipos em `frontend/app/types/campaign.ts`. Teste em `frontend/tests/roll-history.spec.ts` (novo caso; os existentes continuam passando)

### Contas e sessões (R3–R5)

- [X] T013 [P] `PersonName.Normalize` em `backend/src/Companion.Domain/Characters/PersonName.cs` e testes `backend/tests/Companion.UnitTests/PersonNameTests.cs`: "loren b. vonn neuman" vira "Loren B. Vonn Neuman"; espaços; hífen ("Jean-Luc"); apóstrofo ("D'artagnan"); acentos; vazio; mais de 40 caracteres; `NameKey` em maiúsculas invariantes
- [X] T014 Entidade `ParticipantSession` em `backend/src/Companion.Domain/Models/ParticipantSession.cs`; `Participant.NameKey` e `Rename(normalizado)` em `Participant.cs`; registrar no `ICompanionDbContext` (`backend/src/Companion.Application/Contracts/Abstractions.cs`), em `CompanionDbContext.cs` (incluindo a limpeza em `DeleteCampaignAggregateAsync`) e em `ModelConfigurations.cs` (índice único em `TokenHash` da sessão; índice único filtrado `(CampaignId, Role, NameKey) WHERE NameKey IS NOT NULL`)
- [X] T015 `ParticipantAuthorization.Require` (`backend/src/Companion.Application/Campaigns/ParticipantAuthorization.cs`) passa a buscar pela sessão via índice e conferir `CampaignId`. O SignalR `CampaignHub.JoinCampaign` continua usando `Require`. `LastSeenAt` é atualizado no máximo 1x por hora
- [X] T016 `JoinAsync` e `CreateAsync` em `backend/src/Companion.Application/Campaigns/CampaignService.cs`: normalizar o nome; procurar a conta por `(CampaignId, Role, NameKey)`; existe → só nova sessão; não existe → participante e sessão; na violação do índice único (corrida), reler e usar a vencedora; `ParticipantJoined` só na criação
- [X] T017 `DELETE /api/campaigns/{id}/participants/me/session` em `backend/src/Companion.Api/Endpoints/CampaignEndpoints.cs`, que apaga só a sessão do token usado
- [X] T018 `ParticipantNameBackfill` em `backend/src/Companion.Application/Campaigns/ParticipantNameBackfill.cs`, chamado em `backend/src/Companion.Api/Program.cs` depois de `MigrateAsync` e antes do seeder: idempotente; só o mais recente de cada grupo recebe `NameKey`; uma falha é logada sem derrubar a API

## Phase 3: User Story 2b - Entrar na conta do personagem pelo nome (P1)

**Goal**: o mesmo código mais o mesmo nome levam à mesma conta, com várias sessões e sem perda para campanhas antigas.
**Independent Test**: entrar como "loren b. vonn neuman", depois como "LOREN B. VONN NEUMAN" em outro navegador: mesmo `participantId`, nome "Loren B. Vonn Neuman", e o primeiro continua válido.

- [X] T019 [US2b] Migration `AddPlayerCharacterSheet` (parte de contas) em `backend/src/Companion.Infrastructure/Persistence/Migrations/`: tabela `ParticipantSessions`, coluna `NameKey`, índices, `INSERT INTO ParticipantSessions SELECT … FROM Participants` com o `TokenHash` atual e `DiceRolls.Expression` com 256 caracteres. Gerada com `COMPANION_DESIGN_CONNECTION` apontando para o banco descartável, nunca para o banco em uso (parte de contas; demais tabelas entram depois)
- [X] T020 [US2b] Integração `backend/tests/Companion.IntegrationTests/AccountJoinTests.cs` cobrindo os 6 cenários da US 2b, mais uma corrida de 10 joins simultâneos com nome novo que resulta em 1 conta. Inclui um teste de que o "Sair" apaga uma sessão e a outra continua
- [X] T021 [US2b] Integração `backend/tests/Companion.IntegrationTests/MigrationBackfillTests.cs`. Semeia um banco no esquema anterior à migration (participantes repetidos e em minúsculas, com tokens), aplica a migration e o backfill e verifica: zero linhas perdidas, todo token antigo continua autorizando, nomes normalizados e `NameKey` só no mais recente. Rodar o backfill duas vezes dá o mesmo resultado
- [X] T022 [P] [US2b] Frontend: `frontend/app/components/UserMenu.vue` e a ação de sair em `frontend/app/stores/campaign.ts` chamam `DELETE …/me/session` antes de limpar o `localStorage`, e a falha de rede não impede a saída local. `frontend/app/components/CampaignAccess.vue` mostra a dica "Use o mesmo nome para voltar ao seu personagem.". Teste em `frontend/tests/campaign-access.spec.ts`

## Phase 4: User Story 1 - Abrir e preencher a própria ficha (P1) 🎯 MVP

**Goal**: o jogador abre "Ficha" na barra superior, edita os campos-base e vê os derivados calculados pelo servidor.
**Independent Test**: DES 14→16 e nível 4→5 mudam o modificador, a iniciativa, a CA sem armadura, as perícias de DES e a proficiência; ao recarregar, os valores continuam.

- [X] T023 [P] [US1] Domínio `backend/src/Companion.Domain/Characters/`: `AbilityCode.cs`, `Skills.cs` (18 perícias e o atributo de cada uma, em PT-BR), `CharacterSheet.cs` (validações do data-model, `UpdateFrom(input)`), `CharacterStats.cs`
- [X] T024 [US1] Casos de regra da ficha em `backend/tests/Fixtures/character-rules.cases.json`, já com os de inventário para não reescrever depois. Pelo menos 30 casos: modificadores 1..30, proficiência por nível nas fronteiras 4/5, 8/9, 12/13 e 16/17, salvaguardas, perícia com especialização, percepção passiva, CA sem armadura com e sem `UnarmoredAbility`, leve, média com DES 18 (+2), pesada, escudo, efeitos que somam, efeito duplicado suprimido, `min` abaixo e acima do base, desequipar restaura, PV máximo final menor que os atuais, CD e ataque mágico, ataque com arma de acuidade, à distância e com `abilityOverride`, proficiência da arma independente da geral, armadura sem proficiência ativando `disadvantageOnStrDex`
- [X] T025 [US1] Testes `backend/tests/Companion.UnitTests/CharacterRulesTests.cs` guiados pelos casos de T024. Devem falhar
- [X] T026 [US1] `CharacterRules.Compute` em `backend/src/Companion.Domain/Characters/CharacterRules.cs`, com a composição (`parts`) de cada valor, até T025 passar
- [ ] T027 [US1] Persistência da ficha: `CharacterSheet` no DbContext e na interface, configuração EF (colunas JSON com `ValueComparer`, `rowversion`, PK = `ParticipantId`, cascade) e migration (parte da ficha) em `backend/src/Companion.Infrastructure/Persistence/`
- [ ] T028 [US1] `CharacterService` (`GetOrCreateAsync`, `PatchAsync` com versão → 409) e `CharacterDtos.cs`/`CharacterMapper.cs` em `backend/src/Companion.Application/Characters/`. Depois do commit, sincroniza `Participant.UpdateCombatProfile` com iniciativa e PV da ficha e emite `ParticipantUpdated` e `CharacterChanged`. Registrar em `Program.cs`
- [ ] T029 [US1] `backend/src/Companion.Api/Endpoints/CharacterEndpoints.cs`: `GET` e `PATCH …/participants/me/character`; `MapCharacterEndpoints` em `Program.cs`
- [ ] T030 [US1] Integração `backend/tests/Companion.IntegrationTests/CharacterSheetApiTests.cs`: criação idempotente no primeiro GET (2 GETs simultâneos geram 1 ficha), PATCH devolve `stats` recalculados, 409 com versão velha, `ParticipantUpdated` com a nova iniciativa
- [ ] T031 [P] [US1] Navegação: renomear `MasterViewNav.vue` para `frontend/app/components/ViewNav.vue` e `useMasterView.ts` para `frontend/app/composables/useAppView.ts`, com as opções do papel (`ficha` só para jogador, `lucide:scroll-text`). Manter `data-master-nav` no nav do mestre e atualizar `frontend/app/pages/index.vue`. Testes em `frontend/tests/view-nav.spec.ts`; `frontend/tests/compendium-nav.spec.ts` passa sem mudança de asserção
- [ ] T032 [P] [US1] `frontend/app/types/character.ts`, `frontend/app/stores/character.ts` (carregar, aplicar `CharacterDto`, recarregar no `CharacterChanged` do próprio `me.id`) e as chamadas novas em `frontend/app/composables/useCompanionApi.ts`
- [ ] T033 [US1] `frontend/app/components/character/CharacterSheetView.vue`, aba Ficha: campos `[data-field]`, derivados `[data-derived]` com popover de composição, gravação automática de 400 ms com `[data-save-state]` (salvo/salvando/erro/conflito), `[data-stale]` enquanto espera. Estilos em `frontend/app/assets/css/character.css`, registrado em `nuxt.config.ts`. Testes em `frontend/tests/character-sheet.spec.ts`, incluindo a garantia de que nenhum derivado é calculado no navegador

## Phase 5: User Story 2 - Ninguém mexe na ficha dos outros (P1)

**Goal**: o dono é sempre quem está na sessão; ids de outros donos não vazam.
**Independent Test**: com A, B e C (outra campanha), toda tentativa cruzada é recusada e nada muda.

- [ ] T034 [US2] Integração `backend/tests/Companion.IntegrationTests/CharacterOwnershipTests.cs`, com uma matriz que cresce a cada história. Cada rota de `contracts/character-api.md` é testada contra: id de item, magia ou rolagem de B usado por A (404, e B não muda); mestre (403); sem token (401); token de outra campanha (401). Nesta fase cobre GET e PATCH; T042, T051 e T063 acrescentam as rotas novas
- [ ] T035 [US2] Um endereço `?tela=ficha` aberto pelo mestre e `?tela=compendio` aberto pelo jogador caem na Mesa. Teste em `frontend/tests/view-nav.spec.ts`

## Phase 6: User Story 3 - Inventário e equipamentos que mudam a ficha (P1)

**Goal**: CRUD de itens, equipar com os limites, efeitos que somam sem duplicar e base preservada.
**Independent Test**: a sequência de CA 12, 16, 19, 20, 17 da spec, com os atributos-base iguais aos do início.

- [ ] T036 [P] [US3] Domínio: `InventoryItem.cs` e `ItemEffect.cs` (tipos, alvos, operações, validação, `DuplicateKey`) em `backend/src/Companion.Domain/Characters/`, e `CharacterSheet.Equip(item, equipped)` com o limite de armadura e escudo. Testes em `backend/tests/Companion.UnitTests/InventoryRulesTests.cs`: armadura troca, escudo troca, várias armas, poção não equipa, validação de efeito por tipo
- [ ] T037 [US3] Persistência de `InventoryItem` (blocos JSON de arma, armadura, escudo e efeitos, `rowversion`, índice por `ParticipantId`) e migration (parte do inventário)
- [ ] T038 [US3] `CharacterService`: criar, editar, equipar e remover item. O equipar devolve `stats.notices` quando outro item sai. A marcação `Proficient` inicial vem das proficiências gerais
- [ ] T039 [US3] Rotas `…/me/character/items` (POST, PUT, POST equip, DELETE) em `CharacterEndpoints.cs`
- [ ] T040 [US3] Integração `backend/tests/Companion.IntegrationTests/InventoryApiTests.cs`: sequência de CA da US3, efeito duplicado suprimido e reativado ao desequipar o outro, base intacta, `min` de FOR, aviso de desequipar
- [ ] T041 [US3] `frontend/app/components/character/InventoryPanel.vue` e `ItemEditorModal.vue` (blocos por categoria; editor de efeitos tipo → alvo → operação → valor; `[data-equip]`, `[data-proficient]`, `[data-suppressed]`). Testes em `frontend/tests/inventory-panel.spec.ts`
- [ ] T042 [US3] Acrescentar as rotas de itens à matriz de T034 em `CharacterOwnershipTests.cs`

## Phase 7: User Story 4 - Rolar pelo painel do jogador no Modo Jogo (P1)

**Goal**: painel com dados, fonte, tipo, adicionais, vantagem e repetição; expressão ao vivo; mochila editável no Modo Jogo.
**Independent Test**: Espada Longa +1 com FOR 16 e nível 5 mostra `d20 + (FOR) 3 + (prof) 3 + (arma) 1`; com vantagem, mostra a forma com `2#(…) maior` e o histórico traz as duas execuções.

- [ ] T043 [P] [US4] `frontend/app/composables/useLongPress.ts` (500 ms, cancela o clique, cancela ao sair do alvo) e `frontend/app/components/character/DicePad.vue` (clique +1, segurar −1, `Delete`/`Backspace`/`-` −1, limpar, contador, alvos de 44 px). Testes em `frontend/tests/dice-pad.spec.ts` com timers falsos
- [ ] T044 [US4] `frontend/app/components/character/PlayerRollPanel.vue`: fonte (arma equipada, magia, atributo, perícia, salvaguarda), tipo, adicionais, vantagem (exclusiva), repetições de 1 a 20, `[data-expression]` montada só com `rollExpression.ts` e `stats.weaponRolls`/`spellRolls`/`skills`, desvantagem pré-selecionada e sinalizada quando `rollDefaults.disadvantageOnStrDex`, aviso antes de enviar se passar dos limites. Rolagem pela rota de dados existente, com rótulo descritivo. Testes em `frontend/tests/player-roll-panel.spec.ts`
- [ ] T045 [US4] Integração no Modo Jogo em `frontend/app/pages/index.vue`: jogador com ficha vê `PlayerRollPanel` no lugar do `QuickDiceRoller`, e jogador sem ficha mantém o `QuickDiceRoller`. Um ataque com arma, sendo combatente num encontro ativo, abre o `AttackConfirmModal`, e "acertou" abre o `DamageRollModal` com o dano da mesma arma. Botão "Mochila" abre o `InventoryPanel` num modal sem sair do Modo Jogo. Testes em `frontend/tests/game-mode.spec.ts`, com os casos existentes intactos

## Phase 8: User Story 5 - Vantagem, desvantagem e repetição com a mesma semântica (P1)

**Goal**: a tela e o servidor executam a mesma estrutura.
**Independent Test**: com dados controlados, `d20 + 5 + 2d6` com vantagem, com desvantagem e com 3 repetições mais vantagem.

- [ ] T046 [US5] Teste de ponta a ponta da semântica em `backend/tests/Companion.IntegrationTests/DiceRollGroupingApiTests.cs`. Para cada caso de `dice-expressions.cases.json` marcado `"roundTrip": true`, a string gerada pelo frontend (o campo `canonical`) é aceita pelo servidor e volta idêntica em `DiceRollDto.expression`
- [ ] T047 [US5] Teste do painel em `frontend/tests/player-roll-panel.spec.ts`: as combinações de vantagem e repetição produzem as formas da US5 (cenários 1 a 6), e o aviso de limite aparece antes do envio

## Phase 9: User Story 6 - Rolagens salvas (P2)

**Goal**: criar, editar, duplicar, apagar e usar rolagens com nome e ícone, sempre com os valores atuais.
**Independent Test**: "Ataque com espada" muda junto quando a FOR muda; o ícone persiste depois de recarregar; a arma removida marca a rolagem como incompleta.

- [ ] T048 [P] [US6] `SavedRoll.cs` e `SavedRollIcons.cs` (os 16 ícones de `contracts/ui-interaction.md`) no domínio, com validação. Testes em `backend/tests/Companion.UnitTests/SavedRollTests.cs`
- [ ] T049 [US6] Persistência, migration (parte das rolagens), serviço (o `incomplete` é calculado na leitura) e rotas `…/me/character/rolls` (POST, PUT, duplicate, DELETE)
- [ ] T050 [US6] Integração `backend/tests/Companion.IntegrationTests/SavedRollApiTests.cs`: CRUD, ícone fora da lista dá 400, item de outro dono dá 400, remover a arma marca `incomplete`
- [ ] T051 [US6] Acrescentar as rotas de rolagens à matriz de T034
- [ ] T052 [P] [US6] `frontend/app/utils/savedRollIcons.ts` e um teste que compara a lista com `SavedRollIcons.cs` lendo o arquivo C# (`frontend/tests/saved-roll-icons.spec.ts`)
- [ ] T053 [US6] `frontend/app/components/character/SavedRollEditor.vue`, que reaproveita `DicePad` e os mesmos controles do painel, para cumprir o FR-036. A aba Rolagens fica na ficha; no painel, uma barra `[data-saved-roll]` mostra `[data-incomplete]`. Testes em `frontend/tests/saved-roll-editor.spec.ts`: para a mesma configuração, o editor e o painel mostram a mesma `[data-expression]`

## Phase 10: User Story 7 - Catálogo de itens e de magias (P2)

**Goal**: catálogos importados das fontes decididas, busca, cópia para a ficha e itens e magias personalizados.
**Independent Test**: Espada Longa vem com 1d8 cortante e versátil 1d10; Bola de Fogo vem nível 3 com 8d6; o item personalizado "Amuleto da avó" fica disponível sem o catálogo.

### Importadores (fora do app; podem começar em paralelo à Phase 2)

- [ ] T054 [P] [US7] `scripts/import-catalogs/parse-spells.mjs` (função pura) e `import-spells.mjs`: download com cache, no máximo 2 requisições, User-Agent genérico, `--offline`. Fixtures em `scripts/import-catalogs/fixtures/` com as primeiras 5 magias reais de `spells.js` mais Bola de Fogo. Testes em `parse-spells.test.mjs`: contagens por nível (27, 63, 59, 49, 35, 42, 32, 20, 18, 16), classes batendo 100%, ritual 32, concentração 154, dano sugerido, tipo de rolagem (ataque ou salvaguarda) e HTML filtrado. Uma estrutura inesperada falha sem escrever o JSON. Saída: `backend/src/Companion.Infrastructure/Persistence/Seed/spells.magias5e.json`, sem campo de crédito
- [ ] T055 [P] [US7] `scripts/import-catalogs/glossary.mjs` (propriedades, maestrias, tipos de dano, raridades e categorias em PT-BR, como tabela fixa) e `units.mjs` (pés para metros na regra 5 pés = 1,5 m; libras para kg na regra 1 lb = 0,5 kg), com testes em `glossary.test.mjs` e `units.test.mjs`
- [ ] T056 [US7] `scripts/import-catalogs/map-items.mjs` e `import-items.mjs`. Lêem os dois JSONs 2024 do `5e-bits/5e-srd-api` fixados num commit (SHA registrado no README) e mapeiam para o formato de `CatalogItem`. Normalizam raridade (incluindo "Rarity Varies" e variantes +1/+2/+3, gerando um item por variante com `MagicBonus`), sintonização, blocos de arma, armadura e escudo, e efeitos óbvios (Anel e Manto de Proteção, Armadura e Escudo +N, Manoplas de Força do Ogro com FOR `min` 19, Cinto de Força do Gigante, Poções de Cura como texto mais o dado de cura). Testes em `map-items.test.mjs` com fixtures reais
- [ ] T057 [US7] Tradução congelada em `scripts/import-catalogs/translations/items.pt-BR.json` (cerca de 459 nomes e 38 mil palavras), em lotes de cerca de 40 itens por subagente, cada um com o glossário de T055 e as regras de unidade. Os subagentes que escrevem em paralelo usam `isolation: "worktree"`, cada lote num arquivo `translations/batches/NNN.json`, e a junção é sequencial
- [ ] T058 [US7] Validador em `scripts/import-catalogs/validate-translation.mjs`, com teste. Exige um `index` traduzido para cada item da fonte; o multiconjunto de números e notações de dados idêntico ao original depois da conversão de unidades; nenhum termo mecânico fora do glossário; e nenhuma palavra em inglês da lista de termos de regra. O `import-items.mjs` chama o validador e falha sem escrever o JSON. Saída: `backend/src/Companion.Infrastructure/Persistence/Seed/items.srd52.json`, com o texto de atribuição CC-BY-4.0 do SRD 5.2.1. Relatório em `scripts/import-catalogs/last-run-report.md` com as contagens contra a fonte
- [ ] T059 [P] [US7] `scripts/import-catalogs/README.md`: como rodar, a política de fontes (sem crédito para magias, atribuição CC-BY para itens) e como refazer um lote de tradução

### App

- [ ] T060 [US7] Domínio `backend/src/Companion.Domain/Catalog/CatalogItem.cs` e `CatalogSpell.cs`; `CharacterSpell.cs` em `Characters/`; persistência e migration (parte dos catálogos e das magias); `EmbeddedItemCatalogSeedSource.cs` e `EmbeddedSpellCatalogSeedSource.cs` em `backend/src/Companion.Infrastructure/Catalog/` (os JSONs como `EmbeddedResource` no `.csproj`); `CatalogSeeder.cs` em `backend/src/Companion.Application/Catalog/`, idempotente por hash, chamado no startup sem derrubar a API. Testes em `backend/tests/Companion.UnitTests/CatalogSeederTests.cs` (mesmo hash não escreve nada, item sumido é marcado, linha inválida é pulada)
- [ ] T061 [US7] `CatalogService` e `backend/src/Companion.Api/Endpoints/CatalogEndpoints.cs` (busca por `SearchKey`, filtros, paginação, `attribution` só nos itens). `CharacterService` ganha: item do catálogo (cópia), magias com CRUD, magia do catálogo (409 se repetida) e dano da magia em `stats.spellRolls`
- [ ] T062 [US7] Integração em `backend/tests/Companion.IntegrationTests/CatalogApiTests.cs`: busca sem acento, filtros, cópia independente do catálogo, catálogo atualizado não muda a cópia, item personalizado sobrevive a um seed vazio, fonte "fora do ar" (seed source que lança exceção) mantém a API saudável
- [ ] T063 [US7] Acrescentar as rotas de magias e de "do catálogo" à matriz de T034
- [ ] T064 [US7] `frontend/app/components/character/CatalogPicker.vue` (itens e magias, busca com espera, filtros, paginação, "Adicionar", atribuição só nos itens) e `SpellList.vue` (aba Magias, editor de magia personalizada, CD e ataque mágico do `stats`). Testes em `frontend/tests/catalog-picker.spec.ts` e `spell-list.spec.ts`

## Phase 11: Polish & Cross-Cutting

- [ ] T065 [P] Remover o crédito ao Pocket DM de `frontend/app/components/creatures/CreatureLibrary.vue:126` (FR-040), ajustar os testes que o verificam e registrar a decisão em `scripts/import-creatures/README.md`
- [ ] T066 E2E `frontend/tests/e2e/player-sheet.spec.ts`, nos projetos desktop e Pixel 7, contra `E2E_BASE_URL` da instância própria. Cobre o roteiro do quickstart, os passos 1 a 8: conta por nome em 2 contextos, ficha, mochila com CA, Modo Jogo com vantagem e repetição, rolagem salva e o mestre intacto. Verifica alvos de 44 px e ausência de rolagem horizontal em 375 px, no padrão de `compendium-sheet.spec.ts`
- [ ] T067 Rodar a suíte completa: `dotnet test backend/Companion.slnx`, `pnpm --dir frontend test`, `pnpm --dir frontend exec nuxt typecheck`, `node --test scripts/import-catalogs/ scripts/import-creatures/`, e o e2e da feature junto com `compendium-sheet` e `token-sheet-shortcut` na instância própria. Registrar os números em `specs/011-ficha-jogador/checklists/baseline.md` e compará-los com T001
- [ ] T068 Revisão crítica fria (CLAUDE.md, "Fan-out + harsh critic"). Agentes sem contexto da construção, um por eixo: (a) segurança e posse, tentando ler ou alterar a ficha de outro; (b) atacante de regras, re-quebrando CA, duplicidade, proficiência e vantagem com entradas vizinhas; (c) UX em 1280 e 375 px contra a rubrica do SC-010; (d) leitura do diff da migration e do backfill. Cada achado vira tarefa nova neste arquivo, e as evidências ficam no scratchpad da sessão
- [ ] T069 Corrigir os achados do crítico até ele aprovar, com teste de regressão para cada um
- [ ] T070 Commit, rebase em `origin/main`, push, PR e merge na `main` (memória `always-merge-to-main`). **Não** reconstruir nem reiniciar os containers em uso. Entregar ao Marcos os comandos de implantação: exportar `Participants` primeiro, depois `docker compose up -d --build api frontend`, que é dele de executar

## Dependencies & Execution Order

- Phase 1 → Phase 2. Dentro da Phase 2 há dois trilhos independentes: gramática (T005–T012) e contas (T013–T018). Exceção: o caso de 200 caracteres de T010 depende do alargamento de `DiceRolls.Expression` em T019.
- US2b (T019–T022) depende das contas. US1 (T023–T033) depende de T019 (migration base) e de T018.
- US2 (T034–T035) depende de US1, e sua matriz cresce com T042, T051 e T063.
- US3 depende de US1. US4 depende de US3 e da gramática. US5 depende de US4. US6 depende de US4.
- US7: os importadores (T054–T059) não dependem de nada do app e podem rodar desde a Phase 2. O app de catálogo (T060–T064) depende de US3.
- T065 é independente. T066 depende de todas as histórias. Depois vêm T067, T068, T069 e por fim T070.
- **Migration única**: T019, T027, T037, T049 e T060 editam a mesma migration `AddPlayerCharacterSheet` em sequência (ou geram uma migration por parte, se o tempo entre elas exigir). Nunca em paralelo.

## Parallel Example

```text
Trilho A (gramática):  T005 → T006 → T007 → T008 → T009 → T010 ; T011 || T012
Trilho B (contas):     T013 → T014 → T015 → T016 → T017 → T018
Trilho C (importadores): T054 || T055 → T056 → T057 (lotes em worktrees isolados) → T058 ; T059
Na US1: T031 (nav) || T032 (store) || T023 (domínio)
Na US3/US4: T043 (DicePad) || T036 (domínio do inventário)
Polish: T065 || qualquer história
```

## Implementation Strategy

- **MVP**: Phase 2 + US2b + US1 + US2. O jogador entra pelo nome, tem ficha própria e protegida, e os derivados são calculados no servidor. Isso já entrega valor e é a base de tudo.
- **Incremento 2**: US3 (mochila e efeitos), depois US4 e US5 (painel e semântica de dados), que fecham os P1.
- **Incremento 3**: US6 (rolagens salvas) e US7 (catálogos). Os importadores e a tradução correm em paralelo desde o começo, porque são o caminho mais longo.
- Cada incremento termina com os testes da história verdes e a suíte existente intacta. Um único PR no fim, com merge na `main` por T070. A troca dos containers fica com o Marcos.
