# Feature Specification: Efeitos de características na ficha

**Feature Branch**: `codex/class-features-rolls`

**Created**: 2026-11-20

**Status**: Draft

**Input**: User description: "adicione a capacidade de associar características de classe na ficha, e essas características devem impactar na rolagem; por exemplo, Dueling dá +2 no dano. Corrija também o campo Traços, que apaga um espaço quase instantaneamente."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Característica altera rolagens (Priority: P1)

Como jogador, quero associar efeitos mecânicos às características da ficha para que ataques, danos e demais valores derivados já incluam os bônus da minha classe, espécie ou talento.

**Why this priority**: Sem o efeito mecânico, a característica é apenas uma anotação e o jogador precisa corrigir manualmente cada rolagem.

**Independent Test**: Criar a característica "Dueling" com bônus de dano +2 para ataques corpo a corpo e confirmar que o dano de uma arma corpo a corpo recebe +2, enquanto uma arma à distância não recebe o bônus.

**Acceptance Scenarios**:

1. **Given** uma ficha com espada equipada, **When** o jogador adiciona à característica "Dueling" um bônus de dano +2 com alvo corpo a corpo, **Then** a composição e a rolagem de dano da espada incluem +2 identificado como proveniente de "Dueling".
2. **Given** a mesma característica, **When** o jogador consulta o dano de uma arma à distância ou de uma magia, **Then** o bônus corpo a corpo não é aplicado.
3. **Given** uma característica com efeitos válidos, **When** o jogador salva e recarrega a ficha, **Then** os efeitos permanecem associados e continuam alterando os valores calculados.

---

### User Story 2 - Editar efeitos da característica (Priority: P2)

Como jogador, quero adicionar, alterar e remover efeitos no editor de características usando controles compreensíveis, para representar regras diferentes sem depender do nome da característica.

**Why this priority**: A configuração precisa ser explícita e reutilizável; reconhecer nomes como "Dueling" criaria regras ocultas e frágeis.

**Independent Test**: Abrir o editor, adicionar um efeito, escolher tipo, alvo, operação e valor, salvar, reabrir e remover o efeito.

**Acceptance Scenarios**:

1. **Given** o editor de uma característica, **When** o jogador adiciona um efeito numérico e informa seus campos, **Then** o efeito é validado e salvo junto da característica.
2. **Given** um efeito inválido ou incompleto, **When** o jogador tenta salvar, **Then** a ficha permanece aberta e explica o campo que deve ser corrigido.
3. **Given** uma característica antiga sem efeitos, **When** ela é carregada após a atualização, **Then** continua funcionando e aparece com a lista de efeitos vazia.

---

### User Story 3 - Espaços em Traços não desaparecem (Priority: P3)

Como jogador, quero digitar texto normalmente no campo Traços sem que um espaço no fim da frase desapareça durante o autosave.

**Why this priority**: O defeito interrompe a digitação, embora não altere as regras do personagem.

**Independent Test**: Digitar "Ataque " no campo Traços, aguardar o autosave e confirmar que o espaço continua visível e permite continuar com a próxima palavra.

**Acceptance Scenarios**:

1. **Given** o campo Traços em edição, **When** o jogador digita um espaço e o autosave termina, **Then** o valor visível não é substituído por uma versão sem o espaço.
2. **Given** apenas espaços em Traços, **When** a ficha é recarregada depois do salvamento, **Then** o campo pode ser tratado como vazio sem quebrar a digitação ativa.

### Edge Cases

- Uma característica pode conter até 20 efeitos, seguindo o limite já usado por itens.
- Efeitos numéricos devem respeitar os mesmos alvos, operações e limites aceitos nos efeitos de itens.
- Um bônus de alvo `melee` não afeta ataques à distância nem magias; `all` afeta todos os tipos compatíveis.
- Remover ou alterar a característica deve recalcular imediatamente a resposta completa da ficha.
- Características antigas cujo JSON não possua `effects` devem ser interpretadas como uma lista vazia.
- Uma edição recebida enquanto outra gravação está em andamento não pode ser perdida pela resposta anterior.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: O sistema MUST permitir associar zero a 20 efeitos a cada característica da ficha.
- **FR-002**: Os efeitos de característica MUST usar os mesmos tipos, alvos, operações, limites e mensagens de validação dos efeitos configuráveis de itens.
- **FR-003**: O sistema MUST persistir os efeitos junto da característica e devolver a característica completa após criação ou edição.
- **FR-004**: O servidor MUST calcular todos os valores derivados e rolagens usando os efeitos das características, sem delegar o cálculo ao navegador.
- **FR-005**: Cada parcela derivada de uma característica MUST identificar o nome da característica como origem na composição da rolagem.
- **FR-006**: Um efeito de dano `melee` MUST afetar somente danos de armas corpo a corpo; os alvos `ranged`, `spell` e `all` MUST manter a semântica já usada em itens.
- **FR-007**: O editor MUST permitir adicionar, editar e remover efeitos e MUST impedir o salvamento de efeitos incompletos.
- **FR-008**: Características existentes sem a propriedade de efeitos MUST continuar válidas e ser tratadas como possuindo zero efeitos.
- **FR-009**: Criar, editar ou remover uma característica MUST devolver os valores e rolagens recalculados na mesma resposta.
- **FR-010**: O campo Traços MUST preservar o texto local digitado enquanto o autosave confirma uma versão normalizada do servidor.
- **FR-011**: A correção de Traços MUST manter a normalização de valores vazios no armazenamento e MUST preservar edições feitas durante uma requisição em andamento.
- **FR-012**: Regras de domínio, contrato HTTP e regressões de interface MUST possuir testes automatizados proporcionais às mudanças.

### Key Entities

- **Característica da ficha**: Regra de classe, espécie ou talento com nome, origem, descrição, contador opcional e uma coleção ordenada de efeitos.
- **Efeito**: Alteração declarativa com tipo, alvo, operação e valor opcional, reutilizada por itens e características.
- **Parcela de valor derivado**: Contribuição individual mostrada na composição de um atributo ou rolagem, incluindo o nome da origem.
- **Rascunho da ficha**: Texto local em edição que pode estar à frente da versão confirmada pelo servidor.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Em 100% dos casos automatizados de Dueling, somente danos corpo a corpo recebem exatamente +2 e a origem aparece na composição.
- **SC-002**: O jogador consegue configurar e salvar um efeito de característica em menos de 1 minuto usando apenas os controles do editor.
- **SC-003**: 100% das características preexistentes sem efeitos continuam carregando, editando e salvando sem migração manual.
- **SC-004**: Em teste automatizado, um espaço final em Traços permanece visível após pelo menos dois ciclos de autosave e uma nova palavra pode ser digitada sem reposicionar o cursor.

## Assumptions

- A característica produz efeitos continuamente enquanto existir; contadores de uso não ligam nem desligam seus efeitos.
- Não haverá reconhecimento automático por nome: "Dueling" é configurado pelo usuário como bônus de dano +2 para corpo a corpo.
- Efeitos de características e itens podem somar; a regra existente de supressão de efeitos idênticos entre itens não será aplicada entre fontes de natureza diferente nesta entrega.
- O armazenamento JSON atual aceita a evolução aditiva da coleção de efeitos sem nova tabela ou serviço operacional.

