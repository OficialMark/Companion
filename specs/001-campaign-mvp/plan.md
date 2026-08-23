# Implementation Plan: MVP de Campanha D&D

**Branch**: `001-campaign-mvp` | **Date**: 2026-08-23 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/001-campaign-mvp/spec.md`

## Summary

Entregar uma mesa web responsiva para criação e entrada em campanhas, rolagem pública de dados, histórico, iniciativa e turnos sincronizados. O backend será um monólito modular ASP.NET Core em Clean Architecture, com SQL Server como estado durável e SignalR para notificações. O frontend será uma SPA/PWA Nuxt servida por Nginx, que também fará proxy relativo para API e WebSocket. Um Docker Compose iniciará frontend, API e banco, publicando somente a porta web em todas as interfaces do host.

## Technical Context

**Language/Version**: C# 14 / .NET 10 LTS; TypeScript 5 / Node.js 22 LTS

**Primary Dependencies**: ASP.NET Core Minimal APIs, SignalR, EF Core SQL Server; Nuxt 4, Vue 3, Pinia, `@microsoft/signalr`

**Storage**: Microsoft SQL Server 2022, acessado por EF Core com migrations versionadas e volume Docker persistente

**Testing**: xUnit para domínio e aplicação; `WebApplicationFactory` e Testcontainers SQL Server para integração; Vitest para frontend; Playwright para fluxos desktop/mobile

**Target Platform**: Containers Linux em host Windows/Linux; navegadores Chromium atuais em Windows e Android; acesso por LAN ou adaptador Hamachi/Radmin VPN

**Project Type**: Aplicação web com frontend e backend separados no mesmo repositório

**Performance Goals**: Rolagem persistida e entregue aos clientes em até 1 s no p95 em LAN; snapshot inicial visível em até 2 s; até 20 conexões simultâneas por campanha

**Constraints**: Backend autoritativo; persistir antes de publicar; frontend sem URL absoluta embutida; bind `0.0.0.0`; sem Redis, broker ou Kubernetes no MVP; HTTP limitado a rede privada

**Scale/Scope**: Até 100 campanhas, 20 participantes por campanha, 50 rolagens retornadas por snapshot e um combate ativo por campanha

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Gate | Status | Evidence |
|------|--------|----------|
| Clean Architecture e monólito modular | PASS | Quatro projetos backend e módulos Campaigns, DiceRolls e Encounters |
| Servidor como autoridade | PASS | API gera dados/iniciativa, grava transação e só então publica SignalR |
| Windows e Android resilientes | PASS | SPA/PWA mobile-first, proxy relativo, reconexão e ressincronização |
| Qualidade verificável | PASS | Testes unitários, integração SQL/HTTP/SignalR e E2E desktop/mobile previstos |
| SQL Server com EF migrations | PASS | SQL Server 2022, EF Core provider e migration inicial |
| Simplicidade operacional | PASS | Uma API, um frontend e um banco; sem serviços distribuídos adicionais |

Nenhuma violação requer justificativa.

## Project Structure

### Documentation (this feature)

```text
specs/001-campaign-mvp/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── tasks.md
├── checklists/
│   └── requirements.md
└── contracts/
    ├── openapi.yaml
    └── realtime-events.md
```

### Source Code (repository root)

```text
backend/
├── Companion.slnx
├── src/
│   ├── Companion.Domain/
│   ├── Companion.Application/
│   ├── Companion.Infrastructure/
│   └── Companion.Api/
└── tests/
    ├── Companion.UnitTests/
    └── Companion.IntegrationTests/

frontend/
├── app/
│   ├── assets/css/
│   ├── components/
│   ├── composables/
│   ├── pages/
│   ├── stores/
│   └── types/
├── public/
├── tests/
└── nuxt.config.ts

docker-compose.yml
.env.example
README.md
```

**Structure Decision**: Backend e frontend permanecem implantáveis separadamente, enquanto o domínio e os casos de uso do backend seguem as quatro camadas da Clean Architecture. O Compose e o Nginx unem os componentes em uma única URL pública para eliminar configuração específica de IP.

## Phase 0: Research Outcome

As decisões, justificativas e alternativas estão em [research.md](research.md). Todos os pontos técnicos foram resolvidos; não existem marcadores `NEEDS CLARIFICATION`.

## Phase 1: Design Outcome

- Modelo e transições: [data-model.md](data-model.md)
- Contrato HTTP: [contracts/openapi.yaml](contracts/openapi.yaml)
- Contrato em tempo real: [contracts/realtime-events.md](contracts/realtime-events.md)
- Validação ponta a ponta: [quickstart.md](quickstart.md)

### Post-design Constitution Check

PASS. O modelo mantém campanhas isoladas, token opaco com hash persistido, comandos de mestre autorizados, migrations SQL Server, eventos pós-persistência, snapshot para reconexão e arquitetura operacional mínima.

## Complexity Tracking

Não há violações constitucionais ou complexidade adicional a justificar.
