using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Companion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CampaignAccessAndInitiative : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CampaignAccessCodes",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nchar(4)", fixedLength: true, maxLength: 4, nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignAccessCodes", x => x.Code);
                    table.ForeignKey(
                        name: "FK_CampaignAccessCodes_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                IF (SELECT COUNT(*) FROM [Campaigns]) > 5000
                    THROW 51000, 'Não há códigos de quatro dígitos suficientes para migrar as mesas existentes.', 1;

                WITH [Ranked] AS (
                    SELECT [Id], ROW_NUMBER() OVER (ORDER BY [CreatedAt], [Id]) - 1 AS [Position]
                    FROM [Campaigns]
                )
                INSERT INTO [CampaignAccessCodes] ([Code], [CampaignId], [Role])
                SELECT RIGHT('0000' + CAST([Position] * 2 AS varchar(4)), 4), [Id], 'Player' FROM [Ranked];

                WITH [Ranked] AS (
                    SELECT [Id], ROW_NUMBER() OVER (ORDER BY [CreatedAt], [Id]) - 1 AS [Position]
                    FROM [Campaigns]
                )
                INSERT INTO [CampaignAccessCodes] ([Code], [CampaignId], [Role])
                SELECT RIGHT('0000' + CAST([Position] * 2 + 1 AS varchar(4)), 4), [Id], 'Master' FROM [Ranked];
                """);

            migrationBuilder.DropIndex(
                name: "IX_Campaigns_Code",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Campaigns");

            migrationBuilder.AddColumn<int>(
                name: "InitiativeModifier",
                table: "Participants",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ParticipantId",
                table: "Combatants",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Combatants_EncounterId_ParticipantId",
                table: "Combatants",
                columns: new[] { "EncounterId", "ParticipantId" },
                unique: true,
                filter: "[ParticipantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Combatants_ParticipantId",
                table: "Combatants",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAccessCodes_CampaignId_Role",
                table: "CampaignAccessCodes",
                columns: new[] { "CampaignId", "Role" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Combatants_Participants_ParticipantId",
                table: "Combatants",
                column: "ParticipantId",
                principalTable: "Participants",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Combatants_Participants_ParticipantId",
                table: "Combatants");

            migrationBuilder.DropTable(
                name: "CampaignAccessCodes");

            migrationBuilder.DropIndex(
                name: "IX_Combatants_EncounterId_ParticipantId",
                table: "Combatants");

            migrationBuilder.DropIndex(
                name: "IX_Combatants_ParticipantId",
                table: "Combatants");

            migrationBuilder.DropColumn(
                name: "InitiativeModifier",
                table: "Participants");

            migrationBuilder.DropColumn(
                name: "ParticipantId",
                table: "Combatants");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Campaigns",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_Code",
                table: "Campaigns",
                column: "Code",
                unique: true);
        }
    }
}
