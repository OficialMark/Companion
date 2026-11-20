# Quickstart: validar a spec 014

```powershell
dotnet test backend/Companion.slnx
```

```powershell
cd frontend; pnpm vitest run; pnpm typecheck
```

Manual (jogador com Martelo de Guerra, Malho e Besta Leve na mochila):

1. Ficha > Características > Nova: "Estilo de Luta: Duelismo", efeito Bônus de dano +2, alvo "corpo a corpo de uma mão".
2. Mochila/rolagens: Martelo de Guerra 1d8 + mod + 2 (a composição mostra "Duelismo"); dano versátil 1d10 + mod sem o +2; Malho e Besta Leve sem mudança.
3. Remover a característica: os valores voltam.
