# Research: Vida e Ataques em Combate

## Preferência de vida e cópia por encontro

**Decision**: Persistir vida opcional no participante e copiá-la para o combatente quando o jogador for importado. A cópia do encontro passa a ser independente.

**Rationale**: Reutiliza o valor informado na tela inicial sem fazer alterações de um combate contaminarem encontros futuros.

**Alternatives considered**: Referência viva ao participante sobrescreveria edições do mestre; manter vida apenas no combatente exigiria redigitação em todo encontro.

## Vida obrigatória e limites

**Decision**: Aceitar inteiros entre 0 e 99.999. A preferência pode ficar vazia, mas todo combatente precisa de vida antes do início do combate e antes de ser alvo.

**Rationale**: Diferencia “não informado” de zero e fornece limite explícito para validação e testes.

**Alternatives considered**: Usar zero como padrão confundiria personagem derrotado com campo ausente; vida sem limite exporia persistência e interface a valores impraticáveis.

## Vínculo entre ataque e rolagem

**Decision**: O ataque referencia uma rolagem pública existente, pertencente ao autor e com exatamente um resultado. O total final, limitado ao mínimo zero, é aplicado igualmente a cada alvo.

**Rationale**: Reutiliza parser, aleatoriedade e animação existentes e impede que o cliente escolha o dano.

**Alternatives considered**: Rolar novamente no endpoint de ataque duplicaria a experiência; aceitar total enviado pelo cliente quebraria a autoridade do servidor; repetições criariam ambiguidade sobre qual total aplicar.

## Atomicidade e idempotência

**Decision**: Persistir `CombatAttack` com chave única `(AuthorParticipantId, ClientRequestId)` e todos os `CombatAttackTarget` na mesma confirmação que atualiza as vidas. Alvos são distintos e qualquer alvo inválido rejeita a ação inteira.

**Rationale**: Reenvios não duplicam dano e nenhum subconjunto de alvos fica alterado após erro.

**Alternatives considered**: Atualização alvo a alvo permitiria sucesso parcial; confiar apenas no botão desabilitado não cobre reconexão e retry HTTP.

## Concorrência de vida

**Decision**: Usar a versão do encontro como barreira de concorrência. Conflitos retornam resposta recuperável para sincronização, e uma nova tentativa reutiliza a chave idempotente.

**Rationale**: Preserva mudanças confirmadas sem introduzir Redis ou locks distribuídos no MVP de instância única.

**Alternatives considered**: Última escrita vence perderia dano/curas; lock apenas no navegador não protege clientes diferentes; infraestrutura distribuída viola simplicidade operacional.

## Privacidade em tempo real

**Decision**: Mapear snapshots por observador e publicar somente invalidação sem vida para o grupo SignalR nas mudanças sensíveis. Cada cliente recarrega o snapshot autenticado.

**Rationale**: Um payload único no grupo não consegue mostrar todas as vidas ao mestre e ocultá-las dos jogadores. O snapshot REST já é a fonte recuperável prevista na constituição.

**Alternatives considered**: Enviar a vida completa no grupo vazaria dados; manter grupos por participante aumentaria o acoplamento do notifier; ocultar apenas na interface ainda exporia o payload.

## Histórico do ataque

**Decision**: Armazenar autor, rolagem, dano, alvos e vidas anterior/posterior. Para jogadores, respostas e eventos omitem vidas alheias; o mestre pode consultar o estado atual pelo snapshot.

**Rationale**: O registro permite auditoria e testes de idempotência sem contrariar a visibilidade por papel.

**Alternatives considered**: Não registrar impediria explicar alterações; expor todo o histórico aos jogadores violaria privacidade.