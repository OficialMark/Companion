# Implementation Plan: Tokens do Compêndio e de Itens no Battlemap

**Branch**: `OficialMark/tokens-compendio-item-6fc489fd` | **Date**: 2026-11-27 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/016-tokens-compendio-itens/spec.md`

## Summary

O token representativo (spec 006, `BattlemapMarker`) ganha uma origem opcional: uma criatura do compêndio ou um item do catálogo. O servidor resolve a entrada com as regras de visibilidade de cada participante, deriva nome e tamanho, e o snapshot do mapa devolve a origem e a imagem da criatura. Uma rota nova lista criaturas com o mínimo necessário para escolher o token, liberada para jogadores mas restrita ao compêndio público. No frontend, o botão "Do compêndio" passa a existir para todos e abre o `TokenSourcePicker` (abas Criaturas/Itens; o mestre escolhe "Só token" ou "No combate"). Itens são desenhados como quadrado arredondado.

## Technical Context

**Language/Version**: C# 14 / .NET 10 LTS; TypeScript 5.9; Vue 3.5

**Primary Dependencies**: ASP.NET Core minimal API, Entity Framework Core, Nuxt 4, Pinia 3, @nuxt/icon (Lucide)

**Storage**: SQL Server, PostgreSQL e SQLite; duas colunas novas anuláveis em `BattlemapMarkers` (migration `AddMarkerSources` nos três conjuntos)

**Testing**: xUnit (unitário e integração em SQLite), Vitest + Vue Test Utils

**Target Platform**: API em container Linux; PWA em navegadores modernos Windows e Android

**Project Type**: Aplicação web com backend e frontend

**Performance Goals**: Busca paginada (padrão 8–12 itens), um único `SELECT` a mais no snapshot para as imagens das criaturas de marcadores

**Constraints**: Servidor é autoridade da visibilidade; nenhum dado de ficha sai na busca de jogadores; tokens de texto livre inalterados

**Scale/Scope**: Domínio Battlemaps (criação e snapshot), Creatures (rota de opções), board do mapa no frontend

## Constitution Check

- **Camadas**: PASS. Regra de origem única e corte de nome em Domain (`BattlemapMarker`); resolução e permissões em Application (`BattlemapService`, `CreatureService`); Api só mapeia a rota.
- **Autoridade do servidor**: PASS. O cliente envia só o identificador; nome, tamanho e imagem são derivados no servidor. SignalR continua só invalidando o mapa após `SaveChanges`.
- **Web responsiva/resiliente**: PASS. Seletor com alvos de 44 px, botões só ícone abaixo de 420 px com `aria-label`, desabilitado offline.
- **Qualidade verificável**: PASS. Testes unitários de domínio, 6 testes de integração do contrato (incluindo vazamento de criatura do mestre) e testes de componente.
- **Evolução segura**: PASS. Migration aditiva com colunas anuláveis; marcadores antigos continuam como texto livre.
- **Dependências operacionais**: PASS. Nenhuma nova.

Reavaliação pós-design: todos os gates atendidos.

## Project Structure

### Documentation (this feature)

```text
specs/016-tokens-compendio-itens/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── battlemap-token-source-api.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
backend/
├── src/Companion.Domain/Models/BattlemapMarker.cs                (CreatureId, CatalogItemId, LabelFrom)
├── src/Companion.Application/Battlemaps/BattlemapService.cs       (CreateMarkerAsync, TokenCreature, snapshot)
├── src/Companion.Application/Contracts/Dtos.cs                    (CreateMarkerRequest, BattlemapTokenDto)
├── src/Companion.Application/Creatures/CreatureService.cs         (ListTokenOptions)
├── src/Companion.Application/Creatures/CreatureDtos.cs            (CreatureTokenOptionDto)
├── src/Companion.Api/Endpoints/CreatureEndpoints.cs               (GET token-options)
├── src/Companion.Infrastructure/Persistence/{Migrations,PostgreSqlMigrations,SqliteMigrations}/*_AddMarkerSources.cs
└── tests/
    ├── Companion.UnitTests/BattlemapMarkerTests.cs
    └── Companion.IntegrationTests/BattlemapTokenSourceApiTests.cs
frontend/
├── app/components/battlemap/TokenSourcePicker.vue
├── app/components/battlemap/BattlemapBoard.vue                    (botão "Do compêndio" para todos)
├── app/components/battlemap/BattlemapToken.vue                    (item = rect)
├── app/stores/campaign.ts                                         (createMarker aceita origem)
├── app/composables/useCompanionApi.ts                             (listCreatureTokenOptions)
└── tests/{token-source-picker,battlemap-markers}.spec.ts
```

**Structure Decision**: estender o marcador existente em vez de criar um tipo de token novo; posição, aparência, área de ataque, remoção e névoa já funcionam para marcadores e passam a valer sem código extra.

## Complexity Tracking

Sem violações.
