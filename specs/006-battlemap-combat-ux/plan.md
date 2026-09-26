# Implementation Plan: Aprimoramentos de Combate no Battlemap

**Branch**: `006-battlemap-combat-ux` | **Date**: 2026-09-25 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/006-battlemap-combat-ux/spec.md`

## Summary

Estende o battlemap existente com cinco capacidades de combate: (1) destaque visual do token cujo turno está ativo, calculado no cliente a partir de dados de iniciativa já disponíveis; (2) barra de vida semi-transparente acima de cada token, exigindo um novo campo de vida máxima em `Participant`/`Combatant`, a remoção da regra de privacidade de HP hoje aplicada em `DtoMapper.ToDto` (decisão explícita da spec, FR-009a), e — assim como o destaque de turno — resolvida inteiramente no cliente por junção com `activeEncounter.combatants`/`participants` já sincronizados, em vez de duplicada em `BattlemapTokenDto` (ver data-model.md, evita o token de vida ficar dessincronizado das mutações de HP, que hoje disparam `EncounterInvalidated`, não `BattlemapInvalidated`); (3) tamanho de token (1x1–5x5) e (4) condições de status, ambos guardados numa nova entidade por-token `BattlemapTokenAppearance` (mesmo padrão de `AttackArea`: uma linha opcional por token, ausência = tamanho 1x1 sem condições) e expostos no mesmo painel hoje usado para configurar área/projétil (`AttackAreaPanel.vue`); (5) tokens representativos adicionais controlados por jogadores, modelados como uma nova entidade leve `BattlemapMarker` que estende o padrão dual `ParticipantId?/CombatantId?` já usado por `BattlemapTokenPosition` e `AttackArea` para um terceiro slot `MarkerId?`. Toda mutação nova segue o padrão já estabelecido em `BattlemapService`: comando REST autenticado por `X-Participant-Token`, persistência via EF Core, e notificação `BattlemapInvalidatedAsync` (invalidar+refetch) via `CampaignHub`/`SignalRCampaignNotifier` — nenhum novo tipo de evento SignalR é necessário.

## Technical Context

**Language/Version**: C# / .NET 10 (backend, Clean Architecture: Domain/Application/Infrastructure/Api); TypeScript + Vue 3.5 / Nuxt 4 (frontend)

**Primary Dependencies**: ASP.NET Core Minimal APIs, SignalR (`CampaignHub`, `SignalRCampaignNotifier`), EF Core (`Companion.Infrastructure/Persistence`); Nuxt 4, Pinia (`stores/campaign.ts`), `@microsoft/signalr` client (`useCampaignRealtime.ts`)

**Storage**: Banco relacional via EF Core (mesma infraestrutura já usada por `Battlemap`/`BattlemapTokenPosition`/`AttackArea`); uma migration adiciona `MaxHitPoints` a `Participant` e `Combatant`, e duas novas tabelas (`BattlemapTokenAppearances`, `BattlemapMarkers`)

**Testing**: xUnit (`backend/tests/Companion.UnitTests`, `backend/tests/Companion.IntegrationTests`); Vitest (`frontend/tests/*.spec.ts`) e Playwright (`frontend/tests/e2e/battlemap.spec.ts`)

**Target Platform**: Web (PWA Nuxt), navegadores modernos em Windows e Android, mobile-first

**Project Type**: Web application (frontend Nuxt + backend ASP.NET Core, projeto já existente — não introduz novos processos/serviços)

**Performance Goals**: Propagação do destaque de turno e da barra de vida para os demais participantes em até 3s (SC-001/SC-003), mesmo orçamento já usado pelas áreas de ataque; abertura do menu de interação do token em até 2s a partir de um único clique/toque (SC-004)

**Constraints**: Painel de interação do token permanece compacto e tocável (mesma restrição de `AttackAreaPanel.vue` já validada em mobile); servidor é a autoridade do estado (Princípio II) — tamanho, condições e posição de marcadores só se tornam visíveis após confirmação do servidor, seguindo o mesmo padrão otimista-até-confirmar já usado por `MoveAsync`; a mudança de privacidade de HP (FR-009a) é uma decisão de produto explícita registrada na spec, não uma falha a corrigir silenciosamente

**Scale/Scope**: Escopo de uma sessão de campanha por vez (mesma escala do battlemap atual); um token (participante, criatura ou marcador) tem no máximo uma configuração de área e uma linha de aparência (tamanho + condições) ativas por vez

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Monólito modular e Clean Architecture**: PASS. Duas novas entidades de domínio (`BattlemapTokenAppearance`, `BattlemapMarker`) e um campo novo em duas entidades existentes (`Participant.MaxHitPoints`, `Combatant.MaxHitPoints`), todos dentro do agrupamento `Battlemaps`/`Campaigns` já existente no monólito (`Companion.Domain`/`Application`/`Infrastructure`/`Api`). Nenhum novo processo ou serviço distribuído.
- **II. Servidor como autoridade do jogo**: PASS. Turno atual e vida já são calculados e persistidos pelo backend (`Encounter.CurrentTurnIndex`, `Combatant.CurrentHitPoints`) — o destaque de turno é apenas uma leitura no cliente desses dados existentes, nunca um cálculo local. Tamanho, condições e posição de marcadores são comandos REST persistidos antes de qualquer notificação, seguindo exatamente o padrão de `SetAttackAreaAsync`/`MoveAsync`.
- **III. Experiência web responsiva e resiliente**: PASS. O painel de interação do token estendido reaproveita o layout mobile-first já validado por `AttackAreaPanel.vue`; a reconexão SignalR e o refetch por `BattlemapInvalidated` já cobrem os novos dados sem necessidade de novo código de reconexão.
- **IV. Qualidade verificável**: PASS. Novos testes unitários de domínio (`Participant`/`Combatant.MaxHitPoints`, `BattlemapTokenAppearance`, `BattlemapMarker`, autorização dono-ou-mestre) em `Companion.UnitTests`; testes de integração de API em `Companion.IntegrationTests` (mesmo padrão de `BattlemapApiTests`/`BattlemapSecurityApiTests`); testes de componente Vitest para `BattlemapToken.vue`/`BattlemapBoard.vue`/`AttackAreaPanel.vue`; extensão do E2E Playwright (`frontend/tests/e2e/battlemap.spec.ts`) para turno/HP/marcadores entre duas sessões.
- **V. Simplicidade operacional**: PASS, com uma decisão registrada — condições de status são persistidas como uma string delimitada de um enum (`StatusCondition`) em vez de uma tabela de junção própria, evitando uma tabela adicional para um conjunto pequeno e fixo de valores (mesmo espírito de simplicidade já aplicado a `AttackArea.Shape`).

Nenhuma violação identificada; seção "Complexity Tracking" não é necessária.

## Project Structure

### Documentation (this feature)

```text
specs/006-battlemap-combat-ux/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
backend/
├── src/
│   ├── Companion.Domain/Models/
│   │   ├── Participant.cs                       # + MaxHitPoints, validação em UpdateCombatProfile
│   │   ├── Combatant.cs                          # + MaxHitPoints, validação em Update/AddCombatant
│   │   ├── BattlemapTokenAppearance.cs           # nova entidade (tamanho 1-5, condições de status)
│   │   ├── BattlemapMarker.cs                    # nova entidade (token representativo do jogador)
│   │   └── StatusCondition.cs                    # novo enum (Stunned, Restrained, Blinded, Poisoned, Invisible, Prone)
│   ├── Companion.Application/Battlemaps/
│   │   └── BattlemapService.cs                   # + SetTokenAppearanceAsync; + CreateMarkerAsync/RemoveMarkerAsync;
│   │                                              #   ResolveTokenForMutation passa a resolver também MarkerId;
│   │                                              #   ToSnapshot inclui HP/tamanho/condições/marcadores
│   ├── Companion.Application/Encounters/
│   │   └── EncounterService.cs                    # AddCombatantAsync/RollInitiativeAsync/UpdateCombatantAsync passam MaxHitPoints
│   ├── Companion.Application/Campaigns/
│   │   └── CampaignService.cs                     # UpdateCombatProfileAsync aceita MaxHitPoints (tela inicial)
│   ├── Companion.Application/Contracts/
│   │   ├── Abstractions.cs
│   │   └── Dtos.cs                                # + MaxHitPoints em ParticipantDto/CombatantDto;
│   │                                               #   + tamanho/condições/isMarker/ownerParticipantId em BattlemapTokenDto
│   │                                               #   (sem HP — ver nota em data-model.md);
│   │                                               #   + SetTokenAppearanceRequest, CreateMarkerRequest
│   ├── Companion.Application/Contracts/
│   │   └── DtoMapper.cs                           # remove filtro de HP por observador (FR-009a)
│   ├── Companion.Infrastructure/Persistence/
│   │   ├── Migrations/                            # + AddMaxHitPoints, + AddTokenAppearanceAndMarkers
│   │   └── (EF configuration para as novas entidades/colunas)
│   ├── Companion.Api/Endpoints/
│   │   └── BattlemapEndpoints.cs                  # + PUT tokens/{tokenId}/appearance;
│   │                                               #   + POST markers, DELETE markers/{markerId}
│   └── Companion.Api/Realtime/
│       └── SignalRCampaignNotifier.cs             # reaproveitado sem alteração de contrato (BattlemapInvalidated)
└── tests/
    ├── Companion.UnitTests/
    │   ├── BattlemapTests.cs                       # + tamanho/condições/marcadores
    │   ├── AttackAreaTests.cs                       # inalterado (referência de padrão)
    │   └── CombatantTests.cs                        # + MaxHitPoints (novo ou existente arquivo de combatente)
    └── Companion.IntegrationTests/
        ├── BattlemapApiTests.cs                     # + appearance, markers, HP sempre visível
        └── BattlemapSecurityApiTests.cs             # + autorização dono-ou-mestre para appearance/markers

frontend/
├── app/
│   ├── types/campaign.ts                          # + currentHitPoints/maxHitPoints/size/statusConditions/isMarker/ownerParticipantId em BattlemapToken; + StatusCondition; + SetTokenAppearanceRequest
│   ├── components/battlemap/
│   │   ├── BattlemapBoard.vue                      # destaque de turno E vida (cruza combatants[].isCurrent/HP e
│   │   │                                            # participants[].HP com tokens — mesma junção local para os dois),
│   │   │                                            # renderização com footprint (N células), botão "Inserir token",
│   │   │                                            # encaminha appearance/marker para o composable/API
│   │   ├── BattlemapToken.vue                       # + barra de HP (recebida via props resolvidas em BattlemapBoard),
│   │   │                                            # + anel de turno, + ícones de condição,
│   │   │                                            # raio derivado do tamanho (footprint) em vez de fixo
│   │   └── AttackAreaPanel.vue                      # + seção de tamanho (1x1-5x5), + seção de condições de status
│   ├── composables/
│   │   ├── useAttackAreas.ts                        # inalterado (referência de padrão otimista)
│   │   └── useBattlemapMarkers.ts                   # novo — cria/reposiciona/remove marcadores (mesmo padrão pendente/otimista)
│   └── components/
│       └── InitiativeTracker.vue                    # sem alteração de template — HP de terceiros passa a aparecer
│                                                     # automaticamente quando o backend para de filtrar (FR-009a)
└── tests/
    ├── battlemap.spec.ts                            # + footprint, HP bar, turn ring, status icons
    ├── battlemap-realtime.spec.ts                   # + sincronização de HP/turno/marcadores entre duas instâncias
    └── e2e/battlemap.spec.ts                        # + fluxo completo: turno, HP, tamanho, condição, marcador
```

**Structure Decision**: Web application existente (Nuxt 4 + ASP.NET Core), sem novos projetos/pastas de nível superior. A extensão do menu de interação do token é feita no próprio `AttackAreaPanel.vue` (adicionando seções, não um novo componente), porque o painel já é o único ponto de entrada de configuração por-token e a spec exige que tamanho e condições vivam "no mesmo menu". Tokens representativos reaproveitam a infraestrutura de posição/área/aparência já existente por meio de um terceiro identificador opcional (`MarkerId`), em vez de duplicar essas tabelas para um novo tipo de token.

## Complexity Tracking

Nenhuma violação da constituição identificada — seção não aplicável.
