# Implementation Plan: Battlemap com Grid Interativo

**Branch**: `003-battlemap-grid` | **Date**: 2026-08-28 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/003-battlemap-grid/spec.md`

## Summary

Adicionar um battlemap único ao encontro ativo, configurado pelo mestre com imagem e dimensões de 1 a 100 linhas/colunas, e projetar um pino por jogador com posição opcional no grid. O servidor persiste a imagem, valida permissões e coordenadas, confirma movimentos antes de notificá-los e expõe snapshots recuperáveis; o frontend Nuxt renderiza imagem, linhas e pinos em SVG dentro de um visualizador de tamanho estável, com escala-alvo de 40 px, pan/zoom e interações equivalentes por mouse, toque e teclado.

## Technical Context

**Language/Version**: C# 14 em .NET 10 LTS; TypeScript 5.9 com Vue 3 e Nuxt 4

**Primary Dependencies**: ASP.NET Core Minimal APIs, Entity Framework Core 10, SignalR 10, Pinia 3, Nuxt 4 e Vue 3; sem nova dependência operacional ou de renderização

**Storage**: Microsoft SQL Server 2022; imagem em `varbinary(max)` e metadados/posições relacionais via migration EF Core

**Testing**: xUnit para domínio, xUnit + Testcontainers SQL Server para integração, Vitest para componentes/composables/store e Playwright para desktop e Android

**Target Platform**: API em container Linux; SPA/PWA em navegadores modernos no Windows e Android

**Project Type**: Aplicação web SPA/PWA com backend monolítico modular

**Performance Goals**: snapshot do battlemap sem binário visível em até 1 s para 95% das consultas na LAN; movimento confirmado e refletido aos conectados em até 2 s; pan/zoom e arraste com resposta visual contínua próxima de 60 fps em dispositivos suportados

**Constraints**: um battlemap ativo por encontro; PNG/JPEG/WebP até 20 MB; grid de 1×1 a 100×100; até 20 pinos; células quadradas com alvo inicial de 40 px; área visual estável por breakpoint; persistência antes de notificação; último estado seguro durante desconexão

**Scale/Scope**: Até 100 mesas, 20 participantes simultâneos por mesa, um encontro ativo e um battlemap por mesa, uma única instância da API no MVP

## Constitution Check

*GATE: PASS antes da pesquisa e PASS após o design.*

- **I. Monólito modular e Clean Architecture — PASS**: `Battlemap` e regras de coordenadas ficam em Domain; casos de uso e projeções em Application; persistência binária/relacional em Infrastructure; HTTP e SignalR em Api. Nenhum novo processo ou módulo distribuído.
- **II. Servidor como autoridade — PASS**: imagem vigente, dimensões, permissões e posições são validadas e persistidas no servidor. Eventos carregam somente encontro/revisão e o cliente sempre recupera o snapshot REST após entrada ou reconexão.
- **III. Experiência responsiva e resiliente — PASS**: visualizador possui área estável por breakpoint, controles por toque/mouse/teclado, alvo de 40 px, pan/zoom e estado pendente. Desconexão conserva o último snapshot e bloqueia confirmação aparente.
- **IV. Qualidade verificável — PASS**: testes unitários cobrem dimensões e posições; integração cobre upload, autorização, persistência, concorrência e eventos; Vitest/Playwright cobrem mestre/jogador, desktop/Android e reconexão.
- **V. Simplicidade operacional — PASS**: reutiliza SQL Server, REST e SignalR. A imagem fica no banco para evitar armazenamento externo, Redis, filas ou serviços adicionais no MVP.
- **Restrições tecnológicas — PASS**: mantém .NET 10, EF Core, SQL Server, Nuxt 4, REST e SignalR conforme a constituição.
- **Reavaliação pós-design — PASS**: modelo, contrato e quickstart preservam as mesmas fronteiras; o endpoint binário é separado do snapshot e eventos só invalidam estado já persistido.

## Project Structure

### Documentation (this feature)

```text
specs/003-battlemap-grid/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── openapi.yaml
└── tasks.md
```

### Source Code (repository root)

```text
backend/
├── src/
│   ├── Companion.Domain/Models/{Battlemap,BattlemapTokenPosition,Encounter}.cs
│   ├── Companion.Application/
│   │   ├── Battlemaps/BattlemapService.cs
│   │   └── Contracts/{Abstractions,DtoMapper,Dtos}.cs
│   ├── Companion.Infrastructure/Persistence/
│   │   ├── CompanionDbContext.cs
│   │   ├── ModelConfigurations.cs
│   │   └── Migrations/
│   └── Companion.Api/
│       ├── Endpoints/BattlemapEndpoints.cs
│       ├── Realtime/SignalRCampaignNotifier.cs
│       └── Program.cs
└── tests/
    ├── Companion.UnitTests/BattlemapTests.cs
    └── Companion.IntegrationTests/BattlemapApiTests.cs

frontend/
├── app/
│   ├── components/battlemap/{BattlemapBoard,BattlemapSetup,BattlemapToken}.vue
│   ├── composables/{useBattlemapViewport,useCompanionApi,useCampaignRealtime}.ts
│   ├── stores/campaign.ts
│   ├── types/campaign.ts
│   ├── app.vue
│   └── assets/css/main.css
└── tests/
    ├── battlemap.spec.ts
    └── e2e/battlemap.spec.ts
```

**Structure Decision**: Reutilizar as quatro camadas e a tela de campanha existentes. O agregado de battlemap é associado ao encontro, mas isolado em serviço/endpoints próprios para não carregar a imagem no snapshot de campanha. No cliente, componentes dedicados entram na coluna principal da tela e o estado compartilhado permanece na store da campanha.

## Design Decisions

- `Battlemap` é uma entidade única por encontro e armazena imagem, tipo de mídia, nome original sanitizado, dimensões do grid, revisão e datas. A substituição atualiza o mesmo recurso e retorna posições que saíram do novo limite para a reserva.
- `BattlemapTokenPosition` persiste apenas pinos posicionados. O pino em reserva é uma projeção dos jogadores da campanha sem uma posição correspondente, permitindo entrada tardia sem acoplar o fluxo de ingresso ao battlemap.
- O binário fica no SQL Server no MVP, mas nunca integra o snapshot JSON. Um endpoint de imagem com ETag permite cache e evita retransmissão após movimentos.
- Upload usa multipart, valida limite, tipo declarado e assinatura PNG/JPEG/WebP antes de persistir. SVG é excluído para evitar conteúdo ativo e porque não faz parte do escopo.
- O contrato REST separa configuração, snapshot, imagem e movimento. Revisões otimistas detectam substituição concorrente do mapa e disputa entre mestre/jogador pelo mesmo pino.
- Eventos `battlemap.invalidated` contêm somente `encounterId` e `revision`. O cliente busca o snapshot autorizado; a imagem é buscada novamente apenas quando seu ETag/revisão muda.
- O board usa SVG: uma imagem recortada ao retângulo do grid, no máximo 202 linhas vetoriais para um grid 100×100, pinos como elementos focáveis e cálculo matemático da célula selecionada. Não serão criados 10.000 elementos de célula.
- A escala inicial usa célula de 40 px quando o encaixe total produziria menos de 36 px ou mais de 44 px; o mapa fica centralizado e navegável. Um comando “Ajustar mapa” mostra o todo sob demanda, sem alterar a altura do visualizador.
- Pointer Events unificam mouse e toque; toque/teclado também suportam selecionar pino e depois célula. Pan é ativado por fundo/controle explícito para não competir com arraste de pino.
- O visualizador usa altura estável por breakpoint e `overflow: hidden`; redimensionamento/orientação recalculam somente a transformação visual, nunca as coordenadas persistidas.

## Complexity Tracking

Nenhuma violação constitucional exige justificativa.
