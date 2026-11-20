# Quickstart: validar a spec 013

## Testes automáticos

```powershell
dotnet test backend/Companion.slnx
```

```powershell
cd frontend; pnpm vitest run; pnpm playwright test tests/compendium-edit.spec.ts tests/character-features.spec.ts
```

## Manual (app em http://localhost:3636)

1. Crie uma campanha como mestre. Compêndio > Itens > "Novo item": crie "Corpo Fênix" (Armadura, pesada, CA 18, +1). A lista mostra o selo "Personalizada".
2. Compêndio > Magias: abra "Bola de Fogo", "Editar", dano 10d6, salve. A lista mostra uma "Bola de Fogo" só, com selo "Editada".
3. Entre como jogador na mesma campanha. Ficha > Magias > catálogo: "Bola de Fogo" mostra 10d6. Adicione.
4. Volte ao mestre, exclua a versão editada: a original (8d6) volta. A magia na ficha do jogador continua 10d6.
5. Jogador > Ficha > Características: crie "Sentido Divino", 5 usos, descanso longo. Gaste 2 (3/5). "Descanso curto": 3/5. "Descanso longo": 5/5. Recarregue: 5/5.
