using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Companion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CharacterSheetPerPlayerName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CharacterSheets_Participants_ParticipantId",
                table: "CharacterSheets");

            migrationBuilder.AddColumn<string>(
                name: "OwnerKey",
                table: "CharacterSheets",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "",
                collation: "Latin1_General_100_BIN2");

            // 2026-11-20: a sheet belongs to the player name, across campaigns. The newest sheet of each name
            // becomes the account's sheet; older sheets of the same name are kept (no data is deleted) under a
            // LEGACY key that nothing looks up.
            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT s.ParticipantId,
                           COALESCE(p.NameKey, UPPER(LTRIM(RTRIM(p.DisplayName))) COLLATE Latin1_General_100_BIN2) AS NameKey,
                           ROW_NUMBER() OVER (
                               PARTITION BY COALESCE(p.NameKey, UPPER(LTRIM(RTRIM(p.DisplayName))) COLLATE Latin1_General_100_BIN2)
                               ORDER BY s.UpdatedAt DESC, s.ParticipantId) AS Position
                    FROM CharacterSheets s
                    LEFT JOIN Participants p ON p.Id = s.ParticipantId)
                UPDATE s
                SET OwnerKey = CASE
                    WHEN r.NameKey IS NOT NULL AND r.Position = 1 THEN r.NameKey
                    ELSE 'LEGACY:' + CONVERT(nvarchar(36), s.ParticipantId) END
                FROM CharacterSheets s
                JOIN ranked r ON r.ParticipantId = s.ParticipantId;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_CharacterSheets_OwnerKey",
                table: "CharacterSheets",
                column: "OwnerKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CharacterSheets_OwnerKey",
                table: "CharacterSheets");

            migrationBuilder.DropColumn(
                name: "OwnerKey",
                table: "CharacterSheets");

            // The FK back to Participants is NOT restored: after a campaign is deleted, sheets of its players
            // still exist (they belong to the name), and the FK would fail on them. Rollback must not delete sheets.
        }
    }
}
