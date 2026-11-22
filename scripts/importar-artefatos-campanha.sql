/*
  Importa NPCs, pistas e itens mágicos dos PDFs na campanha indicada.
  Idempotente: registros com o mesmo tipo/título ou nome não são duplicados.
  Execute no banco Companion depois de conferir @CampaignCode.
*/
USE [Companion];
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;

DECLARE @CampaignCode nchar(4) = N'4896'; -- troque pelo código da outra campanha
DECLARE @CampaignId uniqueidentifier;
DECLARE @MasterId uniqueidentifier;
DECLARE @Now datetimeoffset = TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00');

SELECT @CampaignId = CampaignId
FROM dbo.CampaignAccessCodes
WHERE Code = @CampaignCode;

IF @CampaignId IS NULL
    THROW 51000, 'Código de campanha não encontrado.', 1;

SELECT TOP (1) @MasterId = Id
FROM dbo.Participants
WHERE CampaignId = @CampaignId AND Role = N'Master'
ORDER BY Id;

IF @MasterId IS NULL
    THROW 51001, 'Participante mestre não encontrado para a campanha.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @Knowledge TABLE
    (
        Kind nvarchar(16) NOT NULL,
        Title nvarchar(120) NOT NULL,
        Summary nvarchar(500) NULL,
        Description nvarchar(max) NULL
    );

    INSERT @Knowledge (Kind, Title, Summary, Description) VALUES
    (N'Npc', N'Gorak Tallemore', NULL, NULL),
    (N'Npc', N'Kairon Sharpblade', NULL, NULL),
    (N'Npc', N'Edward Leonharth', NULL, NULL),
    (N'Npc', N'Dornli', N'Taverneiro da Rosa Dourada.', NULL),
    (N'Npc', N'Flitiuic', N'Gnomo da biblioteca.', NULL),
    (N'Npc', N'Dario Veratus', NULL, NULL),
    (N'Clue', N'Marcas — Ecos da Primeira Era', N'Quatro marcas e suas propriedades.', N'Marca da Comunhão: permite que a vontade atravesse para outra pessoa, perceber o mundo pelos sentidos dela e estender o próprio corpo. Não concede controle absoluto. A conexão voluntária é recíproca; se for forçada, torna-se instável, enfrenta resistência e pode se romper. O mestre dessa marca também carrega uma marca-mor. Marca da Corrupção: surge onde a natureza foi ferida, altera o ciclo natural dos seres vivos e se alimenta do que deveria permanecer adormecido. O corrompido pode não ser servo; primeiro se torna diferente e, depois, vazio. A corrupção só pode ser preenchida mais tarde. Marca do Caçador (marca-mor): revela rastros ocultos, presenças e ecos do passado, conduzindo ao alvo que o espírito deseja encontrar. Não concede força extra nem transformação bestial. Marca da Lua (possível marca-mor): desenhos antigos associam a marca à vida e à morte; seu portador é desconhecido.'),
    (N'Clue', N'O demônio corrompido do porão de B. Lorgen', N'Marca no corpo do demônio corrompido.', N'A marca foi encontrada no demônio corrompido no porão de B. Lorgen. Sua natureza ainda não foi identificada.'),
    (N'Clue', N'Marca encontrada no mago do covil do dragão', N'Marca no mago do covil do dragão.', N'A natureza da marca ainda não foi identificada.'),
    (N'Clue', N'Marca vista com Everdic e seus capangas', N'Apareceu primeiro com Everdic e depois em alguns capangas.', N'A natureza da marca ainda não foi identificada.'),
    (N'Clue', N'Marca encontrada em B. Lorgen', N'Marca no corpo de B. Lorgen.', N'A natureza da marca ainda não foi identificada.'),
    (N'Clue', N'Índice do livro de Lifthal', N'Índice do livro que Lifthal, a gnoma, estava traduzindo.', NULL),
    (N'Clue', N'Diário de B. Lorgen', N'Anotações do diário de B. Lorgen.', N'B. Lorgen deseja encontrar o restante do livro, remover a maldição e alcançar a paz. Sua sanidade está piorando, e ele acredita que não deveria permanecer neste mundo. “Se ao menos ela ainda estivesse comigo...”'),
    (N'Clue', N'Carta de Everdic a B. Lorgen', N'Carta de Everdic endereçada a B. Lorgen.', NULL);

    INSERT dbo.CampaignKnowledgeEntries
        (Id, CampaignId, Kind, Title, Summary, SharedDescription, IsPlayerVisible,
         CreatedByParticipantId, CreatedAt, UpdatedAt)
    SELECT NEWID(), @CampaignId, k.Kind, k.Title, k.Summary, k.Description, 1,
           @MasterId, @Now, @Now
    FROM @Knowledge k
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.CampaignKnowledgeEntries e
        WHERE e.CampaignId = @CampaignId AND e.Kind = k.Kind AND e.Title = k.Title
    );

    UPDATE existing
    SET Summary = source.Summary,
        SharedDescription = source.Description,
        IsPlayerVisible = 1,
        UpdatedAt = @Now
    FROM dbo.CampaignKnowledgeEntries existing
    INNER JOIN @Knowledge source ON source.Kind = existing.Kind AND source.Title = existing.Title
    WHERE existing.CampaignId = @CampaignId;

    DECLARE @Items TABLE
    (
        Name nvarchar(80) NOT NULL,
        Subcategory nvarchar(80) NULL,
        Rarity nvarchar(30) NULL,
        Cost nvarchar(40) NULL,
        RequiresAttunement bit NOT NULL,
        MagicBonus int NOT NULL,
        Category nvarchar(12) NOT NULL,
        Description nvarchar(max) NOT NULL
    );

    INSERT @Items VALUES
    (N'Flecha Assassina de Dragões',N'Munição — flecha',N'Muito rara',N'1.500 PO',0,0,N'Gear',N'Quando um dragão sofre dano desta flecha, faz teste de Constituição CD 17. Falha: sofre 6d10 de dano adicional; sucesso: metade. Após causar esse dano adicional, a flecha perde a magia.'),
    (N'Flecha Assassina de Mortos-Vivos',N'Munição — flecha',N'Muito rara',N'1.500 PO',0,0,N'Gear',N'Quando um morto-vivo sofre dano desta flecha, faz teste de Constituição CD 17. Falha: sofre 6d10 de dano adicional; sucesso: metade. Após causar esse dano adicional, a flecha perde a magia.'),
    (N'Flecha Assassina de Corruptores',N'Munição — flecha',N'Muito rara',N'1.500 PO',0,0,N'Gear',N'Quando um corruptor sofre dano desta flecha, faz teste de Constituição CD 17. Falha: sofre 6d10 de dano adicional; sucesso: metade. Após causar esse dano adicional, a flecha perde a magia.'),
    (N'Flecha Assassina de Constructos',N'Munição — flecha',N'Muito rara',N'1.500 PO',0,0,N'Gear',N'Quando um constructo sofre dano desta flecha, faz teste de Constituição CD 17. Falha: sofre 6d10 de dano adicional; sucesso: metade. Após causar esse dano adicional, a flecha perde a magia.'),
    (N'Flecha Assassina de Gigantes',N'Munição — flecha',N'Muito rara',N'1.500 PO',0,0,N'Gear',N'Quando um gigante sofre dano desta flecha, faz teste de Constituição CD 17. Falha: sofre 6d10 de dano adicional; sucesso: metade. Após causar esse dano adicional, a flecha perde a magia.'),
    (N'Flecha Assassina de Elementais',N'Munição — flecha',N'Muito rara',N'1.500 PO',0,0,N'Gear',N'Quando um elemental sofre dano desta flecha, faz teste de Constituição CD 17. Falha: sofre 6d10 de dano adicional; sucesso: metade. Após causar esse dano adicional, a flecha perde a magia.'),
    (N'Flecha Assassina de Aberrações',N'Munição — flecha',N'Muito rara',N'1.500 PO',0,0,N'Gear',N'Quando uma aberração sofre dano desta flecha, faz teste de Constituição CD 17. Falha: sofre 6d10 de dano adicional; sucesso: metade. Após causar esse dano adicional, a flecha perde a magia.'),
    (N'Flecha Assassina de Monstruosidades',N'Munição — flecha',N'Muito rara',N'1.500 PO',0,0,N'Gear',N'Quando uma monstruosidade sofre dano desta flecha, faz teste de Constituição CD 17. Falha: sofre 6d10 de dano adicional; sucesso: metade. Após causar esse dano adicional, a flecha perde a magia.'),
    (N'Arma de Alerta',N'Qualquer arma',N'Incomum',N'300 PO',1,0,N'Weapon',N'Concede vantagem na iniciativa. Você e aliados a até 9 m não podem ser surpreendidos enquanto você não estiver incapacitado. Acorda aliados adormecidos quando o combate começa.'),
    (N'Arma +1',N'Qualquer arma',N'Incomum',N'300 PO',0,1,N'Weapon',N'Bônus de +1 nas jogadas de ataque e dano.'),
    (N'Arma +2',N'Qualquer arma',N'Rara',N'2.500 PO',0,2,N'Weapon',N'Bônus de +2 nas jogadas de ataque e dano.'),
    (N'Arma +3',N'Qualquer arma',N'Muito rara',N'25.000 PO',0,3,N'Weapon',N'Bônus de +3 nas jogadas de ataque e dano.'),
    (N'Arma Viciosa',N'Qualquer arma',N'Rara',N'2.500 PO',1,0,N'Weapon',N'Em um acerto crítico, causa 12 de dano adicional do tipo da arma.'),
    (N'Azagaia de Relâmpago',N'Azagaia',N'Incomum',N'1.000 PO',1,0,N'Weapon',N'Uma vez por amanhecer, ao ser arremessada transforma-se em relâmpago: linha de 4d6 de dano elétrico e mais 4d6 no alvo atingido.'),
    (N'Defensora',N'Qualquer espada',N'Lendária',N'15.000 PO',1,3,N'Weapon',N'Bônus de +3 nas jogadas de ataque e dano. A cada turno, pode transferir parte desse bônus para a CA, até +3.'),
    (N'Arma Dançarina',N'Qualquer espada',N'Lendária',N'20.000 PO',1,0,N'Weapon',N'Flutua e ataca independentemente. Como ação bônus, pode movê-la até 9 m; retorna após quatro ataques.'),
    (N'Espada Retaliadora de Kaedor',N'Espada longa',N'Lendária',NULL,1,3,N'Weapon',N'Bônus de +3 nas jogadas de ataque e dano. Como reação, ataca com vantagem uma criatura ao alcance que tenha ferido você; ignora imunidade e resistência.'),
    (N'Espada da Vingança',N'Qualquer espada',N'Incomum',N'2.500 PO',1,1,N'Weapon',N'Bônus de +1. Amaldiçoada: impõe desvantagem ao usar outras armas e pode compelir o portador a atacar quem o feriu.'),
    (N'Espada Decepadora',N'Qualquer espada',N'Muito rara',N'25.000 PO',1,0,N'Weapon',N'Causa dano máximo a objetos. Em crítico, causa 14 de dano adicional; um segundo resultado 20 pode decepar um membro. Emite luz.'),
    (N'Martelo Esmagador',N'Martelo ou maça',N'Muito rara',N'25.000 PO',1,0,N'Weapon',N'Causa dano máximo a objetos. Em crítico, causa 14 de dano contundente adicional; um segundo resultado 20 pode esmagar um membro. Emite luz.'),
    (N'Arma Ladra de Vidas',N'Qualquer arma',N'Rara',N'2.500 PO',1,0,N'Weapon',N'Em crítico, causa 10 de dano necrótico adicional e concede 10 PV temporários, exceto contra constructos e mortos-vivos.'),
    (N'Espada do Sangramento',N'Qualquer espada ou adaga',N'Rara',N'2.500 PO',1,0,N'Weapon',N'Ferimentos impedem recuperação normal de PV. No início de cada turno, cada ferimento causa 1d4 de dano necrótico.'),
    (N'Espada Vorpal',N'Espada cortante',N'Lendária',N'50.000 PO',1,3,N'Weapon',N'Bônus de +3 e ignora resistência a dano cortante. Em um 20, pode decapitar; se o alvo não puder sobreviver sem a cabeça, morre.'),
    (N'Benção de Auriel',N'Qualquer arma',N'Rara',N'3.500 PO',1,2,N'Weapon',N'Lâmina de radiação pura. Bônus de +2 nas jogadas de ataque e dano, causa dano radiante e causa +2d8 contra mortos-vivos. Emite luz.'),
    (N'Língua Flamejante',N'Qualquer arma corpo a corpo',N'Rara',N'2.500 PO',1,0,N'Weapon',N'Uma palavra de comando envolve a arma em chamas e acrescenta 2d6 de dano de fogo. Emite luz intensa.'),
    (N'Maça da Destruição',N'Maça',N'Rara',N'5.000 PO',1,1,N'Weapon',N'Bônus de +1 nas jogadas de ataque e dano, +3 contra constructos. Em crítico causa +7 de dano contundente (+14 contra constructos). Pode destruir constructos com até 25 PV.'),
    (N'Encantamento de Rompimento',N'Maça, martelo ou malho',N'Rara',N'2.500 PO',1,0,N'Weapon',N'Contra corruptores e mortos-vivos, causa +2d6 de dano radiante. Pode destruir criaturas com até 25 PV em falha de resistência de Sabedoria CD 15; a falha também causa medo.'),
    (N'Benção de Serath',N'Maça, martelo ou malho',N'Rara',N'5.000 PO',1,0,N'Weapon',N'Possui 4 cargas. Gasta 1 para criar onda de terror de 9 m; alvos que falham ficam amedrontados e não podem se aproximar. Recupera 1d4 cargas ao amanhecer.'),
    (N'Matadora de Dragões',N'Qualquer arma',N'Rara',N'5.000 PO',1,1,N'Weapon',N'Bônus de +1 nas jogadas de ataque e dano e +4d6 de dano adicional contra dragões.'),
    (N'Matador de Gigantes',N'Qualquer machado ou espada',N'Rara',N'2.500 PO',1,1,N'Weapon',N'Bônus de +1 nas jogadas de ataque e dano e +2d6 contra gigantes. Pode derrubar o alvo se ele falhar em resistência de Força CD 15.'),
    (N'Vingadora Sagrada',N'Qualquer espada',N'Lendária',N'75.000 PO',1,3,N'Weapon',N'Requer sintonização por paladino ou clérigo. Bônus de +3 nas jogadas de ataque e dano; +2d10 radiante contra corruptores e mortos-vivos. Aura de 3 m concede vantagem contra magias e efeitos mágicos.');

    INSERT dbo.CatalogItems
        (Id, Slug, Name, NameEn, Category, Subcategory, Rarity, RequiresAttunement, Cost,
         Template, SearchKey, IsRemovedFromCatalog, RequiresBaseItem, SearchText, BaseId, CampaignId)
    SELECT NEWID(), CONCAT(N'campanha-', LOWER(REPLACE(CONVERT(nvarchar(36), NEWID()), N'-', N''))), i.Name,
           NULL, i.Category, i.Subcategory,
           i.Rarity,
           i.RequiresAttunement, i.Cost,
           (SELECT i.Name AS [name], i.Category AS [category], 1 AS [quantity],
                   CAST(NULL AS decimal(10,2)) AS [weight], i.Description AS [description],
                   CAST(NULL AS nvarchar(4000)) AS [notes], CAST(0 AS bit) AS [equipped],
                   CAST(0 AS bit) AS [proficient], i.MagicBonus AS [magicBonus],
                   CAST(NULL AS nvarchar(max)) AS [weapon], CAST(NULL AS nvarchar(max)) AS [armor],
                   CAST(NULL AS nvarchar(max)) AS [shield], JSON_QUERY(N'[]') AS [effects]
            FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES),
           LOWER(i.Name), 0,
           CASE WHEN i.Category = N'Weapon' THEN 1 ELSE 0 END,
           LOWER(CONCAT(i.Name, N' | ', i.Description)), NULL, @CampaignId
    FROM @Items i
    WHERE NOT EXISTS (SELECT 1 FROM dbo.CatalogItems x WHERE x.CampaignId = @CampaignId AND x.Name = i.Name AND x.IsRemovedFromCatalog = 0);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT Kind, COUNT(*) AS Total,
       SUM(CASE WHEN IsPlayerVisible = 1 THEN 1 ELSE 0 END) AS VisiveisAosJogadores
FROM dbo.CampaignKnowledgeEntries
WHERE CampaignId = @CampaignId
GROUP BY Kind
ORDER BY Kind;

SELECT COUNT(*) AS ItensNoCatalogo
FROM dbo.CatalogItems
WHERE CampaignId = @CampaignId AND IsRemovedFromCatalog = 0;
