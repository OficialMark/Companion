# Research: Compêndio na barra superior e ficha de criatura legível

Não havia NEEDS CLARIFICATION no Technical Context. As decisões abaixo resolvem as escolhas de design.

## D1. Onde guardar a tela escolhida

- **Decision**: no endereço, `?tela=compendio`.
  - `useMasterView()` deriva a tela de `route.query.tela`.
  - A troca usa `router.replace`, então não cria passos no botão Voltar.
  - Jogador sempre fica na `mesa`, mesmo que o endereço peça outra coisa.
  - Valor desconhecido também cai na `mesa`.
- **Rationale**: recarregar ou abrir o mesmo endereço reabre a tela (FR-009) sem estado no servidor. A guarda de jogador é uma regra visível e testável.
- **Alternatives considered**:
  - `localStorage`: não sobrevive a trocar de aparelho e fica invisível.
  - Rota `/compendio`: duplicaria o setup de sessão e SignalR que vive em `pages/index.vue`.
  - `router.push`: encheria o histórico.

## D2. Navegação na barra superior

- **Decision**: o componente `MasterViewNav` é um `nav` com dois botões que usam `aria-current="page"`. É navegação entre telas, não abas, então não usa `role="tablist"`.
  - **Desktop**: a navegação fica logo após a marca, como coluna da grade da topbar.
  - **Até 900 px**: navegação e nome da campanha formam a segunda linha. Os dois ficam num wrapper `.topbar-center`, que é `display: contents` no desktop e linha flex própria abaixo de 900 px.
  - **Até 480 px**: o nome da campanha sai da segunda linha e sobe para a primeira, ao lado do logo, truncado. Ali o texto "Companion" já fica oculto e sobra espaço.
- **Rationale**: na primeira versão, a navegação dividia a grade com as ações e espremia o "Modo Jogo" para 20 px de largura em 375 px e 41 px em 768 px. O e2e pegou. Com a navegação numa linha própria, a largura dela nunca afeta as ações. Em 375 px, ao lado da navegação, o nome ficava como "Co…". A primeira solução, mandar o nome para o tooltip do código, foi reprovada pelo crítico: tooltip não existe no toque, e o mestre perdia de vista qual mesa estava aberta.
- **Alternatives considered**:
  - Botões só com ícone no celular: "Compêndio" perde a descoberta.
  - Terceira linha na topbar: tira altura demais de uma barra fixa.

## D3. Compêndio em tela cheia

- **Decision**: com `view === 'compendio'`, a página mostra só o `CreatureLibrary`, sem boas-vindas, sem aviso do código e sem grade da mesa.
  - A tela ganha o título "Compêndio" e o atalho "Minhas criaturas".
  - O atalho é o filtro de origem `Custom`, que já existe, sincronizado com o select de origem.
  - Os selects usam rótulos curtos ("Tamanho", "Tipo", "Origem").
  - Os filtros ficam numa grade definida. A busca ocupa a linha toda. Abaixo dela vêm os cinco selects com largura igual e o "Limpar" no fim. No celular são pares, e a Origem ocupa a linha inteira quando não há "Limpar".
  - O aviso "Comandos pausados" também aparece no Compêndio, que não tem a linha de boas-vindas onde ele vive na Mesa.
  - Sem criaturas próprias, o estado vazio oferece "Criar a primeira".
- **Rationale**: o filtro de origem da API já separa criadas e editadas, então não há mudança de contrato. Com rótulos longos, "Todos os tamanhos" virava "Todos os taman…" e "ND mín." virava "N…" em 375 px. No layout flex anterior, o Tipo se esticava até 270 px enquanto o Tamanho ficava com 100 px.
- **Alternatives considered**:
  - Filtros grudados no topo: a topbar muda de altura com a largura e os cobriria.
  - "Minhas" incluir também as editadas: mistura duas coisas que a origem já separa.

## D4. Ficha em modo de leitura

- **Decision**:
  - Números rápidos em 4 cartões: CA, PV, Iniciativa e ND, em 2×2 no celular.
  - Deslocamento numa linha própria.
  - Atributos em 6 colunas, 3×2 no celular.
  - Detalhes em linhas `rótulo | valor` com divisória fina; no celular, o rótulo fica acima do valor.
  - Ações separadas por divisória tracejada.
  - Rodapé do celular em uma linha: quantidade, adicionar e editar como ícone de 44 px.
- **Rationale**: com 5 cartões, o celular ficava com um buraco na grade, e o deslocamento é texto longo. O rodapé em três linhas comia cerca de 200 px da tela do celular.
- **Alternatives considered**:
  - Deslocamento dentro de um cartão largo: quebrava o ritmo da grade.
  - Esconder "Editar ficha" num menu: um toque a mais numa ação frequente.

## D5. Editor

- **Decision**:
  - Grade de 6 colunas em todas as larguras, com classes de span `.col-*` (desktop) e `.m-col-*` (até 680 px). A distribuição de cada seção está no [data-model.md](data-model.md).
  - O `legend` fica dentro do cartão (`float` + `width: 100%`).
  - Itens de lista viram cartão: nome e remover na primeira linha, descrição embaixo.
  - A barra Cancelar/Salvar vira linha própria do modal. Só `.creature-editor-fields` rola.
- **Rationale**: na grade antiga de 4 colunas, ND/XP/Proficiência deixavam célula vazia. Com 6 colunas, terços e metades fecham todas as linhas. Uma barra `sticky` cobria campos durante a rolagem, e o teste de sobreposição pegou.
- **Alternatives considered**:
  - Grade de 12 colunas: mais classes sem ganho.
  - Uma coluna em toda largura: desperdiça o desktop e alonga o formulário.

## Verificação automática de layout

- **Decision**: o e2e varre a área de rolagem da ficha e do editor a cada tela e mede três coisas:
  - **Sobreposição**: compara as caixas por linha renderizada (`getClientRects`), recortadas pelas áreas de rolagem ancestrais.
  - **Alvos**: controles visíveis menores que 44 px. Links dentro de frase ficam de fora, pela exceção da WCAG 2.5.8.
  - **Rolagem horizontal**.
- **Rationale**: capturas sozinhas dependem de olho. A medição deixa os critérios SC-001 e SC-004 verificáveis a cada mudança.
- **Alternatives considered**:
  - Comparação de imagem pixel a pixel: frágil com fontes e animações.
  - Ferramenta externa de acessibilidade: não mede sobreposição.
