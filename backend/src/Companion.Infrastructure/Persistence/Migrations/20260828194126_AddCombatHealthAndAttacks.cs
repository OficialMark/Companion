using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Companion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCombatHealthAndAttacks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentHitPoints",
                table: "Participants",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentHitPoints",
                table: "Combatants",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                table: "Combatants",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateTable(
                name: "CombatAttacks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EncounterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorParticipantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DiceRollId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Damage = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CombatAttacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CombatAttacks_DiceRolls_DiceRollId",
                        column: x => x.DiceRollId,
                        principalTable: "DiceRolls",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CombatAttacks_Encounters_EncounterId",
                        column: x => x.EncounterId,
                        principalTable: "Encounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CombatAttacks_Participants_AuthorParticipantId",
                        column: x => x.AuthorParticipantId,
                        principalTable: "Participants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CombatAttackTargets",
                columns: table => new
                {
                    AttackId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CombatantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DamageApplied = table.Column<int>(type: "int", nullable: false),
                    HitPointsBefore = table.Column<int>(type: "int", nullable: false),
                    HitPointsAfter = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CombatAttackTargets", x => new { x.AttackId, x.CombatantId });
                    table.ForeignKey(
                        name: "FK_CombatAttackTargets_CombatAttacks_AttackId",
                        column: x => x.AttackId,
                        principalTable: "CombatAttacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CombatAttackTargets_Combatants_CombatantId",
                        column: x => x.CombatantId,
                        principalTable: "Combatants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CombatAttacks_AuthorParticipantId_ClientRequestId",
                table: "CombatAttacks",
                columns: new[] { "AuthorParticipantId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CombatAttacks_DiceRollId",
                table: "CombatAttacks",
                column: "DiceRollId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CombatAttacks_EncounterId",
                table: "CombatAttacks",
                column: "EncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_CombatAttackTargets_CombatantId",
                table: "CombatAttackTargets",
                column: "CombatantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CombatAttackTargets");

            migrationBuilder.DropTable(
                name: "CombatAttacks");

            migrationBuilder.DropColumn(
                name: "CurrentHitPoints",
                table: "Participants");

            migrationBuilder.DropColumn(
                name: "CurrentHitPoints",
                table: "Combatants");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Combatants");
        }
    }
}
