# Implementation Plan: Compêndio editável pelo mestre e características com contador de usos

**Branch**: `OficialMark/compendio-editavel-habilidades-e778e391` | **Date**: 2026-11-20 | **Spec**: [spec.md](spec.md)

## Summary

Duas fatias independentes:

- **US1, catálogo por campanha**: `CatalogItem` e `CatalogSpell` ganham `CampaignId` e `BaseId`, no mesmo modelo de `Creature` (spec 008). Linhas globais (`CampaignId` nulo) continuam só leitura e são as únicas que o seeder toca. A visão de uma campanha é: linhas da campanha + linhas globais que nenhuma linha da campanha substitui. Endpoints novos (só mestre): criar, salvar (em uma global cria a cópia) e excluir. A adição à ficha resolve pela mesma visão. No frontend, `ItemEditorModal` e `SpellEditorModal` aceitam um modo catálogo (callback de salvar + campos extras), e o `CatalogPicker` em modo `browse` ganha botão "Novo", filtro de origem, selo e ações Editar/Excluir no detalhe.
- **US2, características**: tabela nova `CharacterFeatures` (como `CharacterSpells`: dados em JSON validados no domínio, FK em cascata para a ficha). Operações no `CharacterService`, todas pelo `WriteAsync` (mesmo evento de mudança e sincronia entre campanhas). Aba nova "Características" na ficha, com contador, -1/+1, restaurar, mover e os botões de descanso.
- **US3**: carga de dados do Vashkar pela API nova depois do deploy (script na pasta do spec).

## Technical Context

| Campo | Valor |
|---|---|
| **Language/Version** | C# .NET 10, TypeScript (Nuxt 4 SPA, Vue 3) |
| **Primary Dependencies** | ASP.NET Minimal APIs, EF Core SQL Server, Pinia, Vitest, Playwright |
| **Storage** | SQL Server. Migration `CampaignCatalogAndFeatures`: colunas `CampaignId`, `BaseId` em `CatalogItems` e `CatalogSpells` (FKs, índices filtrados), índice de slug passa a filtrado; tabela `CharacterFeatures` |
| **Testing** | xUnit unit + integração (Testcontainers), Vitest, Playwright |
| **Target Platform** | Navegador desktop e celular |
| **Performance Goals** | Lista do catálogo continua abaixo de 300 ms (cerca de 820 linhas globais + dezenas por campanha) |
| **Constraints** | Compêndio global preservado (memória do Marcos: só o compêndio importa nesta fase). Migration aditiva. Deploy com `docker compose up -d --build` |
| **Scale/Scope** | 1 migration, 2 entidades alteradas, 1 nova, ~8 endpoints, ~6 componentes Vue tocados, 2 novos |

## Constitution Check

| Princípio | Como o plano atende | Situação |
|---|---|---|
| I. Monólito modular | Regras de origem no domínio (`CatalogItem.ForCampaign`, `CharacterFeatureData.Validated`); serviços na Application; EF na Infrastructure | PASS |
| II. Servidor autoridade | Contador limitado no servidor (delta, restaurar, descanso); permissão de mestre no servidor; persistência antes do evento `CharacterChanged` | PASS |
| III. Web responsiva | Aba nova e modais seguem os padrões existentes; teste em Pixel 7 no e2e | PASS |
| IV. Qualidade verificável | Unit (validação, descanso, origem), integração (visão por campanha, seeder não toca, permissão, add à ficha, contador), Vitest dos componentes, e2e das telas | PASS |
| V. Evolução segura | Migration só adiciona; linhas globais existentes ficam com `CampaignId` nulo; índice de slug filtrado mantém a unicidade global | PASS |

## Project Structure

```text
backend/src/Companion.Domain/Catalog/CatalogEntities.cs              # CampaignId, BaseId, ForCampaign, Origin
backend/src/Companion.Domain/Characters/CharacterFeature.cs          # novo: entidade + CharacterFeatureData + FeatureRecovery
backend/src/Companion.Application/Catalog/CatalogService.cs          # visão por campanha, origem, criar/salvar/excluir
backend/src/Companion.Application/Catalog/CatalogSeeder.cs           # ignora linhas de campanha
backend/src/Companion.Application/Characters/CharacterService.cs     # features + resolução de catálogo por campanha
backend/src/Companion.Application/Characters/CharacterDtos.cs        # Features no CharacterDto, requests
backend/src/Companion.Application/Contracts/Abstractions.cs          # CharacterFeatures
backend/src/Companion.Infrastructure/Persistence/*                   # configs, DbSet, delete de campanha, migration
backend/src/Companion.Api/Endpoints/CharacterEndpoints.cs            # rotas novas
frontend/app/types/character.ts                                      # tipos
frontend/app/composables/useCharacterApi.ts                          # chamadas
frontend/app/stores/character.ts                                     # features no estado
frontend/app/components/character/FeatureList.vue                    # novo
frontend/app/components/character/FeatureEditorModal.vue             # novo
frontend/app/components/character/CharacterSheetView.vue             # aba Características
frontend/app/components/character/ItemEditorModal.vue                # modo catálogo
frontend/app/components/character/SpellEditorModal.vue               # modo catálogo
frontend/app/components/character/CatalogPicker.vue                  # Novo, filtro de origem, selo
frontend/app/components/character/CatalogDetail.vue                  # Editar / Excluir
```

## Complexity Tracking

Nenhuma violação.
