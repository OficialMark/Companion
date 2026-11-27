# Quickstart: validar a spec 017

Roteiro completo da instância de validação em [docs/testes-e2e.md](../../docs/testes-e2e.md).

1. Subir a API de validação em `Testing` e o frontend estático com `serve-validation.mjs` (porta 3616).
2. Gate de layout: `npx playwright test layout-audit --project=desktop` com `E2E_BASE_URL=http://127.0.0.1:3616`. Esperado: 1 passed; `summary.txt` sem nenhuma coluna diferente de 0 e largura ≥ 80% em 1920/2560.
3. E2E completo: `npx playwright test --grep-invert "layout audit"`. Esperado: tudo verde exceto `admin.spec.ts` (pulado sem `E2E_ADMIN_KEY`).
4. Vitest: `npx vitest run` (frontend).

Manual (1920 e 390 px): mapa no centro em 3 colunas; no celular, mapa primeiro, abas embaixo, código da mesa sem cobrir nada.
