using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Companion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerCharacterSheet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NameKey",
                table: "Participants",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true,
                collation: "Latin1_General_100_BIN2");

            migrationBuilder.AlterColumn<string>(
                name: "Expression",
                table: "DiceRolls",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.CreateTable(
                name: "ParticipantSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<byte[]>(type: "varbinary(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParticipantSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParticipantSessions_Participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "Participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Participants_CampaignId_Role_NameKey",
                table: "Participants",
                columns: new[] { "CampaignId", "Role", "NameKey" },
                unique: true,
                filter: "[NameKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ParticipantSessions_ParticipantId",
                table: "ParticipantSessions",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_ParticipantSessions_TokenHash",
                table: "ParticipantSessions",
                column: "TokenHash",
                unique: true);

            // Spec 011 (FR-043, research R3): a autorização passa a ler ParticipantSessions. Cada
            // participante existente ganha uma sessão com o TokenHash que já tem, para nenhum navegador
            // conectado perder o acesso. Participants.TokenHash é único, então não há colisão no índice.
            migrationBuilder.Sql(
                """
                INSERT INTO [ParticipantSessions] ([Id], [ParticipantId], [CampaignId], [TokenHash], [CreatedAt], [LastSeenAt])
                SELECT NEWID(), [Id], [CampaignId], [TokenHash], [CreatedAt], [CreatedAt]
                FROM [Participants];
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParticipantSessions");

            migrationBuilder.DropIndex(
                name: "IX_Participants_CampaignId_Role_NameKey",
                table: "Participants");

            migrationBuilder.DropColumn(
                name: "NameKey",
                table: "Participants");

            // DiceRolls.Expression fica em nvarchar(256) de propósito (T076): encolher para 128 falha
            // com "String or binary data would be truncated" assim que existe uma rolagem com mais de
            // 128 caracteres, que o domínio aceita (DiceExpression.MaxLength = 256). A versão anterior
            // da API grava no máximo 128, então a coluna mais larga não a atrapalha.
        }
    }
}
