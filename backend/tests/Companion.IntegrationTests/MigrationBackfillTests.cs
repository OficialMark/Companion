using Companion.Application.Campaigns;
using Companion.Application.Contracts;
using Companion.Domain.Models;
using Companion.Infrastructure.Persistence;
using Companion.Infrastructure.Security;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Companion.IntegrationTests;

/// <summary>
/// Spec 011, T021: um banco no esquema anterior à AddPlayerCharacterSheet, com participantes antigos
/// (nomes repetidos, em minúsculas, mestre e jogador com o mesmo nome), migra e passa pelo backfill
/// sem perder linhas nem derrubar tokens. Usa um banco próprio no servidor do Testcontainers, porque
/// o banco da fixture já está na última migration.
/// </summary>
[Collection(CompanionCollection.Name)]
public sealed class MigrationBackfillTests(CompanionApiFactory factory)
{
    private const string MigrationBefore = "20261118125106_AddCreatureLibrary";

    private sealed record Legacy(Guid Id, Guid CampaignId, string Name, ParticipantRole Role, DateTimeOffset CreatedAt, ParticipantCredential Credential);

    /// <summary>
    /// T076: o Down das migrations da spec 011 não pode encolher DiceRolls.Expression para 128. Com uma
    /// rolagem de 200 caracteres (que o domínio aceita) o ALTER falhava com "would be truncated".
    /// </summary>
    [Fact]
    public async Task Rolling_back_keeps_dice_expressions_longer_than_128_characters()
    {
        var connection = new SqlConnectionStringBuilder(factory.ConnectionString)
        {
            InitialCatalog = $"Rollback_{Guid.NewGuid():N}"
        }.ConnectionString;
        var options = new DbContextOptionsBuilder<CompanionDbContext>().UseSqlServer(connection).Options;
        var now = DateTimeOffset.UtcNow;
        var campaign = new Campaign(Guid.NewGuid(), "Mesa", now);
        var participant = new Participant(Guid.NewGuid(), campaign.Id, "Loren", ParticipantRole.Player, new ParticipantTokenService().Create().Hash, now);
        var expression = "d20 + " + string.Join(" + ", Enumerable.Repeat("(prof) 1", 24)); // 6 + 24 * 8 + 23 * 3 = 267 > 200
        expression = expression[..200];
        try
        {
            await using (var db = new CompanionDbContext(options))
            {
                await db.Database.MigrateAsync();
                db.Add(campaign);
                db.Add(participant);
                await db.SaveChangesAsync();
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO [DiceRolls] ([Id], [CampaignId], [ParticipantId], [ClientRequestId], [CreatedAt], [Expression], [Label], [Modifier], [RevealedAt], [Total], [ValuesJson])
                    VALUES ({Guid.NewGuid()}, {campaign.Id}, {participant.Id}, {Guid.NewGuid()}, {now}, {expression}, {null as string}, 0, {now}, 10, {"[]"});
                    """);
            }

            await using (var db = new CompanionDbContext(options))
            {
                await db.GetService<IMigrator>().MigrateAsync(MigrationBefore);
                var stored = await db.Database.SqlQuery<string>($"SELECT [Expression] AS [Value] FROM [DiceRolls]").SingleAsync();
                Assert.Equal(expression, stored);
            }

            // And forward again: the Up's widen is a no-op on a 256 column.
            await using (var db = new CompanionDbContext(options))
            {
                await db.Database.MigrateAsync();
                Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            }
        }
        finally
        {
            await using var db = new CompanionDbContext(options);
            await db.Database.EnsureDeletedAsync();
        }
    }

    /// <summary>
    /// 2026-11-20: sheets move from "one per participant" to "one per player name". The same name with a
    /// sheet in two campaigns keeps the newest as the account's sheet; the older one stays in the table
    /// (nothing is deleted) under a LEGACY key. Sheets of other names keep their own key.
    /// </summary>
    [Fact]
    public async Task Sheet_owner_backfill_keeps_the_newest_sheet_per_name_and_deletes_nothing()
    {
        var connection = new SqlConnectionStringBuilder(factory.ConnectionString) { InitialCatalog = $"Owner_{Guid.NewGuid():N}" }.ConnectionString;
        var options = new DbContextOptionsBuilder<CompanionDbContext>().UseSqlServer(connection).Options;
        var older = DateTimeOffset.UtcNow.AddDays(-2);
        var newer = DateTimeOffset.UtcNow.AddDays(-1);
        Guid campaignA = Guid.NewGuid(), campaignB = Guid.NewGuid(), lorenA = Guid.NewGuid(), lorenB = Guid.NewGuid(), mira = Guid.NewGuid();
        try
        {
            await using (var db = new CompanionDbContext(options))
            {
                await db.GetService<IMigrator>().MigrateAsync("20261119185009_AddCharacterSheetTables");
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO [Campaigns] ([Id], [Name], [CreatedAt]) VALUES ({campaignA}, {"A"}, {older}), ({campaignB}, {"B"}, {older});
                    INSERT INTO [Participants] ([Id], [CampaignId], [DisplayName], [NameKey], [Role], [TokenHash], [CreatedAt]) VALUES
                        ({lorenA}, {campaignA}, {"Loren"}, {"LOREN"}, {"Player"}, {Guid.NewGuid().ToByteArray().Concat(Guid.NewGuid().ToByteArray()).ToArray()}, {older}),
                        ({lorenB}, {campaignB}, {"Loren"}, {"LOREN"}, {"Player"}, {Guid.NewGuid().ToByteArray().Concat(Guid.NewGuid().ToByteArray()).ToArray()}, {older}),
                        ({mira}, {campaignA}, {"Mira"}, {null as string}, {"Player"}, {Guid.NewGuid().ToByteArray().Concat(Guid.NewGuid().ToByteArray()).ToArray()}, {older});
                    INSERT INTO [CharacterSheets] ([ParticipantId], [Data], [CreatedAt], [UpdatedAt]) VALUES
                        ({lorenA}, {"{}"}, {older}, {older}), ({lorenB}, {"{}"}, {older}, {newer}), ({mira}, {"{}"}, {older}, {older});
                    """);
            }

            await using (var db = new CompanionDbContext(options))
            {
                await db.Database.MigrateAsync();
                var keys = await db.Database.SqlQuery<string>($"SELECT CONCAT(CONVERT(nvarchar(36), [ParticipantId]), '=', [OwnerKey]) AS [Value] FROM [CharacterSheets]").ToListAsync();
                Assert.Equal(3, keys.Count);
                Assert.Contains($"{lorenB}=LOREN".ToUpperInvariant(), keys.Select(x => x.ToUpperInvariant()));
                Assert.Contains($"{lorenA}=LEGACY:{lorenA}".ToUpperInvariant(), keys.Select(x => x.ToUpperInvariant()));
                Assert.Contains($"{mira}=MIRA".ToUpperInvariant(), keys.Select(x => x.ToUpperInvariant()));
            }
        }
        finally
        {
            await using var db = new CompanionDbContext(options);
            await db.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task Legacy_participants_survive_the_migration_and_the_backfill()
    {
        var connection = new SqlConnectionStringBuilder(factory.ConnectionString)
        {
            InitialCatalog = $"Backfill_{Guid.NewGuid():N}"
        }.ConnectionString;
        var options = new DbContextOptionsBuilder<CompanionDbContext>().UseSqlServer(connection).Options;
        var tokens = new ParticipantTokenService();
        var campaignA = Guid.NewGuid();
        var campaignB = Guid.NewGuid();
        var day = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
        Legacy[] legacy =
        [
            new(Guid.NewGuid(), campaignA, "loren", ParticipantRole.Player, day, tokens.Create()),
            new(Guid.NewGuid(), campaignA, "LOREN", ParticipantRole.Player, day.AddHours(1), tokens.Create()),
            new(Guid.NewGuid(), campaignA, "Loren  ", ParticipantRole.Player, day.AddHours(2), tokens.Create()),
            new(Guid.NewGuid(), campaignA, "loren", ParticipantRole.Master, day.AddMinutes(-5), tokens.Create()),
            new(Guid.NewGuid(), campaignA, "jean-luc   picard", ParticipantRole.Player, day.AddHours(3), tokens.Create()),
            new(Guid.NewGuid(), campaignA, "Joao", ParticipantRole.Player, day.AddHours(4), tokens.Create()),
            new(Guid.NewGuid(), campaignA, "João", ParticipantRole.Player, day.AddHours(5), tokens.Create()),
            new(Guid.NewGuid(), campaignB, "loren", ParticipantRole.Player, day, tokens.Create()),
            new(Guid.NewGuid(), campaignB, "Mestre", ParticipantRole.Master, day, tokens.Create()),
        ];

        await using (var db = new CompanionDbContext(options))
        {
            await db.GetService<IMigrator>().MigrateAsync(MigrationBefore);
            await SeedLegacy(db, legacy, campaignA, campaignB, day);
        }

        try
        {
            await using (var db = new CompanionDbContext(options))
            {
                await db.Database.MigrateAsync();
                Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            }

            ParticipantNameBackfillResult first;
            await using (var db = new CompanionDbContext(options))
                first = await new ParticipantNameBackfill(db).RunAsync();
            Assert.True(first.Changed);
            Assert.Empty(first.Invalid);
            // Chaves: jogador Loren (A), mestre Loren (A), Jean-Luc Picard, Joao, João, Loren (B), Mestre (B).
            Assert.Equal(7, first.KeysAssigned);

            await using (var db = new CompanionDbContext(options))
            {
                var rows = await db.Participants.AsNoTracking().ToDictionaryAsync(x => x.Id);
                Assert.Equal(legacy.Length, rows.Count);
                Assert.Equal(legacy.Length, await db.ParticipantSessions.CountAsync());

                Assert.Equal("Loren", rows[legacy[0].Id].DisplayName);
                Assert.Equal("Loren", rows[legacy[1].Id].DisplayName);
                Assert.Equal("Loren", rows[legacy[2].Id].DisplayName);
                Assert.Null(rows[legacy[0].Id].NameKey);
                Assert.Null(rows[legacy[1].Id].NameKey);
                Assert.Equal("LOREN", rows[legacy[2].Id].NameKey); // o mais recente do grupo
                Assert.Equal("LOREN", rows[legacy[3].Id].NameKey); // mestre: grupo separado
                Assert.Equal("Jean-Luc Picard", rows[legacy[4].Id].DisplayName);
                Assert.Equal("JEAN-LUC PICARD", rows[legacy[4].Id].NameKey);
                Assert.Equal("JOAO", rows[legacy[5].Id].NameKey);
                Assert.Equal("JOÃO", rows[legacy[6].Id].NameKey); // acento diferencia contas
                Assert.Equal("LOREN", rows[legacy[7].Id].NameKey); // outra campanha
                Assert.Equal("Mestre", rows[legacy[8].Id].DisplayName);
                Assert.All(legacy, x => Assert.Equal(x.CreatedAt, rows[x.Id].CreatedAt));
                Assert.All(legacy, x => Assert.Equal(x.Credential.Hash, rows[x.Id].TokenHash));

                var authorization = new ParticipantAuthorization(db, tokens, TimeProvider.System);
                foreach (var participant in legacy)
                {
                    var authorized = authorization.Require(participant.CampaignId, participant.Credential.Token);
                    Assert.Equal(participant.Id, authorized.Id);
                }
                var cross = Assert.Throws<AppException>(() => authorization.Require(campaignB, legacy[0].Credential.Token));
                Assert.Equal(AppErrorKind.Unauthorized, cross.Kind);
            }

            // Segunda rodada: nada muda.
            var before = await Snapshot(new CompanionDbContext(options));
            await using (var db = new CompanionDbContext(options))
            {
                var second = await new ParticipantNameBackfill(db).RunAsync();
                Assert.False(second.Changed);
                Assert.Equal(0, second.KeysAssigned);
                Assert.Equal(0, second.Renamed);
                Assert.Equal(before, await Snapshot(new CompanionDbContext(options)));
            }
        }
        finally
        {
            await using var db = new CompanionDbContext(options);
            await db.Database.EnsureDeletedAsync();
        }
    }

    private static async Task<string> Snapshot(CompanionDbContext db)
    {
        await using (db)
        {
            var rows = await db.Participants.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
            return string.Join('\n', rows.Select(x => $"{x.Id}|{x.DisplayName}|{x.NameKey}|{x.Role}|{x.CreatedAt:O}"));
        }
    }

    private static async Task SeedLegacy(
        CompanionDbContext db, IEnumerable<Legacy> legacy, Guid campaignA, Guid campaignB, DateTimeOffset createdAt)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO [Campaigns] ([Id], [Name], [CreatedAt]) VALUES ({campaignA}, {"Mesa A"}, {createdAt}), ({campaignB}, {"Mesa B"}, {createdAt});
            INSERT INTO [CampaignAccessCodes] ([Code], [CampaignId], [Role]) VALUES
                ('9101', {campaignA}, 'Player'), ('9102', {campaignA}, 'Master'),
                ('9103', {campaignB}, 'Player'), ('9104', {campaignB}, 'Master');
            """);
        foreach (var participant in legacy)
        {
            var role = participant.Role.ToString();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO [Participants] ([Id], [CampaignId], [DisplayName], [Role], [TokenHash], [CreatedAt])
                VALUES ({participant.Id}, {participant.CampaignId}, {participant.Name}, {role}, {participant.Credential.Hash}, {participant.CreatedAt});
                """);
        }
    }
}
