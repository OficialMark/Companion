# Implementation Plan: Acesso, Administração e Iniciativa da Campanha

**Branch**: `002-campaign-access-initiative` | **Date**: 2026-08-23 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/002-campaign-access-initiative/spec.md`

## Summary

Evoluir o acesso para dois códigos numéricos globais de quatro dígitos, proteger uma área administrativa capaz de listar e excluir mesas e integrar o modificador persistido por jogador à preparação do encontro. O desenho adiciona códigos normalizados, configuração externa para o segredo administrativo, vínculo opcional entre participante e combatente e exclusão transacional, preservando o backend como fonte de verdade.

## Technical Context

**Language/Version**: C# 14 em .NET 10 LTS; TypeScript 5.9 com Vue 3 e Nuxt 4

**Primary Dependencies**: ASP.NET Core Minimal APIs, Entity Framework Core 10, SignalR, Pinia, Nuxt e rate limiting nativo do ASP.NET Core

**Storage**: Microsoft SQL Server 2022 com migrations EF Core versionadas

**Testing**: xUnit com Testcontainers SQL Server; Vitest; Playwright desktop e Android

**Target Platform**: Containers Linux; navegadores modernos em Windows e Android

**Project Type**: Aplicação web SPA/PWA com backend monolítico modular

**Performance Goals**: Até 100 mesas administrativas em 2 segundos; entrada e atualização em 1 segundo na LAN; cinco jogadores preparados em uma ação e menos de 10 segundos

**Constraints**: 10.000 códigos possíveis e dois por mesa; segredos ausentes de logs e snapshots comuns; exclusão confirmada; eventos somente após commit

**Scale/Scope**: Até 100 mesas existentes, 20 participantes simultâneos por mesa e uma instância da API

## Constitution Check

*GATE: PASS antes da pesquisa e PASS após o design.*

- **I. Monólito modular e Clean Architecture — PASS**: casos de uso permanecem nas quatro camadas e nos módulos Campaigns, Administration e Encounters.
- **II. Servidor como autoridade — PASS**: papel, modificadores, importação, edição, exclusão e iniciativa são validados e persistidos antes dos eventos.
- **III. Experiência responsiva e resiliente — PASS**: `/admin`, acesso e modificador cobrem desktop/Android; snapshots recuperam estado.
- **IV. Qualidade verificável — PASS**: testes unitários, integração, componentes e E2E são obrigatórios.
- **V. Simplicidade operacional — PASS**: nenhum serviço novo; segredo externo e migrations versionadas.
- **Restrições tecnológicas — PASS**: permanecem .NET 10, SQL Server, Nuxt 4, REST e SignalR.

## Project Structure

### Documentation (this feature)

```text
specs/002-campaign-access-initiative/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/openapi.yaml
└── tasks.md
```

### Source Code (repository root)

```text
backend/
├── src/
│   ├── Companion.Domain/Models/
│   ├── Companion.Application/{Campaigns,Administration,Encounters,Contracts}/
│   ├── Companion.Infrastructure/{Persistence,Security}/
│   └── Companion.Api/{Endpoints,Realtime}/
└── tests/{Companion.UnitTests,Companion.IntegrationTests}/

frontend/
├── app/{components,composables,pages,stores,types}/
└── tests/e2e/
```

**Structure Decision**: Reutilizar a aplicação e as quatro camadas existentes. Administração recebe caso de uso e endpoints próprios no mesmo processo; o frontend usa uma página Nuxt `/admin` e um componente de modificador na mesa.

## Complexity Tracking

Nenhuma violação constitucional exige justificativa.
