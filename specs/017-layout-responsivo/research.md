# Research: Layout Responsivo

## R1. Zonas com `display: contents` em vez de duplicar componentes

- **Decision**: três wrappers (pessoas, mapa, dados). No celular os laterais viram `display: contents` e os filhos se ordenam com `order` (mapa, rolagem, combate, participantes, histórico); no desktop viram colunas.
- **Rationale**: um DOM só, sem montar componentes duas vezes (estado, SignalR, foco).
- **Alternatives**: `grid-template-areas` com todos os painéis como itens diretos (rejeitada: alturas desiguais criam buracos entre linhas); componentes por breakpoint (rejeitada: estado duplicado).

## R2. Navegação inferior no celular

- **Decision**: a `view-nav` vira barra fixa embaixo abaixo de 681 px; a barra superior perde o `backdrop-filter` para não virar o bloco de contenção do elemento fixo.
- **Rationale**: alcance do polegar e fim da sobreposição código/aba.

## R3. Container queries onde o componente muda de coluna

- **Decision**: seletor de criaturas e grade de dados usam `@container` pela própria largura.
- **Rationale**: o mesmo componente vive numa coluna de 400 px no desktop e na tela inteira do celular; media query pela viewport erra os dois.

## R4. Mapa ajustado ao palco

- **Decision**: célula inicial entre 16 e 80 px; reenquadra a cada redimensionamento até a primeira interação (`hold()` em toque, arraste, tecla, zoom).
- **Rationale**: o palco cresce depois da primeira medição (imagem, colunas). Parar na primeira interação evita que algo se mova sob o cursor (regressão real do e2e de clique direito).

## R5. Barras fixas e foco

- **Decision**: `scroll-padding-top` igual à barra superior (84/140 px) e `scroll-padding-bottom` acima da barra inferior.
- **Rationale**: `focus()` rolava o mapa e escondia a primeira linha sob a barra.

## R6. Auditor como gate

- **Decision**: Playwright mede no navegador (`elementFromPoint`, bordas, `getComputedStyle`, contraste calculado) e grava capturas; com `LAYOUT_AUDIT_ONLY=1` só coleta.
- **Rationale**: regra determinística fora do julgamento do modelo; o crítico cego julga só o que a regra não captura (hierarquia, proximidade).
