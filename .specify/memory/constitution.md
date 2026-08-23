<!--
Sync Impact Report
- Version change: template (unratified) -> 1.0.0
- Modified principles:
  - Placeholder principles -> I. Monólito modular e Clean Architecture
  - Placeholder principles -> II. Servidor como autoridade do jogo
  - Placeholder principles -> III. Experiência web responsiva e resiliente
  - Placeholder principles -> IV. Qualidade verificável
  - Placeholder principles -> V. Simplicidade operacional e evolução segura
- Added sections:
  - Restrições de arquitetura e tecnologia
  - Fluxo de desenvolvimento e gates de qualidade
- Removed sections: none
- Templates reviewed:
  - ✅ .specify/templates/plan-template.md (Constitution Check is compatible)
  - ✅ .specify/templates/spec-template.md (mandatory scenarios and criteria are compatible)
  - ✅ .specify/templates/tasks-template.md (testing and cross-cutting phases are compatible)
- Follow-up TODOs: none
-->

# Companion Constitution

## Core Principles

### I. Monólito modular e Clean Architecture

O sistema MUST iniciar como um monólito modular. O backend MUST separar Domain,
Application, Infrastructure e Api, mantendo dependências apontadas para o núcleo:
Domain não depende das demais camadas; Application depende somente de Domain;
Infrastructure e Api implementam ou consomem contratos internos. Funcionalidades
MUST ser organizadas por domínio, como Campaigns, DiceRolls, Encounters, Characters
e Users. Microsserviços, filas e novos processos distribuídos somente podem ser
introduzidos mediante necessidade mensurável e registrada no plano da feature.

### II. Servidor como autoridade do jogo

Resultados de dados, iniciativa, ordem de turnos e estado de combate MUST ser
calculados, validados e persistidos pelo backend. O cliente nunca pode ser a fonte
de verdade desses valores. REST MUST fornecer comandos e snapshots recuperáveis;
SignalR MUST apenas propagar eventos e avisos de mudança para o grupo da campanha.
Uma reconexão MUST reconstruir o estado atual por API, sem depender do histórico de
eventos recebido pelo navegador. Eventos públicos somente podem ser emitidos após a
persistência da alteração correspondente ser confirmada.

### III. Experiência web responsiva e resiliente

Toda jornada MUST funcionar em navegadores modernos no Windows e Android, com
layout mobile-first, controles adequados a toque e uso por teclado. O frontend MUST
ser uma PWA Nuxt responsiva e MUST continuar apresentando o último estado seguro
durante falhas transitórias. Conexões SignalR MUST usar reconexão automática,
reentrada autorizada no grupo da campanha e nova consulta do snapshot. Perda de
conexão MUST ser indicada ao usuário e MUST impedir comandos que pareçam concluídos
sem confirmação do servidor.

### IV. Qualidade verificável

Regras de domínio e casos de uso MUST possuir testes unitários. Persistência no SQL
Server, contratos HTTP, autenticação e fluxos SignalR MUST possuir testes de
integração quando alterados. Jornadas críticas MUST ter testes ponta a ponta em
viewport de desktop e Android. Cada user story MUST declarar critérios de aceitação
independentes e ser validável sem depender de histórias posteriores. Correções MUST
incluir teste de regressão quando o defeito puder ser reproduzido automaticamente.

### V. Simplicidade operacional e evolução segura

O MVP MUST evitar Redis, brokers, Kubernetes e serviços adicionais enquanto uma
única instância da API atender aos requisitos medidos. Configurações e segredos MUST
ser externos ao código. Mudanças de banco MUST usar migrations versionadas do EF
Core e preservar compatibilidade ou documentar uma migração explícita. Logs MUST ser
estruturados e correlacionar requisições, conexões e eventos sem registrar tokens,
senhas ou conteúdo privado. Otimizações e abstrações MUST responder a um requisito
ou medição concreta, não a uma possibilidade hipotética.

## Restrições de arquitetura e tecnologia

- O backend MUST usar ASP.NET Core em .NET 10 LTS, C# e Entity Framework Core.
- O banco relacional MUST ser Microsoft SQL Server. PostgreSQL ou outro banco não
  pode ser introduzido sem emenda desta constituição.
- O frontend MUST usar Vue 3, Nuxt 4 e TypeScript, preferencialmente como SPA/PWA
  para as áreas autenticadas.
- Operações comuns MUST usar API REST; notificações em tempo real MUST usar ASP.NET
  Core SignalR e grupos isolados por campanha.
- O gerador de dados MUST executar no backend por meio de uma abstração testável e
  de uma fonte de aleatoriedade apropriada para impedir resultados escolhidos pelo
  cliente.
- A autorização MUST validar participação e papel do usuário na campanha para cada
  comando e inscrição em grupo SignalR.
- Datas e eventos persistidos MUST usar UTC; identificadores MUST ser opacos e não
  devem expor sequência interna sensível.
- O deploy inicial MUST admitir frontend estático em CDN, API em container único e
  SQL Server gerenciado ou em container apenas para desenvolvimento e testes.

## Fluxo de desenvolvimento e gates de qualidade

Cada feature MUST seguir o fluxo Spec Kit: especificação, esclarecimento quando
necessário, plano, tarefas, análise de consistência e implementação. Antes do plano
ser aprovado, o Constitution Check MUST confirmar:

1. aderência às quatro camadas e aos limites dos módulos afetados;
2. autoridade do servidor e estratégia de persistência antes de eventos SignalR;
3. comportamento em desktop, Android, desconexão e reconexão;
4. estratégia de testes unitários, integração e ponta a ponta proporcional ao risco;
5. uso de SQL Server e migrations EF Core para qualquer alteração persistente;
6. justificativa registrada para qualquer nova dependência operacional.

Uma user story somente está concluída quando seus critérios de aceitação passam,
os testes relevantes passam e não existem placeholders ou decisões técnicas sem
registro. O plano MUST listar violações desta constituição na tabela de complexidade;
uma violação sem justificativa bloqueia a implementação.

## Governance

Esta constituição prevalece sobre convenções locais, planos e tarefas conflitantes.
Emendas MUST registrar motivação, impacto nos artefatos existentes e plano de
migração quando houver incompatibilidade. A versão segue Semantic Versioning:
MAJOR para remoção ou redefinição incompatível de princípio, MINOR para novo
princípio ou expansão material e PATCH para esclarecimentos sem mudança normativa.

Toda revisão de plano e de código MUST verificar conformidade com os princípios.
Exceções temporárias MUST estar documentadas no plano da feature, possuir responsável
e critério objetivo de remoção. O uso das skills do Spec Kit e o Constitution Check
são os mecanismos oficiais de orientação e revisão do projeto.

**Version**: 1.0.0 | **Ratified**: 2026-08-23 | **Last Amended**: 2026-08-23
