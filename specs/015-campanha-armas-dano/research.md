# Research: Ataques Tipados e Caderno da Campanha

## Decisões

### Arma com opções de dano e componentes de rolagem

- **Decisão**: manter `WeaponData.DamageType` como compatibilidade legada e acrescentar uma coleção normalizada de tipos disponíveis para cada arma. O catálogo e as cópias da mochila passam a guardar a lista. Cada componente efetivamente rolado escolhe exatamente um tipo; uma arma com opções permite escolher qual tipo se aplica ao ataque atual. Componentes adicionais salvos têm dados, rótulo e tipo próprios.
- **Motivo**: o modelo atual (`backend/src/Companion.Domain/Characters/CharacterData.cs`) contém somente `damageDice` e `damageType`, e armas do catálogo são importadas como itens-template. A mudança aditiva mantém dados existentes legíveis e cobre armas flexíveis e armas mágicas sem codificar exceções por nome.
- **Alternativas consideradas**: converter imediatamente `DamageType` em enum fechado (rejeitado: conteúdo customizado e dados legados); manter somente texto livre (rejeitado: typos e inconsistência); inferir tipo pelo nome da arma (rejeitado: não confiável e impede personalização).

### Catálogo de tipos

- **Decisão**: fornecer lista padrão de tipos comuns em português com chaves estáveis e rótulos exibidos traduzidos; aceitar um tipo personalizado limitado a 30 caracteres. Seed/importação deve associar os tipos aos itens SRD de arma existentes, com migração de segurança para armas sem tipo.
- **Motivo**: `WeaponData` já limita campos curtos e o catálogo tem origem seedada e cópias por campanha; chaves estáveis evitam depender de strings localizadas no armazenamento.
- **Alternativas consideradas**: tabela de tipos administrável por campanha (rejeitada para esta feature: aumenta estado e fluxo de manutenção sem pedido de customização de catálogo global); armazenamento de enum ordinal (rejeitado por fragilidade entre versões).

### Registros de campanha e isolamento de dados

- **Decisão**: usar um registro polimórfico por campanha com tipo fechado (NPC, mapa, pista ou diário); conteúdo colaborativo e contribuições autoradas são campos/filhos públicos. Notas do mestre ficam em entidade separada. Imagens de referência pertencem a registros de mapa e seus pontos de interesse guardam coordenadas normalizadas.
- **Motivo**: todos os quatro conteúdos compartilham autorização, busca, autoria, data e visibilidade; mapas táticos atuais pertencem a encontros e carregam estado de combate (tokens, grid e névoa), devendo permanecer separados.
- **Alternativas consideradas**: quatro tabelas isoladas (rejeitadas: duplicam operações comuns e dificultam busca unificada); reutilizar `Battlemap` (rejeitado: lifecycle e mutações táticas são diferentes de mapas de referência); campo JSON opaco para todas as propriedades (rejeitado: validação, busca e privacidade ficam frágeis).

### Privacidade e concorrência

- **Decisão**: consultar primeiro o papel da sessão e filtrar entidades ocultas no caso de uso antes de montar DTOs. O registro privado e a anotação privada não aparecem em qualquer DTO de jogador. Eventos SignalR carregam apenas um aviso genérico de invalidação da campanha; clientes autorizados consultam REST para obter projeções novas. Usar `rowversion` para alterações no registro e contribuições aditivas para preservar autoria.
- **Motivo**: a constitution exige autorização por comando, servidor como autoridade e snapshots recuperáveis. Payload de evento sem identificador evita revelar até mesmo a existência de registro oculto.
- **Alternativas consideradas**: enviar todos os registros e ocultar no frontend (rejeitado por vazamento); evento com ID do registro (rejeitado por exposição de identificadores); espelhar toda a lista em cada evento (rejeitado por payload e concorrência).

### Imagens

- **Decisão**: mapas de referência aceitam PNG, JPEG e WebP com limite de 20 MB, seguindo a validação e o tratamento de MIME/imagem de `Battlemap`; a imagem não participa do snapshot de listas, sendo buscada pelo detalhe do mapa.
- **Motivo**: reutiliza limites conhecidos, evita grandes listas e mantém mapa reabrível em qualquer encontro.
