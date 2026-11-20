using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Companion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CampaignCatalogAndFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CatalogSpells_Slug",
                table: "CatalogSpells");

            migrationBuilder.DropIndex(
                name: "IX_CatalogItems_Slug",
                table: "CatalogItems");

            migrationBuilder.AddColumn<Guid>(
                name: "BaseId",
                table: "CatalogSpells",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CampaignId",
                table: "CatalogSpells",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BaseId",
                table: "CatalogItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CampaignId",
                table: "CatalogItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CharacterFeatures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Data = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterFeatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterFeatures_CharacterSheets_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "CharacterSheets",
                        principalColumn: "ParticipantId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogSpells_BaseId",
                table: "CatalogSpells",
                column: "BaseId");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogSpells_CampaignId",
                table: "CatalogSpells",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogSpells_CampaignId_BaseId",
                table: "CatalogSpells",
                columns: new[] { "CampaignId", "BaseId" },
                unique: true,
                filter: "[CampaignId] IS NOT NULL AND [BaseId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogSpells_Slug",
                table: "CatalogSpells",
                column: "Slug",
                unique: true,
                filter: "[CampaignId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItems_BaseId",
                table: "CatalogItems",
                column: "BaseId");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItems_CampaignId",
                table: "CatalogItems",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItems_CampaignId_BaseId",
                table: "CatalogItems",
                columns: new[] { "CampaignId", "BaseId" },
                unique: true,
                filter: "[CampaignId] IS NOT NULL AND [BaseId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItems_Slug",
                table: "CatalogItems",
                column: "Slug",
                unique: true,
                filter: "[CampaignId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterFeatures_ParticipantId_SortOrder",
                table: "CharacterFeatures",
                columns: new[] { "ParticipantId", "SortOrder" });

            migrationBuilder.AddForeignKey(
                name: "FK_CatalogItems_Campaigns_CampaignId",
                table: "CatalogItems",
                column: "CampaignId",
                principalTable: "Campaigns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CatalogItems_CatalogItems_BaseId",
                table: "CatalogItems",
                column: "BaseId",
                principalTable: "CatalogItems",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CatalogSpells_Campaigns_CampaignId",
                table: "CatalogSpells",
                column: "CampaignId",
                principalTable: "Campaigns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CatalogSpells_CatalogSpells_BaseId",
                table: "CatalogSpells",
                column: "BaseId",
                principalTable: "CatalogSpells",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Without CampaignId a campaign's entries would turn into global catalog rows seen by every table
            // (and their generated slugs would join the global unique index): drop them first.
            migrationBuilder.Sql("DELETE FROM [CatalogItems] WHERE [CampaignId] IS NOT NULL;");
            migrationBuilder.Sql("DELETE FROM [CatalogSpells] WHERE [CampaignId] IS NOT NULL;");

            migrationBuilder.DropForeignKey(
                name: "FK_CatalogItems_Campaigns_CampaignId",
                table: "CatalogItems");

            migrationBuilder.DropForeignKey(
                name: "FK_CatalogItems_CatalogItems_BaseId",
                table: "CatalogItems");

            migrationBuilder.DropForeignKey(
                name: "FK_CatalogSpells_Campaigns_CampaignId",
                table: "CatalogSpells");

            migrationBuilder.DropForeignKey(
                name: "FK_CatalogSpells_CatalogSpells_BaseId",
                table: "CatalogSpells");

            migrationBuilder.DropTable(
                name: "CharacterFeatures");

            migrationBuilder.DropIndex(
                name: "IX_CatalogSpells_BaseId",
                table: "CatalogSpells");

            migrationBuilder.DropIndex(
                name: "IX_CatalogSpells_CampaignId",
                table: "CatalogSpells");

            migrationBuilder.DropIndex(
                name: "IX_CatalogSpells_CampaignId_BaseId",
                table: "CatalogSpells");

            migrationBuilder.DropIndex(
                name: "IX_CatalogSpells_Slug",
                table: "CatalogSpells");

            migrationBuilder.DropIndex(
                name: "IX_CatalogItems_BaseId",
                table: "CatalogItems");

            migrationBuilder.DropIndex(
                name: "IX_CatalogItems_CampaignId",
                table: "CatalogItems");

            migrationBuilder.DropIndex(
                name: "IX_CatalogItems_CampaignId_BaseId",
                table: "CatalogItems");

            migrationBuilder.DropIndex(
                name: "IX_CatalogItems_Slug",
                table: "CatalogItems");

            migrationBuilder.DropColumn(
                name: "BaseId",
                table: "CatalogSpells");

            migrationBuilder.DropColumn(
                name: "CampaignId",
                table: "CatalogSpells");

            migrationBuilder.DropColumn(
                name: "BaseId",
                table: "CatalogItems");

            migrationBuilder.DropColumn(
                name: "CampaignId",
                table: "CatalogItems");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogSpells_Slug",
                table: "CatalogSpells",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItems_Slug",
                table: "CatalogItems",
                column: "Slug",
                unique: true);
        }
    }
}
