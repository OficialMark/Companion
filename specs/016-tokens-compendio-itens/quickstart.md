# Quickstart: validar a spec 016

## Automático

```powershell
dotnet test backend/tests/Companion.UnitTests --filter "FullyQualifiedName~BattlemapMarker"
dotnet test backend/tests/Companion.IntegrationTests --filter "FullyQualifiedName~BattlemapTokenSource"
cd frontend; npx vitest run tests/token-source-picker.spec.ts tests/battlemap-markers.spec.ts
```

Esperado: 10 unitários, 6 de integração e 15 de componente aprovados.

## Manual

1. `docker compose up --build -d`, abrir `http://localhost:3636`, criar mesa (mestre) e entrar como jogador em outra aba.
2. Mestre: criar encontro e publicar um mapa.
3. Jogador: no mapa, "Do compêndio" > Criaturas > buscar "ogro" > Adicionar. Token "Ogro" na reserva; ao posicionar, ocupa 2x2 com imagem.
4. Jogador: aba Itens > buscar "corda" > Adicionar. Token quadrado na reserva.
5. Mestre: vê os dois tokens; em preparação "Do compêndio" mostra só tokens; com o combate em andamento, o switch "Reforço no combate" leva ao catálogo de combate.
6. Mestre cria a criatura "Zumbaraque Secreto"; jogador busca "zumbaraque": nada aparece.
