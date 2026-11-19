# Linha de base dos testes (spec 011)

Medida em 2026-11-19, no commit `16c66ac` (antes de qualquer código da feature), no worktree da sessão.

| Suíte | Comando | Resultado |
|---|---|---|
| Backend unit | `dotnet test backend/tests/Companion.UnitTests` | 132 passaram, 0 falharam |
| Backend integração (Testcontainers) | `dotnet test backend/tests/Companion.IntegrationTests` | 56 passaram, 0 falharam |
| Frontend Vitest | `pnpm --dir frontend test` (depois de `nuxt prepare`) | 220 passaram em 28 arquivos |

Observação: num worktree novo, o Vitest falha nos 28 arquivos até rodar `pnpm exec nuxt prepare` (falta `.nuxt/`). Não é regressão.

## Depois da feature (2026-11-19, versão fechada)

| Suíte | Resultado |
|---|---|
| Backend unit | 374 passaram |
| Backend integração | 96 passaram |
| Frontend Vitest | 408 passaram |
| Importadores de catálogo | 56 passaram, 3 pulados (cache completo ausente) |
| E2E player-sheet (desktop, 1280 e 375 px) | passou |
| E2E antigas | 11 falham igual na `main` (comparação lado a lado); não são regressões |

