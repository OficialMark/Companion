# Implementation Plan: Ficha pessoal, inventário e rolagens do jogador

**Branch**: `OficialMark/ficha-jogador-78ed27c1` | **Date**: 2026-11-19 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/011-ficha-jogador/spec.md`

## Summary

Cada nome de personagem vira uma conta da campanha. O jogador recebe "Mesa | Ficha" na barra superior, uma ficha D&D 5e com valores-base editáveis e valores finais calculados, uma mochila com equipamento, bônus mágicos e efeitos, magias tiradas de um catálogo e um painel de rolagem no Modo Jogo com rolagens salvas.

Abordagem:

- **Regras.** `CharacterRules`, um módulo puro no domínio, calcula tudo no servidor. Toda escrita devolve a ficha recalculada.
- **Dados.** O analisador de expressões do backend ganha repetição com colchetes, seleção "maior/menor" sobre a expressão inteira e constantes rotuladas, mantendo a sintaxe atual intacta.
- **Catálogos.** Seguem o padrão do compêndio de criaturas: importador offline, JSON versionado embutido e seeder idempotente.
  - Magias: avribacki/magias5e, 361 magias.
  - Itens: SRD 5.2.1 via 5e-bits, traduzido uma vez para PT-BR e validado por script.
- **Contas.** Uma tabela de sessões permite vários aparelhos por conta.
- **Ambiente.** Nada derruba os containers em uso.

## Technical Context

**Language/Version**: C# / .NET 10 (SDK 10.0.303); TypeScript / Vue 3 / Nuxt 4; Node 20+ para os importadores

**Primary Dependencies**: ASP.NET Core Minimal APIs, EF Core (SQL Server), SignalR, Pinia 3, @nuxt/icon (lucide). Nenhuma dependência nova.

**Storage**: SQL Server. Migration aditiva: 6 tabelas novas, 1 coluna nova em `Participants` e `DiceRolls.Expression` de 128 para 256.

**Testing**: xUnit (unit) e xUnit + Testcontainers.MsSql (integração); Vitest + jsdom; Playwright (desktop e Pixel 7) contra uma instância própria; `node:test` para os importadores.

**Target Platform**: navegadores modernos no Windows e Android (PWA Nuxt, `ssr: false`); API em container Linux.

**Project Type**: aplicação web (backend + frontend) num monólito modular.

**Performance Goals**: recálculo da ficha devolvido em menos de 150 ms (p95) numa ficha com 50 itens e 40 magias; busca no catálogo em menos de 300 ms; expressão do painel atualizada no mesmo quadro do clique (pura formatação no cliente).

**Constraints**: zero parada dos containers `companion-*` em uso; migration sem perda e compatível com tokens existentes; sintaxe de dados legada inalterada; alvos de toque de 44 px; sem rolagem horizontal em 375 px.

**Scale/Scope**: mesas de 2 a 8 jogadores; catálogos com cerca de 460 itens e 361 magias; cerca de 25 endpoints novos; 6 componentes Vue novos.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Gate | Status | Evidência |
|---|---|---|---|
| 1 | Quatro camadas e limites dos módulos | PASS | Domain: `Characters/` (entidades, `CharacterRules`, `ItemEffect`, `PersonName`), `Dice/` (gramática) e `Catalog/`. Application: `Characters/CharacterService`, `Catalog/CatalogService`, `Catalog/CatalogSeeder`, `Campaigns/` (join). Infrastructure: configurações EF, seed sources e migration. Api: `CharacterEndpoints` e `CatalogEndpoints`. |
| 2 | Servidor como autoridade; persistir antes de emitir | PASS | Regras e dados só no servidor (R1, R2). `ParticipantUpdated` e `CharacterChanged` são emitidos depois do `SaveChanges`. O cliente só formata a expressão a partir de `stats` do servidor. |
| 3 | Desktop, Android, desconexão e reconexão | PASS | E2E nos dois projetos Playwright. A ficha mostra o último estado salvo; a gravação automática indica erro e conflito, sem fingir sucesso. A reconexão busca a ficha via `GET`. |
| 4 | Testes proporcionais ao risco | PASS | Unit: gramática, regras (30 casos ou mais), nome e parse. Integração: posse, contas, migração, concorrência e vantagem com randomizador controlado. E2E: jornadas P1. Importadores: `node:test` com fixtures. |
| 5 | SQL Server e migrations EF Core | PASS | Uma migration `AddPlayerCharacterSheet`, aditiva. O backfill de nomes em C# é idempotente (R5). |
| 6 | Nova dependência operacional justificada | PASS | Nenhuma. Os importadores rodam fora do app, como o de criaturas. |
| — | "Autorização operacional local" (Docker) | Restrita | O pedido proíbe mexer nos containers em uso, e isso prevalece durante esta feature (R12). |

**Re-check pós-design**: PASS. Os contratos mantêm a autorização por participante em todo comando (FR-003). Nenhuma escrita emite evento antes do commit. A única mudança de esquema que mexe em dados existentes (sessões e `NameKey`) preserva cada token atual e não apaga nenhuma linha.

## Project Structure

### Documentation (this feature)

```text
specs/011-ficha-jogador/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── dice-expression.md
│   ├── character-api.md
│   └── ui-interaction.md
├── checklists/requirements.md
└── tasks.md            # /speckit-tasks
```

### Source Code (repository root)

```text
backend/
├── src/
│   ├── Companion.Domain/
│   │   ├── Dice/DiceExpression.cs                 # analisador recursivo: N#[…], 2#(…) maior|menor, (ROTULO) N
│   │   ├── Characters/                            # novo
│   │   │   ├── CharacterSheet.cs  InventoryItem.cs  CharacterSpell.cs  SavedRoll.cs
│   │   │   ├── ItemEffect.cs  AbilityCode.cs  Skills.cs  SavedRollIcons.cs
│   │   │   ├── CharacterRules.cs  CharacterStats.cs
│   │   │   └── PersonName.cs
│   │   ├── Catalog/CatalogItem.cs  CatalogSpell.cs  # novo
│   │   └── Models/Participant.cs  ParticipantSession.cs (novo)
│   ├── Companion.Application/
│   │   ├── Campaigns/CampaignService.cs  ParticipantAuthorization.cs  ParticipantNameBackfill.cs
│   │   ├── Characters/CharacterService.cs  CharacterDtos.cs  CharacterMapper.cs
│   │   ├── Catalog/CatalogService.cs  CatalogSeeder.cs  CatalogDtos.cs
│   │   └── DiceRolls/DiceRollService.cs           # execução com seleção e rótulos
│   ├── Companion.Infrastructure/
│   │   ├── Persistence/ModelConfigurations.cs  CompanionDbContext.cs  Migrations/*_AddPlayerCharacterSheet.cs
│   │   ├── Persistence/Seed/items.srd52.json  spells.magias5e.json
│   │   └── Catalog/EmbeddedItemCatalogSeedSource.cs  EmbeddedSpellCatalogSeedSource.cs
│   └── Companion.Api/Endpoints/CharacterEndpoints.cs  CatalogEndpoints.cs  CampaignEndpoints.cs  Program.cs
└── tests/
    ├── Fixtures/character-rules.cases.json  dice-expressions.cases.json
    ├── Companion.UnitTests/  (DiceExpressionGroupingTests, CharacterRulesTests, PersonNameTests, …)
    └── Companion.IntegrationTests/ (AccountJoinTests, CharacterOwnershipTests, CharacterSheetApiTests,
                                     DiceRollGroupingApiTests, MigrationBackfillTests, CatalogApiTests)

frontend/
├── app/
│   ├── components/ViewNav.vue                     # era MasterViewNav.vue
│   ├── components/character/CharacterSheetView.vue  InventoryPanel.vue  ItemEditorModal.vue
│   │                        CatalogPicker.vue  SpellList.vue  PlayerRollPanel.vue  SavedRollEditor.vue  DicePad.vue
│   ├── components/RollHistory.vue  UserMenu.vue   # execuções da seleção; sair chama DELETE session
│   ├── components/creatures/CreatureLibrary.vue   # remove o crédito do Pocket DM (FR-040)
│   ├── composables/useAppView.ts  useCharacter.ts  useLongPress.ts
│   ├── stores/character.ts
│   ├── types/character.ts
│   ├── utils/rollExpression.ts  savedRollIcons.ts
│   └── pages/index.vue
├── nuxt.config.ts                                 # alvo do proxy por NUXT_API_TARGET (padrão 8080)
└── tests/ (roll-expression.spec.ts, player-roll-panel.spec.ts, view-nav.spec.ts, …, e2e/player-sheet.spec.ts)

scripts/import-catalogs/
├── import-spells.mjs  import-items.mjs  parse-spells.mjs  map-items.mjs  glossary.mjs  units.mjs
├── translations/items.pt-BR.json
├── fixtures/  *.test.mjs  README.md  last-run-report.md
```

**Structure Decision**: o monólito modular existente (`backend/` em 4 camadas, `frontend/` Nuxt), mais `scripts/import-catalogs/` no mesmo molde de `scripts/import-creatures/`. A feature ocupa uma pasta de domínio nova (`Characters`, prevista na constituição) e uma de catálogo, e mexe pouco em `Campaigns` (join, autorização) e `Dice` (gramática).

## Fases de entrega (ordem e dependências)

1. **Fundação de dados e contas**: `ParticipantSession`, `NameKey`, `PersonName`, backfill e join por nome (US 2b). Bloqueia tudo, porque a ficha precisa de uma conta estável.
2. **Gramática de dados**: analisador novo, execução com seleção, `ValuesJson` estendido e `RollHistory`. Independente da fase 1, pode correr em paralelo.
3. **Ficha e regras**: entidades, `CharacterRules` com os casos, endpoints da ficha, nav e tela Ficha (US1, US2).
4. **Inventário e efeitos**: itens, equipar, duplicidade e a mochila na Ficha e no Modo Jogo (US3). Depende da fase 3.
5. **Painel do jogador e rolagens salvas** (US4, US5, US6). Depende das fases 2 e 4.
6. **Catálogos**: importadores, tradução congelada, seeders, endpoints e picker (US7). Os importadores podem correr em paralelo desde o início; o picker depende da fase 4.
7. **Acabamento**: remover o crédito do Pocket DM, e2e completos, verificação de 375 px e revisão crítica.

## Riscos e mitigação

| Risco | Mitigação |
|---|---|
| A migration ou o backfill falham em produção | Testes de integração com um banco "legado" semeado (nomes repetidos, minúsculas). O backfill é idempotente e não derruba a API. A exportação de `Participants` antes da troca está no quickstart. |
| Tradução altera números ou regras | O importador compara os números e dados de cada item; termos mecânicos vêm de uma tabela de código; um item sem tradução falha a importação. |
| Gramática nova quebra rolagens antigas | `DiceExpressionTests` atuais continuam passando sem alteração; o arquivo de casos compartilhado inclui as entradas legadas. |
| Gravação automática gera conflito entre aparelhos | `rowversion` com 409, mais o evento `CharacterChanged` que faz o outro aparelho recarregar. |
| E2E acertar o ambiente em uso por engano | Os scripts e2e da feature falham se `E2E_BASE_URL` não estiver definido ou apontar para a porta 3636. |

## Complexity Tracking

Sem violações da constituição.
