# Quickstart: validar a ficha pelo botão direito e a espera do resultado

## Pré-requisitos

A stack rodando (`docker compose up -d --build`) e as dependências do frontend instaladas, como no [quickstart da 009](../009-compendio-ficha/quickstart.md).

## Testes automáticos

```powershell
cd frontend; pnpm vitest run tests/token-sheet-shortcut.spec.ts
```

```powershell
cd frontend; $env:E2E_BASE_URL='http://localhost:3636'; npx playwright test tests/e2e/token-sheet-shortcut.spec.ts
```

**Resultado esperado**:
- os 11 testes de unidade passam;
- o e2e passa no projeto `desktop` (o projeto `android` pula, porque o botão direito é gesto de mouse).

## Roteiro manual

1. Como mestre, abra a preparação, adicione um Goblin pelo catálogo, publique um mapa e posicione o Goblin.
2. A dica abaixo do mapa termina com "Botão direito numa criatura abre a ficha."
3. Clique com o botão direito no token do Goblin. A ficha abre com "Token · Goblin", sem menu do token e sem menu do navegador.
4. Com o foco num token (Tab), pressione Shift+F10. A ficha abre igual.
5. Na ficha, clique em **Rolar**. A área dos dados entra na vista, e o total fica na tela por cerca de 2 s. Depois aparece "Último resultado".
6. Role duas vezes seguidas: a segunda rolagem substitui a primeira na hora.
7. Clique com o botão esquerdo no token: o menu do token abre como antes.
