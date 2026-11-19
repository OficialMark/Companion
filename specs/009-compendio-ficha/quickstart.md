# Quickstart: validar o Compêndio e a ficha

## Pré-requisitos

- Docker Desktop e Node com pnpm.
- `.env` na raiz (copiado do checkout principal).
- Dependências do frontend instaladas: `pnpm install` e `pnpm nuxt prepare` em `frontend/`.

## Subir a stack

```powershell
docker compose up -d --build
```

O app fica em `http://localhost:3636`. A migration e o compêndio são aplicados na inicialização da API.

## Testes automáticos

```powershell
cd frontend; pnpm vitest run
```

```powershell
cd frontend; pnpm nuxt typecheck
```

```powershell
cd frontend; $env:E2E_BASE_URL='http://localhost:3636'; npx playwright test tests/e2e/compendium-sheet.spec.ts
```

**Resultado esperado**:
- Unidade e typecheck sem falhas.
- O e2e passa em 1280, 768 e 375 px e no projeto Android: nenhuma sobreposição de texto, nenhum controle abaixo de 44 px, nenhuma rolagem horizontal.

Para salvar capturas da revisão, defina `$env:SHOTS` com uma pasta.

## Roteiro manual

1. **Navegação (US3)**:
   1. Crie uma mesa e entre como mestre.
   2. Na barra superior, clique em **Compêndio**. A tela abre do topo, sem o aviso do código, e o endereço termina em `?tela=compendio`.
   3. Recarregue a página: continua no Compêndio.
   4. Clique em **Mesa** para voltar.
2. **Minhas criaturas**: ligue o atalho. Aparece "Você ainda não criou nenhuma criatura nesta mesa".
3. **Nova criatura**: clique em **Criar a primeira**, dê um nome e salve. A criatura aparece na lista filtrada.
4. **Ficha (US1)**: busque "dragão vermelho adulto" e abra a ficha. Confira:
   - os 4 números e a linha de deslocamento;
   - os atributos;
   - os detalhes em linhas separadas;
   - as ações sem texto sobreposto.
5. **Editor (US2)**: clique em **Editar ficha**. Confira:
   - as seções com título dentro do cartão;
   - a grade sem buracos;
   - a barra Cancelar/Salvar sempre visível e sem cobrir campos.
6. **Celular**: repita os passos 1 a 5 em 375 px (ferramentas do navegador). Confira:
   - a segunda linha da barra com Mesa/Compêndio;
   - o rodapé da ficha em uma linha;
   - um campo por linha no editor, com exceção dos pares e trios numéricos.
7. **Jogador**: entre como jogador com o código da mesa, abrindo `/?tela=compendio`. Você deve cair na Mesa, sem a navegação do mestre.
