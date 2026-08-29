using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Companion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBattlemapCreatureTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BattlemapTokenPositions_BattlemapId_ParticipantId",
                table: "BattlemapTokenPositions");

            migrationBuilder.AlterColumn<Guid>(
                name: "ParticipantId",
                table: "BattlemapTokenPositions",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "CombatantId",
                table: "BattlemapTokenPositions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BattlemapTokenPositions_BattlemapId_CombatantId",
                table: "BattlemapTokenPositions",
                columns: new[] { "BattlemapId", "CombatantId" },
                unique: true,
                filter: "[CombatantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BattlemapTokenPositions_BattlemapId_ParticipantId",
                table: "BattlemapTokenPositions",
                columns: new[] { "BattlemapId", "ParticipantId" },
                unique: true,
                filter: "[ParticipantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BattlemapTokenPositions_CombatantId",
                table: "BattlemapTokenPositions",
                column: "CombatantId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BattlemapTokenPositions_Owner",
                table: "BattlemapTokenPositions",
                sql: "([ParticipantId] IS NOT NULL AND [CombatantId] IS NULL) OR ([ParticipantId] IS NULL AND [CombatantId] IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_BattlemapTokenPositions_Combatants_CombatantId",
                table: "BattlemapTokenPositions",
                column: "CombatantId",
                principalTable: "Combatants",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BattlemapTokenPositions_Combatants_CombatantId",
                table: "BattlemapTokenPositions");

            migrationBuilder.DropIndex(
                name: "IX_BattlemapTokenPositions_BattlemapId_CombatantId",
                table: "BattlemapTokenPositions");

            migrationBuilder.DropIndex(
                name: "IX_BattlemapTokenPositions_BattlemapId_ParticipantId",
                table: "BattlemapTokenPositions");

            migrationBuilder.DropIndex(
                name: "IX_BattlemapTokenPositions_CombatantId",
                table: "BattlemapTokenPositions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BattlemapTokenPositions_Owner",
                table: "BattlemapTokenPositions");

            migrationBuilder.DropColumn(
                name: "CombatantId",
                table: "BattlemapTokenPositions");

            migrationBuilder.AlterColumn<Guid>(
                name: "ParticipantId",
                table: "BattlemapTokenPositions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BattlemapTokenPositions_BattlemapId_ParticipantId",
                table: "BattlemapTokenPositions",
                columns: new[] { "BattlemapId", "ParticipantId" },
                unique: true);
        }
    }
}
