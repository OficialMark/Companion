using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Companion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatureLibrary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Label",
                table: "DiceRolls",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatureId",
                table: "Combatants",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastRollAbility",
                table: "BattlemapTokenAppearances",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastRollBonus",
                table: "BattlemapTokenAppearances",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastRollCount",
                table: "BattlemapTokenAppearances",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastRollDie",
                table: "BattlemapTokenAppearances",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Creatures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BaseCreatureId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Slug = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    SearchKey = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    SourceBook = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SourceUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Size = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Alignment = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ArmorClass = table.Column<int>(type: "int", nullable: false),
                    ArmorClassNote = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    HitPoints = table.Column<int>(type: "int", nullable: false),
                    HitDice = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Speed = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Initiative = table.Column<int>(type: "int", nullable: true),
                    Strength = table.Column<int>(type: "int", nullable: false),
                    Dexterity = table.Column<int>(type: "int", nullable: false),
                    Constitution = table.Column<int>(type: "int", nullable: false),
                    Intelligence = table.Column<int>(type: "int", nullable: false),
                    Wisdom = table.Column<int>(type: "int", nullable: false),
                    Charisma = table.Column<int>(type: "int", nullable: false),
                    SavingThrows = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Skills = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DamageVulnerabilities = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DamageResistances = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DamageImmunities = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ConditionImmunities = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Senses = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Languages = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ChallengeRating = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ChallengeValue = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: false),
                    ExperiencePoints = table.Column<int>(type: "int", nullable: true),
                    ProficiencyBonus = table.Column<int>(type: "int", nullable: true),
                    Traits = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Actions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BonusActions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reactions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LegendaryActions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LegendaryActionsIntro = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ImageSourceUrl = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ImageRevision = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsRemovedFromCompendium = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Creatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Creatures_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Creatures_Creatures_BaseCreatureId",
                        column: x => x.BaseCreatureId,
                        principalTable: "Creatures",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SeedVersions",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AppliedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeedVersions", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "CreatureImages",
                columns: table => new
                {
                    CreatureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    MediaType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Revision = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromSource = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatureImages", x => x.CreatureId);
                    table.ForeignKey(
                        name: "FK_CreatureImages_Creatures_CreatureId",
                        column: x => x.CreatureId,
                        principalTable: "Creatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Combatants_CreatureId",
                table: "Combatants",
                column: "CreatureId");

            migrationBuilder.CreateIndex(
                name: "IX_Creatures_BaseCreatureId",
                table: "Creatures",
                column: "BaseCreatureId");

            migrationBuilder.CreateIndex(
                name: "IX_Creatures_CampaignId_BaseCreatureId",
                table: "Creatures",
                columns: new[] { "CampaignId", "BaseCreatureId" },
                unique: true,
                filter: "[CampaignId] IS NOT NULL AND [BaseCreatureId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Creatures_CampaignId_SearchKey",
                table: "Creatures",
                columns: new[] { "CampaignId", "SearchKey" });

            migrationBuilder.CreateIndex(
                name: "IX_Creatures_Slug",
                table: "Creatures",
                column: "Slug",
                unique: true,
                filter: "[CampaignId] IS NULL AND [Slug] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Combatants_Creatures_CreatureId",
                table: "Combatants",
                column: "CreatureId",
                principalTable: "Creatures",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Combatants_Creatures_CreatureId",
                table: "Combatants");

            migrationBuilder.DropTable(
                name: "CreatureImages");

            migrationBuilder.DropTable(
                name: "SeedVersions");

            migrationBuilder.DropTable(
                name: "Creatures");

            migrationBuilder.DropIndex(
                name: "IX_Combatants_CreatureId",
                table: "Combatants");

            migrationBuilder.DropColumn(
                name: "Label",
                table: "DiceRolls");

            migrationBuilder.DropColumn(
                name: "CreatureId",
                table: "Combatants");

            migrationBuilder.DropColumn(
                name: "LastRollAbility",
                table: "BattlemapTokenAppearances");

            migrationBuilder.DropColumn(
                name: "LastRollBonus",
                table: "BattlemapTokenAppearances");

            migrationBuilder.DropColumn(
                name: "LastRollCount",
                table: "BattlemapTokenAppearances");

            migrationBuilder.DropColumn(
                name: "LastRollDie",
                table: "BattlemapTokenAppearances");
        }
    }
}
