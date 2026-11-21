# Feature Specification: Ataques Tipados e Caderno da Campanha

**Feature Branch**: `014-campanha-armas-dano`\
**Created**: 2026-11-21\
**Status**: Draft\
**Input**: User description: "Catalogar tipos de dano das armas, permitir dano adicional de outro tipo em ataques salvos e criar uma área Campanha colaborativa para NPCs, mapas, pistas e diário, com notas privadas do mestre, visibilidade configurável e pontos marcados nos mapas."

## User Scenarios & Testing

### User Story 1 - Configurar dano tipado em ataques (Priority: P1)

Como jogador, quero salvar ataques com componentes de dano separados por tipo, para rolar de uma vez o dano da arma e de efeitos adicionais, como uma adaga que causa dano cortante e dano gélido.

**Why this priority**: Reaproveita a ficha e as rolagens salvas existentes e resolve uma necessidade recorrente em combate.

**Independent Test**: Cadastrar uma arma com seu dano-base e tipo, criar um ataque com um componente adicional de outro tipo, rolar e confirmar que cada resultado e o total ficam identificados corretamente.

**Acceptance Scenarios**:

1. **Given** uma arma do catálogo com dano físico e tipo de dano, **When** o jogador a seleciona na ficha, **Then** um ataque salvo pode usar os dados-base, o atributo aplicável e o tipo de dano catalogado.
2. **Given** um ataque salvo associado a uma arma, **When** o jogador adiciona um efeito encantado, **Then** pode configurar dados e tipo de dano adicionais sem substituir o dano original.
3. **Given** um ataque com vários componentes, **When** o jogador o rola, **Then** a rolagem mostra o resultado de cada componente e o total, identificando o tipo de dano de cada um.
4. **Given** uma arma com mais de um tipo de dano válido, **When** o mestre ou mantenedor edita o catálogo, **Then** o jogador pode escolher o tipo correspondente ao componente configurado.
5. **Given** uma rolagem salva de ataque existente, **When** a feature é instalada, **Then** seus dados, referências e resultados continuam disponíveis e editáveis.

### User Story 2 - Consultar e construir o caderno compartilhado (Priority: P1)

Como participante da campanha, quero abrir **Campanha** e consultar ou cadastrar NPCs, mapas, pistas e entradas de diário para que as informações descobertas fiquem organizadas e acessíveis ao grupo.

**Why this priority**: Dá ao grupo uma fonte comum de conhecimento que persiste entre sessões e reduz a dependência de anotações espalhadas.

**Independent Test**: Um jogador cadastra um NPC, uma pista, um mapa com descrição e uma entrada de diário; outro participante e o mestre veem as informações confirmadas após atualização e reconexão.

**Acceptance Scenarios**:

1. **Given** um participante de uma campanha, **When** seleciona **Campanha** na navegação superior, **Then** acessa as áreas NPCs, Mapas, Pistas e Diário.
2. **Given** um jogador ou mestre na área Campanha, **When** cria ou complementa uma informação compartilhada, **Then** o conteúdo salvo identifica autor e data e aparece aos demais participantes autorizados.
3. **Given** uma área de campanha com registros, **When** um participante busca ou filtra informações, **Then** consegue localizar registros pelo nome e tipo sem percorrer toda a lista.
4. **Given** participantes conectados, **When** uma informação compartilhada é criada ou alterada, **Then** os demais recebem a atualização e uma reconexão recupera o estado persistido.

### User Story 3 - Guardar segredos e controlar visibilidade (Priority: P1)

Como mestre, quero manter anotações pessoais e ocultar NPCs, mapas ou pistas dos jogadores para preparar revelações sem expor conteúdo reservado.

**Why this priority**: Privacidade é parte essencial do caderno de campanha; registros privados não podem vazar nem por busca, snapshot ou evento em tempo real.

**Independent Test**: O mestre cria notas pessoais e marca um NPC, mapa e pista como ocultos; participantes jogadores não encontram nem recebem esses registros, enquanto o mestre continua podendo consultá-los e editá-los.

**Acceptance Scenarios**:

1. **Given** um mestre editando um NPC, mapa ou pista, **When** ativa a opção de visibilidade exclusiva do mestre, **Then** o registro e seus dados associados ficam disponíveis somente ao mestre.
2. **Given** um registro oculto, **When** um jogador consulta listas, busca, contagens, snapshot ou recebe eventos em tempo real, **Then** não recebe conteúdo nem metadados que revelem sua existência.
3. **Given** um mestre editando qualquer registro do caderno, **When** acrescenta uma anotação pessoal, **Then** essa anotação é visível apenas aos mestres e não substitui a descrição compartilhada.
4. **Given** um registro antes oculto, **When** o mestre o torna visível, **Then** ele aparece aos jogadores somente depois da confirmação persistida da alteração.
5. **Given** um jogador tentando criar ou editar conteúdo privado ou alterar a visibilidade, **When** envia a operação, **Then** a ação é recusada e o estado permanece inalterado.

### User Story 4 - Marcar pontos de interesse nos mapas (Priority: P2)

Como participante, quero marcar pontos em um mapa da campanha e adicionar informações a eles, para registrar lugares importantes e conectar a navegação às descobertas do grupo.

**Why this priority**: Conecta as anotações de locais à referência visual e torna o conhecimento compartilhado mais fácil de explorar.

**Independent Test**: Um participante abre um mapa compartilhado, cria um marcador nomeado com uma nota; outros jogadores autorizados veem o ponto na mesma posição após atualizar ou reconectar.

**Acceptance Scenarios**:

1. **Given** um mapa visível, **When** um participante adiciona um ponto de interesse, **Then** o ponto mantém sua posição relativa à imagem e inclui nome e descrição opcionais.
2. **Given** um ponto já marcado, **When** um participante autorizado complementa suas informações, **Then** a atualização fica visível aos demais sem mover o ponto.
3. **Given** um mapa oculto, **When** jogadores consultam pontos de interesse, **Then** nenhum ponto nem informação derivada do mapa é retornado.
4. **Given** um mapa redimensionado ou visualizado em outro dispositivo, **When** o participante abre a imagem, **Then** os pontos permanecem ancorados ao local correto sem depender do tamanho da tela.

## Edge Cases

- O catálogo contém armas antigas sem tipo de dano definido; a arma continua utilizável e recebe uma indicação clara de que o mestre precisa completar o catálogo antes de selecionar um tipo.
- Um ataque salvo referencia uma arma removida ou alterada; a ficha preserva a configuração atual salva e permite revisar a referência sem perder os componentes.
- O mesmo ataque mistura danos físicos, mágicos e adicionais; a rolagem não combina silenciosamente componentes distintos.
- Dois participantes editam o mesmo registro ao mesmo tempo; uma confirmação não deve apagar alterações concorrentes sem aviso ou recuperação.
- Um jogador acessa diretamente uma rota, identificador ou evento de registro oculto; o servidor não revela sua existência.
- Uma anotação pessoal do mestre é salva ao mesmo tempo que a descrição compartilhada muda; as duas partes continuam independentes.
- Uma imagem é substituída ou tem proporção diferente; os pontos mantêm posição normalizada quando possível, e o mestre é informado quando uma posição não pode ser preservada.
- O mapa ou o marcador fica indisponível durante perda de conexão; a interface não apresenta uma edição não confirmada como salva.
- Campanha sem registros, com nomes repetidos, descrições longas, muitas pistas ou tela estreita continua navegável e legível.

## Requirements

### Functional Requirements

- **FR-001**: O sistema DEVE catalogar um ou mais tipos de dano para cada arma do catálogo, incluindo tipos físicos e mágicos, sem remover armas existentes.
- **FR-002**: O sistema DEVE permitir configurar o dano-base da arma com dados, atributo aplicável e tipo ou tipos de dano associados.
- **FR-003**: O sistema DEVE permitir que uma rolagem salva de ataque inclua componentes adicionais independentes, cada um com dados e tipo de dano.
- **FR-004**: O sistema DEVE rolar e apresentar os componentes separadamente e também informar o total combinado, sem aplicar resistências, vulnerabilidades ou imunidades automaticamente nesta versão.
- **FR-005**: O sistema DEVE manter a compatibilidade das rolagens salvas e dados existentes ao atualizar o catálogo ou a ficha.
- **FR-006**: O sistema DEVE disponibilizar a seção **Campanha** na navegação superior a jogadores e mestres da campanha ativa.
- **FR-007**: A área **Campanha** DEVE oferecer páginas de NPCs, Mapas, Pistas e Diário.
- **FR-008**: Jogadores e mestres DEVEM poder consultar, cadastrar e complementar conteúdo compartilhado nos quatro tipos de página.
- **FR-009**: O sistema DEVE atribuir autor e data a cada registro e contribuição compartilhada e atualizar informações confirmadas aos participantes conectados.
- **FR-010**: O sistema DEVE permitir ao mestre manter uma anotação pessoal por registro, separada do conteúdo compartilhado e visível apenas aos mestres.
- **FR-011**: O sistema DEVE permitir ao mestre marcar NPCs, mapas e pistas como visíveis ou exclusivos do mestre.
- **FR-012**: Registros ocultos e suas notas privadas NÃO DEVEM ser incluídos em respostas, buscas, contagens, snapshots, notificações ou eventos destinados a jogadores.
- **FR-013**: Somente o mestre DEVE poder alterar visibilidade, ver ou editar anotações pessoais e remover registros compartilhados.
- **FR-014**: O sistema DEVE permitir criar pontos de interesse nomeados e descritos sobre mapas visíveis, guardar coordenadas relativas à imagem e sincronizar alterações autorizadas.
- **FR-015**: A visibilidade de pontos de interesse DEVE herdar a visibilidade do mapa; um jogador não pode inferir conteúdo de um mapa oculto.
- **FR-016**: A área Campanha DEVE permitir busca por nome e filtros por tipo de conteúdo e manter listas utilizáveis em dispositivos móveis.
- **FR-017**: Todas as mutações DEVEM validar participação e papel na campanha no servidor e persistir antes de notificar outros participantes.
- **FR-018**: As telas e controles DEVEM ser responsivos, acessíveis por teclado e toque e legíveis em viewports de 360 px, tablet e desktop, respeitando os padrões visuais do Companion.
- **FR-019**: Falhas de rede ou conflitos de edição DEVEM preservar a última informação confirmada e comunicar claramente quando uma mudança ainda não foi salva.

### Key Entities

- **Tipo de dano**: categoria identificável de dano físico ou mágico associada a componentes de ataque.
- **Componente de dano**: parte nomeada de uma rolagem de ataque, com dados, tipo de dano e referência opcional à arma ou efeito.
- **Registro de campanha**: NPC, mapa, pista ou entrada de diário associada à campanha, com texto, autoria e data.
- **Anotação pessoal do mestre**: informação privada associada a um registro, separada do conteúdo compartilhado.
- **Ponto de interesse**: coordenada proporcional à imagem de um mapa, com nome, descrição e autoria.

## Success Criteria

### Measurable Outcomes

- **SC-001**: Um jogador consegue configurar e rolar um ataque com dois tipos de dano em até 60 segundos, sem redigitar os dados em cada uso.
- **SC-002**: Em todos os resultados de ataque com múltiplos componentes, participantes identificam cada subtotal, seu tipo de dano e o total combinado.
- **SC-003**: Um participante encontra NPC, mapa ou pista pelo nome em até 10 segundos em uma campanha com 100 registros.
- **SC-004**: Em 100% das consultas e atualizações destinadas a jogadores, registros ocultos e notas pessoais estão ausentes.
- **SC-005**: Pontos marcados permanecem no mesmo local visual em 360 px, tablet e desktop e após reconexão.
- **SC-006**: As jornadas de consultar, cadastrar e localizar conteúdo podem ser concluídas em desktop e celular sem rolagem horizontal nem ações dependentes apenas de hover.

## Assumptions

- A feature usa o catálogo de armas e os itens já disponíveis na ficha e mantém a rolagem salva como entrada principal para ataques do jogador.
- Tipos de dano começam com a lista comum de D&D, incluindo contundente, cortante, perfurante, ácido, elétrico, gélido, ígneo, força, necrótico, psíquico, radiante, trovejante e veneno; mestre pode selecionar tipo personalizado quando necessário.
- Componentes de ataque representam rolagens separadas; modificadores compartilhados da arma não são duplicados em componentes mágicos adicionais, a menos que sejam explicitamente configurados.
- A aba Campanha é uma área colaborativa da campanha atual, não um espaço público ou global.
- Jogadores podem editar e complementar texto compartilhado; o mestre controla exclusão, visibilidade e notas pessoais.
- Diário é compartilhado por padrão e não recebe opção de ocultação nesta versão.
- Mapas do caderno são referências persistentes da campanha com imagem e pontos de interesse; mapas táticos e estado de combate continuam vinculados ao encontro.
- Os pontos de interesse herdam a privacidade do mapa; visibilidade individual por ponto pode ser adicionada depois se houver necessidade.
- Resistências, vulnerabilidades e imunidades são mostradas como regras futuras e não modificam automaticamente o dano nesta versão.
