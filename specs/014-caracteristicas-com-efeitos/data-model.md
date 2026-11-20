# Data model: spec 014

## CharacterFeatureData (alterado)

| Campo | Tipo | Regra |
|---|---|---|
| `effects` | `ItemEffect[]` | 0..20; cada um validado como efeito de item; `null` (registro antigo) = `[]` |

## ItemEffect (alterado)

Alvos de `AttackBonus` e `DamageBonus`: `all`, `melee`, `ranged`, `spell` e o novo `melee-one-handed`.

`melee-one-handed` casa com uma arma quando ela é corpo a corpo e não tem a propriedade `two-handed`. No dano versátil nunca casa.

## Efeitos ativos (cálculo)

Ordem: efeitos dos itens equipados (ordem da mochila), depois os das características (ordem da lista). Chave de repetição: nome da origem normalizado + `type|target|op|value`. O primeiro conta; os seguintes aparecem em `suppressedEffects`.
