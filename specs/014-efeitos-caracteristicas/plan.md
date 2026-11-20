# Implementation Plan: Efeitos de características na ficha

**Branch**: `codex/class-features-rolls` | **Date**: 2026-11-20 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/014-efeitos-caracteristicas/spec.md`

## Summary

Evoluir as características já persistidas na ficha para carregarem a mesma coleção declarativa de efeitos usada pelos itens. O domínio valida os efeitos, `CharacterRules` combina efeitos de itens equipados e de características e o serviço sempre devolve estatísticas recalculadas. No frontend, o editor de características ganha as linhas de efeito já conhecidas pelo editor de itens. O bug de Traços é corrigido mantendo o rascunho local exato após a confirmação do autosave, enquanto o servidor continua armazenando texto normalizado.

## Technical Context

**Language/Version**: C# 14 / .NET 10 LTS; TypeScript 5.9; Vue 3.5

**Primary Dependencies**: ASP.NET Core, Entity Framework Core, Nuxt 4, Pinia 3

**Storage**: SQL Server; `CharacterFeatureData` persiste como JSON em `CharacterFeatures.Data`

**Testing**: xUnit (unitário e integração), Vitest + Vue Test Utils (frontend)

**Target Platform**: API em Linux/container; PWA em navegadores modernos Windows e Android

**Project Type**: Aplicação web com backend e frontend

**Performance Goals**: Recalcular a ficha na mesma requisição de escrita, sem chamadas adicionais; listas limitadas a 100 características × 20 efeitos

**Constraints**: Servidor é autoridade; compatibilidade com JSON antigo sem `effects`; nenhuma dependência ou processo operacional novo

**Scale/Scope**: Um agregado de ficha por jogador; alterações restritas ao domínio Characters, API existente e editor da ficha

## Constitution Check

- **Camadas**: PASS — validação e cálculo ficam em Domain; Application apenas carrega e mapeia; UI edita dados declarativos.
- **Autoridade do servidor**: PASS — rolagens e composições são recalculadas no backend antes da resposta.
- **Web responsiva/resiliente**: PASS — controles existentes são reutilizados; rascunho local não é perdido pelo autosave.
- **Qualidade verificável**: PASS — testes unitários de regra, integração de persistência/contrato e regressões de componente estão planejados.
- **Evolução segura**: PASS — evolução aditiva do JSON, com `effects` ausente tratado como vazio; nenhuma migration estrutural é necessária.
- **Dependências operacionais**: PASS — nenhuma nova dependência.

Reavaliação pós-design: todos os gates permanecem atendidos; não há violação a justificar.

## Project Structure

### Documentation (this feature)

```text
specs/014-efeitos-caracteristicas/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── character-feature-api.md
└── tasks.md
```

### Source Code (repository root)

```text
backend/
├── src/
│   ├── Companion.Domain/Characters/
│   └── Companion.Application/Characters/
└── tests/
    ├── Companion.UnitTests/
    └── Companion.IntegrationTests/

frontend/
├── app/
│   ├── components/character/
│   ├── stores/
│   └── types/
└── tests/
```

**Structure Decision**: Manter o monólito modular existente; não criar módulo, endpoint ou tabela novos. A forma de efeito existente é compartilhada por itens e características.

## Complexity Tracking

Nenhuma violação constitucional.

