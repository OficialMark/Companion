# Research: Battlemap com Grid Interativo

## 1. Limite do agregado

**Decision**: Associar um único `Battlemap` ao `Encounter`, com posições de pinos relacionadas ao battlemap e participantes da mesma campanha.

**Rationale**: O ciclo de vida acompanha o encontro, enquanto serviço e endpoints próprios evitam aumentar a responsabilidade do `EncounterService` e mantêm a imagem fora do snapshot comum.

**Alternatives considered**: Battlemap diretamente na campanha, rejeitado por perder o vínculo com a cena; incorporar todas as regras ao agregado `Encounter`, rejeitado pelo volume binário e acoplamento.

## 2. Persistência da imagem

**Decision**: Persistir PNG/JPEG/WebP de até 20 MB em `varbinary(max)` no SQL Server, junto dos metadados, e expor o binário em endpoint separado com ETag.

**Rationale**: O MVP opera em instância única e já depende de SQL Server com backup e volume persistente. Essa escolha mantém consistência transacional e evita um novo serviço operacional.

**Alternatives considered**: Sistema de arquivos do container, rejeitado por não ser durável nem portátil; object storage, rejeitado por adicionar configuração e serviço sem escala que o justifique; base64 no JSON, rejeitado por aumentar snapshots e impedir cache eficiente.

## 3. Validação e substituição

**Decision**: Receber configuração por multipart, validar tamanho, MIME e assinatura mágica, sanitizar apenas o nome exibido e substituir o mapa em transação usando revisão esperada.

**Rationale**: O tipo informado pelo navegador não é suficiente. A revisão impede que uma aba antiga substitua uma configuração mais recente; a transação também devolve à reserva posições fora do novo grid.

**Alternatives considered**: Confiar somente em extensão/MIME, rejeitado por segurança; criar um novo registro a cada edição, rejeitado porque histórico/versionamento de mapas está fora do escopo.

## 4. Representação dos pinos

**Decision**: Derivar um pino para cada participante com papel Player e persistir somente `BattlemapTokenPosition` quando houver coordenada. Ausência de posição significa reserva.

**Rationale**: Jogadores que entram depois da criação aparecem automaticamente sem exigir alteração no fluxo de ingresso. Identidade, nome e papel continuam pertencendo a `Participant`.

**Alternatives considered**: Criar uma linha de pino para cada jogador no upload, rejeitado por exigir sincronização adicional na entrada/saída; guardar linha/coluna em `Participant`, rejeitado por misturar estado específico de encontro ao perfil da campanha.

## 5. Concorrência e autoridade

**Decision**: Validar papel, campanha, encontro, revisão e coordenadas no servidor; usar versão otimista por battlemap e posição. Persistir antes de emitir `battlemap.invalidated`.

**Rationale**: Mestre e dono podem disputar o mesmo pino, e abas antigas podem apontar para grids substituídos. Conflitos retornam estado recuperável em vez de aceitar coordenadas obsoletas.

**Alternatives considered**: Última escrita vence sem revisão, rejeitado por ocultar conflitos de arbitragem; bloqueio distribuído, rejeitado por complexidade desnecessária.

## 6. Snapshot e tempo real

**Decision**: Disponibilizar snapshot REST de metadados e pinos, binário separado e evento SignalR de invalidação contendo somente encontro/revisão.

**Rationale**: Reconexão reconstrói o estado atual sem replay, eventos não expõem conteúdo e movimentos não retransmitem a imagem de 20 MB.

**Alternatives considered**: Enviar posições completas no evento, rejeitado porque cria duas fontes de verdade; incluir imagem no snapshot, rejeitado por custo de rede; transmitir binário por SignalR, rejeitado por confiabilidade e complexidade.

## 7. Renderização e escala

**Decision**: Renderizar em SVG sem biblioteca adicional: uma imagem, linhas de grid O(linhas + colunas), realce de célula e até 20 pinos. Coordenadas lógicas são transformadas por uma matriz de pan/zoom.

**Rationale**: Para 100×100, SVG requer no máximo 202 linhas, preserva nitidez, permite elementos focáveis e evita 10.000 nós de célula. O cálculo matemático seleciona a célula sem materializá-la.

**Alternatives considered**: Grid DOM, rejeitado pelo número de elementos; canvas, rejeitado pelo custo de acessibilidade e testes; biblioteca de mapas, rejeitada porque as necessidades cabem nas APIs nativas.

## 8. UX responsiva e acessibilidade

**Decision**: Manter altura fixa por breakpoint, alvo inicial de 40 px (faixa 36–44), pan/zoom interno, botão para ajustar o mapa, Pointer Events e fluxo alternativo “selecionar pino → selecionar célula”, com teclado e região de status.

**Rationale**: A área não desorganiza a página, células não precisam ficar minúsculas para mostrar grids grandes e a mesma jornada funciona com mouse, toque ou teclado.

**Alternatives considered**: Sempre encaixar o mapa inteiro, rejeitado por tornar grids grandes inutilizáveis; somente drag-and-drop, rejeitado por precisão móvel e acessibilidade; rolagem da página para navegar, rejeitada por conflito com o layout.
