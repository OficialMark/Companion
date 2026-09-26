# Research: Exibição de Área de Ataque no Battlemap

## 1. Onde persistir e sincronizar a área de ataque

- **Decision**: Nova entidade de domínio `AttackArea` associada ao `Battlemap` (via `BattlemapId`) e a um token (via `CombatantId` ou `ParticipantId`, mesmo padrão de `BattlemapTokenPosition`), persistida por EF Core e exposta por endpoints REST em `BattlemapEndpoints.cs` (`PUT/DELETE /campaigns/{campaignId}/battlemap/tokens/{tokenId}/attack-area`). Mudanças confirmadas disparam um evento via `SignalRCampaignNotifier`/`CampaignHub` para o grupo `campaign:{id}`.
- **Rationale**: Segue o Princípio II da constituição (servidor como autoridade do jogo) e o padrão já estabelecido por `BattlemapTokenPosition`/`BattlemapService`: comando REST → persistência → evento SignalR de notificação. Reconexão reidrata via GET do snapshot do battlemap (que passa a incluir as áreas ativas), sem depender do histórico de eventos SignalR.
- **Alternatives considered**:
  - *Somente estado local (sem backend)*: rejeitado — viola o Princípio II/III (nenhuma sincronização entre participantes, estado se perde ao reconectar).
  - *SignalR como fonte de verdade (sem persistência)*: rejeitado — viola explicitamente o Princípio II ("SignalR MUST apenas propagar eventos... reconexão MUST reconstruir o estado atual por API").

## 2. Representação geométrica dos formatos

- **Decision**: Um único registro `AttackArea` com um campo `Shape` (enum: `Circle`, `Square`, `Cone`, `Cylinder`), `Radius` (blocos, usado por Circle/Square como raio/meio-lado, e por Cylinder/Cone como distância), `Width` (blocos, aplicável somente a Cone/Cylinder) e `DirectionDegrees` (0–359, aplicável somente a Cone/Cylinder). Círculo e quadrado são sempre centrados na origem (token); cone e cilindro partem da origem na direção configurada.
- **Rationale**: Minimiza campos (evita polimorfismo de tabelas) e mapeia diretamente para os sliders da UI (FR-003/FR-004). "Cilindro" nesta feature representa um feixe retangular longo com largura configurável (conforme documentado em Assumptions do spec), diferenciando-se do cone apenas pela geometria do desenho (retângulo vs. leque), não pelos campos armazenados.
- **Alternatives considered**: Modelar cada formato como entidade própria — rejeitado por complexidade desnecessária (mesmo conjunto de campos serve a todos os formatos, apenas com significado ligeiramente diferente).

## 3. Renderização do desenho sobre o battlemap

- **Decision**: Desenhar as áreas como elementos SVG adicionais dentro do mesmo `<svg>` do `BattlemapBoard.vue`, usando `viewport.cellSize` e `viewport.pan` (já expostos por `useBattlemapViewport.ts`) para converter blocos → pixels, do mesmo modo que a grade e os tokens já são desenhados.
- **Rationale**: Reaproveita a infraestrutura de coordenadas existente; evita introduzir uma segunda superfície de desenho (canvas) ou biblioteca externa.
- **Alternatives considered**: Canvas separado sobreposto ao SVG — rejeitado, adicionaria complexidade de sincronização de camadas sem benefício (o SVG atual já lida com zoom/pan).

## 4. Painel de seleção/configuração compacto (mobile + desktop)

- **Decision**: Um popover/painel flutuante (`AttackAreaPanel.vue`) ancorado próximo ao token clicado, com um seletor de 4 ícones de formato e sliders nativos (`<input type="range">`) estilizados, seguindo o padrão visual mobile-first já usado em outros componentes novos do projeto (`GameModeToggle.vue`, `QuickDiceRoller.vue`). Fecha ao tocar fora do painel ou em um botão "Concluir".
- **Rationale**: Sliders nativos garantem acessibilidade e uso por toque/mouse sem dependência extra; um popover compacto atende ao requisito de não ocupar permanentemente a tela (FR-006, SC-003).
- **Alternatives considered**: Modal de tela cheia — rejeitado, ocuparia espaço demais e contraria FR-006/SC-003.

## 5. Autorização (quem pode configurar/reconfigurar uma área)

- **Decision**: Reaproveitar a mesma lógica já usada em `BattlemapBoard.vue.controllable(token)`: mestre (`store.isMaster`) pode configurar qualquer token; jogador só pode configurar tokens cujo `participantId` corresponde ao seu próprio `store.session.participantId`. A mesma checagem é replicada no backend (autorização por participante já existente na camada de API, análoga à usada para mover tokens) para não confiar apenas no cliente.
- **Rationale**: Consistência com o padrão de autorização já implementado; evita duplicar regras de negócio divergentes entre mover token e configurar área.
- **Alternatives considered**: Nenhuma — a regra já está bem definida pelo comportamento existente do battlemap.

## 6. Ciclo de vida da área (remoção automática)

- **Decision**: `AttackArea` é removida quando: (a) o usuário autorizado remove explicitamente; (b) o token/combatente associado é removido (cascade); (c) o encontro é encerrado ou o battlemap é substituído/reiniciado (cascade via `BattlemapService`/`EncounterService`).
- **Rationale**: Atende às Edge Cases do spec (FR-011, FR-012) e evita "áreas órfãs" persistidas indefinidamente.
- **Alternatives considered**: Expiração automática por tempo — rejeitado, não solicitado pelo spec e adicionaria complexidade (jobs/timers) sem necessidade.
