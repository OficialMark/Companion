# Research: Acesso, Administração e Iniciativa da Campanha

## Códigos de acesso globais

**Decision**: Registros separados, com código de quatro dígitos como chave única e associação à campanha e ao papel.

**Rationale**: Uma restrição cobre códigos dos dois papéis e de todas as mesas.

**Alternatives considered**: Duas colunas na campanha não garantem facilmente unicidade cruzada; prefixo de papel violaria quatro dígitos.

## Migração de códigos existentes

**Decision**: Criar dois códigos numéricos únicos por campanha existente, remover o código alfanumérico anterior e preservar tokens de sessão.

**Rationale**: Mantém campanhas e sessões; somente códigos antigos de entrada são substituídos.

**Alternatives considered**: Compatibilidade legada quebraria o formato; excluir mesas causaria perda.

## Proteção administrativa

**Decision**: Segredo externo da instalação, comparação em tempo constante, retenção somente na sessão da página e limite de tentativas nas rotas de entrada e administração.

**Rationale**: A URL não autentica; um segredo externo atende ao escopo doméstico sem criar contas administrativas.

**Alternatives considered**: URL secreta é descobrível; conta completa amplia escopo.

## Exclusão de campanha

**Decision**: Excluir o agregado em transação e emitir `CampaignDeleted` somente após commit.

**Rationale**: Evita exclusão parcial e eventos de operação revertida.

**Alternatives considered**: Soft delete conflita com exclusão permanente; cascata total cria múltiplos caminhos problemáticos.

## Preferência e cópia de iniciativa

**Decision**: Persistir `InitiativeModifier` na participação e `ParticipantId` opcional no combatente. A rolagem geral em preparação importa jogadores online ainda não vinculados; a entrada passa a ser uma cópia editável pelo mestre.

**Rationale**: Identidade evita duplicidade e a cópia preserva edições do encontro.

**Alternatives considered**: Nome falha com duplicatas; referência viva sobrescreve ajustes do mestre.

## Edição durante combate

**Decision**: Permitir editar e remover combatentes preservando o atual quando ele continuar; remover o atual seleciona deterministicamente o próximo da ordem resultante.

**Rationale**: Atende à edição durante combate sem perder rodada e turno.

**Alternatives considered**: Restringir à preparação contraria a especificação; reiniciar combate perde estado.
