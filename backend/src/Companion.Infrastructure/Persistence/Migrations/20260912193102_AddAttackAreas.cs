using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Companion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAttackAreas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttackAreas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BattlemapId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CombatantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Shape = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Radius = table.Column<int>(type: "int", nullable: false),
                    Width = table.Column<int>(type: "int", nullable: true),
                    DirectionDegrees = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttackAreas", x => x.Id);
                    table.CheckConstraint("CK_AttackAreas_Owner", "([ParticipantId] IS NOT NULL AND [CombatantId] IS NULL) OR ([ParticipantId] IS NULL AND [CombatantId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_AttackAreas_Battlemaps_BattlemapId",
                        column: x => x.BattlemapId,
                        principalTable: "Battlemaps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AttackAreas_Combatants_CombatantId",
                        column: x => x.CombatantId,
                        principalTable: "Combatants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AttackAreas_Participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "Participants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttackAreas_BattlemapId",
                table: "AttackAreas",
                column: "BattlemapId");

            migrationBuilder.CreateIndex(
                name: "IX_AttackAreas_BattlemapId_CombatantId",
                table: "AttackAreas",
                columns: new[] { "BattlemapId", "CombatantId" },
                unique: true,
                filter: "[CombatantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AttackAreas_BattlemapId_ParticipantId",
                table: "AttackAreas",
                columns: new[] { "BattlemapId", "ParticipantId" },
                unique: true,
                filter: "[ParticipantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AttackAreas_CombatantId",
                table: "AttackAreas",
                column: "CombatantId");

            migrationBuilder.CreateIndex(
                name: "IX_AttackAreas_ParticipantId",
                table: "AttackAreas",
                column: "ParticipantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttackAreas");
        }
    }
}
