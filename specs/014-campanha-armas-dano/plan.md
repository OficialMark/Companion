# Implementation Plan: Ataques Tipados e Caderno da Campanha

**Branch**: `014-campanha-armas-dano` | **Date**: 2026-11-21 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/014-campanha-armas-dano/spec.md`

## Summary

Evoluir os ataques salvos para identificar componentes e tipos de dano, ampliando os dados de arma no catálogo e nas cópias de inventário. Criar a área superior **Campanha** para registros colaborativos de NPCs, mapas, pistas e diário, com notas exclusivas do mestre, filtro de visibilidade aplicado à leitura e pontos ancorados às imagens. Reusar autorização por campanha, API REST, EF Core/SQL Server, grupos SignalR e as convenções visuais já existentes.

## Technical Context

**Language/Version**: C# / .NET 10; TypeScript, Vue 3 e Nuxt 4\
**Primary Dependencies**: ASP.NET Core Minimal APIs, EF Core, SQL Server, SignalR, Pinia\
**Storage**: SQL Server via migrations versionadas; imagens de mapas armazenadas no banco, limitadas a 20 MB conforme battlemap\
**Testing**: xUnit unitário, integração ASP.NET/SQL Server, Vitest e Playwright; validar regras tipadas, persistência, autorização e fluxos de interface\
**Target Platform**: navegadores modernos em Windows, Android e outros dispositivos com viewport responsivo\
**Project Type**: monólito modular web com frontend e backend\
**Performance Goals**: listas paginadas/buscáveis; atualizar registros confirmados em até 3 s para participantes online\
**Constraints**: servidor como autoridade, validar papel em cada operação, persistir antes de eventos; clientes de jogadores nunca recebem campos privados nem registros ocultos; manter payloads legados de armas e rolagens legíveis durante a migração\
**Scale/Scope**: campanhas existentes, até 1000 registros por campanha sem carregamento integral no cliente; até 100 marcadores por mapa; preservar imagens e mapas táticos existentes

## Constitution Check

| Gate | Resultado | Evidência / decisão |
|---|---|---|
| I. Monólito modular e Clean Architecture | PASS | Domínio em `Companion.Domain`, casos de uso em `Application`, persistência em `Infrastructure`, endpoints em `Api`; nenhuma nova infraestrutura operacional. |
| II. Servidor como autoridade do jogo | PASS | Cálculo de rolagem e validação dos dados permanecem no servidor; autorização e filtro de visibilidade executados antes da composição de cada resposta; eventos apenas após `SaveChangesAsync`. |
| III. Web responsiva e resiliente | PASS | Nova área segue navegação de campanha, acessível em teclado/toque; atualizações SignalR invalidam estado e os snapshots REST reconstroem o conteúdo após reconexão. |
| IV. Qualidade verificável | PASS | Testes unitários e de integração cobrem subtotais tipados, histórico, privacidade por papel e pontos de mapa; suíte Vitest completa aprovada. Jornadas manuais visuais em desktop e Android ainda devem ser verificadas antes da publicação. |
| V. Evolução segura | PASS | Migrations EF Core aditivas; campos antigos tratados com compatibilidade; sem novas dependências ou processo externo. |

**Constitution Check pós-design**: PASS para arquitetura, segurança, persistência e qualidade automatizada. A revisão visual manual em dispositivos reais permanece como etapa operacional antes da publicação.

## Project Structure

### Documentation

```text
specs/014-campanha-armas-dano/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── campaign-notebook-api.md
│   └── character-damage-api.md
└── tasks.md
```

### Source Code

```text
backend/src/Companion.Domain/Characters/        # WeaponData, SavedRollData e componentes tipados
backend/src/Companion.Domain/Campaigns/         # registros, notas pessoais e pontos de mapa
backend/src/Companion.Application/Characters/   # projeção e execução de rolagens
backend/src/Companion.Application/Campaigns/    # serviços de caderno e contratos de notificação
backend/src/Companion.Api/Endpoints/            # campanhas/notebook e atualização do catálogo existente
backend/src/Companion.Api/Realtime/              # evento de invalidação não sensível
backend/src/Companion.Infrastructure/Persistence/ # configuração EF e migration SQL Server
backend/src/Companion.Infrastructure/Persistence/Seed/ # catálogo de armas e tipos
frontend/app/pages/                              # destino/rota da área Campanha
frontend/app/components/campaign/                 # listas, editor, detalhe, mapa e marcadores
frontend/app/components/character/                # editor e apresentação de rolagens com dano tipado
frontend/app/composables/                         # clientes REST e atualizações do caderno
frontend/app/stores/                              # estado de campanha e feature de caderno
frontend/app/types/                               # contratos TypeScript
frontend/app/assets/css/                          # estilos mobile-first integrados ao tema
scripts/import-catalogs/                          # importação/versionamento do catálogo, se aplicável
backend/tests/                                    # testes existentes a atualizar quando autorizado
frontend/tests/                                   # jornadas existentes a atualizar quando autorizado
```

**Structure Decision**: Reusar a organização atual por domínio do Companion, sem novo projeto ou serviço. Armazenar uma imagem de referência da campanha separadamente dos mapas táticos (`Battlemap`, ligados a `Encounter`) para não misturar estado de combate com conteúdo persistente de campanha.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|---|---|---|
| Nenhuma violação arquitetural | — | — |
