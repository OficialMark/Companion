# Feature Specification: Biblioteca de criaturas, névoa antes de publicar e alinhamento dos ícones

**Feature Branch**: `OficialMark/creature-library-163f89b1` (empilhado sobre o PR #4)

**Created**: 2026-11-18

**Status**: Draft

**Input**: User description: "os ícones de condições ficaram bons, porém, quando ficam pequenos, eles ficam desalinhados; permita posicionar a névoa enquanto seleciona a imagem para subir, pois no modelo atual os players veem a imagem completa antes; também adicione uma aba de biblioteca de criaturas, já carregada com um catálogo de criaturas e permitindo que o mestre adicione suas próprias, o catálogo deve ser guardado em banco e o mestre pode editar as características de qualquer criatura, inclusive adicionar ou editar imagens (para aparecer no ícone do token), o mestre pode adicionar uma criatura do catálogo no battlemap, clicando em cima dela, pode ver num modal seus atributos, habilidades, ataques, e anotações pessoais, também pode fazer uma rolagem específica para aquela criatura selecionando o dado, o atributo modificador e um valor adicional (que é um valor incrementável), dentro desse battlemap, pra cada token, guarde sua última rolagem, já a pré-carregando pré-selecionado quando for rolar para aquela criatura; compêndio de criaturas para carregar no banco https://pocketdm.com.br/monstros"

**Fonte do compêndio**: o dono do projeto obteve autorização da equipe do Pocket DM para usar o compêndio PT-BR deles. A página de licença do Pocket DM pede crédito e link; o conteúdo SRD de origem é CC BY 4.0 (Wizards of the Coast).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Ícones de condição alinhados em qualquer zoom (Priority: P1)

Com o mapa afastado (células pequenas), os ícones de condição continuam centralizados dentro do selo circular de cada token.

**Why this priority**: É um defeito visual numa feature recém-entregue, visível em toda mesa com mapa grande, e de correção pequena.

**Independent Test**: Marcar três condições num token, reduzir o zoom ao mínimo e conferir que cada ícone fica centralizado no seu círculo (desvio menor que 1 px de tela).

**Acceptance Scenarios**:

1. **Given** um token com condições, **When** o zoom do mapa é reduzido até o mínimo, **Then** cada ícone permanece centralizado no seu selo.
2. **Given** o mesmo token, **When** o zoom é aumentado até o máximo, **Then** os ícones continuam centralizados e nítidos.

---

### User Story 2 - Névoa posicionada antes de publicar o mapa (Priority: P1)

Ao escolher a imagem de um novo mapa (ou substituir o atual), o mestre vê uma pré-visualização com o grid e pode cobrir tudo e revelar só o ponto de partida antes de publicar. Os jogadores nunca veem o mapa descoberto.

**Why this priority**: Hoje o mapa aparece inteiro para os jogadores no instante em que é publicado, antes de o mestre conseguir cobri-lo. Isso estraga a exploração.

**Independent Test**: O mestre escolhe a imagem, deixa "Publicar coberto pela névoa" ligado, revela uma sala na pré-visualização e publica. Um jogador conectado nunca recebe o mapa sem névoa: o primeiro estado que ele vê já tem só a sala revelada.

**Acceptance Scenarios**:

1. **Given** o mestre selecionou uma imagem, **When** a pré-visualização aparece, **Then** ela mostra o grid escolhido e as ferramentas de névoa (ocultar/revelar, pincel, retângulo, cobrir tudo, revelar tudo).
2. **Given** um mapa novo, **When** a pré-visualização abre, **Then** "Publicar coberto pela névoa" vem ligado e o mapa aparece todo coberto.
3. **Given** o mestre mudou colunas ou linhas, **When** a pré-visualização atualiza, **Then** a névoa desenhada é refeita para o novo grid (coberta por inteiro se a opção estiver ligada) e o mestre é avisado.
4. **Given** o mestre publica, **When** os jogadores recebem o mapa, **Then** o primeiro estado recebido já contém a névoa definida, e criaturas cobertas não são enviadas.
5. **Given** o mestre substitui a imagem mantendo o mesmo grid, **When** abre a pré-visualização, **Then** ela começa com a névoa atual do mapa.

---

### User Story 3 - Biblioteca de criaturas com compêndio (Priority: P1)

O mestre abre a aba "Criaturas" e encontra o compêndio de ~725 criaturas em português, com busca por nome e filtros por ND, tipo e tamanho. Pode criar criaturas próprias e editar qualquer uma (atributos, ações, imagem, anotações). As edições valem só para a sua mesa.

**Why this priority**: É a base das histórias 4 e 5 e elimina a digitação manual de criaturas.

**Independent Test**: Buscar "goblin", abrir a ficha, editar a CA e a imagem, salvar, recarregar e confirmar que a mudança persiste só nesta mesa (outra mesa vê o original).

**Acceptance Scenarios**:

1. **Given** uma mesa nova, **When** o mestre abre a aba Criaturas, **Then** o compêndio completo está disponível, paginado, com busca por nome (PT e EN) e filtros por ND, tipo e tamanho.
2. **Given** o mestre cria uma criatura própria, **When** salva, **Then** ela aparece na lista marcada como "Minha".
3. **Given** o mestre edita uma criatura do compêndio, **When** salva, **Then** a versão editada substitui a original apenas nesta mesa, marcada como "Editada", com opção de restaurar a original.
4. **Given** o mestre envia uma imagem PNG, JPEG ou WebP de até 1 MB, **When** salva, **Then** ela aparece na ficha e no ícone dos tokens dessa criatura.
5. **Given** um jogador, **When** abre o app, **Then** a aba Criaturas não existe para ele e a API recusa qualquer acesso à biblioteca.
6. **Given** o app em uso, **When** o mestre abre a biblioteca, **Then** há crédito visível ao Pocket DM com link e o aviso de licença do SRD.

---

### User Story 4 - Adicionar criatura do catálogo ao mapa (Priority: P2)

Na biblioteca (ou na ficha), o mestre toca "Adicionar ao combate" e escolhe quantas. As criaturas entram no encontro atual com nome numerado, vida, modificador de iniciativa, tamanho e imagem vindos da ficha, prontas para posicionar no mapa.

**Why this priority**: Fecha o ciclo entre catálogo e combate; sem isso a biblioteca é só leitura.

**Independent Test**: Com um encontro aberto, adicionar 3 "Goblin". Aparecem "Goblin 1, 2, 3" na iniciativa e na reserva do mapa, cada um com 7/7 de vida, modificador +2, imagem do goblin; ao posicionar no mapa, ocupam 1x1.

**Acceptance Scenarios**:

1. **Given** há um encontro em preparação, **When** o mestre adiciona N cópias de uma criatura, **Then** N combatentes são criados com nome numerado sem repetir números já usados, vida atual = vida máxima = PV da ficha, modificador = modificador de Destreza e tamanho do token pelo tamanho da criatura (Grande 2x2, Enorme 3x3, Imenso 4x4).
2. **Given** não há encontro em preparação (nenhum combate ou combate já iniciado), **When** o mestre tenta adicionar, **Then** o botão explica o motivo e, sem combate, oferece atalho para iniciar a preparação.
3. **Given** a criatura tem imagem, **When** o token aparece no mapa ou na reserva, **Then** a imagem é exibida no lugar das iniciais.
4. **Given** a ficha da criatura é editada depois, **When** os tokens já existentes são exibidos, **Then** a imagem atualizada aparece, mas a vida dos combatentes já criados não muda.

---

### User Story 5 - Ficha no modal e rolagem da criatura (Priority: P2)

Tocando numa criatura do catálogo, ou num token de criatura no mapa (pelo menu do token), o mestre vê a ficha completa num modal: atributos e modificadores, CA, PV, deslocamento, perícias, sentidos, traços, ações, ações lendárias e suas anotações pessoais. No modal, rola para a criatura escolhendo o dado, o atributo modificador e um valor adicional com botões de mais e menos. No mapa, cada token lembra a última configuração e a traz pré-selecionada.

**Why this priority**: É o uso em mesa da biblioteca, mas depende das histórias 3 e 4.

**Independent Test**: No token "Goblin 1", abrir a ficha, escolher d20 + DES + 2 e rolar. O histórico mostra "Goblin 1: 1d20+2+2" com o resultado. Fechar, reabrir a ficha do mesmo token: d20, DES e +2 já vêm selecionados. Abrir "Goblin 2": vem o padrão (d20, sem atributo, +0).

**Acceptance Scenarios**:

1. **Given** o modal aberto, **When** exibido em celular, **Then** ocupa a tela com rolagem interna, cabeçalho fixo com nome e botão de fechar, e todos os controles com 44 px ou mais.
2. **Given** a seção de rolagem, **When** o mestre escolhe d4, d6, d8, d10, d12, d20 ou d100, um atributo (FOR, DES, CON, INT, SAB, CAR ou nenhum) e um adicional entre -20 e +20, **Then** a expressão resultante aparece antes de rolar (ex.: "1d20 + 2 (DES) + 1").
3. **Given** o mestre rola, **When** o resultado chega, **Then** a rolagem entra no histórico da mesa identificada com o nome do token ou da criatura.
4. **Given** um token no mapa, **When** o mestre rola pela ficha desse token, **Then** a configuração fica guardada para aquele token naquele mapa e volta pré-selecionada na próxima vez, inclusive depois de recarregar a página.
5. **Given** o mestre escreve anotações pessoais, **When** salva, **Then** elas ficam na ficha da criatura nesta mesa e nunca são enviadas a jogadores.
6. **Given** uma ação da ficha mostra um ataque (ex.: "+4 para acertar, 5 (1d6 + 2)"), **When** o mestre toca nela, **Then** a seção de rolagem é preenchida com o dado e os valores da ação para rolar o acerto ou o dano em um toque.

---

### Edge Cases

- Compêndio com criatura sem imagem: o token usa as iniciais; a ficha mostra um ícone neutro.
- Imagem do compêndio indisponível no momento (site fora do ar): o servidor tenta buscar uma vez, guarda o resultado quando consegue e, enquanto isso, mostra as iniciais sem erro.
- Mestre restaura a versão original de uma criatura editada: anotações e imagem próprias são descartadas após confirmação.
- Adicionar 20 criaturas de uma vez: limite de 20 por operação, com mensagem clara acima disso.
- Combatente criado a partir de uma criatura que depois foi excluída da biblioteca da mesa: o combatente continua no combate, sem ficha (o botão "Ficha" some).
- Rolagem com adicional negativo e atributo negativo: expressão e resultado corretos (ex.: 1d20 - 1 - 2).
- Duas abas do mestre editando a mesma criatura: a segunda gravação recebe aviso de conflito e recarrega.
- Grid trocado na pré-visualização depois de desenhar a névoa: a névoa desenhada é descartada e a opção "coberto" reaplicada, com aviso.
- Pré-visualização em celular: o editor de névoa cabe na largura sem rolagem horizontal.

## Requirements *(mandatory)*

### Functional Requirements

**Ícones**

- **FR-001**: Os ícones de condição DEVEM ficar centralizados no selo em qualquer zoom do mapa, com desvio visual menor que 1 px de tela.

**Névoa antes de publicar**

- **FR-002**: O formulário de publicação do mapa DEVE mostrar uma pré-visualização da imagem com o grid escolhido assim que a imagem é selecionada.
- **FR-003**: A pré-visualização DEVE oferecer as mesmas ferramentas de névoa do mapa publicado (ocultar/revelar, pincel 1/3/5, retângulo, cobrir tudo, revelar tudo), com mouse e toque.
- **FR-004**: Para mapa novo, a opção "Publicar coberto pela névoa" DEVE vir ligada.
- **FR-005**: A névoa definida na pré-visualização DEVE ser gravada na mesma operação que publica a imagem, de forma que nenhum participante receba o mapa sem ela.
- **FR-006**: Ao substituir a imagem mantendo o grid, a pré-visualização DEVE começar com a névoa atual; mudando o grid, DEVE recomeçar e avisar.

**Biblioteca**

- **FR-007**: O sistema DEVE manter um compêndio global de criaturas carregado no banco a partir do Pocket DM (~725 criaturas), com nome PT e EN, tamanho, tipo, alinhamento, CA, PV e dados de vida, deslocamento, iniciativa, seis atributos, salvaguardas, perícias, vulnerabilidades/resistências/imunidades, imunidades a condição, sentidos, idiomas, ND, XP, traços, ações, ações bônus, reações, ações lendárias, fonte e imagem.
- **FR-008**: A biblioteca DEVE ser acessível apenas ao mestre da mesa; jogadores não veem a aba nem acessam a API.
- **FR-009**: A lista DEVE permitir busca por nome (PT e EN, sem acento e sem diferenciar maiúsculas), filtros por ND, tipo e tamanho, e paginação, respondendo rápido mesmo com todo o compêndio.
- **FR-010**: O mestre DEVE poder criar criaturas próprias com os mesmos campos.
- **FR-011**: O mestre DEVE poder editar qualquer criatura; editar uma criatura do compêndio cria uma versão da mesa que a substitui só nessa mesa, preservando o original e permitindo restaurá-lo.
- **FR-012**: O mestre DEVE poder enviar, trocar e remover a imagem de uma criatura (PNG, JPEG ou WebP, até 1 MB), validada no servidor.
- **FR-013**: A biblioteca DEVE exibir crédito ao Pocket DM com link e o aviso de licença CC BY 4.0 do SRD.
- **FR-014**: O mestre DEVE poder excluir criaturas próprias ou versões editadas da mesa (o original do compêndio nunca é excluído).

**Combate**

- **FR-015**: O mestre DEVE poder adicionar de 1 a 20 cópias de uma criatura ao encontro em preparação (a mesma fase em que hoje se adicionam combatentes), com nome numerado único, vida atual e máxima pelos PV, modificador de iniciativa pela Destreza (ou pela iniciativa da ficha, quando houver) e tamanho do token pelo tamanho da criatura.
- **FR-016**: Combatentes criados a partir da biblioteca DEVEM guardar o vínculo com a criatura de origem.
- **FR-017**: Tokens de criaturas vinculadas a uma criatura com imagem DEVEM exibir a imagem no mapa e na reserva, para todos os participantes.

**Ficha e rolagem**

- **FR-018**: O mestre DEVE poder abrir a ficha completa em modal a partir da biblioteca e a partir do menu de um token de criatura vinculada.
- **FR-019**: O modal DEVE permitir editar as anotações pessoais da criatura e salvá-las sem sair da ficha.
- **FR-020**: O modal DEVE ter rolagem com escolha de dado (d4, d6, d8, d10, d12, d20, d100), atributo (seis ou nenhum) e adicional inteiro de -20 a +20 com botões de incremento, mostrando a expressão final antes de rolar.
- **FR-021**: A rolagem DEVE ser feita pelo servidor (como as demais) e aparecer no histórico identificada pelo nome do token ou da criatura.
- **FR-022**: Para cada token em cada mapa, o sistema DEVE guardar a última configuração de rolagem (dado, atributo, adicional) e pré-selecioná-la ao reabrir a ficha desse token, inclusive após recarregar.
- **FR-023**: Tocar numa ação da ficha que contenha um bônus de acerto ou uma expressão de dano DEVE preencher a seção de rolagem com esses valores.

**Usabilidade**

- **FR-024**: Aba, lista, modal e formulário DEVEM funcionar em 360 px, 768 px e 1280 px sem rolagem horizontal, com alvos de toque de 44 px ou mais e uso por teclado (Esc fecha o modal, foco preso no modal).

### Key Entities

- **Criatura**: ficha completa de uma criatura (campos de FR-007), com origem (compêndio, versão editada da mesa ou própria da mesa), imagem opcional e anotações pessoais (só nas versões da mesa).
- **Combatente vinculado**: combatente do encontro que guarda de qual criatura veio.
- **Última rolagem do token**: dado, atributo e adicional usados pela última vez para um token num mapa.
- **Névoa inicial**: estado de névoa enviado junto com a publicação da imagem do mapa.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 0 casos em que um jogador recebe o mapa publicado sem a névoa definida na pré-visualização.
- **SC-002**: O mestre encontra uma criatura pelo nome e a adiciona ao combate em menos de 15 segundos.
- **SC-003**: Pelo menos 98% das criaturas do compêndio carregadas com PV, CA e os seis atributos.
- **SC-004**: A busca devolve resultados em menos de 1 segundo com o compêndio completo.
- **SC-005**: Uma rolagem repetida para o mesmo token exige no máximo 1 toque após abrir a ficha (configuração pré-selecionada).
- **SC-006**: Ícones de condição centralizados (desvio < 1 px) em 100% dos níveis de zoom testados (mínimo, padrão, máximo).
- **SC-007**: Em 360 px, 768 px e 1280 px, nenhuma rolagem horizontal e todos os controles novos com 44 px ou mais.

## Assumptions

- O compêndio é global (igual para todas as mesas). Edições, criaturas próprias, imagens enviadas e anotações pertencem à mesa (não há contas de usuário entre mesas no app).
- O compêndio é carregado por um importador a partir do site do Pocket DM, com autorização obtida pelo dono do projeto, e gravado como dado de semente versionado; as imagens do compêndio são buscadas sob demanda pelo servidor e guardadas, nunca carregadas pelo navegador direto do site.
- Conteúdo do compêndio restrito pelo Pocket DM a usuários logados (Kobold Press) não é importado.
- "Clicando em cima dela" é atendido nos dois lugares: no card da biblioteca e no menu do token de criatura vinculada.
- A última rolagem é guardada para tokens no mapa (por token e por mapa); no catálogo, a rolagem usa o padrão d20 sem atributo.
- A rolagem segue a visibilidade normal do histórico da mesa (pública); rolagem secreta do mestre fica fora do escopo.
- Limite conhecido herdado da spec 007: o arquivo da imagem do mapa ainda é baixado inteiro pelo navegador do jogador; a névoa é opaca na interface.
