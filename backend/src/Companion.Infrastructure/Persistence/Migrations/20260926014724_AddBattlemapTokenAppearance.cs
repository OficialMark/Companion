using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Companion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBattlemapTokenAppearance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BattlemapTokenAppearances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BattlemapId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CombatantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MarkerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FootprintSize = table.Column<int>(type: "int", nullable: false),
                    StatusConditions = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BattlemapTokenAppearances", x => x.Id);
                    table.CheckConstraint("CK_BattlemapTokenAppearances_Owner", "(CASE WHEN [ParticipantId] IS NOT NULL THEN 1 ELSE 0 END) + (CASE WHEN [CombatantId] IS NOT NULL THEN 1 ELSE 0 END) + (CASE WHEN [MarkerId] IS NOT NULL THEN 1 ELSE 0 END) = 1");
                    table.ForeignKey(
                        name: "FK_BattlemapTokenAppearances_Battlemaps_BattlemapId",
                        column: x => x.BattlemapId,
                        principalTable: "Battlemaps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BattlemapTokenAppearances_Combatants_CombatantId",
                        column: x => x.CombatantId,
                        principalTable: "Combatants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BattlemapTokenAppearances_Participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "Participants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_BattlemapTokenAppearances_BattlemapId_CombatantId",
                table: "BattlemapTokenAppearances",
                columns: new[] { "BattlemapId", "CombatantId" },
                unique: true,
                filter: "[CombatantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BattlemapTokenAppearances_BattlemapId_MarkerId",
                table: "BattlemapTokenAppearances",
                columns: new[] { "BattlemapId", "MarkerId" },
                unique: true,
                filter: "[MarkerId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BattlemapTokenAppearances_BattlemapId_ParticipantId",
                table: "BattlemapTokenAppearances",
                columns: new[] { "BattlemapId", "ParticipantId" },
                unique: true,
                filter: "[ParticipantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BattlemapTokenAppearances_CombatantId",
                table: "BattlemapTokenAppearances",
                column: "CombatantId");

            migrationBuilder.CreateIndex(
                name: "IX_BattlemapTokenAppearances_ParticipantId",
                table: "BattlemapTokenAppearances",
                column: "ParticipantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BattlemapTokenAppearances");
        }
    }
}
