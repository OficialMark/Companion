# Contrato de interface: navegação Mesa / Compêndio

Contrato da interface do mestre. A API HTTP não muda; a listagem de criaturas segue o contrato da spec 008 ([../../008-biblioteca-criaturas/contracts/creatures-api.md](../../008-biblioteca-criaturas/contracts/creatures-api.md)).

## Endereço

| Endereço | Mestre | Jogador |
|---|---|---|
| `/` | Mesa | Mesa |
| `/?tela=compendio` | Compêndio | Mesa (a navegação não aparece) |
| `/?tela=<outro valor>` | Mesa | Mesa |

- Trocar de tela usa *replace*: o botão Voltar não percorre as trocas.
- Os outros parâmetros do endereço são preservados.

## Navegação na barra superior

- **Elemento**: `nav[aria-label="Áreas do mestre"][data-master-nav]`, renderizado só para o mestre.
- **Itens**: dois botões, `[data-view="mesa"]` ("Mesa") e `[data-view="compendio"]` ("Compêndio").
- **Item atual**: `aria-current="page"`. O outro item não tem o atributo.
- **Alvo**: cada item mede pelo menos 44 × 44 px em qualquer largura.
- **Layout**:
  - Até 900 px, a navegação e o nome da campanha ocupam a segunda linha da barra.
  - Até 480 px, o nome da campanha sobe para a primeira linha, ao lado do logo, truncado. Ele continua visível.

## Tela do Compêndio

- **Título**: `h1` "Compêndio".
- **Ações**:
  - `[data-mine]`: "Minhas criaturas", com `aria-pressed` refletindo `origin === 'Custom'`.
  - `[data-new-creature]`: "Nova criatura".
- **Filtros**: busca (`aria-label="Buscar criatura"`) numa linha própria. Abaixo, Tamanho, Tipo, ND mínimo, ND máximo e Origem com largura igual.
- **Rodapé da lista**: paginação `nav[aria-label="Páginas"]` e o crédito com link para o Pocket DM.
- **Sem conexão**: `[data-compendio-offline]` com "Comandos pausados até a conexão voltar".
- **Estado vazio**:
  - Com "Minhas criaturas" ligado e busca vazia: `[data-mine-empty]` e o botão "Criar a primeira".
- **Ausentes nesta tela**: aviso do código do mestre e painéis da mesa.

## Modal da ficha

- **Visualização**:
  - Números rápidos (CA, PV, Iniciativa, ND).
  - `[data-creature-speed]` quando a criatura tem deslocamento.
  - Atributos, painel de rolagem, detalhes, seções, anotações.
- **Edição**: `.creature-editor-fields` é a única área que rola; a barra de ações fica numa linha própria do modal.
- **Rodapé até 680 px**: "Editar ficha" é um botão de ícone de 44 px, com `aria-label="Editar ficha"`.
