using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Companion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCharacterSheetTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CatalogItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    Subcategory = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Rarity = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    RequiresAttunement = table.Column<bool>(type: "bit", nullable: false),
                    Cost = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Template = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SearchKey = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    IsRemovedFromCatalog = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CatalogSpells",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    School = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Ritual = table.Column<bool>(type: "bit", nullable: false),
                    Concentration = table.Column<bool>(type: "bit", nullable: false),
                    Classes = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    HigherLevels = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Template = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SearchKey = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    IsRemovedFromCatalog = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogSpells", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CharacterSheets",
                columns: table => new
                {
                    ParticipantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Data = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterSheets", x => x.ParticipantId);
                    table.ForeignKey(
                        name: "FK_CharacterSheets_Participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "Participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CharacterSpells",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CatalogSpellId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Data = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterSpells", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterSpells_CharacterSheets_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "CharacterSheets",
                        principalColumn: "ParticipantId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InventoryItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CatalogItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Data = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryItems_CharacterSheets_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "CharacterSheets",
                        principalColumn: "ParticipantId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SavedRolls",
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
                    table.PrimaryKey("PK_SavedRolls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedRolls_CharacterSheets_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "CharacterSheets",
                        principalColumn: "ParticipantId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItems_IsRemovedFromCatalog_Category",
                table: "CatalogItems",
                columns: new[] { "IsRemovedFromCatalog", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItems_Slug",
                table: "CatalogItems",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatalogSpells_IsRemovedFromCatalog_Level",
                table: "CatalogSpells",
                columns: new[] { "IsRemovedFromCatalog", "Level" });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogSpells_Slug",
                table: "CatalogSpells",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CharacterSpells_ParticipantId_SortOrder",
                table: "CharacterSpells",
                columns: new[] { "ParticipantId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_ParticipantId_SortOrder",
                table: "InventoryItems",
                columns: new[] { "ParticipantId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_SavedRolls_ParticipantId_SortOrder",
                table: "SavedRolls",
                columns: new[] { "ParticipantId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CatalogItems");

            migrationBuilder.DropTable(
                name: "CatalogSpells");

            migrationBuilder.DropTable(
                name: "CharacterSpells");

            migrationBuilder.DropTable(
                name: "InventoryItems");

            migrationBuilder.DropTable(
                name: "SavedRolls");

            migrationBuilder.DropTable(
                name: "CharacterSheets");
        }
    }
}
