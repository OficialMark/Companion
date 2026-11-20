# Feature Specification: Características com efeitos nas rolagens

**Feature Branch**: `OficialMark/caracteristicas-efeitos-e778e391`
**Created**: 2026-11-20
**Status**: Draft
**Input**: Pedido do Marcos: "adicione a capacidade de associar características de classe na ficha, e essas características devem impactar na rolagem; meu paladino por exemplo usa duelling, então tem +2 no dano". As características com contador já existem (spec 013); os efeitos mecânicos já existem nos itens (spec 011).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Característica com efeito muda os números e as rolagens (Priority: P1)

O jogador associa à característica os mesmos efeitos que um item mágico pode ter (bônus de dano, de ataque, de CA, de salvaguarda, de perícia, atributo, deslocamento, resistência e afins). A característica não precisa ser equipada: o efeito vale enquanto ela estiver na ficha. Os números da ficha e as rolagens já saem com o bônus, e a composição do valor mostra de onde ele veio.

**Why this priority**: estilos de luta, auras e talentos mudam rolagens toda rodada. Sem isso o jogador soma de cabeça e o histórico de rolagens mostra o valor errado.

**Independent Test**: Vashkar com "Estilo de Luta: Duelismo" (dano +2, corpo a corpo de uma mão). O dano do Martelo de Guerra passa de 1d8+3 para 1d8+5 e a composição mostra "Duelismo +2"; a rolagem pelo painel usa +5.

**Acceptance Scenarios**:

1. **Given** uma característica sem efeitos, **When** o jogador adiciona o efeito "Bônus de dano +2, corpo a corpo de uma mão" e salva, **Then** o dano de toda arma corpo a corpo que não é de duas mãos ganha +2, com a origem igual ao nome da característica.
2. **Given** o efeito de Duelismo, **When** o jogador olha uma arma de duas mãos (Malho) ou uma arma à distância, **Then** o dano dela não muda.
3. **Given** uma arma versátil (Martelo de Guerra), **When** a ficha mostra o dano com uma mão e o dano versátil (duas mãos), **Then** só o dano com uma mão recebe o bônus de Duelismo.
4. **Given** uma característica com "CA +1" ou "Salvaguarda +3 todas", **When** a ficha recalcula, **Then** CA e salvaguardas mostram o bônus com a origem da característica.
5. **Given** um efeito igual (mesmo nome de origem e mesmo efeito) vindo de dois lugares, **When** a ficha recalcula, **Then** ele conta uma vez só e a ficha avisa que o repetido não somou, como já acontece com itens.
6. **Given** uma característica com efeitos, **When** o jogador a remove, **Then** os números voltam ao que eram sem ela.
7. **Given** uma rolagem salva ou uma rolagem do painel com essa arma, **When** o jogador rola, **Then** a expressão usa o novo valor e o rótulo mostra a origem do bônus.

### User Story 2 - Duelismo do Vashkar cadastrado (Priority: P2)

Depois do deploy, o Vashkar tem a característica "Estilo de Luta: Duelismo" (Paladino 2) com dano +2 corpo a corpo de uma mão.

**Independent Test**: entrar como "Vashkar": o Martelo de Guerra e o Maul Leve +1 (versátil, uma mão) mostram +2 a mais no dano; o dano versátil não muda.

### Edge Cases

- Efeito inválido (alvo desconhecido, valor fora de -30..30, texto vazio): recusado com mensagem em português, como nos itens.
- Mais de 20 efeitos numa característica: recusado.
- Característica salva antes desta mudança (sem lista de efeitos): continua funcionando, sem efeitos.
- Um item e uma característica com o mesmo nome e o mesmo efeito: contam uma vez (regra existente: nome + efeito).
- O alvo "corpo a corpo de uma mão" em bônus de ataque também respeita a regra: só armas corpo a corpo sem duas mãos.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Cada característica MUST poder ter até 20 efeitos, dos mesmos tipos e com as mesmas validações dos efeitos de item.
- **FR-002**: Os efeitos de característica MUST valer sempre, sem depender de equipar, em todos os números derivados e rolagens onde o mesmo efeito de item vale.
- **FR-003**: A composição de cada valor MUST mostrar a característica como origem do bônus.
- **FR-004**: Bônus de ataque e de dano MUST aceitar o alvo "corpo a corpo de uma mão": vale para armas corpo a corpo sem a propriedade duas mãos e não entra no dano versátil.
- **FR-005**: A regra de efeito repetido (mesmo nome de origem + mesmo efeito conta uma vez, e o repetido é avisado) MUST valer entre itens e características.
- **FR-006**: O editor de característica MUST ter a seção de efeitos com os mesmos controles do editor de item.
- **FR-007**: Características existentes sem efeitos MUST continuar abrindo e funcionando.

### Key Entities

- **Característica** (spec 013): ganha a lista de efeitos.
- **Efeito**: tipo, alvo, operação, valor (o mesmo dos itens); novo alvo de ataque e dano "corpo a corpo de uma mão".

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Com Duelismo cadastrado, o dano de arma corpo a corpo de uma mão do Vashkar sobe exatamente 2 e nenhum outro valor da ficha muda (verificado em teste automatizado e na ficha real).
- **SC-002**: 100% das rolagens de dano feitas a partir da ficha com essa arma incluem o bônus, sem conta manual.
- **SC-003**: Associar um efeito a uma característica leva menos de 1 minuto.

## Assumptions

- "Associar características de classe" = dar efeitos mecânicos às características que a ficha já tem (spec 013). Não há catálogo de características de classe; o jogador cadastra a sua.
- Duelismo exige, na regra, que não haja outra arma na outra mão. A ficha não sabe o que está em cada mão; o escudo é permitido pela regra, e a segunda arma fica a cargo do jogador.
- Efeitos de característica não têm condição de ativação (sem "só enquanto em fúria"); quem precisa ligar e desligar remove ou edita o efeito.
