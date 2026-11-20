# Implementation Plan: Mesa com compêndio em abas, busca ampla, rolagem completa e overview da ficha

**Branch**: `OficialMark/mesa-compendio-rolagem-c6d85eea` | **Date**: 2026-11-20 | **Spec**: [spec.md](spec.md)

## Summary

A spec tem sete ajustes. Quase tudo é frontend e reaproveita peças existentes:

- **Rolagem**: o `PlayerRollPanel` passa a aparecer também na visão completa. Para o mestre, um seletor de criaturas da mesa alimenta o `CreatureRollPanel`.
- **Compêndio**: o `CatalogPicker` ganha um modo só leitura, usado nas abas Magias e Itens.
- **Backend**: só a busca do catálogo muda. Entra a coluna `SearchText` (nome PT + nome EN + descrição, normalizada) em itens e magias, e `NameEn` em magias, preenchido por uma tabela PT→EN fixa no seed.

## Technical Context

| Campo | Valor |
|---|---|
| **Language/Version** | C# .NET 10, TypeScript (Nuxt 4 SPA, Vue 3) |
| **Primary Dependencies** | ASP.NET Minimal APIs, EF Core SQL Server, Pinia, Vitest, Playwright |
| **Storage** | SQL Server. Migration nova: `CatalogItems.SearchText`, `CatalogSpells.NameEn` e `CatalogSpells.SearchText` |
| **Testing** | xUnit unit + integração (Testcontainers), Vitest, Playwright desktop e Pixel 7 |
| **Target Platform** | Navegador desktop e celular |
| **Performance Goals** | Busca do catálogo abaixo de 300 ms. São cerca de 820 linhas; `Contains` em `nvarchar(max)` sem índice basta |
| **Constraints** | Compêndio preservado. Deploy com `docker compose up -d --build` depois do merge |
| **Scale/Scope** | 1 migration, cerca de 6 componentes Vue tocados, 2 novos (`CompendiumTabs`, `SheetOverviewModal`) |

## Constitution Check

| Princípio | Como o plano atende | Situação |
|---|---|---|
| I. Monólito modular | Busca no `CatalogService`, regra no domínio (`CatalogText.SearchText`) | PASS |
| II. Servidor autoridade | Rolagens continuam pela rota de dados do servidor. A expressão da criatura usa o mesmo construtor da spec 008. A vida é salva pela escrita da ficha | PASS |
| III. Web responsiva | Abas, overview e seletor testados em Pixel 7. O modal segue o padrão de foco existente (`trapTab`) | PASS |
| IV. Qualidade verificável | Teste de integração da busca (nome EN, descrição, ordem), Vitest dos componentes, e2e das telas | PASS |
| V. Evolução segura | Migration aditiva. O seed recalcula `SearchText` por hash, então o compêndio não perde linhas | PASS |

## Project Structure

```text
backend/src/Companion.Domain/Catalog/CatalogEntities.cs          # NameEn (magia), SearchText (ambos)
backend/src/Companion.Application/Catalog/CatalogService.cs      # busca e ordenação por relevância
backend/src/Companion.Application/Catalog/CatalogSeeder.cs       # aplica nomeEn da tabela
backend/src/Companion.Infrastructure/Persistence/Seed/spells.names-en.json   # tabela PT→EN (nova)
backend/src/Companion.Infrastructure/Migrations/*CatalogSearchText*
frontend/app/components/CompendiumTabs.vue                       # novo: abas Criaturas | Magias | Itens
frontend/app/components/character/CatalogPicker.vue              # prop browse (só leitura)
frontend/app/components/MasterRollPanel.vue                      # novo: seletor de criatura + CreatureRollPanel / DiceRoller
frontend/app/components/SheetOverviewModal.vue                   # novo: overview + dano/cura
frontend/app/components/InitiativeModifier.vue                   # vira o resumo de rodapé que abre o overview
frontend/app/pages/index.vue                                     # rolagem por papel, código do mestre na barra
frontend/app/utils/scrollToResult.ts                             # novo: rolar até o resultado (reduced motion)
frontend/app/assets/css/*.css                                    # optgroup escuro
```

## Complexity Tracking

Nenhuma violação.
