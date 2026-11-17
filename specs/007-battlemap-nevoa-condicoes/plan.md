# Implementation Plan: Névoa de guerra, condições com ícones e HP controlado pelo mestre

**Branch**: `feat/battlemap-condicoes-nevoa` | **Date**: 2026-11-16 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/007-battlemap-nevoa-condicoes/spec.md`

## Summary

Quatro entregas sobre o battlemap recuperado da spec 006:

1. **Menu do token no PC** (bug): o painel abre no `pointerdown` do token, mas o `click` que o navegador sintetiza após o `pointerup` é redirecionado para a superfície (por causa do `setPointerCapture`) e cai em `commitCell`, que move o token para a célula sob o cursor e fecha o painel. O guard atual só cobre o caso "célula clicada == célula âncora" (falha para tokens 2x2+ e tokens empilhados com deslocamento). Correção determinística: um flag `suppressSurfaceClick` armado no fim de toda sessão de ponteiro iniciada num token e consumido pelo próximo `click` da superfície (zerado em qualquer novo `pointerdown`). Arraste e seleção só com botão principal; botão direito abre o menu sem arrastar e suprime o menu de contexto; arrastar com botão direito/meio no fundo faz pan.
2. **Condições com ícones**: o enum `StatusCondition` ganha 12 valores (anexados ao fim; persistência por nome, então os 6 antigos continuam válidos), a coluna CSV cresce de 128 para 512. No frontend, um catálogo único (`utils/statusConditions.ts`) define rótulo, ícone Lucide e cor; menu com grade de chips com ícone; token com até 3 selos coloridos com ícone e um selo "+N".
3. **Visibilidade de HP**: `Combatant.IsHitPointsHidden` (criaturas apenas). `DtoMapper.ToDto(encounter, observer)` remove HP atual/máximo de criaturas ocultas para não mestres (mantém `IsDefeated`) e expõe `IsHitPointsHidden`. Endpoints mestre-only por criatura e em lote. `AttackService` alinha a regra (`canSee = mestre || dono || !oculto`). Junto, o bug que apagava HP/foto em toda mudança de presença (`ParticipantsChanged`) já foi corrigido no merge.
4. **Névoa de guerra**: `Battlemap.FogMask` (bitset row-major, `varbinary(1250)`, null = tudo revelado). `PUT .../battlemap/fog` mestre-only com a máscara completa em base64 (idempotente; o mestre é o único escritor). `ToSnapshot` passa a receber o observador: para jogadores, criaturas e marcadores do mestre cuja área ocupada está toda coberta são omitidos (com suas áreas). Substituir a imagem com outro grid zera a névoa. No frontend, `useFogOfWar` mantém a máscara local, gera um único `<path>` (runs horizontais), e o board ganha um modo "Névoa" com pincel 1/3/5, retângulo, cobrir/revelar, cobrir tudo/revelar tudo.

## Technical Context

**Language/Version**: C# / .NET 10 (Domain/Application/Infrastructure/Api); TypeScript + Vue 3.5 / Nuxt 4

**Primary Dependencies**: ASP.NET Core Minimal APIs, SignalR, EF Core SQL Server; Nuxt 4, Pinia, `@nuxt/icon` com `@iconify-json/lucide` local (modo svg, `clientBundle.scan`)

**Storage**: SQL Server via EF Core. Uma migration: `Combatants.IsHitPointsHidden bit not null default 0`, `Battlemaps.FogMask varbinary(1250) null`, `BattlemapTokenAppearances.StatusConditions nvarchar(512)`.

**Testing**: xUnit (unit + integration com SQL Server em container), Vitest (componentes), Playwright (E2E)

**Target Platform**: PWA web, Windows e Android, mobile-first

**Project Type**: Web application (frontend + backend existentes)

**Performance Goals**: névoa propagada em até 3 s (SC-005); pintura com pré-visualização a 60 fps em grid 100x100 (path único, recalculado só quando uma célula muda)

**Constraints**: servidor é autoridade (HP oculto e criaturas sob névoa nunca saem do servidor para jogadores); alvos de toque >= 44 px; nada de rolagem horizontal em 360 px; ícones têm de aparecer literalmente no código-fonte para entrar no bundle de ícones

**Scale/Scope**: 1 battlemap por encontro, até 100x100 células, dezenas de tokens

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Monólito modular e Clean Architecture**: PASS. Alterações em entidades existentes (`Combatant`, `Battlemap`, enum `StatusCondition`), regras no Domain (`Battlemap.SetFog/IsFogged`, `Combatant.SetHitPointsHidden`), casos de uso no Application (`BattlemapService`, `EncounterService`), endpoints na Api.
- **II. Servidor como autoridade**: PASS. Filtragem de HP e de tokens sob névoa acontece na montagem do DTO por observador; eventos SignalR continuam só como invalidação (`BattlemapInvalidated`/`EncounterInvalidated`) e cada cliente refaz o GET com o próprio token. Nenhum dado oculto vai em evento de grupo.
- **III. Responsiva e resiliente**: PASS. Ferramentas de névoa em barra compacta que quebra linha em 360 px; pintura otimista com reversão para o estado do servidor se o PUT falhar ou a conexão cair.
- **IV. Qualidade verificável**: PASS. Unit: `Battlemap` (máscara, tamanho, ocupação), `Combatant.SetHitPointsHidden`, `DtoMapper` filtrando HP. Integração: fog PUT (mestre ok, jogador 403, tamanho inválido 400), snapshot do jogador sem criatura coberta, encontro do jogador sem HP oculto, condições novas persistidas. Vitest: gesto clique/arraste para 1x1, 2x2 e empilhado, botão direito, ícones/"+N", modo névoa. E2E: fluxo mestre/jogador.
- **V. Simplicidade operacional**: PASS. Máscara de bits numa coluna (no máximo 1250 bytes) em vez de tabela de células; PUT de máscara completa em vez de protocolo de operações.

## Project Structure

### Documentation (this feature)

```text
specs/007-battlemap-nevoa-condicoes/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── battlemap-api.md
├── checklists/requirements.md
└── tasks.md            (speckit-tasks)
```

### Source Code (repository root)

```text
backend/
├── src/Companion.Domain/Models/
│   ├── StatusCondition.cs            (+12 valores)
│   ├── Combatant.cs                  (IsHitPointsHidden)
│   └── Battlemap.cs                  (FogMask, SetFog, ClearFog, IsCellFogged, IsAreaFogged)
├── src/Companion.Application/
│   ├── Contracts/Dtos.cs             (CombatantDto.IsHitPointsHidden, BattlemapSnapshotDto.FogMask, requests)
│   ├── Contracts/DtoMapper.cs        (filtro de HP por observador)
│   ├── Battlemaps/BattlemapService.cs (SetFogAsync, ToSnapshot por observador)
│   └── Encounters/EncounterService.cs, AttackService.cs
├── src/Companion.Infrastructure/Persistence/ (configuração + migration)
├── src/Companion.Api/Endpoints/       (BattlemapEndpoints, EncounterEndpoints)
└── tests/ (UnitTests, IntegrationTests)

frontend/
├── app/utils/statusConditions.ts       (catálogo: rótulo, ícone, cor)
├── app/utils/fogMask.ts                (codificação base64 <-> bits, pincel, retângulo, path)
├── app/composables/useFogOfWar.ts      (estado local + pré-visualização + commit)
├── app/components/battlemap/
│   ├── BattlemapBoard.vue              (gesto corrigido, camada de névoa, modo névoa)
│   ├── BattlemapToken.vue              (selos com ícones, "+N", indicador de HP oculto)
│   ├── AttackAreaPanel.vue             (grade de condições, toggle de HP)
│   └── FogToolbar.vue                  (novo)
├── app/components/InitiativeTracker.vue (toggle de HP por criatura + "ocultar todos")
├── app/stores/campaign.ts, composables/useCompanionApi.ts, types/campaign.ts
└── tests/ (Vitest + e2e)
```

**Structure Decision**: Web application existente; nenhum projeto novo.

## Complexity Tracking

Sem violações da constituição.
