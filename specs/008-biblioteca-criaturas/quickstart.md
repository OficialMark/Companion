# Quickstart: validar a spec 008

## Pré-requisitos

Stack local: `docker compose up -d --build` na raiz (o compêndio é semeado no primeiro startup). Mestre e jogador em sessões separadas (por exemplo `localhost:3636` e `127.0.0.1:3636`).

## Testes

```bash
node --test scripts/import-creatures/
```

```bash
cd backend && dotnet test
```

```bash
cd frontend && pnpm vitest run && pnpm nuxt typecheck
```

## Cenários manuais

1. **Ícones (US1)**: marque 3 condições num token, use "Diminuir zoom" até o mínimo e depois aumente até o máximo. Os glifos ficam centralizados nos círculos.
2. **Névoa ao publicar (US2)**: em "Substituir mapa" escolha uma imagem. A pré-visualização abre coberta; revele uma sala com o retângulo e publique. O jogador recebe já com a névoa (sem flash do mapa inteiro).
3. **Biblioteca (US3)**: aba Criaturas, busque "lobo", filtre ND ≤ 1, abra "Lobo", troque a imagem e a CA, salve. Recarregue: continua "Editada". Em outra mesa, o Lobo segue original. "Restaurar original" desfaz.
4. **Adicionar (US4)**: com combate em preparação, adicione 3 "Goblin". Aparecem "Goblin 1..3" na iniciativa e na reserva, com imagem e 7/7 de vida.
5. **Ficha e rolagem (US5)**: no menu do token "Goblin 2" toque em "Ficha". Toque na ação "Cimitarra" → "Acerto". Role. O histórico mostra "Goblin 2: 1d20+4". Feche e reabra: a configuração está pré-selecionada. Recarregue a página: continua.
6. **Responsividade**: 360, 768 e 1280 px: lista, filtros, modal e editor sem rolagem horizontal e com toques de 44 px.
