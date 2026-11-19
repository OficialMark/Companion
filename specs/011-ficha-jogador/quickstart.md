# Quickstart: validar a feature 011 sem tocar no ambiente em uso

**Regra:** os containers `companion-frontend-1`, `companion-api-1` e `companion-sqlserver-1` estão servindo a mesa. Nada aqui usa `docker compose` do projeto. Os testes de integração sobem um SQL Server descartável próprio (Testcontainers); o app de validação roda em portas diferentes, apontando para um banco descartável.

## Pré-requisitos

- .NET SDK 10.0.303 (`global.json`), Node 20+, pnpm 11, Docker Desktop rodando (só para o container descartável dos testes).
- Worktree da feature com `pnpm install` em `frontend/`.

## 1. Testes rápidos (sem banco)

```powershell
dotnet test backend/tests/Companion.UnitTests
```

Esperado: passam `DiceExpressionTests` (legado inalterado), `DiceExpressionGroupingTests`, `CharacterRulesTests` (lê `backend/tests/Fixtures/character-rules.cases.json`), `PersonNameTests`, `CatalogSeedParsingTests`.

```powershell
pnpm --dir frontend test
```

Esperado: `roll-expression.spec.ts` (lê `backend/tests/Fixtures/dice-expressions.cases.json`, os mesmos casos do backend), `player-roll-panel.spec.ts`, `view-nav.spec.ts`, `saved-roll-icons.spec.ts` e todos os testes existentes.

```powershell
node --test scripts/import-catalogs/
```

Esperado: parse das magias (361, contagem por nível igual à da fonte) e dos itens a partir das fixtures salvas.

## 2. Integração (SQL Server descartável)

```powershell
dotnet test backend/tests/Companion.IntegrationTests
```

Esperado, além da suíte atual:

- `AccountJoinTests`: mesmo nome com outras maiúsculas abre a mesma conta; duas sessões ativas; mestre e jogador com o mesmo nome são contas diferentes; duplicados legados resolvidos pelo mais recente; corrida de dois joins simultâneos com nome novo gera uma conta só.
- `CharacterOwnershipTests`: A não lê nem altera nada de B (troca de ids de item, magia e rolagem → 404); mestre → 403; sem token → 401; token de outra campanha → 401.
- `CharacterSheetApiTests`: equipar armadura desequipa a anterior; base preservada; efeito duplicado suprimido; 409 com versão velha; PV e iniciativa refletidos em `ParticipantUpdated`.
- `DiceRollGroupingApiTests`: vantagem com randomizador controlado executa todos os termos duas vezes e escolhe o maior; `DiceRoll.Expression` com 256 caracteres grava.
- `MigrationBackfillTests`: banco com participantes legados (nomes repetidos, minúsculas) migra sem perda e todos os tokens antigos continuam válidos.

## 3. App lado a lado, em portas próprias

Banco descartável (nome próprio, porta 14333, nunca o volume `companion-sql-data`):

```powershell
docker run -d --name companion-011-sql -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD=Dev011!Pass -p 14333:1433 mcr.microsoft.com/mssql/server:2022-latest
```

API na porta 5311:

```powershell
$env:ConnectionStrings__Companion = 'Server=localhost,14333;Database=Companion011;User Id=sa;Password=Dev011!Pass;Encrypt=True;TrustServerCertificate=True'; $env:ASPNETCORE_URLS = 'http://localhost:5311'; dotnet run --project backend/src/Companion.Api
```

Frontend na porta 3611, com proxy para 5311 (`NUXT_API_TARGET`, ver `nuxt.config.ts`):

```powershell
$env:NUXT_API_TARGET = 'http://localhost:5311'; pnpm --dir frontend dev --port 3611
```

E2E contra essa instância, nunca contra a 3636:

```powershell
$env:E2E_BASE_URL = 'http://127.0.0.1:3611'; pnpm --dir frontend exec playwright test tests/e2e/player-sheet.spec.ts tests/e2e/compendium-sheet.spec.ts
```

Roteiro manual (1280 e 375 px):

1. Criar campanha como "mestre maria"; o topo mostra "Mestre Maria", "Mesa | Compêndio", sem crédito ao Pocket DM.
2. Em outro navegador, entrar com o código de jogador como "loren b. vonn neuman": nome exibido "Loren B. Vonn Neuman", nav "Mesa | Ficha".
3. Ficha: DES 14 → 16 e nível 4 → 5: modificador, iniciativa, CA sem armadura, perícias de DES e proficiência mudam; o popover de cada valor mostra a composição.
4. Mochila → Do catálogo: Cota de Malha (CA 16), Escudo, Espada Longa. Criar "Anel da avó" com `armorClass add 1`. Equipar tudo: CA 19. Equipar outra armadura: aviso, a cota sai.
5. Entrar com "LOREN B. VONN NEUMAN" num terceiro navegador: mesma ficha; o segundo navegador continua conectado.
6. Modo Jogo: escolher Espada Longa, ataque, vantagem, 2 repetições. Expressão `2#[2#(d20 + (FOR) 3 + (prof) 3) maior]`. Rolar: histórico mostra 2 resultados, cada um com duas execuções e a escolhida destacada.
7. Salvar como "Ataque com espada" (ícone `sword`), mudar FOR, usar de novo: expressão nova.
8. Como mestre: ficha de criatura, rolador livre, rolador rápido e compêndio funcionam como antes.

Limpeza (só os recursos desta validação):

```powershell
docker rm -f companion-011-sql
```

## 4. Importar os catálogos (fora do app)

```powershell
node scripts/import-catalogs/import-spells.mjs
node scripts/import-catalogs/import-items.mjs
```

Gera `backend/src/Companion.Infrastructure/Persistence/Seed/spells.magias5e.json` e `items.srd52.json`, mais `scripts/import-catalogs/last-run-report.md` com as contagens contra a fonte. Uma estrutura inesperada faz o script falhar sem escrever o JSON.

## 5. Implantação (só com o Marcos)

A migration e o backfill rodam no startup da API, como as anteriores. **Antes** de trocar o container da API, exportar a tabela `Participants` (é a única com linhas alteradas pelo backfill) para um arquivo fora do repositório. O comando de troca do container é do Marcos; nenhuma etapa desta feature o executa.
