using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Companion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBattlemapMarkers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_BattlemapTokenPositions_Owner",
                table: "BattlemapTokenPositions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AttackAreas_Owner",
                table: "AttackAreas");

            migrationBuilder.AddColumn<Guid>(
                name: "MarkerId",
                table: "BattlemapTokenPositions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MarkerId",
                table: "AttackAreas",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BattlemapMarkers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BattlemapId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerParticipantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BattlemapMarkers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BattlemapMarkers_Battlemaps_BattlemapId",
                        column: x => x.BattlemapId,
                        principalTable: "Battlemaps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BattlemapMarkers_Participants_OwnerParticipantId",
                        column: x => x.OwnerParticipantId,
                        principalTable: "Participants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_BattlemapTokenPositions_BattlemapId_MarkerId",
                table: "BattlemapTokenPositions",
                columns: new[] { "BattlemapId", "MarkerId" },
                unique: true,
                filter: "[MarkerId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BattlemapTokenPositions_Owner",
                table: "BattlemapTokenPositions",
                sql: "(CASE WHEN [ParticipantId] IS NOT NULL THEN 1 ELSE 0 END) + (CASE WHEN [CombatantId] IS NOT NULL THEN 1 ELSE 0 END) + (CASE WHEN [MarkerId] IS NOT NULL THEN 1 ELSE 0 END) = 1");

            migrationBuilder.CreateIndex(
                name: "IX_AttackAreas_BattlemapId_MarkerId",
                table: "AttackAreas",
                columns: new[] { "BattlemapId", "MarkerId" },
                unique: true,
                filter: "[MarkerId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AttackAreas_Owner",
                table: "AttackAreas",
                sql: "(CASE WHEN [ParticipantId] IS NOT NULL THEN 1 ELSE 0 END) + (CASE WHEN [CombatantId] IS NOT NULL THEN 1 ELSE 0 END) + (CASE WHEN [MarkerId] IS NOT NULL THEN 1 ELSE 0 END) = 1");

            migrationBuilder.CreateIndex(
                name: "IX_BattlemapMarkers_BattlemapId",
                table: "BattlemapMarkers",
                column: "BattlemapId");

            migrationBuilder.CreateIndex(
                name: "IX_BattlemapMarkers_OwnerParticipantId",
                table: "BattlemapMarkers",
                column: "OwnerParticipantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BattlemapMarkers");

            migrationBuilder.DropIndex(
                name: "IX_BattlemapTokenPositions_BattlemapId_MarkerId",
                table: "BattlemapTokenPositions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BattlemapTokenPositions_Owner",
                table: "BattlemapTokenPositions");

            migrationBuilder.DropIndex(
                name: "IX_AttackAreas_BattlemapId_MarkerId",
                table: "AttackAreas");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AttackAreas_Owner",
                table: "AttackAreas");

            migrationBuilder.DropColumn(
                name: "MarkerId",
                table: "BattlemapTokenPositions");

            migrationBuilder.DropColumn(
                name: "MarkerId",
                table: "AttackAreas");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BattlemapTokenPositions_Owner",
                table: "BattlemapTokenPositions",
                sql: "([ParticipantId] IS NOT NULL AND [CombatantId] IS NULL) OR ([ParticipantId] IS NULL AND [CombatantId] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AttackAreas_Owner",
                table: "AttackAreas",
                sql: "([ParticipantId] IS NOT NULL AND [CombatantId] IS NULL) OR ([ParticipantId] IS NULL AND [CombatantId] IS NOT NULL)");
        }
    }
}
