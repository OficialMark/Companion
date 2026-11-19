# Feature Specification: Ficha pessoal, inventário e rolagens do jogador

**Feature Branch**: `OficialMark/ficha-jogador-78ed27c1`

**Created**: 2026-11-19

**Status**: Draft

**Input**: User description: "Feature: Ficha pessoal, inventário e sistema de rolagens do jogador. Implementar uma ficha pessoal para cada jogador, integrada ao inventário, aos equipamentos, aos catálogos de itens e magias e ao sistema de rolagens do modo jogo, aproveitando a arquitetura existente (acesso do mestre ao Compêndio e rolagens das criaturas). Seções: acesso à ficha, ficha do personagem, inventário e equipamentos, catálogos de itens (https://www.dndbeyond.com/sources/dnd/br-2024/equipment#MagicItems) e magias (https://avribacki.gitlab.io/magias5e/), modo de jogo do jogador, rolagens pré-configuradas, expressão e execução das rolagens, integração e consistência. O aplicativo está em uso: não derrubar, reiniciar, recriar ou substituir containers."

## Contexto

Hoje o jogador não tem ficha. O que existe para ele é o "perfil de combate" (modificador de iniciativa e PV, guardados no próprio participante), o rolador livre de dados e, no Modo Jogo, o rolador rápido (d20, d8, d6). O mestre tem o Compêndio de criaturas (specs 008 e 009) na barra superior, com uma ficha que monta rolagens a partir de quantidade, dado, atributo e adicional (spec 010).

Fatos do sistema atual que moldam esta feature:

- **Identidade.** Não há conta de usuário. Cada pessoa que entra na campanha com um código vira um participante com um token secreto guardado no navegador. O servidor identifica quem chama pelo token e já tem rotas "meu participante" (perfil de combate, foto). Entrar de novo com o código cria outro participante, com outro token.
- **Navegação.** A barra superior mostra "Mesa | Compêndio" só para o mestre, e a tela escolhida fica no endereço da página. O jogador não tem navegação ali hoje, e existe teste garantindo que ele nunca veja o Compêndio.
- **Rolagens.** O servidor é a autoridade dos dados: o cliente envia uma expressão, o servidor rola, guarda e avisa a mesa em tempo real. A sintaxe atual aceita termos somados (`2d6 + 3`) e uma repetição da expressão inteira (`3#d20+5`). Não existe vantagem, desvantagem, agrupamento por parênteses nem escolha do maior ou menor resultado.
- **Catálogos.** O compêndio de criaturas veio de um site externo por um importador que roda fora do app e gera um arquivo versionado. O app carrega esse arquivo ao iniciar e nunca consulta o site durante o uso. Por isso uma queda do site nunca afeta a mesa.
- **Fontes indicadas.** As magias (avribacki.gitlab.io/magias5e) são 361 magias em português (regras de 2014), servidas como arquivos estáticos, com nome, nível, escola, ritual, tempo de conjuração, alcance, componentes, duração, concentração, descrição e lista por classe. Os dados de dano só aparecem no texto. O dono autorizou o uso. A página de equipamentos da D&D Beyond ("br-2024" = Basic Rules 2024) está **em inglês**: 38 armas, 13 armaduras e 82 itens de aventura. Os 253 itens mágicos (incluindo poções) ficam numa página irmã ("magic-items-a-z"); a âncora `#MagicItems` só traz as regras gerais de itens mágicos. O mesmo conteúdo está publicado no SRD 5.2 sob licença aberta, que é a fonte usada para itens (ver Clarifications).
- **Nomes repetidos.** Hoje dois participantes podem ter o mesmo nome na campanha, e cada entrada com o código cria um participante novo. Já existem campanhas com participantes de nomes repetidos.

## Clarifications

### Session 2026-11-19

- Q: Fonte e idioma do catálogo de itens (a página da D&D Beyond está em inglês e os termos do site provavelmente proíbem extração)? → A: usar o mesmo conteúdo pelo SRD 5.2 (licença aberta CC-BY-4.0), em PT-BR.
- Q: O que acontece quando o jogador perde a sessão e entra de novo? → A: cada nome de personagem vira uma conta na campanha. Ao entrar com o código e digitar o nome, a pessoa entra nessa conta, com a ficha dela. O nome é guardado e exibido com a primeira letra de cada palavra maiúscula e as demais minúsculas (ex.: "Loren B. Vonn Neuman").
- Q: Licença e crédito do catálogo de magias? → A: o dono autorizou o uso e não quer crédito. A equipe do Pocket DM também não quer crédito no compêndio de criaturas.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Abrir e preencher a própria ficha (Priority: P1)

O jogador clica em "Ficha" na barra superior, no mesmo lugar onde o mestre vê "Compêndio". Na primeira vez a ficha nasce vazia, com valores padrão. Ele preenche nome do personagem, classe, nível, espécie, antecedente, os seis atributos, proficiências em salvaguardas e perícias, PV máximos base e atributo de conjuração. Os valores derivados (modificadores, bônus de proficiência, salvaguardas, perícias, percepção passiva, iniciativa, CA, CD e ataque de magia) aparecem calculados ao lado e mudam no instante em que um valor de origem muda.

**Why this priority**: sem a ficha, nenhuma outra parte (inventário, rolagens, modo jogo) tem de onde tirar valores.

**Independent Test**: como jogador, abrir "Ficha", mudar Destreza de 14 para 16 e o nível de 4 para 5, e ver o modificador de DES, a iniciativa, a CA sem armadura, as perícias de DES e o bônus de proficiência mudarem sem salvar manualmente nada além do campo editado. Recarregar a página e ver os valores mantidos.

**Acceptance Scenarios**:

1. **Given** um jogador sem ficha, **When** ele abre "Ficha" pela primeira vez, **Then** a ficha aparece com atributos 10, nível 1 e campos de texto vazios, pronta para edição.
2. **Given** a ficha aberta, **When** o jogador muda um atributo-base, **Then** o modificador, as salvaguardas, as perícias, a iniciativa e os demais valores que dependem dele são recalculados imediatamente.
3. **Given** um valor derivado (por exemplo, o modificador de FOR), **When** o jogador tenta editá-lo, **Then** o campo não é editável e mostra de onde vem o valor (base + cada modificador que contribuiu).
4. **Given** a ficha salva, **When** o jogador recarrega a página ou abre em outro aparelho com a mesma sessão, **Then** vê os mesmos valores.
5. **Given** o mestre na mesa, **When** a página carrega, **Then** ele continua vendo "Mesa | Compêndio" e não vê "Ficha"; o Compêndio continua funcionando como antes.

---

### User Story 2 - Ninguém mexe na ficha dos outros (Priority: P1)

Cada jogador só vê e altera a própria ficha, o próprio inventário e as próprias rolagens salvas. Não importa se o pedido vem da interface, de um endereço digitado ou de uma requisição montada à mão: o servidor decide de quem é a ficha pelo participante que fez o pedido.

**Why this priority**: é um requisito de segurança explícito do pedido, e a falha seria silenciosa.

**Independent Test**: com dois jogadores na mesma campanha e um jogador de outra campanha, tentar ler e alterar a ficha, o inventário e as rolagens de outro participante por requisição direta. Todas as tentativas são recusadas e nada muda.

**Acceptance Scenarios**:

1. **Given** os jogadores A e B na mesma campanha, **When** A envia uma requisição para ler ou alterar algo da ficha de B (trocando identificadores na requisição), **Then** o servidor recusa e a ficha de B não muda.
2. **Given** um pedido sem sessão válida, **When** ele chega a qualquer operação da ficha, **Then** é recusado como não autenticado.
3. **Given** o mestre, **When** ele chama as operações de ficha pessoal, **Then** recebe recusa (o mestre não tem ficha pessoal nesta feature) e o Compêndio dele segue intacto.
4. **Given** um jogador que digita o endereço da tela "Ficha" sendo mestre, ou o endereço do Compêndio sendo jogador, **When** a página carrega, **Then** cada um cai na Mesa.

---

### User Story 2b - Entrar na conta do personagem pelo nome (Priority: P1)

O jogador abre a campanha em outro aparelho, ou depois de limpar o navegador, digita o código da mesa e o nome do personagem. Se já existe na campanha uma conta com esse nome, ele entra nela e encontra a mesma ficha, mochila, magias, rolagens salvas, foto e posição no battlemap. Se não existe, a conta é criada. O nome sempre aparece com a primeira letra de cada palavra maiúscula, não importa como foi digitado.

**Why this priority**: sem isso, a ficha some na primeira troca de aparelho, o que derruba o valor de todo o resto.

**Independent Test**: entrar como "loren b. vonn neuman", preencher a ficha, entrar em outro navegador com o mesmo código e "LOREN B. VONN NEUMAN", e ver a mesma ficha com o nome exibido "Loren B. Vonn Neuman"; o primeiro navegador continua conectado.

**Acceptance Scenarios**:

1. **Given** uma campanha sem ninguém chamado "Loren", **When** alguém entra com o código de jogador e o nome "  loren  ", **Then** a conta "Loren" é criada.
2. **Given** a conta "Loren B. Vonn Neuman" com ficha, **When** alguém entra com o código de jogador e "loren b. VONN neuman", **Then** entra nessa conta e vê a ficha existente; nenhuma conta nova é criada.
3. **Given** a conta aberta num celular, **When** a pessoa entra pelo nome num computador, **Then** as duas sessões continuam válidas.
4. **Given** "Loren" na campanha A, **When** alguém entra na campanha B como "Loren", **Then** é outra conta, sem relação com a da campanha A.
5. **Given** a conta "Loren" de jogador, **When** alguém entra com o código de mestre e o nome "Loren", **Then** entra na conta de mestre "Loren", que é separada da conta de jogador; papéis nunca se misturam.
6. **Given** uma campanha antiga com dois participantes jogadores chamados "loren", **When** a feature é instalada e alguém entra como "Loren", **Then** entra numa conta única e previsível (regra em FR-044), sem perder dados dos outros participantes.

---

### User Story 3 - Inventário e equipamentos que mudam a ficha (Priority: P1)

O jogador cadastra, edita e remove itens da mochila, com categoria (arma, armadura, escudo, equipamento, diversos, poção), quantidade e descrição. Armas, armaduras e escudos podem ter bônus mágico (+1, +2, +3). Qualquer item pode ter efeitos configuráveis: modificar um atributo, PV máximos, CA, deslocamento, visão, resistências, condições ou outros. Ao marcar um item como equipado, os efeitos entram nos valores finais da ficha; ao desmarcar, saem. Os valores-base nunca são alterados.

**Why this priority**: é o que dá sentido à separação entre base e final, e é o que as rolagens precisam para estarem corretas.

**Independent Test**: com CA base sem armadura 12 (DES 14), equipar uma Cota de Malha (CA 16), depois um Escudo +1 (CA 19), depois um Anel de Proteção (+1 CA, total 20); desequipar o escudo (CA 17) e ver os atributos-base iguais aos do início.

**Acceptance Scenarios**:

1. **Given** uma armadura equipada, **When** o jogador equipa outra armadura, **Then** a anterior é desequipada automaticamente, ambas continuam na mochila e a CA reflete só a nova.
2. **Given** um escudo equipado, **When** o jogador equipa outro escudo, **Then** o anterior é desequipado e só um escudo conta para a CA.
3. **Given** duas armas na mochila, **When** o jogador equipa as duas, **Then** ambas ficam equipadas e disponíveis para rolagem.
4. **Given** dois itens equipados com efeitos diferentes que somam (por exemplo, +1 CA de um anel e +1 CA de uma capa), **When** a ficha é calculada, **Then** os dois bônus são somados (+2).
5. **Given** dois itens equipados que concedem um efeito exatamente igual (mesmo tipo, mesmo alvo, mesma operação, mesmo valor; por exemplo, dois Anéis de Proteção), **When** a ficha é calculada, **Then** o efeito é aplicado uma vez só, e a ficha indica qual item ficou sem efeito por duplicidade.
6. **Given** um item equipado que dá +2 em FOR, **When** o jogador desequipa o item, **Then** FOR final volta ao valor-base, e tudo que depende de FOR é recalculado.
7. **Given** um item com efeito "FOR passa a ser 19" e FOR-base 21, **When** o item é equipado, **Then** FOR final continua 21 (um valor fixo só vale se for maior que o valor que já existe).
8. **Given** a mochila com várias armaduras e escudos, **When** o jogador olha o inventário, **Then** todos continuam lá; só a regra de "equipado" é limitada.

---

### User Story 4 - Rolar pelo painel do jogador no Modo Jogo (Priority: P1)

No Modo Jogo, o jogador tem um painel de rolagem parecido com o das criaturas. Escolhe os dados (cada clique num dado soma uma unidade, segurar remove uma, um botão limpa tudo), um atributo, uma arma equipada ou uma magia, modificadores adicionais, vantagem ou desvantagem e quantas vezes repetir. A expressão que será executada aparece em tempo real, já com o modificador do atributo, a proficiência e o bônus da arma. Ele rola sem sair do Modo Jogo, e o resultado vai para o histórico da mesa como as outras rolagens.

**Why this priority**: é o uso de toda sessão. O pedido exige rolar sem ir para outra tela configurar modificadores.

**Independent Test**: no Modo Jogo, escolher a Espada Longa +1 equipada (FOR 16, proficiente, nível 5), ver "d20 + (FOR) 3 + (prof) 3 + (arma) 1", marcar vantagem, ver "2#(d20 + (FOR) 3 + (prof) 3 + (arma) 1) maior", rolar, e ver no histórico as duas execuções completas e o maior total escolhido.

**Acceptance Scenarios**:

1. **Given** o painel aberto, **When** o jogador clica três vezes no d6 e uma vez no d20, **Then** a expressão mostra "d20 + 3d6" e cada novo clique soma uma unidade.
2. **Given** 3d6 selecionados, **When** o jogador mantém o d6 pressionado (ou usa a tecla de remover com o d6 em foco), **Then** passa a 2d6; ao limpar, nenhum dado fica selecionado.
3. **Given** uma arma escolhida, **When** a rolagem é de ataque, **Then** entram o atributo da arma (FOR para corpo a corpo, DES para à distância, o maior dos dois para acuidade, com troca manual possível), a proficiência só se a arma estiver marcada como proficiente, e o bônus mágico da arma; **When** a rolagem é de dano, **Then** entram o dado de dano da arma, o atributo e o bônus mágico, sem proficiência.
4. **Given** uma magia escolhida, **When** a rolagem é de ataque mágico, **Then** usa o atributo de conjuração configurado na ficha mais a proficiência; **When** é de dano, **Then** usa os dados de dano da magia (editáveis) e não soma o atributo, salvo marcação do jogador.
5. **Given** o jogador desequipa um item que dava +1 em ataques, **When** volta ao painel, **Then** a expressão já não tem o +1, sem ação manual.
6. **Given** o Modo Jogo ativo, **When** o jogador abre a mochila pelo painel, **Then** pode adicionar, editar, remover, equipar e desequipar itens sem sair do Modo Jogo, e a expressão em montagem se atualiza.
7. **Given** o mestre usando a ficha de criatura, o rolador rápido ou o rolador livre, **When** a feature é entregue, **Then** tudo funciona exatamente como antes.

---

### User Story 5 - Vantagem, desvantagem e repetição com a mesma semântica na tela e no servidor (Priority: P1)

Vantagem executa a expressão completa duas vezes e fica com o maior total; desvantagem, com o menor. A repetição configurada pelo jogador é outra coisa: executa a expressão N vezes e mostra os N totais. As duas podem ser combinadas, e a expressão exibida é exatamente a que o servidor executa.

**Why this priority**: é a regra mais fácil de errar em silêncio, e o pedido pede que a tela e a execução nunca divirjam.

**Independent Test**: com uma sequência de dados controlada no teste, rolar "d20 + 5 + 2d6" com vantagem e conferir que os dois totais completos foram calculados (d20, 5 e 2d6 em cada execução) e o maior foi escolhido; repetir com desvantagem (menor); repetir com 3 repetições e vantagem e conferir 3 resultados, cada um sendo o maior de duas execuções completas.

**Acceptance Scenarios**:

1. **Given** a expressão "d20 + 5 + 2d6" sem vantagem nem desvantagem, **When** o jogador rola, **Then** há uma execução e um total.
2. **Given** a mesma expressão com vantagem, **When** ele olha a tela, **Then** vê "2#(d20 + 5 + 2d6) maior"; **When** rola, **Then** o servidor executa a expressão completa duas vezes e o resultado é o maior dos dois totais, mostrando os dois.
3. **Given** a mesma expressão com desvantagem, **When** ele rola, **Then** a tela mostrava "2#(d20 + 5 + 2d6) menor" e o resultado é o menor dos dois totais.
4. **Given** 3 repetições sem vantagem, **When** ele olha a tela, **Then** vê "3#[d20 + 5 + 2d6]" e recebe 3 totais independentes.
5. **Given** 3 repetições com vantagem, **When** ele olha a tela, **Then** vê "3#[2#(d20 + 5 + 2d6) maior]" e recebe 3 totais, cada um o maior de duas execuções completas.
6. **Given** uma configuração cuja execução ultrapassaria os limites de dados ou repetições, **When** o jogador tenta rolar, **Then** a tela avisa antes de enviar e o servidor também recusa; nenhuma repetição é cortada ou ignorada em silêncio.

---

### User Story 6 - Rolagens salvas (Priority: P2)

O jogador cria rolagens com nome e ícone ("Ataque com espada", "Bola de Fogo", "Teste de Furtividade"), guardando dados, modificadores, atributo, arma ou magia, vantagem ou desvantagem e repetições. Elas aparecem como botões no painel do Modo Jogo e podem ser editadas, duplicadas e apagadas. Como guardam referências (à arma, ao atributo), uma rolagem salva sempre usa os valores atuais da ficha.

**Why this priority**: acelera a mesa, mas o painel livre (US4) já entrega o valor principal.

**Independent Test**: criar "Ataque com espada" usando a Espada Longa, subir FOR de 16 para 18, usar a rolagem salva e ver o modificador novo; trocar o ícone, recarregar e ver o ícone mantido.

**Acceptance Scenarios**:

1. **Given** o editor de rolagem, **When** o jogador escolhe nome, ícone, dados, atributo, arma, adicionais, vantagem e repetições, **Then** a mesma expressão em tempo real do painel aparece e a rolagem é salva com essa configuração.
2. **Given** uma rolagem salva que usa uma arma, **When** a arma é removida da mochila, **Then** a rolagem fica marcada como "arma ausente" e não executa até ser editada; ela não some em silêncio.
3. **Given** a lista de ícones, **When** o jogador escolhe um, **Then** só existem ícones do mesmo conjunto visual usado no restante do app.
4. **Given** uma rolagem salva, **When** o jogador a usa, **Then** o rótulo da rolagem no histórico da mesa é o nome dela.

---

### User Story 7 - Catálogo de itens e de magias (Priority: P2)

O jogador busca e filtra um catálogo de itens (armas, armaduras, equipamentos, itens mágicos, poções) e um de magias (nome, nível, escola, classe, ritual, concentração). Ele adiciona um item do catálogo à mochila com os dados já preenchidos (dano e propriedades da arma, CA e tipo da armadura, bônus mágico) e pode editá-lo depois sem mudar o catálogo. Ele escolhe as magias que o personagem conhece e as usa no painel de rolagem. Também pode criar itens e magias próprios, que não dependem das fontes.

**Why this priority**: poupa digitação e reduz erro, mas o jogador consegue jogar só com itens personalizados.

**Independent Test**: buscar "espada longa" no catálogo e adicioná-la (vem com 1d8 cortante, versátil 1d10); buscar "bola de fogo" e adicioná-la às magias (vem nível 3, evocação, 8d6 sugerido como dano); criar o item "Amuleto da avó" com +1 em SAB; simular a fonte externa fora do ar e ver os três continuarem disponíveis.

**Acceptance Scenarios**:

1. **Given** o catálogo de itens, **When** o jogador busca por nome (com ou sem acento) ou filtra por categoria, **Then** vê os itens importados do SRD 5.2 em português, com o aviso de licença exigido pelo SRD.
2. **Given** um item do catálogo, **When** o jogador o adiciona à mochila, **Then** uma cópia editável entra na mochila; editar a cópia não muda o catálogo nem a mochila de outros jogadores.
3. **Given** o catálogo de magias, **When** o jogador adiciona uma magia à ficha, **Then** ela aparece na lista de magias do personagem e no painel de rolagem.
4. **Given** um item ou magia personalizados, **When** o catálogo é atualizado ou a fonte some, **Then** eles continuam intactos na ficha.
5. **Given** uma nova versão do catálogo, **When** o app inicia, **Then** os itens já copiados para mochilas não mudam.

---

### Edge Cases

- Atributo-base fora de 1 a 30, nível fora de 1 a 20, quantidade negativa: recusado com mensagem; valor final (com efeitos) pode passar de 20, mas fica no máximo em 30.
- Armadura média com DES alta: o bônus de DES na CA para em +2; armadura pesada não soma DES; sem armadura, CA = 10 + DES (ou uma CA base sem armadura configurável, para Defesa sem Armadura de bárbaro e monge).
- Escudo sem armadura: soma ao cálculo sem armadura.
- PV máximo final menor que os PV atuais após desequipar um item de PV: os PV atuais passam a ser o novo máximo.
- Desequipar o único item que dava um efeito duplicado: o outro item com o mesmo efeito passa a valer (a duplicidade é recalculada, não guardada).
- Efeito de tipo que a ficha ainda não sabe calcular (texto livre, condição): aparece na lista de efeitos ativos, sem mexer em números.
- Arma sem dado de dano configurado: a rolagem de dano fica indisponível com aviso; ataque continua possível.
- Magia sem dado de dano detectado: o jogador informa os dados à mão.
- Proficiência geral "armas marciais" marcada e espada não marcada como proficiente: vale a marcação da espada. Os dois marcados: a proficiência entra uma vez só.
- Armadura equipada sem proficiência: a ficha avisa e as rolagens de FOR e DES chegam com desvantagem pré-selecionada e sinalizada, que o jogador pode remover.
- Vantagem e desvantagem não podem ficar marcadas juntas: marcar uma desmarca a outra.
- O mesmo jogador com a ficha aberta em dois aparelhos: a segunda gravação baseada em dados antigos é recusada com aviso para recarregar; nada é sobrescrito em silêncio.
- Conexão caiu no meio da edição: a ficha mostra o último estado salvo e não finge que salvou.
- Jogador perde a sessão (limpou o navegador) e entra de novo com o código e o mesmo nome: entra na mesma conta, com a mesma ficha (US 2b).
- Nome com espaços extras ou só espaços: espaços nas pontas são removidos e espaços repetidos viram um; nome vazio é recusado.
- Nome com hífen ou apóstrofo ("jean-luc", "d'artagnan"): cada parte separada por espaço ou hífen ganha inicial maiúscula ("Jean-Luc", "D'artagnan").
- Nomes que só diferem em acento ("Joao" e "João"): são contas diferentes; a comparação ignora só maiúsculas e minúsculas.
- Jogador sai da campanha pelo menu: a sessão daquele aparelho é encerrada; a conta e a ficha continuam e voltam ao entrar de novo pelo nome.
- Catálogo vazio (importação ainda não feita ou falhou): o catálogo mostra estado vazio com explicação, e itens personalizados seguem disponíveis.
- Rolagem salva com magia removida da ficha: marcada como "magia ausente" até ser editada.

## Requirements *(mandatory)*

### Functional Requirements

**Acesso e permissões**

- **FR-001**: A barra superior MUST mostrar ao jogador a navegação "Mesa | Ficha" no mesmo lugar e com o mesmo visual da navegação "Mesa | Compêndio" do mestre. O mestre MUST continuar vendo só "Mesa | Compêndio".
- **FR-002**: A tela escolhida MUST ficar no endereço da página, como já acontece com o Compêndio. O endereço da Ficha aberto pelo mestre, e o do Compêndio aberto pelo jogador, MUST abrir a Mesa.
- **FR-003**: Toda leitura e escrita de ficha, inventário, magias do personagem e rolagens salvas MUST identificar o dono pela sessão de quem pede, nunca por um identificador enviado pelo cliente. Pedidos sem sessão válida MUST ser recusados como não autenticados; o mestre MUST receber recusa de permissão.
- **FR-004**: Cada participante jogador MUST ter no máximo uma ficha por campanha, criada na primeira vez que ele a abre.

**Ficha**

- **FR-005**: A ficha MUST ter campos editáveis: nome do personagem, classe, subclasse, nível (1 a 20), espécie, antecedente, alinhamento, experiência, os seis atributos-base (1 a 30), proficiência em cada salvaguarda, proficiência e especialização em cada uma das 18 perícias, proficiências gerais (armaduras, armas, ferramentas, idiomas como texto e marcações), PV máximos base, PV atuais, PV temporários, CA base sem armadura, deslocamento base, sentidos base, atributo de conjuração, traços e anotações.
- **FR-006**: A ficha MUST calcular, sem edição direta: modificador de cada atributo final (arredondado para baixo de (valor − 10) / 2), bônus de proficiência pelo nível (+2 nos níveis 1–4, +3 nos 5–8, +4 nos 9–12, +5 nos 13–16, +6 nos 17–20), salvaguardas, perícias, percepção passiva (10 + Percepção), iniciativa, CA final, PV máximos finais, deslocamento final, sentidos finais, CD de magia (8 + proficiência + modificador de conjuração) e bônus de ataque mágico (proficiência + modificador de conjuração).
- **FR-007**: Cada valor derivado MUST mostrar sua composição (base e cada contribuição, com o nome do item ou regra de origem).
- **FR-008**: Valores-base e valores finais MUST ficar separados: equipar, desequipar, aplicar ou remover efeitos nunca MUST alterar um valor-base.
- **FR-009**: A CA final MUST seguir: sem armadura, CA base sem armadura (padrão 10) + DES; armadura leve, CA da armadura + DES; média, CA da armadura + DES até +2; pesada, CA da armadura; em todos os casos somando escudo equipado, bônus mágico da armadura e do escudo e efeitos de CA.
- **FR-010**: A iniciativa e os PV da ficha MUST ser os mesmos usados pela mesa no combate (o perfil de combate passa a ser alimentado pela ficha quando ela existe), para que ficha, mesa e iniciativa nunca mostrem números diferentes.

**Inventário e efeitos**

- **FR-011**: O jogador MUST poder adicionar, editar e remover itens com nome, categoria (arma, armadura, escudo, equipamento, diversos, poção), quantidade, peso, descrição e anotações.
- **FR-012**: Armas MUST ter dado e tipo de dano, dano versátil opcional, propriedades (acuidade, leve, pesada, duas mãos, arremesso, distância com alcance, munição, recarga, alcance estendido, versátil), corpo a corpo ou à distância, bônus mágico (0 a +3) e marcação individual de proficiência.
- **FR-013**: Armaduras MUST ter CA, tipo (leve, média, pesada), força mínima, desvantagem em Furtividade, bônus mágico e marcação individual de proficiência. Escudos MUST ter bônus de CA (padrão +2), bônus mágico e marcação de proficiência.
- **FR-014**: Qualquer item MUST poder ter uma lista de efeitos. Cada efeito MUST ter tipo, alvo, operação e valor. Tipos mínimos: atributo, PV máximos, CA, deslocamento, visão (tipo e alcance), resistência (tipo de dano), imunidade, condição, bônus em ataque, bônus em dano, bônus em salvaguarda, bônus em perícia, e texto livre. Operações mínimas: somar e definir valor mínimo. Novos tipos MUST poder ser acrescentados sem código específico por item.
- **FR-015**: Só itens equipados MUST aplicar efeitos e bônus mágicos. Itens que não se equipam (poção, diversos) MUST não aplicar efeitos permanentes.
- **FR-016**: Efeitos diferentes MUST se somar. Efeitos exatamente iguais (mesmo tipo, alvo, operação e valor), vindos de itens diferentes, MUST valer uma vez só, e a ficha MUST indicar o item cujo efeito foi ignorado por duplicidade.
- **FR-017**: No máximo uma armadura e um escudo MUST ficar equipados. Equipar um novo MUST desequipar o anterior da mesma categoria, com aviso. Armas e demais itens equipáveis MUST não ter esse limite. A mochila MUST aceitar vários itens de qualquer categoria.
- **FR-018**: As regras de cálculo de valores derivados, efeitos, duplicidade e limites de equipamento MUST existir em um único lugar e ser a mesma fonte para a ficha, o inventário, o painel de rolagem e as rolagens salvas.

**Catálogos**

- **FR-019**: O catálogo de magias MUST ser obtido de https://avribacki.gitlab.io/magias5e/ (uso autorizado pelo dono) e trazer nome, nível, escola, ritual, tempo de conjuração, alcance, componentes, duração, concentração, descrição, classes e uma sugestão de dados de dano extraída do texto quando houver. O app MUST não exibir crédito a essa fonte.
- **FR-020**: O catálogo de itens MUST vir do SRD 5.2 (licença CC-BY-4.0), em português, e trazer armas, armaduras, escudos, equipamento de aventura, itens mágicos e poções com os campos de FR-012 e FR-013 preenchidos quando a fonte os tiver. Se não houver versão oficial em português, o texto MUST ser traduzido na importação, mantendo os números e regras idênticos ao original. O catálogo MUST exibir o aviso de atribuição que a licença CC-BY-4.0 exige.
- **FR-021**: Os catálogos MUST ser importados fora do uso da mesa e carregados pelo app a partir de uma cópia versionada. Indisponibilidade, mudança de estrutura ou falha de acesso às fontes MUST afetar só a importação, nunca o funcionamento do app. Uma importação com estrutura inesperada MUST falhar com relatório, sem gerar catálogo parcial.
- **FR-022**: Os catálogos MUST ter busca por nome sem diferenciar acentos e maiúsculas, filtros (itens: categoria, raridade; magias: nível, escola, classe, ritual, concentração) e paginação.
- **FR-023**: Adicionar um item ou magia do catálogo MUST criar uma cópia na ficha. Atualizações posteriores do catálogo MUST não mudar cópias existentes.
- **FR-024**: O jogador MUST poder criar itens e magias personalizados, com os mesmos campos, sem depender dos catálogos.

**Painel de rolagem e expressão**

- **FR-025**: O Modo Jogo do jogador MUST ter um painel de rolagem com: seleção de dados (d4, d6, d8, d10, d12, d20, d100), atributo, arma equipada ou magia da ficha, tipo de rolagem (teste, ataque, dano), perícia ou salvaguarda quando for teste, modificadores adicionais, vantagem ou desvantagem e repetições (1 a 20).
- **FR-026**: Clicar num dado MUST somar uma unidade; manter pressionado por 0,5 s MUST remover uma; com o dado em foco pelo teclado, uma tecla de remoção MUST remover uma; um botão MUST limpar todos os dados.
- **FR-027**: A expressão MUST ser exibida em tempo real e identificar a origem de cada número fixo: atributo (por exemplo "(FOR) 3"), proficiência ("(prof) 3"), arma ("(arma) 1"), efeito de item ("(Anel) 1") e adicional. Repetição MUST ser exibida como "N#[...]", vantagem como "2#(...) maior" e desvantagem como "2#(...) menor"; combinadas, "N#[2#(...) maior]".
- **FR-028**: A vantagem e a desvantagem MUST envolver a expressão completa: todos os dados e modificadores executados duas vezes, escolhendo o maior ou o menor total. MUST ser aplicadas por último, por dentro da repetição.
- **FR-029**: O servidor MUST executar exatamente a estrutura exibida (repetição, vantagem ou desvantagem, termos) e devolver cada execução, para que a mesa veja os dois totais da vantagem e qual foi escolhido. A sintaxe de rolagem atual MUST continuar aceita, com o mesmo resultado.
- **FR-030**: A proficiência MUST entrar uma vez só por rolagem: ataque com arma usa a marcação da arma; teste de perícia usa a perícia (dobrada se houver especialização); salvaguarda usa a salvaguarda; ataque mágico sempre soma. Marcações gerais da ficha MUST servir de valor inicial da marcação do item quando ele é adicionado, e depois ser independentes.
- **FR-031**: O jogador MUST poder abrir e editar a mochila dentro do Modo Jogo, com as mesmas regras do inventário (FR-011 a FR-017).
- **FR-032**: Rolagens do jogador MUST entrar no histórico da mesa e ser avisadas em tempo real como as rolagens existentes, com um rótulo descritivo (nome da rolagem salva ou "Ataque: Espada Longa").

**Rolagens salvas**

- **FR-033**: O jogador MUST poder criar, editar, duplicar, apagar e usar rolagens salvas com nome (até 40 caracteres), ícone de uma lista fechada de ícones do conjunto visual do app, dados e quantidades, adicionais, atributo, perícia ou salvaguarda, arma ou magia, tipo de rolagem, vantagem ou desvantagem e repetições.
- **FR-034**: Rolagens salvas MUST guardar referências (à arma, à magia, ao atributo), não números já calculados, e ser montadas com os valores atuais da ficha no momento do uso.
- **FR-035**: Uma rolagem salva cuja arma ou magia foi removida MUST ficar marcada como incompleta e não executar até ser editada.
- **FR-036**: O editor de rolagens salvas e o painel do Modo Jogo MUST oferecer as mesmas opções e mostrar a mesma expressão para a mesma configuração.

**Compatibilidade e operação**

- **FR-037**: Compêndio, ficha de criatura, painel de rolagem das criaturas, rolador livre, rolador rápido, confirmação de acerto e dano, iniciativa e battlemap MUST manter o comportamento atual.
- **FR-038**: A entrega MUST ser aplicável sem perda de dados: mudanças de banco aditivas, aplicadas pelo processo já usado pelo app, e nenhuma etapa de desenvolvimento ou validação pode parar, reiniciar, recriar ou substituir os containers em uso.
- **FR-039**: Todas as telas novas MUST funcionar em 1280, 768 e 375 px sem rolagem horizontal, com alvos de toque de pelo menos 44 px e uso por teclado.
- **FR-040**: O compêndio de criaturas MUST deixar de exibir o crédito ao Pocket DM, a pedido da equipe deles. Avisos de licença exigidos pelo SRD, se houver, continuam.

**Conta por nome**

- **FR-041**: O nome digitado ao entrar MUST ser normalizado antes de ser guardado e exibido: espaços nas pontas removidos, espaços repetidos reduzidos a um, e cada parte separada por espaço ou hífen com a primeira letra maiúscula e as demais minúsculas. Nome vazio após a normalização MUST ser recusado.
- **FR-042**: Na mesma campanha e no mesmo papel, o nome normalizado MUST identificar uma única conta (comparação sem diferenciar maiúsculas e minúsculas). Entrar com o código e um nome existente MUST abrir essa conta; um nome novo MUST criar uma conta. Contas de jogador e de mestre com o mesmo nome MUST ser separadas.
- **FR-043**: Entrar numa conta existente MUST criar uma sessão nova sem encerrar as outras sessões da mesma conta. "Sair da campanha" MUST encerrar só a sessão daquele aparelho.
- **FR-044**: Campanhas existentes com participantes de mesmo nome e papel MUST continuar funcionando sem perda de dados. Para essas, entrar pelo nome MUST abrir o participante mais recente com esse nome; os demais seguem acessíveis pelas sessões que já têm.
- **FR-045**: A entrada pelo nome MUST manter o limite atual de tentativas por minuto contra tentativas em massa.

### Key Entities

- **Conta (participante)**: já existe. Passa a ser identificada pelo nome normalizado dentro da campanha e do papel, e a aceitar várias sessões ativas ao mesmo tempo (uma por aparelho).
- **Ficha do personagem**: pertence a um participante jogador de uma campanha (um para um). Guarda os campos editáveis (FR-005). Os valores finais não são guardados como verdade; são calculados a partir da ficha e do inventário.
- **Item do inventário**: pertence a uma ficha. Nome, categoria, quantidade, peso, descrição, equipado, dados de arma ou armadura ou escudo, bônus mágico, marcação de proficiência, lista de efeitos, origem (catálogo com referência ao item original, ou personalizado).
- **Efeito**: pertence a um item. Tipo, alvo, operação, valor. Sua identidade para duplicidade é a combinação desses quatro.
- **Magia do personagem**: pertence a uma ficha. Cópia de uma magia do catálogo ou personalizada, com os dados de dano editáveis.
- **Rolagem salva**: pertence a uma ficha. Nome, ícone, dados, adicionais, referências (atributo, perícia, arma, magia), tipo, vantagem ou desvantagem, repetições.
- **Item de catálogo** e **Magia de catálogo**: globais, somente leitura, com fonte, versão e crédito; atualizados por importação.
- **Execução de rolagem**: a rolagem existente passa a registrar cada execução de uma vantagem ou desvantagem e qual total foi escolhido.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: O jogador chega à própria ficha em 1 clique a partir de qualquer ponto da Mesa e volta em 1 clique.
- **SC-002**: Em 100% das tentativas automatizadas de ler ou alterar ficha, inventário, magias ou rolagens salvas de outro participante (troca de identificadores, sessão de outra campanha, sessão de mestre, sem sessão), nada é revelado nem alterado.
- **SC-003**: Uma bateria de pelo menos 30 casos de cálculo (atributos, CA por tipo de armadura, efeitos que somam, efeitos duplicados, valor mínimo, equipar e desequipar, proficiência, especialização) dá o mesmo resultado na ficha, no inventário, no painel e nas rolagens salvas.
- **SC-004**: Depois de qualquer sequência de equipar e desequipar, os valores-base são idênticos aos iniciais em 100% dos casos testados.
- **SC-005**: Com dados controlados, 100% das rolagens com vantagem ou desvantagem executam todos os termos da expressão duas vezes e escolhem o total certo; a expressão exibida e a executada coincidem em 100% das configurações testadas.
- **SC-006**: O jogador monta e executa uma rolagem de ataque com arma em até 3 toques no Modo Jogo, sem trocar de tela.
- **SC-007**: O catálogo de magias traz as 361 magias da fonte e o de itens traz todas as armas, armaduras e itens mágicos da fonte escolhida, conferidos contra a contagem da própria fonte no relatório de importação.
- **SC-008**: Com a fonte externa inacessível, a mesa, a ficha, o inventário e o catálogo já importado funcionam normalmente (zero erros visíveis ao jogador).
- **SC-009**: Todos os testes existentes de criaturas, Compêndio, rolagens e Modo Jogo continuam passando, e a entrega não exige parar nenhum container em uso.
- **SC-010**: Um revisor que não participou da construção abre a ficha e o painel em 1280 e 375 px e não encontra sobreposição, rolagem horizontal ou controle menor que 44 px.
- **SC-011**: Entrar com o mesmo código e o mesmo nome (em qualquer combinação de maiúsculas e espaços) leva à mesma conta e à mesma ficha em 100% dos casos testados, e nenhuma conta existente perde dados com a instalação da feature.

## Assumptions

- O sistema de RPG é D&D 5ª edição, como no compêndio de criaturas. A ficha segue as regras de 2024 onde elas diferem (por exemplo, "espécie" no lugar de "raça"); as magias da fonte indicada seguem as regras de 2014 e são usadas como estão.
- A ficha pertence ao participante jogador da campanha. O mestre não tem ficha pessoal nesta feature e não consulta as fichas dos jogadores; dar ao mestre uma leitura das fichas (pelo token no battlemap, ponto já reservado na spec 010) fica para outra feature.
- "Mesmo contexto de navegação do Compêndio" é a navegação da barra superior: o jogador recebe "Mesa | Ficha" no mesmo componente em que o mestre tem "Mesa | Compêndio".
- Efeitos de poção e outros consumíveis não são aplicados automaticamente nesta feature: poções ficam na mochila com descrição e, quando curam, com o dado de cura disponível para rolar.
- Espaços de magia, recursos de classe, sintonização com limite de 3 itens, carga e moedas ficam fora do escopo; a sintonização aparece como informação do item.
- Os catálogos seguem o padrão já usado no compêndio de criaturas: um importador roda fora do app, gera uma cópia versionada e o app a carrega ao iniciar. O app nunca consulta as fontes durante o uso.
- Notação: repetição usa colchetes ("3#[...]"), vantagem e desvantagem usam parênteses com "maior" ou "menor" ("2#(...) maior"). Isso evita que "2#(...)" com vantagem se confunda com duas repetições sem vantagem. Combinadas, a vantagem fica por dentro: cada repetição é resolvida com vantagem.
- "(char) 3" no exemplo do pedido é o modificador do atributo escolhido, exibido com a abreviação em português (FOR, DES, CON, INT, SAB, CAR), como no painel das criaturas.
- A regra de servidor como autoridade dos dados continua: a tela monta a expressão e o servidor a executa e registra.
- Conta por nome é proteção leve, por decisão do Marcos: quem sabe o código de jogador e o nome de um personagem consegue entrar na conta dele. É o mesmo nível de confiança que o código da mesa já dá hoje; não há senha.
- Os catálogos de magias e de criaturas não exibem crédito às fontes, a pedido dos donos, que autorizaram o uso. O catálogo de itens exibe só o aviso de atribuição exigido pela licença do SRD 5.2.
- A restrição do pedido ("não derrubar containers") prevalece sobre a autorização operacional local da constituição para esta feature. Testes de integração usam bancos descartáveis próprios, separados do banco em uso.
- Rolagens do jogador seguem a visibilidade atual das rolagens dos jogadores (públicas para a mesa).
