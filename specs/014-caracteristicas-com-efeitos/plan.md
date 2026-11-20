# Implementation Plan: Características com efeitos nas rolagens

**Branch**: `OficialMark/caracteristicas-efeitos-e778e391` | **Date**: 2026-11-20 | **Spec**: [spec.md](spec.md)

## Summary

`CharacterFeatureData` ganha `Effects` (a mesma `ItemEffect` dos itens, mesma validação, até 20). `CharacterRules.Compute` recebe as características e junta os efeitos delas aos dos itens equipados numa lista só de efeitos ativos, com a origem genérica (id + nome), então todo cálculo que já usa efeitos de item passa a valer para características sem código novo por tipo. Novo alvo `melee-one-handed` para `AttackBonus`/`DamageBonus`: casa com armas corpo a corpo sem `two-handed`, e o dano versátil (duas mãos) não o recebe. O `FeatureEditorModal` reaproveita a seção de efeitos do `ItemEditorModal` extraída para um componente. Sem migration: os dados ficam no JSON da característica; registros antigos sem a lista leem como vazia.

## Technical Context

| Campo | Valor |
|---|---|
| **Language/Version** | C# .NET 10, TypeScript (Nuxt 4, Vue 3) |
| **Primary Dependencies** | ASP.NET Minimal APIs, EF Core, Pinia, Vitest, Playwright |
| **Storage** | SQL Server; sem migration (campo novo dentro da coluna JSON `CharacterFeatures.Data`) |
| **Testing** | xUnit (regras em `CharacterRulesTests` + casos), integração da ficha, Vitest, Playwright |
| **Target Platform** | Navegador desktop e celular |
| **Performance Goals** | Recalcular a ficha continua instantâneo (até 100 características × 20 efeitos) |
| **Constraints** | Fichas e características existentes sem efeitos não podem quebrar |
| **Scale/Scope** | 3 arquivos de domínio, 1 serviço, 3 componentes Vue |

## Constitution Check

| Princípio | Atende | Situação |
|---|---|---|
| I. Monólito modular | Regra no domínio (`CharacterRules`, `ItemEffect`) | PASS |
| II. Servidor autoridade | Cálculo só no servidor; o cliente mostra `stats` | PASS |
| III. Web responsiva | Mesmo editor de efeitos já validado em mobile | PASS |
| IV. Qualidade verificável | Unit das regras, integração, Vitest, e2e | PASS |
| V. Evolução segura | Sem migration; leitura tolerante a JSON antigo | PASS |

## Project Structure

```text
backend/src/Companion.Domain/Characters/CharacterFeature.cs      # Effects em CharacterFeatureData
backend/src/Companion.Domain/Characters/ItemEffect.cs            # alvo melee-one-handed
backend/src/Companion.Domain/Characters/CharacterRules.cs        # efeitos de característica + alvo novo
backend/src/Companion.Application/Characters/CharacterService.cs # passa as características ao Compute
frontend/app/components/character/EffectRowsEditor.vue           # novo: seção de efeitos compartilhada
frontend/app/components/character/ItemEditorModal.vue            # usa EffectRowsEditor
frontend/app/components/character/FeatureEditorModal.vue         # usa EffectRowsEditor
frontend/app/components/character/FeatureList.vue                # mostra os efeitos no card
frontend/app/types/character.ts                                  # effects na característica
```

## Complexity Tracking

Nenhuma violação.
