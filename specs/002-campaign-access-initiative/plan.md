# Implementation Plan: Acesso, Administração, Iniciativa e Vida em Combate

**Branch**: `002-campaign-access-initiative` | **Date**: 2026-08-28 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/002-campaign-access-initiative/spec.md`

## Summary

Ampliar o perfil de combate do jogador com vida inicial, copiar esse valor para combatentes editáveis pelo mestre e permitir que jogadores vinculados e mestres resolvam ataques a qualquer momento. Cada ataque referencia uma rolagem pública de resultado único, aplica o total integral de forma idempotente e atômica aos alvos e registra vidas anterior/posterior. Snapshots são filtrados por participante: mestre vê todas as vidas do encontro e jogador somente a própria. Eventos sensíveis apenas invalidam o snapshot, evitando vazamento pelo canal compartilhado.

## Technical Context

**Language/Version**: C# 14 em .NET 10 LTS; TypeScript 5.9 com Vue 3 e Nuxt 4

**Primary Dependencies**: ASP.NET Core Minimal APIs, Entity Framework Core 10, SignalR, Pinia, Nuxt; sem nova dependência operacional

**Storage**: Microsoft SQL Server 2022 com migration EF Core versionada para vida, ataques e alvos

**Testing**: xUnit (domínio), xUnit + Testcontainers SQL Server (integração), Vitest (componentes/store) e Playwright (desktop/Android)

**Target Platform**: Containers Linux; navegadores modernos em Windows e Android

**Project Type**: Aplicação web SPA/PWA com backend monolítico modular

**Performance Goals**: 95% das mudanças de vida visíveis aos autorizados em até um segundo na LAN; ataque com até cinco alvos concluído em menos de 30 segundos; snapshot para até 20 combatentes sem atraso perceptível

**Constraints**: Vida inteira de 0 a 99.999; vida obrigatória para iniciar combate; resultado único por ataque; dano mínimo zero; mestre vê todas as vidas e jogador somente a própria; confirmação idempotente; persistência antes de notificação

**Scale/Scope**: Até 100 mesas, 20 participantes simultâneos por mesa, um combate ativo por mesa e uma única instância da API no MVP

## Constitution Check

*GATE: PASS antes da pesquisa e PASS após o design.*

- **I. Monólito modular e Clean Architecture — PASS**: regras permanecem em Domain; casos de uso em Application; persistência em Infrastructure; HTTP/SignalR em Api. Nenhum módulo ou processo novo.
- **II. Servidor como autoridade — PASS**: dados, vida, autorização, dano, idempotência e estado anterior/posterior são validados e persistidos no servidor antes de eventos. Reconexão usa snapshot REST.
- **III. Experiência responsiva e resiliente — PASS**: perfil, preparação, vida e ataque terão controles mobile-first; comandos ficam pendentes até confirmação; invalidação em tempo real força snapshot autorizado após reconexão.
- **IV. Qualidade verificável — PASS**: domínio cobre limites e transições; integração cobre autorização, privacidade, persistência e idempotência; Vitest/Playwright cobrem jornadas de jogador e mestre.
- **V. Simplicidade operacional — PASS**: reutiliza SQL Server, REST e SignalR existentes; sem Redis, fila ou serviço adicional. Concorrência usa o token de versão do encontro; conflitos retornam resposta recuperável, e o cliente reutiliza a chave idempotente ao tentar novamente.
- **Restrições tecnológicas — PASS**: permanecem .NET 10, EF Core, SQL Server, Nuxt 4, REST e SignalR.

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
│   ├── Companion.Domain/Models/{Participant,Combatant,Encounter,CombatAttack,CombatAttackTarget}.cs
│   ├── Companion.Application/{Campaigns,Encounters,Contracts}/
│   ├── Companion.Infrastructure/Persistence/
│   └── Companion.Api/{Endpoints,Realtime}/
└── tests/{Companion.UnitTests,Companion.IntegrationTests}/

frontend/
├── app/{components,composables,pages,stores,types}/
└── tests/{unit,e2e}/
```

**Structure Decision**: Reutilizar as quatro camadas e a tela única existentes. O componente de modificador passa a editar o perfil de combate; preparação e tracker recebem vida; um componente de ataque orquestra a rolagem existente, seleção posterior e confirmação. Mudanças de vida usam snapshots personalizados, não payloads com vida no grupo SignalR.

## Design Decisions

- `Participant.CurrentHitPoints` é opcional e pertence apenas ao jogador; `Combatant.CurrentHitPoints` é a cópia do encontro e pode permanecer nula somente durante preparação.
- `CombatAttack` identifica autor, rolagem e chave idempotente. `CombatAttackTarget` preserva alvo, dano aplicado e vidas anterior/posterior.
- O mestre pode editar qualquer combatente; o jogador pode editar diretamente apenas o combatente ligado ao próprio `ParticipantId`.
- A rolagem deve pertencer ao autor, à campanha, estar revelada e conter exatamente um resultado. Total negativo aplica dano zero.
- A confirmação deduplica alvos, rejeita todo o comando se qualquer alvo estiver inválido e salva ataque e todas as vidas em uma única unidade de persistência.
- O encontro mantém controle de concorrência. Em conflito, o serviço retorna erro recuperável; o cliente sincroniza o snapshot e pode tentar novamente reutilizando a mesma chave idempotente.
- DTOs de encontro são projetados para um observador. Mestre recebe todas as vidas; jogador recebe apenas a vida do combatente que controla.
- Eventos de vida/ataque no grupo da campanha contêm identificador do encontro e revisão, sem vidas. O cliente busca novo snapshot autenticado.

## Complexity Tracking

Nenhuma violação constitucional exige justificativa.