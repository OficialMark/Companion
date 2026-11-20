# Spec 011: estado em 2026-11-19 e como continuar

Versão estável fechada em 2026-11-19. A feature está funcional, passou pela segunda revisão crítica e está pronta para implantação controlada.

## O que está pronto

- Conta por nome normalizado, várias sessões por conta, "Sair" por aparelho, backfill dos participantes antigos.
- Ficha com valores-base editáveis e derivados calculados só no servidor (`CharacterRules`), gravação automática e conflito de versão.
- Mochila com equipar (uma armadura e um escudo), bônus mágico, proficiência por item e efeitos configuráveis. Efeito igual do mesmo item (pelo nome) não acumula.
- Catálogos: 361 magias (sem crédito) e 459 itens do SRD 5.2.1 traduzidos (atribuição CC BY 4.0). "Arma +1" e similares pedem o item base.
- Gramática de dados: `N#[...]`, `2#(...) maior|menor`, rótulos `(FOR) 3`. A sintaxe antiga não mudou.
- Painel de rolagem do jogador no Modo Jogo, rolagens salvas e mochila no Modo Jogo.
- Sem crédito ao Pocket DM (FR-040).

## Testes rodados (números em `checklists/baseline.md`)

| Suíte | Antes | Agora |
|---|---|---|
| Backend unit | 132 | 374 |
| Backend integração (Testcontainers) | 56 | 96 |
| Frontend Vitest | 220 | 408 |
| Importadores (`node --test scripts/import-catalogs/`) | 0 | 56 passam, 3 pulados (precisam do cache completo) |
| E2E `player-sheet.spec.ts` (1280 e 375 px) | não existia | passa |

As 11 specs e2e antigas que falham (`mvp`, `battlemap`, `health-attack`, `initiative`, parte de `creature-catalog`) falham **igual na `main`**: rodei a `main` lado a lado, com API e frontend próprios, e deu o mesmo conjunto. Não são regressões desta feature, mas precisam de conserto (ver abaixo).

## O que falta

1. **Termos para o Marcos conferir** (sem fonte pública confiável):
   - Maestrias de arma em `scripts/import-catalogs/glossary.mjs` (Trespassar, Raspão, Talho, Empurrão, Enfraquecer, Lentidão, Derrubar, Afligir).
   - "Garrafa de Ferro" (Iron Flask).
   - Se algum termo mudar, corrija os lotes em `translations/batches/`, valide e rode `node scripts/import-catalogs/import-items.mjs`.
2. **Implantação (é do Marcos).**
   - Antes, exporte a tabela `Participants` (o backfill mexe só nela).
   - Depois, rode `docker compose up -d --build api frontend` na raiz do repositório.
   - As migrations `AddPlayerCharacterSheet` e `AddCharacterSheetTables` e o seed dos catálogos rodam no startup da API. A `AddPlayerCharacterSheet` copia cada token atual para `ParticipantSessions`, então ninguém perde o acesso.

## Como validar sem tocar na stack em uso

```powershell
docker run -d --name companion-011-sql -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD=Dev011!Pass -p 127.0.0.1:14333:1433 mcr.microsoft.com/mssql/server:2022-latest
```

API de validação (use 127.0.0.1, não localhost):

```powershell
$env:ConnectionStrings__Companion='Server=127.0.0.1,14333;Database=Companion011;User Id=sa;Password=Dev011!Pass;Encrypt=True;TrustServerCertificate=True'; $env:ASPNETCORE_URLS='http://localhost:5311'; dotnet run --project backend/src/Companion.Api --no-launch-profile
```

Frontend gerado, servido com proxy para a API. O `nuxt dev` reinicia a cada ECONNRESET do WebSocket e derruba o e2e, por isso o frontend é gerado e servido assim:

```powershell
pnpm --dir frontend generate; node frontend/tests/e2e/serve-validation.mjs 3611 http://127.0.0.1:5311
```

```powershell
$env:E2E_BASE_URL='http://127.0.0.1:3611'; pnpm --dir frontend exec playwright test tests/e2e/player-sheet.spec.ts
```

Limpeza:

```powershell
docker rm -f companion-011-sql
```

## Lições registradas

- **Nomes de componentes no Nuxt.** Componentes em `app/components/character/` viram `Character*`, e uma tag sem import não renderiza nada, sem erro. O teste `frontend/tests/component-imports.spec.ts` agora pega isso.
- **Testes unitários não bastam.** Eles montam com stubs. Só o e2e no build real achou o bug acima e as repetições que só atualizavam ao sair do campo.
