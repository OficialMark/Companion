# Testes ponta a ponta e auditoria de layout

Os testes Playwright criam mesas de verdade, então nunca rodam contra a stack que serve a mesa (porta 3636; `tests/e2e/guard.ts` recusa). Eles rodam contra uma **instância de validação** própria: API em SQLite numa porta, frontend estático em outra.

## 1. API de validação (PowerShell, terminal próprio)

```powershell
$env:Database__Provider = 'Sqlite'
$env:ConnectionStrings__Companion = "Data Source=$env:TEMP\companion-e2e.db;Foreign Keys=True"
$env:ASPNETCORE_URLS = 'http://127.0.0.1:5316'
$env:ASPNETCORE_ENVIRONMENT = 'Testing'
dotnet run --project backend/src/Companion.Api --no-launch-profile
```

`Testing` é obrigatório: é o único ambiente sem o limitador de taxa de entrada (`Program.cs`, 12 entradas por minuto por IP). Em `Development` os e2e que entram vários jogadores falham com "Muitas tentativas em pouco tempo".

## 2. Frontend estático com proxy (outro terminal)

```powershell
cd frontend
npx nuxt generate
node tests/e2e/serve-validation.mjs 3616 http://127.0.0.1:5316
```

O `serve-validation.mjs` repassa `/api`, `/hubs` e o WebSocket do SignalR, como o nginx de produção. O `nuxt dev` com `NUXT_API_TARGET` não repassa o WebSocket: a mesa fica "Sem conexão" e os comandos travam.

## 3. Suíte e2e (desktop + Android)

```powershell
cd frontend
$env:E2E_BASE_URL = 'http://127.0.0.1:3616'
npx playwright test --grep-invert "layout audit"
```

`admin.spec.ts` só roda com `E2E_ADMIN_KEY`; os demais pulos são por projeto (larguras fixas no desktop, gestos de mouse).

## 4. Auditoria de layout (gate)

```powershell
cd frontend
$env:E2E_BASE_URL = 'http://127.0.0.1:3616'
$env:LAYOUT_AUDIT_DIR = "$env:TEMP\layout-audit"
npx playwright test layout-audit --project=desktop
```

Percorre Mesa, Mesa com o compêndio aberto, Compêndio, Ficha e Campanha do mestre e do jogador em 7 larguras (360, 390, 768, 1024, 1280, 1920 e 2560 px) e falha se houver:

| Gate | O que mede |
|---|---|
| Rolagem horizontal | `scrollWidth` e a borda direita de todo elemento não recortado |
| Controle coberto | outro elemento por cima no centro e em 2 pontos internos (barras fixas sobre conteúdo rolável não contam) |
| Fora da tela | controle além da largura sem contêiner de rolagem |
| Texto cortado | `nowrap` sem reticências, aba fora da sua lista, nome da campanha com menos de 100 px |
| Alvo de toque | controle menor que 44×44 px abaixo de 768 px |
| Contraste | texto abaixo de 4.5:1 (3:1 se grande) |
| Largura | conteúdo usando menos de 80% da tela em 1920 e 2560 px |

Em `LAYOUT_AUDIT_DIR` ficam `summary.txt`, `report.json` e uma captura de página inteira e outra só da janela (`__view`) por tela. Com `LAYOUT_AUDIT_ONLY=1` o auditor só coleta, sem falhar (para tirar uma linha de base).
