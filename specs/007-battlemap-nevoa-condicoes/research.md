# Research: spec 007

## R1. Por que o menu do token fecha ao soltar o botão esquerdo

- **Decision**: suprimir explicitamente o `click` que segue uma sessão de ponteiro iniciada num token (`suppressSurfaceClick`), em vez de comparar a célula clicada com a célula âncora do token.
- **Rationale**: `startTokenDrag` chama `surface.setPointerCapture`. Com a captura ativa, o `pointerup` e o `click` sintetizado são entregues à superfície `<svg>`, não ao `<g>` do token (o `@click.stop` do token nunca roda). `clickSurface` chama `commitCell(cell)`. O guard existente (`selectedToken.column === column && row === row`) só funciona para tokens 1x1 sem deslocamento; para 2x2+ a metade inferior/direita do token cai em outra célula, e para tokens empilhados o deslocamento pode cruzar a borda. Resultado: o token "pula" e o painel fecha. Botão direito não gera `click` (gera `contextmenu`), por isso funcionava.
- **Alternatives considered**: (a) não capturar o ponteiro: quebra o arraste quando o cursor sai do token; (b) supressão por tempo (ignorar clicks por N ms): frágil em máquinas lentas; (c) usar `pointerup` em vez de `click` para commit de célula: mudaria o fluxo de teclado e o de toque já testados.
- O flag é zerado no próximo `pointerdown` da superfície e no início de cada sessão de token, então nunca engole um clique legítimo posterior, mesmo quando o navegador não emite o `click` (ex.: soltar fora da janela).

## R2. Botões do mouse

- **Decision**: `event.button === 0` (ou toque/caneta) inicia seleção+arraste; `button === 2` sobre token abre o menu sem arraste e `contextmenu` é prevenido no board; `button` 1/2 no fundo faz pan.
- **Rationale**: atende FR-004, preserva o hábito de quem já usava o botão direito, e dá pan no desktop durante o modo névoa (onde o arraste esquerdo pinta).

## R3. Ícones de condição dentro do SVG do mapa

- **Decision**: renderizar `<Icon>` do `@nuxt/icon` (modo svg) dentro de um `<svg x y width height>` aninhado no `<g>` do token; os nomes vivem como literais em `utils/statusConditions.ts` para o scanner do client bundle incluí-los.
- **Rationale**: mantém um único conjunto de ícones (Lucide) no menu e no mapa, escala com o zoom (é vetorial), sem `foreignObject` (que tem problemas de captura de eventos e de transformações no Safari).
- **Mapa de ícones** (verificado em `@iconify-json/lucide`): Caído `arrow-down-to-line`, Atordoado `sparkles`, Paralisado `zap`, Envenenado `biohazard`, Sangrando `droplets`, Imobilizado `link`, Agarrado `grab`, Cego `eye-off`, Surdo `ear-off`, Invisível `ghost`, Amedrontado `frown`, Enfeitiçado `heart-handshake`, Inconsciente `bed`, Petrificado `mountain`, Incapacitado `ban`, Exausto `battery-low`, Concentrando `brain`, Em chamas `flame`.
- **Legibilidade**: selo com raio `max(9, tamanho*0.16)` em px de tela; no máximo 3 selos + "+N"; nome acessível do token lista todas as condições.

## R4. Persistência das condições

- **Decision**: manter CSV de nomes do enum; anexar os novos valores ao fim do enum; aumentar a coluna para 512.
- **Rationale**: o conversor grava `ToString()` e lê com `Enum.Parse`, então a ordem numérica não importa e os dados antigos continuam válidos. 18 nomes somam ~200 caracteres; 128 estouraria.

## R5. Onde filtrar HP oculto

- **Decision**: `DtoMapper.ToDto(Encounter, Guid? observerId, bool isMaster)`: para não mestre, criatura com `IsHitPointsHidden` sai com `CurrentHitPoints = null`, `MaxHitPoints = null`, `IsDefeated` preservado.
- **Rationale**: todo caminho de leitura do encontro (snapshot da campanha, respostas de comandos, resultado de ataque) já passa por esse mapper com o observador; eventos de grupo só invalidam. O token do mapa não carrega HP (é juntado no cliente a partir do encontro), então não há segundo vazamento.
- **Player tokens**: personagens de jogador nunca podem ser ocultados (validação no domínio/serviço).

## R6. Formato da névoa

- **Decision**: bitset row-major (`índice = row*columns + column`, bit `i%8` do byte `i/8`), `null` = tudo revelado; trafega em base64 no snapshot e no PUT.
- **Rationale**: 100x100 = 1250 bytes (1668 em base64). Simples de validar (tamanho exato), barato de comparar, sem tabela extra.
- **Alternatives considered**: lista de retângulos (difícil de editar com pincel), tabela de células (10k linhas por mapa), operações incrementais (exige ordenação e reconciliação; desnecessário com um único escritor).

## R7. Tokens sob a névoa

- **Decision**: para jogadores, omitir do snapshot criaturas e marcadores cujo dono é mestre quando todas as células ocupadas (âncora + footprint, recortadas ao grid) estão cobertas. Tokens na reserva (sem posição) não são afetados pela névoa: não estão no mapa e já aparecem na lista de iniciativa.
- **Rationale**: FR-021 e edge case "parcialmente coberta fica visível".
- **Limite conhecido**: a imagem do mapa é baixada inteira pelo jogador (endpoint de imagem); a névoa é opaca na interface, mas quem inspecionar a imagem vê o mapa todo. Esconder a imagem exigiria compor imagens no servidor; fora do escopo (registrado nas Assumptions do quickstart).

## R8. Renderização da névoa

- **Decision**: um `<path>` com `fill-rule="nonzero"` composto por retângulos de runs horizontais de células cobertas, entre grid e tokens. Jogador: preenchimento opaco com padrão sutil; mestre: mesma forma com opacidade .5 e hachura. Tokens de criatura cobertos aparecem para o mestre com opacidade reduzida e ícone `eye-off`.
- **Rationale**: 1 nó DOM em vez de até 10k `<rect>`; recalcular o path de 100x100 custa < 1 ms.
- **Pré-visualização**: durante o arraste do pincel, a máscara local é alterada e o path recalculado; o retângulo mostra um contorno tracejado e só aplica ao soltar. Ao soltar, PUT com a máscara; em erro, volta à máscara do servidor e mostra aviso.

## R9. Gestos no modo névoa

- **Decision**: no modo névoa tokens e áreas recebem `pointer-events: none`; 1 ponteiro primário pinta; segundo ponteiro cancela o traço em andamento (reverte para a máscara anterior ao traço) e vira pinça; roda do mouse e botões fazem zoom; botão direito/meio faz pan; Esc sai do modo.
