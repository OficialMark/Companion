# Research: MVP de Campanha D&D

## Aplicação distribuída em uma única URL

**Decision**: Servir o build Nuxt por Nginx e encaminhar `/api` e `/hubs` para a API dentro da rede Docker.

**Rationale**: O navegador usa URLs relativas e, portanto, funciona com `localhost`, IP LAN, Hamachi ou Radmin sem recompilar nem configurar CORS por endereço. O mesmo proxy suporta upgrade WebSocket.

**Alternatives considered**:

- Publicar frontend e API em portas distintas: exige descobrir e configurar o endereço da API no navegador e aumenta problemas de CORS.
- Executar SSR em Node em produção: adiciona um processo sem valor para a área autenticada do MVP.

## Identidade sem cadastro completo

**Decision**: Emitir um token opaco aleatório por participante, devolver o valor apenas na criação/entrada e persistir somente seu hash SHA-256. O token segue no cabeçalho `X-Participant-Token` e na chamada de entrada do Hub.

**Rationale**: Fornece isolamento e autorização por campanha sem introduzir e-mail, senha ou provedor externo no MVP. O hash reduz o impacto de leitura indevida do banco.

**Alternatives considered**:

- JWT: adiciona emissão, assinatura e rotação sem benefício para sessões locais revogáveis.
- Apenas nome e código: qualquer pessoa poderia personificar o mestre.

## Consistência de comandos e eventos

**Decision**: Cada comando modifica e salva o estado antes de publicar um evento SignalR. Rolagens aceitam `Idempotency-Key`, persistida como `ClientRequestId` por participante.

**Rationale**: Clientes que reconectam sempre recuperam o snapshot durável; repetição de um POST por instabilidade não cria duas rolagens.

**Alternatives considered**:

- Publicar antes de salvar: pode mostrar estado que não existe após falha.
- Outbox no MVP: melhora garantias entre processo e transporte, porém acrescenta worker e tabelas para uma única instância local.

## Presença online

**Decision**: Manter conexões ativas em memória na única instância da API e transmitir `ParticipantsChanged` quando o primeiro socket conecta ou o último desconecta.

**Rationale**: Presença é efêmera e não deve ser recuperada como verdade durável. O escopo constitucional limita o MVP a uma instância.

**Alternatives considered**:

- Persistir heartbeat no SQL Server: maior carga e risco de presença obsoleta.
- Redis: desnecessário até existir mais de uma instância.

## Aleatoriedade

**Decision**: Usar fonte criptograficamente segura no backend por meio de uma interface `IDiceRandomizer`, com implementação determinística substituível nos testes.

**Rationale**: Impede escolha do resultado pelo cliente e permite testar parser, limites e totais com previsibilidade.

**Alternatives considered**:

- `Random.Shared`: aceitável para jogos casuais, mas menos robusto contra previsão.
- Rolagem no frontend: viola a autoridade do servidor.

## Persistência e concorrência

**Decision**: SQL Server 2022 com EF Core migrations; índices únicos em código de campanha, hash de token e idempotência; `rowversion` no encontro para detectar avanços concorrentes.

**Rationale**: Mantém as invariantes no armazenamento e transforma comandos simultâneos de turno em conflito recuperável.

**Alternatives considered**:

- `EnsureCreated`: não oferece evolução versionada do esquema.
- Bloqueio somente em memória: não protege reinícios e complica testes.

## Implantação LAN/VPN

**Decision**: Publicar `0.0.0.0:3000:80` apenas no frontend; API e SQL Server ficam na rede interna do Compose. Persistência usa volume nomeado. Senha do SQL Server vem de `.env` baseado em `.env.example`.

**Rationale**: Reduz superfície exposta e oferece uma única porta para firewall e dispositivos. Hamachi/Radmin funcionam desde que a interface VPN aceite tráfego de entrada para a mesma porta.

**Alternatives considered**:

- Expor SQL Server e API ao host: desnecessário para usuários da aplicação.
- HTTPS automático: certificados confiáveis para IPs privados exigem configuração adicional e ficam fora do MVP.
