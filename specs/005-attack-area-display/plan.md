# Implementation Plan: Exibição de Área de Ataque no Battlemap

**Branch**: `005-attack-area-display` | **Date**: 2026-09-12 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/005-attack-area-display/spec.md`

## Summary

Adicionar ao battlemap a capacidade de qualquer participante desenhar uma "área de ataque" (círculo, quadrado, cone ou cilindro/feixe) ancorada a um token, configurável por sliders compactos (raio/distância, largura, direção), sincronizada em tempo real via SignalR para todos os participantes da sessão, seguindo o mesmo padrão de comando REST + notificação usado por `BattlemapService`/`BattlemapEndpoints`/`SignalRCampaignNotifier`. Também corrige o bug em que o editor de HP/iniciativa (`InitiativeTracker.vue`) permanece editável durante o modo de combate — deve ficar restrito ao modo de configuração (setup), controlado por `useGameMode`.

## Technical Context

**Language/Version**: C# / .NET 10 (backend, Clean Architecture: Domain/Application/Infrastructure/Api); TypeScript + Vue 3.5 / Nuxt 4 (frontend)

**Primary Dependencies**: ASP.NET Core Minimal APIs, SignalR (`CampaignHub`, `SignalRCampaignNotifier`), EF Core (Infrastructure); Nuxt 4, Pinia (`stores/campaign.ts`), `@microsoft/signalr` client (`useCampaignRealtime.ts`)

**Storage**: Banco relacional via EF Core (mesma infraestrutura já usada por `Battlemap`/`BattlemapTokenPosition`); nova tabela para as áreas de ataque

**Testing**: xUnit (`backend/tests/Companion.UnitTests`); Vitest (`frontend/tests/*.spec.ts`) e Playwright (`test:e2e`)

**Target Platform**: Web (PWA Nuxt), navegadores modernos em Windows e Android, mobile-first

**Project Type**: Web application (frontend Nuxt + backend ASP.NET Core, projeto já existente — não introduz novos processos/serviços)

**Performance Goals**: Propagação de criação/ajuste/remoção de área para os demais participantes em até 3s (SC-002); atualização do desenho local em tempo real (<100ms) enquanto o slider é arrastado

**Constraints**: Painel de configuração compacto (≤ 1/3 da altura da tela em mobile, SC-003), utilizável por toque e mouse; servidor é a autoridade do estado (Princípio II da constituição) — o desenho local otimista deve ser confirmado pelo evento do servidor

**Scale/Scope**: Escopo de uma sessão de campanha por vez (mesma escala do battlemap atual); no máximo uma área ativa por token

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Monólito modular e Clean Architecture**: PASS. A feature adiciona uma entidade de domínio (`AttackArea`) e um serviço de aplicação no monólito existente (`Companion.Domain`/`Companion.Application`/`Companion.Infrastructure`/`Companion.Api`), seguindo o mesmo agrupamento por domínio (`Battlemaps`) já usado por `BattlemapService`. Nenhum novo processo ou serviço distribuído é introduzido.
- **II. Servidor como autoridade do jogo**: PASS, com atenção de design. Embora "área de ataque" seja principalmente visual (não afeta dano/iniciativa), ela é um estado compartilhado de combate — por isso será persistida no backend (não apenas em memória do cliente) e propagada por evento SignalR somente após confirmação da persistência, com reidratação via GET ao reconectar, no mesmo padrão de `BattlemapTokenPosition`.
- **III. Experiência web responsiva e resiliente**: PASS. Painel de configuração mobile-first (sliders grandes, tocáveis), reconexão SignalR reaproveitada de `useCampaignRealtime`, sem comandos que pareçam concluídos sem confirmação do servidor (o desenho fica em estado "pendente" até o evento de confirmação, análogo ao already-existing padrão de mover token).
- **IV. Qualidade verificável**: PASS. Serão adicionados testes unitários de domínio (regras de autorização e validação de forma/raio) e testes de componente/E2E no frontend (seleção de formato, sliders, reabertura ao clicar na área), seguindo a estrutura de testes já existente.

Nenhuma violação identificada; seção "Complexity Tracking" não é necessária.

## Project Structure

### Documentation (this feature)

```text
specs/005-attack-area-display/
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
│   │   └── AttackArea.cs                       # nova entidade (formato, raio, largura, direção, origem, autor)
│   ├── Companion.Application/Battlemaps/
│   │   └── BattlemapService.cs                 # + métodos SetAttackArea/RemoveAttackArea (mesmo serviço do battlemap)
│   ├── Companion.Application/Contracts/
│   │   ├── Abstractions.cs                     # + IAttackAreaNotifier ou extensão de ICampaignNotifier
│   │   └── Dtos.cs                              # + AttackAreaDto
│   ├── Companion.Infrastructure/
│   │   └── (repositório/EF mapping para AttackArea, migration)
│   ├── Companion.Api/Endpoints/
│   │   └── BattlemapEndpoints.cs                # + rotas /tokens/{tokenId}/attack-area (PUT/DELETE)
│   └── Companion.Api/Realtime/
│       └── SignalRCampaignNotifier.cs           # + broadcast de evento de área de ataque
└── tests/Companion.UnitTests/
    └── AttackAreaTests.cs                       # regras de domínio + autorização

frontend/
├── app/
│   ├── components/battlemap/
│   │   ├── BattlemapBoard.vue                   # renderiza áreas desenhadas; clique em token/área abre painel
│   │   └── AttackAreaPanel.vue                  # NOVO: seletor de formato + sliders (raio/distância/largura/direção)
│   ├── composables/
│   │   ├── useBattlemapViewport.ts               # reaproveitado para conversão de coordenadas do desenho
│   │   └── useAttackAreas.ts                     # NOVO: estado local + chamadas de API + assinatura SignalR
│   ├── components/
│   │   └── InitiativeTracker.vue                 # correção do bug: ocultar editor de HP/iniciativa em gameMode.active
│   └── pages/index.vue                           # ajuste da condição de renderização do editor
└── tests/
    ├── attack-area.spec.ts                       # NOVO: seleção/configuração/reabertura/sincronização
    └── initiative-tracker-combat-mode.spec.ts    # NOVO: cobre a correção do bug
```

**Structure Decision**: Reaproveitar a estrutura Web existente (backend ASP.NET Core em Clean Architecture + frontend Nuxt/Vue). A área de ataque é modelada como uma nova entidade de domínio associada ao `Battlemap`/token (mesmo padrão de `BattlemapTokenPosition`), exposta por novos endpoints em `BattlemapEndpoints.cs` e propagada via o `SignalRCampaignNotifier`/`CampaignHub` já existentes. No frontend, um novo composable (`useAttackAreas`) e um novo componente de painel (`AttackAreaPanel.vue`) se integram ao `BattlemapBoard.vue` já existente, sem novas páginas ou rotas. A correção do bug é isolada em `InitiativeTracker.vue`/`index.vue`, reaproveitando o `useGameMode` já introduzido.

## Complexity Tracking

Nenhuma violação da constituição — seção não aplicável.
