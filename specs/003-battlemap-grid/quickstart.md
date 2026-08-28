# Quickstart: Validar Battlemap com Grid Interativo

## Prerequisites

- Docker Desktop com Compose
- Node.js 22+ e pnpm/npm para testes locais do frontend
- .NET 10 SDK
- Dois navegadores ou perfis separados para mestre e jogador
- Uma imagem PNG, JPEG ou WebP menor que 20 MB

Consulte [data-model.md](./data-model.md) para regras de persistência e [contracts/openapi.yaml](./contracts/openapi.yaml) para os contratos REST/SignalR.

## 1. Start the application

Na raiz do repositório:

```powershell
Copy-Item .env.example .env
# Defina MSSQL_SA_PASSWORD e ADMIN_ACCESS_KEY no arquivo .env
docker compose up --build -d
docker compose ps
```

Resultado esperado: `sqlserver`, `api` e `frontend` ficam saudáveis e a aplicação abre em `http://localhost:3000`.

## 2. Run automated checks

```powershell
dotnet test .\backend\Companion.slnx
Set-Location .\frontend
pnpm install
pnpm run typecheck
pnpm run test
pnpm run test:e2e
```

Resultado esperado: testes unitários, integração, componentes e Playwright passam nos projetos `desktop` e `android`.

## 3. Validate master setup (US1)

1. Entre como mestre, crie/inicie a preparação de um encontro e abra a configuração do battlemap.
2. Selecione uma imagem válida, informe 20 colunas e 15 linhas e observe a prévia.
3. Altere para 10×10 e confirme que grid, recorte e imagem continuam alinhados.
4. Publique e atualize a página.

Resultado esperado:

- somente o mestre vê os controles de configuração;
- o snapshot volta com 10 colunas, 10 linhas e URL versionada da imagem;
- o upload não insere bytes no snapshot JSON;
- após recarregar, configuração e imagem são recuperadas;
- arquivo maior que 20 MB, assinatura inválida ou dimensão 0/101 é rejeitado com mensagem acionável.

## 4. Validate player movement (US2)

1. Entre na mesma campanha como dois jogadores.
2. Confirme que cada jogador vê o próprio pino na reserva.
3. No primeiro jogador, arraste o pino para a célula coluna 3, linha 4.
4. Mova o segundo pino usando o fluxo selecionar pino e selecionar célula.
5. Tente mover o pino do outro jogador.
6. Como mestre, mova qualquer um dos pinos.
7. Posicione ambos na mesma célula.

Resultado esperado:

- cada posição encaixa em uma célula e aparece nas demais sessões após confirmação;
- jogador não move pino alheio, mas mestre pode arbitrar qualquer pino;
- dois pinos na mesma célula permanecem identificáveis e selecionáveis;
- soltar fora do grid preserva a última posição confirmada.

## 5. Validate fixed viewport and large grids (US3)

1. Como mestre, substitua o mapa por grids 1×1, 20×15 e 100×100.
2. Em desktop e no projeto Android, registre a altura/largura ocupada pelo visualizador em cada grid.
3. No grid 100×100, use pan, zoom e “Ajustar mapa” para alcançar os quatro cantos.
4. Gire o dispositivo Android entre retrato e paisagem.
5. Use teclado para focar o board, selecionar o próprio pino e escolher uma célula.

Resultado esperado:

- a área reservada ao visualizador permanece estável dentro de cada breakpoint;
- não aparece rolagem horizontal na página;
- a escala inicial mira 40 px e não força células minúsculas para mostrar todo o grid;
- qualquer célula pode ser alcançada e a posição lógica não muda ao redimensionar;
- foco, rótulos e anúncio de posição permitem completar a jornada por teclado.

## 6. Validate replacement impact

1. Posicione pinos dentro e fora da futura área reduzida.
2. Como mestre, reduza as dimensões do grid.
3. Verifique o resumo de pinos afetados e confirme.

Resultado esperado: posições ainda válidas são preservadas; pinos fora dos novos limites voltam à reserva; uma aba com revisão antiga recebe conflito e recarrega o snapshot.

## 7. Validate disconnection and recovery

1. Com mestre e jogador conectados, interrompa a rede do jogador.
2. Tente mover seu pino.
3. Enquanto ele está offline, mova o mesmo pino como mestre.
4. Restabeleça a rede.

Resultado esperado:

- o cliente offline mantém o último snapshot seguro e não apresenta comando sem confirmação como concluído;
- após reconectar, reentra no grupo, busca o snapshot REST e converge para a posição confirmada pelo servidor;
- a imagem não é baixada novamente quando `imageRevision` permanece igual.

## 8. Inspect persistence and event behavior

Confirme pelos testes de integração e logs que:

- a imagem é persistida antes da resposta de publicação;
- posição inválida, papel incorreto e revisão obsoleta não alteram o banco;
- `battlemap.invalidated` é emitido somente após persistência;
- evento contém apenas encontro e revisão;
- entrada tardia projeta pino em reserva sem exigir recriação do battlemap.
