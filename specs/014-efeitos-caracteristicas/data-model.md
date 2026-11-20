# Data Model: Efeitos de características

## CharacterFeatureData

| Campo | Tipo | Regra |
|---|---|---|
| Name | string | Obrigatório, 1–80 após normalização |
| Source | string? | Até 60 |
| Description | string? | Até 8000 |
| MaxUses | int? | Nulo ou 1–999 |
| CurrentUses | int | Controlado pelo servidor |
| Recovery | enum | None, ShortRest ou LongRest |
| Effects | lista de ItemEffect | Ausente/nulo equivale a vazio; máximo 20; cada entrada validada |

## ItemEffect (compartilhado)

| Campo | Tipo | Regra |
|---|---|---|
| Type | EffectType | Tipo conhecido |
| Target | string? | Normalizado de acordo com o tipo |
| Op | EffectOp | Add ou Min quando permitido |
| Value | int? | Obrigatório em efeitos numéricos; ausente em textuais |

## FeatureEntry

Representação imutável usada pelo cálculo: `Id`, `SortOrder` e `Data`. Não cria persistência adicional.

## Transições

- Criar/editar característica valida e normaliza `Effects`.
- JSON legado sem `effects` é projetado como `effects: []` na resposta.
- Adicionar/remover/editar uma característica recalcula todas as estatísticas e rolagens.
- O contador de usos não altera a atividade dos efeitos.

