# Quickstart: validar a spec 012

## Testes automáticos

1. Backend: `dotnet test backend`. Inclui `CatalogSearchTests`, com os casos de nome EN, descrição e ordem.
2. Frontend: `pnpm --dir frontend test`.
3. Validação da tabela PT→EN: `node scripts/import-catalogs/validate-spell-names.mjs`.
4. E2E: `pnpm --dir frontend exec playwright test mesa-012`, contra a instância de validação na porta 3611.

## Roteiro manual

1. **Mestre**: abra o Compêndio.
   - Na aba Magias, busque `fireball` e veja que aparece Bola de Fogo.
   - Busque `invisível` e veja que aparecem magias pela descrição.
   - Na aba Itens, busque `longsword`.
2. **Mestre** com encontro que tenha um Goblin: na visão completa, escolha "Goblin 1" no seletor e role Destreza. O histórico mostra o rótulo e o modificador. A tela desce até o resultado.
3. **Mestre**: a barra superior mostra o código mascarado. O olho alterna entre mostrar e ocultar.
4. **Jogador**:
   - Na visão completa, role pela ficha.
   - No rodapé, abra o resumo, aplique Dano 7 e depois Cura 5, e confira que a barra de vida da mesa acompanha.
   - No seletor de fonte, os títulos de grupo ficam legíveis.
