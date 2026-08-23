# Feature Specification: MVP de Campanha D&D

**Feature Branch**: `001-campaign-mvp`

**Created**: 2026-08-23

**Status**: Draft

**Input**: Aplicação web para facilitar campanhas de D&D em navegadores Windows e Android, com campanhas compartilhadas, rolagem pública de dados, iniciativa de combate, atualização em tempo real e execução na rede local.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Criar e entrar em uma campanha (Priority: P1)

Como mestre, quero criar uma campanha e compartilhar um código curto para que jogadores entrem usando um nome de exibição, sem precisar cadastrar uma conta completa.

**Why this priority**: A campanha e seus participantes formam o contexto obrigatório para todas as demais funcionalidades.

**Independent Test**: Um mestre cria uma campanha, copia o código e um segundo navegador entra com outro nome; ambos passam a ver a mesma campanha e a lista atualizada de participantes.

**Acceptance Scenarios**:

1. **Given** um visitante sem sessão, **When** informa seu nome e o nome da campanha, **Then** uma campanha é criada e ele entra como mestre.
2. **Given** um código válido, **When** outro visitante informa seu nome e o código, **Then** ele entra como jogador e recebe acesso somente àquela campanha.
3. **Given** um código inexistente, **When** alguém tenta entrar, **Then** nenhuma sessão é criada e uma mensagem compreensível é exibida.
4. **Given** dois participantes conectados, **When** um terceiro entra, **Then** os navegadores conectados recebem a lista atualizada sem recarregar a página.

---

### User Story 2 - Rolar dados publicamente (Priority: P1)

Como participante, quero escolher uma expressão de dados e realizar uma rolagem cujo resultado seja exibido imediatamente a todos na campanha.

**Why this priority**: A rolagem compartilhada é a principal entrega de valor do MVP.

**Independent Test**: Em dois navegadores na mesma campanha, um participante rola `2d6+3`; os dois veem os dados individuais, modificador, total, autor e horário, e o resultado permanece no histórico após recarregar.

**Acceptance Scenarios**:

1. **Given** um participante autorizado, **When** rola uma expressão válida, **Then** o resultado é calculado pelo sistema, registrado e apresentado a todos na campanha.
2. **Given** uma expressão inválida ou acima dos limites, **When** o participante tenta rolar, **Then** a tentativa é recusada sem criar resultado no histórico.
3. **Given** uma campanha com rolagens anteriores, **When** um participante abre ou atualiza a mesa, **Then** as 50 rolagens públicas mais recentes aparecem da mais nova para a mais antiga.
4. **Given** um participante de outra campanha, **When** tenta consultar ou criar uma rolagem, **Then** o acesso é recusado.
5. **Given** a expressão `3#3d5+4`, **When** o participante rola, **Then** o histórico apresenta três resultados individuais da mesma expressão dentro de um único teste compartilhado.
6. **Given** a expressão `2d3+4+2d8+3`, **When** o participante rola, **Then** os dois grupos de dados e os modificadores são calculados em conjunto e discriminados no histórico.
7. **Given** um navegador Android sem geração nativa de UUID, **When** o participante rola, **Then** o comando recebe um identificador válido e é processado normalmente.

---

### User Story 3 - Controlar iniciativa e turnos (Priority: P2)

Como mestre, quero iniciar um combate, cadastrar combatentes, rolar ou informar suas iniciativas e avançar os turnos para que todos acompanhem a ordem e a rodada atual.

**Why this priority**: A iniciativa é a segunda funcionalidade central pedida e prepara a evolução para testes, salvaguardas e combate completo.

**Independent Test**: O mestre cria um combate com três combatentes, rola a iniciativa, inicia a ordem e avança até a próxima rodada; outro navegador acompanha cada mudança em tempo real.

**Acceptance Scenarios**:

1. **Given** uma campanha sem combate ativo, **When** o mestre inicia um combate, **Then** todos veem um combate aberto na rodada inicial.
2. **Given** um combate aberto, **When** o mestre adiciona um combatente com modificador válido, **Then** ele aparece para todos e pode ter sua iniciativa rolada pelo sistema.
3. **Given** combatentes com iniciativa, **When** o mestre inicia ou reorganiza a ordem, **Then** ela fica ordenada por maior iniciativa e usa a ordem de criação como desempate estável.
4. **Given** um combate iniciado, **When** o mestre avança depois do último combatente, **Then** o primeiro combatente torna-se ativo e a rodada aumenta em uma unidade.
5. **Given** um jogador comum, **When** tenta alterar o combate ou avançar o turno, **Then** a operação é recusada.
6. **Given** iniciativas já roladas antes ou durante o combate, **When** o mestre rola novamente um combatente ou todos, **Then** os novos resultados substituem os anteriores e o combatente do turno atual permanece ativo.

---

### User Story 4 - Recuperar a mesa após desconexão (Priority: P2)

Como participante usando celular ou computador, quero ser avisado quando perder conexão e recuperar o estado atual automaticamente ao retornar, sem resultados duplicados ou turno incorreto.

**Why this priority**: Navegadores Android suspendem páginas e redes locais ou VPNs podem oscilar; a sessão precisa continuar confiável.

**Independent Test**: Um navegador é desconectado enquanto outro realiza uma rolagem e avança o turno; ao reconectar, o primeiro exibe o histórico e combate atuais sem duplicação.

**Acceptance Scenarios**:

1. **Given** uma conexão interrompida, **When** a página detecta a perda, **Then** mostra claramente o estado offline e não confirma comandos não aceitos.
2. **Given** uma sessão ainda válida, **When** a conexão retorna, **Then** o participante volta à campanha e recebe um snapshot atualizado.
3. **Given** um evento já apresentado, **When** ocorre sincronização ou repetição de entrega, **Then** ele não é exibido em duplicidade.
4. **Given** uma credencial inválida, **When** a página tenta recuperar a sessão, **Then** solicita nova entrada na campanha sem expor dados privados.

---

### User Story 5 - Disponibilizar o MVP na rede local (Priority: P1)

Como responsável pela mesa, quero iniciar toda a aplicação com um único procedimento e acessar uma URL pelo endereço IP do computador para que celulares e outros computadores da mesma rede participem.

**Why this priority**: Sem uma execução integrada e acessível pela LAN, o MVP não atende à forma de uso planejada.

**Independent Test**: Em uma instalação limpa com os pré-requisitos documentados, o responsável inicia o ambiente, identifica o IP do host e acessa a aplicação em outro dispositivo da LAN; campanha, dados e iniciativa funcionam entre os dispositivos.

**Acceptance Scenarios**:

1. **Given** os pré-requisitos e configuração mínima, **When** o responsável executa o comando documentado, **Then** interface, serviço e armazenamento iniciam juntos.
2. **Given** o firewall permitindo a porta publicada, **When** outro dispositivo abre `http://<ip-do-host>:3000`, **Then** a interface carrega e consegue operar todas as jornadas do MVP.
3. **Given** acesso por uma interface de VPN que ofereça conectividade equivalente à LAN, **When** um dispositivo remoto usa o IP da VPN e a mesma porta, **Then** a aplicação não exige alteração de endereço interno.
4. **Given** armazenamento já criado, **When** o ambiente é reiniciado, **Then** campanhas, rolagens e combates persistidos continuam disponíveis.

### Edge Cases

- Nomes vazios, apenas espaços, muito longos ou repetidos na mesma campanha.
- Código de campanha informado com diferenças de caixa ou espaços ao copiar.
- Expressões com zero dados, faces inválidas, modificador extremo ou quantidade excessiva.
- Repetições com `#` vazias, acima do limite ou cujo total combinado ultrapassa o limite de dados.
- Expressões com vários termos, operadores duplicados ou sem nenhum termo de dados.
- Duplo toque no botão de rolagem ou reenvio causado por rede instável.
- Dois comandos de avanço de turno enviados quase simultaneamente.
- Saída do mestre enquanto jogadores continuam conectados.
- Campanha sem participantes online, sem rolagens ou combate sem combatentes.
- Empate de iniciativa entre vários combatentes.
- Nova rolagem que altera a ordem durante o turno de um combatente.
- Reinício de serviço durante uma campanha ou um combate ativo.
- IP do host alterado pelo roteador ou bloqueio da porta pelo firewall do Windows.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: O sistema MUST permitir criar uma campanha com nome da campanha e nome do mestre.
- **FR-002**: O sistema MUST gerar um código curto e não ambíguo para entrada na campanha.
- **FR-003**: O sistema MUST permitir entrar em uma campanha válida com código e nome de exibição.
- **FR-004**: O sistema MUST emitir uma credencial privada e revogável que identifique o participante e seu papel na campanha.
- **FR-005**: O sistema MUST restringir leitura, comandos e atualizações em tempo real aos participantes da campanha correspondente.
- **FR-006**: O sistema MUST distinguir mestre e jogador e reservar operações administrativas ao mestre.
- **FR-007**: O sistema MUST mostrar os participantes da campanha e atualizar a lista quando conexões entram ou saem.
- **FR-008**: O sistema MUST aceitar um ou mais termos `NdF` combinados por adição ou subtração, com modificadores inteiros, incluindo atalhos como `d20`.
- **FR-009**: O sistema MUST aceitar `R#expressão` para executar entre 1 e 20 resultados individuais da mesma expressão, limitando o conjunto a 100 dados, faces entre 2 e 1000 e modificador acumulado entre -10000 e 10000.
- **FR-010**: O sistema MUST gerar os valores dos dados sem aceitar valores escolhidos pelo navegador.
- **FR-011**: O sistema MUST registrar autor, expressão normalizada, cada repetição, termos e valores individuais, modificador, total de cada resultado e horário da rolagem.
- **FR-012**: O sistema MUST notificar participantes conectados sobre uma nova rolagem depois que ela estiver registrada.
- **FR-013**: O sistema MUST retornar as 50 rolagens públicas mais recentes de uma campanha em ordem decrescente de criação.
- **FR-014**: O sistema MUST permitir ao mestre criar no máximo um combate ativo por campanha.
- **FR-015**: O sistema MUST permitir ao mestre adicionar combatantes com nome e modificador de iniciativa entre -100 e 100.
- **FR-016**: O sistema MUST permitir ao mestre rolar ou rolar novamente a iniciativa de um combatente ou de todos, antes ou durante o combate, preservando quem está no turno atual.
- **FR-017**: O sistema MUST ordenar combatentes por iniciativa decrescente e desempatar de forma estável pela ordem de criação.
- **FR-018**: O sistema MUST controlar combatente ativo, turno, rodada e estado do combate.
- **FR-019**: O sistema MUST permitir somente ao mestre avançar o turno e encerrar o combate.
- **FR-020**: O sistema MUST notificar participantes conectados quando o estado do combate mudar.
- **FR-021**: O sistema MUST fornecer um snapshot com campanha, participante atual, participantes, rolagens recentes e combate ativo.
- **FR-022**: O sistema MUST sinalizar perda de conexão e tentar recuperar sessão e snapshot quando a conectividade retornar.
- **FR-023**: O sistema MUST identificar eventos de maneira única para impedir apresentação duplicada no cliente.
- **FR-024**: O sistema MUST iniciar todos os componentes necessários por um único procedimento documentado.
- **FR-025**: O sistema MUST publicar a aplicação em todas as interfaces de rede do host por uma porta configurável, usando endereços relativos para suas comunicações internas.
- **FR-026**: O sistema MUST preservar os dados entre reinicializações normais do ambiente.
- **FR-027**: O sistema MUST fornecer instruções para descoberta do IP local, liberação de firewall e acesso por uma VPN que exponha um adaptador de rede local.
- **FR-028**: O sistema MUST validar entradas e retornar erros compreensíveis sem revelar detalhes internos ou credenciais.
- **FR-029**: O sistema MUST gerar identificadores idempotentes válidos nos navegadores Android suportados mesmo quando a função nativa de UUID não estiver disponível.

### Key Entities

- **Campaign**: A mesa compartilhada, identificada por código, nome e estado de criação.
- **Participant**: Uma pessoa dentro de uma campanha, com nome, papel, credencial e estado de presença.
- **Dice Roll**: Um teste público e imutável com expressão, uma ou mais repetições, termos, valores, totais, autor e horário.
- **Encounter**: Um combate pertencente à campanha, com estado, rodada e turno atual.
- **Combatant**: Um participante da iniciativa, com nome, modificador, resultado e posição estável.
- **Realtime Event**: Uma notificação identificável sobre alteração já confirmada no estado compartilhado.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Uma pessoa cria uma campanha e outra entra por código em menos de dois minutos, sem treinamento prévio.
- **SC-002**: Em condições normais de rede local, 95% das rolagens aparecem nos demais dispositivos em até um segundo após a confirmação.
- **SC-003**: O histórico e o combate recuperados após atualização ou reconexão correspondem ao estado confirmado, sem eventos duplicados em todos os cenários automatizados.
- **SC-004**: O mestre consegue cadastrar cinco combatentes, rolar iniciativas e iniciar a primeira rodada em menos de dois minutos.
- **SC-005**: Todas as jornadas críticas são concluídas em viewport de celular sem rolagem horizontal e com alvos de toque de pelo menos 44 por 44 pixels.
- **SC-006**: Uma instalação limpa fica disponível para dispositivos da LAN em até dez minutos seguindo somente a documentação do projeto.
- **SC-007**: Reiniciar o ambiente não perde campanhas, as 50 rolagens recentes ou o combate ativo.
- **SC-008**: Tentativas automatizadas de acessar outra campanha ou executar comandos de mestre como jogador são recusadas em 100% dos casos testados.
- **SC-009**: O mestre consegue rolar novamente uma iniciativa individual ou todas em uma única interação, sem alterar indevidamente o combatente do turno atual.
- **SC-010**: Expressões repetidas e compostas apresentam 100% dos resultados e termos esperados nos cenários automatizados de computador e celular.

## Assumptions

- O MVP usa nomes de exibição e credenciais locais de participante, sem e-mail, senha ou recuperação de conta.
- A campanha é usada por um grupo pequeno, com até 20 participantes simultâneos e até 100 campanhas persistidas no ambiente doméstico.
- Todas as rolagens do MVP são públicas para a campanha; rolagens privadas ficam para uma versão futura.
- Testes de atributo, salvaguardas, ficha completa, monstros, pontos de vida e chat ficam fora do MVP.
- A rede local ou VPN fornece conectividade IP entre os dispositivos; configuração e segurança da VPN são responsabilidade do usuário.
- O acesso inicial usa HTTP em uma rede privada confiável. Exposição direta à internet exigirá HTTPS e endurecimento adicional.
