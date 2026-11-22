# Importar NPCs, pistas e itens mágicos em outra campanha

O script [importar-artefatos-campanha.sql](../scripts/importar-artefatos-campanha.sql) cadastra os seis NPCs, oito pistas e 31 itens mágicos. Ele pode ser executado novamente sem duplicar entradas com os mesmos títulos e nomes. Os NPCs e as pistas ficam visíveis aos jogadores.

## Passos

1. Abra o arquivo SQL e altere `@CampaignCode` para o código de quatro dígitos da campanha desejada. O valor inicial `4896` aponta para a campanha atual.
2. Execute o arquivo no SQL Server Management Studio, conectado ao banco `Companion`.
3. Confira no resultado os totais importados. NPCs e pistas são criados ou atualizados como visíveis aos jogadores.
4. Na campanha, abra a aba **Campanha**. Em cada NPC ou pista, selecione **Adicionar imagem** e escolha o arquivo indicado na tabela abaixo. As imagens estão em [assets/campanha-4896](assets/campanha-4896/).
5. Abra **Compêndio > Itens** para consultar as 31 armas e flechas.

## Imagens

Imagens podem ser adicionadas a todas as categorias da aba **Campanha**, incluindo **Registros do mestre**, uma aba privada disponível apenas para mestres. Abra o registro, use **Adicionar imagem** ou **Trocar imagem** e selecione PNG, JPEG ou WebP de até 20 MB. Em campanhas novas, faça o upload depois de executar o script, usando o mapa abaixo.

| Categoria | Registro | Arquivo |
|---|---|---|
| NPC | Gorak Tallemore | `npc-gorak.png` |
| NPC | Kairon Sharpblade | `npc-kairon.png` |
| NPC | Edward Leonharth | `npc-edward.png` |
| NPC | Dornli | `npc-dornli.png` |
| NPC | Flitiuic | `npc-flitiuic.png` |
| NPC | Dario Veratus | `npc-dario.png` |
| Pista | Marcas — Ecos da Primeira Era | `pista-marcas-ecos-primeira-era.png` |
| Pista | O demônio corrompido do porão de B. Lorgen | `pista-demonio.png` |
| Pista | Marca encontrada no mago do covil do dragão | `pista-mago.png` |
| Pista | Marca vista com Everdic e seus capangas | `pista-everdic.png` |
| Pista | Marca encontrada em B. Lorgen | `pista-b-lorgen.png` |
| Pista | Índice do livro de Lifthal | `pista-indice-lifthal.png` |
| Pista | Diário de B. Lorgen | `pista-diario-b-lorgen.jpg` |
| Pista | Carta de Everdic a B. Lorgen | `pista-carta-everdic.png` |

Abra a pasta de imagens pelo link acima; os nomes dos arquivos também aparecem no próprio Explorer. O registro **Marcas — Ecos da Primeira Era** usa uma página do PDF como ilustração. O limite do upload é 20 MB por imagem.

## Conteúdo importado

- NPCs: Gorak Tallemore, Kairon Sharpblade, Edward Leonharth, Dornli, Flitiuic e Dario Veratus. Dornli é taverneiro da Rosa Dourada; Flitiuic é gnomo da biblioteca.
- Pistas: quatro marcas e seus efeitos, marcas encontradas no demônio do porão, no mago do covil do dragão, com Everdic e seus capangas e em B. Lorgen, além do índice de Lifthal, o diário de B. Lorgen e a carta de Everdic.
- Catálogo: oito flechas assassinas e 23 armas da tabela da guilda, com raridade, preço, sintonização e efeitos descritos na tabela.

Para aplicar os mesmos artefatos à campanha original, mantenha `@CampaignCode = N'4896'`. A campanha está identificada no sistema como “Teste Camapanha”.
