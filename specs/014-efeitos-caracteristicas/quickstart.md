# Quickstart: validar efeitos de características

## Automatizado

```powershell
dotnet test backend/Companion.slnx --no-restore
pnpm --dir frontend test
pnpm --dir frontend typecheck
```

## Cenário principal

1. Abra a ficha de um paladino com uma arma corpo a corpo e outra à distância.
2. Em Características, crie `Dueling` e adicione `Bônus de dano`, alvo `corpo a corpo`, valor `2`.
3. Salve e abra as composições de dano.
4. Confirme `Dueling +2` na arma corpo a corpo e ausência do bônus na arma à distância.
5. Reabra a característica e confirme que o efeito persistiu.

## Regressão de Traços

1. No campo Traços, digite `Ataque ` e aguarde o indicador de salvamento concluir.
2. Confirme que o espaço permanece visível.
3. Digite `extra`; o resultado deve ser `Ataque extra`, sem reposicionamento do cursor.
4. Recarregue a página; a versão normalizada persistida deve continuar semanticamente igual.

