# Implementation Plan: Biblioteca de criaturas, névoa antes de publicar e alinhamento dos ícones

**Branch**: `OficialMark/creature-library-163f89b1` | **Date**: 2026-11-18 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/008-biblioteca-criaturas/spec.md`

## Summary

1. **Ícones (US1)**: o ícone Lucide entra no selo como um `<svg>` aninhado, cujo tamanho vem de `em`/CSS percentual. Em escalas pequenas o navegador arredonda esse viewport para pixels inteiros de forma diferente do círculo, e o glifo desloca. Correção: desenhar o glifo num `<g transform="translate scale">` com caixa 24x24 fixa (o espaço nativo dos ícones Lucide), sem depender de `em` nem de porcentagem. Detalhes em research R1.
2. **Névoa antes de publicar (US2)**: `BattlemapSetup` ganha uma pré-visualização (imagem local via object URL + grid) que reusa `useFogOfWar` e `FogToolbar`. A máscara vai no mesmo `multipart PUT .../battlemap` (campo `fogMask`), e o servidor aplica `Battlemap.SetFog` na mesma transação, antes do `BattlemapInvalidated`.
3. **Biblioteca (US3)**: nova entidade `Creature` (compêndio global com `CampaignId = null`, mais versões da mesa) e `CreatureImage` (blob separado). Seed de ~725 criaturas a partir de `Persistence/Seed/creatures.pocketdm.json` (gerado por `scripts/import-creatures/`), aplicado no startup por `CompendiumSeeder`, de forma idempotente por hash. Editar uma criatura do compêndio cria uma cópia da mesa (`BaseCreatureId`) que a sobrepõe. As imagens do compêndio são buscadas pelo servidor sob demanda e guardadas em `CreatureImages`. API mestre-only `/api/campaigns/{id}/creatures`.
4. **Combate (US4)**: `POST .../encounters/{id}/creatures` cria N combatentes a partir da ficha (vida, modificador, nome numerado), com `Combatant.CreatureId`, e grava o tamanho na aparência do token. `BattlemapTokenDto.ImageUrl` aponta para a imagem da criatura.
5. **Ficha e rolagem (US5)**: um modal `CreatureSheetModal` com ficha, anotações e `CreatureRollPanel` (quantidade, dado, atributo, adicional). `RollDiceRequest` ganha `Label`, e `DiceRoll.Label` aparece no histórico. A última configuração fica em `BattlemapTokenAppearance` (colunas `LastRoll*`), via `PUT .../tokens/{id}/roll-preset`.

## Technical Context

**Language/Version**: C# / .NET 10; TypeScript + Vue 3.5 / Nuxt 4

**Primary Dependencies**: ASP.NET Core Minimal APIs, EF Core SQL Server, SignalR; Nuxt 4, Pinia, `@nuxt/icon` (Lucide local). Importador: Node 20+ sem dependências novas.

**Storage**: SQL Server. Uma migration: tabelas `Creatures`, `CreatureImages`, `SeedVersions`; colunas `Combatants.CreatureId`, `BattlemapTokenAppearances.LastRollCount/LastRollDie/LastRollAbility/LastRollBonus`, `DiceRolls.Label`.

**Testing**: xUnit (unit + integração com Testcontainers SQL Server), Vitest, `node --test` para o parser do importador.

**Target Platform**: PWA, Windows e Android.

**Project Type**: Web application existente.

**Performance Goals**: busca no compêndio < 1 s (SC-004): índice em `SearchKey` (nome PT+EN normalizado), paginação de 30 e lista sem blobs. Seed idempotente: no-op no startup quando o hash não muda.

**Constraints**: biblioteca é mestre-only (princípio II); imagens validadas no servidor (PNG/JPEG/WebP, 1 MB para upload do mestre); o navegador nunca busca imagens do Pocket DM direto; crédito ao Pocket DM visível.

**Scale/Scope**: ~725 criaturas globais (~2-3 MB de texto) + versões por mesa; até 20 adições por operação.

## Constitution Check

- **I. Clean Architecture**: PASS. Novo módulo `Creatures` (Domain `Creature`, Application `CreatureService`/`CompendiumSeeder`, Infrastructure configuração + seed, Api endpoints). O fetch da imagem do compêndio fica atrás de `ICreatureImageSource` (Application), com a implementação HTTP na Infrastructure.
- **II. Servidor como autoridade**: PASS. Rolagens continuam no servidor (com `Label`); combatentes são criados pelo servidor a partir da ficha; presets de rolagem são persistidos. Eventos continuam sendo invalidações.
- **III. Responsiva e resiliente**: PASS. Modal em tela cheia no celular, lista paginada, falha ao buscar imagem degrada para iniciais.
- **IV. Qualidade verificável**: PASS. Unit: `Creature` (validação, modificador, cópia), seeder (idempotência), numeração de nomes. Integração: CRUD mestre-only, override por mesa, adicionar ao combate, imagem, publicação com névoa inicial, preset, label de rolagem. Vitest: alinhamento do glifo, preview de névoa, lista/filtros, modal, rolagem e preset. Importador: `node --test`.
- **V. Simplicidade operacional**: PASS, com uma justificativa registrada. Nova dependência operacional: busca HTTP de imagem no Pocket DM, sob demanda e com cache em banco. É opcional: se falhar, o token usa as iniciais. Não adiciona serviço, fila nem cache externo.

## Project Structure

```text
scripts/import-creatures/              importador (Node) + fixtures + testes + README
backend/src/Companion.Domain/Models/   Creature.cs, CreatureImage.cs, CreatureSize.cs, Ability.cs; Combatant.CreatureId; BattlemapTokenAppearance.LastRoll*; DiceRoll.Label
backend/src/Companion.Application/Creatures/  CreatureService.cs, CompendiumSeeder.cs, CreatureDtos.cs, ICreatureImageSource.cs
backend/src/Companion.Infrastructure/Persistence/  configs, migration, Seed/creatures.pocketdm.json (EmbeddedResource)
backend/src/Companion.Infrastructure/Creatures/HttpCreatureImageSource.cs
backend/src/Companion.Api/Endpoints/CreatureEndpoints.cs (+ Battlemap/Encounter/DiceRoll endpoints)
frontend/app/components/creatures/     CreatureLibrary.vue, CreatureCard.vue, CreatureSheetModal.vue, CreatureRollPanel.vue, CreatureEditor.vue
frontend/app/components/battlemap/     BattlemapSetup.vue (preview + névoa), FogPreview.vue, BattlemapToken.vue (glifo), AttackAreaPanel.vue (botão Ficha)
frontend/app/utils/                    creatureRoll.ts (expressão, modificador, parser de ações), statusConditions.ts
frontend/app/stores/creatures.ts       store da biblioteca
frontend/app/pages/index.vue           abas Mesa / Criaturas (mestre)
```

## Complexity Tracking

| Item | Por quê | Alternativa mais simples rejeitada |
|---|---|---|
| Busca HTTP de imagens do compêndio sob demanda | ~725 imagens (~10 MB) não podem ser commitadas (regra de binários) e o navegador não deve fazer hotlink | Commitar as imagens: viola "no binaries"; hotlink direto: expõe o cliente a outra origem e quebra offline |
