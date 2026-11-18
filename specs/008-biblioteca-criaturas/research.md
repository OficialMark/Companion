# Research: spec 008

## R1. Ícones de condição desalinhados em tamanho pequeno

- **Decision**: desenhar o glifo dentro de `<g :transform="translate(-s/2 -s/2) scale(s/24)">` com o `<Icon>` em caixa fixa 24x24 (atributos `width="24" height="24"`), sem `<svg>` intermediário dimensionado por CSS percentual ou `em`. A posição passa a ser só aritmética de transform, a mesma do círculo.
- **Rationale**: hoje o ícone é um `<svg>` aninhado com `x/y/width/height` fracionários, e dentro dele vai o `<svg class="icon" width="1em" style="font-size:1.1em">` do `@nuxt/icon`, esticado por `width:100%`. Viewports de `<svg>` aninhado são arredondados para pixel de dispositivo de forma independente do círculo. Quando o selo tem ~10 px, um arredondamento de 1 px já é 10% do diâmetro e o glifo aparece deslocado (o print do usuário mostra exatamente isso). Com transform não há novo viewport, então não há arredondamento separado. A verificação no navegador com zoom mínimo, padrão e máximo fica registrada na revisão (T0xx).
- **Alternatives**: `foreignObject` (problemas de evento e Safari); colar o path do ícone (duplica o catálogo de ícones).

## R2. Névoa no momento da publicação

- **Decision**: a pré-visualização local usa `URL.createObjectURL(file)` + o mesmo cálculo de grid; a névoa é editada com `useFogOfWar` sobre uma fonte local (sem servidor) e enviada como campo `fogMask` (base64) no multipart de `PUT .../battlemap`. O servidor valida o tamanho contra o novo grid e chama `SetFog` antes do `SaveChanges`, numa única transação, antes de notificar.
- **Rationale**: atende SC-001 por construção: não existe instante em que o mapa novo está gravado sem a névoa.
- **Alternatives**: publicar "oculto" e depois editar (dois estados, mais UI); PUT de névoa logo após o upload (janela de corrida em que jogadores recebem o mapa descoberto).

## R3. Compêndio global e edições por mesa

- **Decision**: uma tabela `Creatures` com `CampaignId` nulo para o compêndio. Uma versão da mesa tem `CampaignId` e, quando deriva do compêndio, `BaseCreatureId`. A listagem de uma mesa = criaturas da mesa ∪ compêndio sem versão da mesa (anti-join por `BaseCreatureId`). Restaurar = apagar a versão da mesa.
- **Rationale**: o app não tem contas que atravessem mesas; editar o global afetaria todas as mesas. Copy-on-write mantém o compêndio intacto e o seed reaplicável.
- **Alternatives**: copiar o compêndio inteiro por mesa (725 linhas por mesa, sem ganho); tabela de "patches" campo a campo (complexa).

## R4. Seed idempotente

- **Decision**: o JSON vai como EmbeddedResource da Infrastructure. No startup, depois do `Migrate`, `CompendiumSeeder` calcula o SHA-256 do recurso; se for igual ao de `SeedVersions('creatures.pocketdm')`, não faz nada. Se mudou, faz upsert por `Slug` (cria, atualiza ou marca como removida) em lotes, e grava o hash.
- **Rationale**: startup barato no caso comum; reimportar o compêndio atualiza sem duplicar; testes de integração rodam o seed uma vez por container.

## R5. Imagens do compêndio

- **Decision**: `Creature.ImageSourceUrl` (absoluta, pocketdm). `GET /api/campaigns/{id}/creatures/{creatureId}/image` serve a imagem de `CreatureImages`. Se não houver e existir `ImageSourceUrl`, o servidor busca (timeout de 5 s, tamanho máx. 1 MB, tipo validado por assinatura), grava e serve. Falha: grava `ImageFetchFailedAt` e não tenta de novo por 24 h; responde 404 e o cliente usa as iniciais. Uploads do mestre gravam direto em `CreatureImages` da criatura da mesa.
- **Acesso**: qualquer participante da mesa pode baixar a imagem de uma criatura do compêndio ou da própria mesa (os tokens precisam dela). A ficha e a lista continuam mestre-only.

## R6. Rolagem da criatura

- **Decision**: o cliente monta a expressão `"{count}d{die}{+abilityMod}{+bonus}"` (termos zero omitidos) e chama `POST /dice-rolls` com `Label` = nome do token ou da criatura. `DiceExpression` já aceita vários termos inteiros. `Label` é texto opcional (≤ 80) guardado em `DiceRoll` e exibido no histórico.
- **Preset**: `PUT .../battlemap/tokens/{tokenId}/roll-preset {count, die, ability, bonus}`, permitido a quem controla o token. É gravado em `BattlemapTokenAppearance` (já é por token e por mapa) e devolvido em `BattlemapTokenDto.RollPreset`.
- **Ação → rolagem**: parser no cliente procura `+N` de acerto ("+9 para acertar", "corpo a corpo +9") e o primeiro `NdM (+|-) K` de dano; gera dois atalhos "Acerto" e "Dano".

## R7. Adicionar ao combate

- **Decision**: `POST .../encounters/{id}/creatures {creatureId, count}`. Só em `Preparing` (o domínio já restringe `AddCombatant` a essa fase; mantido). Nome: `"{Nome} {n}"` com n = menor inteiro ≥ 1 ainda não usado por combatentes com o mesmo nome base; com count = 1 e nenhum outro do mesmo nome, usa só o nome. Modificador = `Initiative` da ficha ou o modificador de DES. Vida atual = máxima = PV. Tamanho: Large 2, Huge 3, Gargantuan 4, demais 1 (gravado na aparência quando há battlemap).
- **Rationale**: consistente com o fluxo de preparação existente (rolar iniciativa antes de começar).
