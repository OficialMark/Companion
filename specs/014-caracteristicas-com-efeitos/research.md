# Research: spec 014

## R1. Onde aplicar os efeitos de característica
- **Decision**: unir aos efeitos de itens equipados na mesma lista de efeitos ativos de `CharacterRules`, com origem genérica (id, nome).
- **Rationale**: todo tipo de efeito já é tratado ali uma vez (CA, atributos, perícias, ataque, dano...). Uma fonte nova não precisa de código por tipo.
- **Alternatives**: calcular bônus de característica à parte em cada número (duplicaria cada regra).

## R2. Duelismo
- **Decision**: alvo `melee-one-handed` para bônus de ataque e dano: arma corpo a corpo sem `two-handed`; nunca no dano versátil.
- **Rationale**: é a condição observável da ficha. O dano versátil é, por definição, o golpe com as duas mãos, onde Duelismo não vale.
- **Alternatives**: bônus "corpo a corpo" simples (somaria no Malho de duas mãos e no versátil, errado pela regra).

## R3. Efeito repetido
- **Decision**: mesma chave dos itens (nome normalizado + efeito), com os itens equipados primeiro e as características depois.
- **Rationale**: FR-005; o aviso já existente cobre os dois.

## R4. Sem migration
- **Decision**: `effects` dentro do JSON da característica; `null` lido como lista vazia.
- **Rationale**: a coluna já é JSON validado no domínio; as características gravadas hoje não têm o campo.

## R5. Editor
- **Decision**: extrair a seção de efeitos do `ItemEditorModal` para `EffectRowsEditor.vue`, usada pelos dois editores.
- **Rationale**: um só lugar para a tabela de efeitos (FR-006); o item continua igual.
