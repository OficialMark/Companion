using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Companion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMarkerIdForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_BattlemapTokenPositions_MarkerId",
                table: "BattlemapTokenPositions",
                column: "MarkerId");

            migrationBuilder.CreateIndex(
                name: "IX_BattlemapTokenAppearances_MarkerId",
                table: "BattlemapTokenAppearances",
                column: "MarkerId");

            migrationBuilder.CreateIndex(
                name: "IX_AttackAreas_MarkerId",
                table: "AttackAreas",
                column: "MarkerId");

            migrationBuilder.AddForeignKey(
                name: "FK_AttackAreas_BattlemapMarkers_MarkerId",
                table: "AttackAreas",
                column: "MarkerId",
                principalTable: "BattlemapMarkers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BattlemapTokenAppearances_BattlemapMarkers_MarkerId",
                table: "BattlemapTokenAppearances",
                column: "MarkerId",
                principalTable: "BattlemapMarkers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BattlemapTokenPositions_BattlemapMarkers_MarkerId",
                table: "BattlemapTokenPositions",
                column: "MarkerId",
                principalTable: "BattlemapMarkers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AttackAreas_BattlemapMarkers_MarkerId",
                table: "AttackAreas");

            migrationBuilder.DropForeignKey(
                name: "FK_BattlemapTokenAppearances_BattlemapMarkers_MarkerId",
                table: "BattlemapTokenAppearances");

            migrationBuilder.DropForeignKey(
                name: "FK_BattlemapTokenPositions_BattlemapMarkers_MarkerId",
                table: "BattlemapTokenPositions");

            migrationBuilder.DropIndex(
                name: "IX_BattlemapTokenPositions_MarkerId",
                table: "BattlemapTokenPositions");

            migrationBuilder.DropIndex(
                name: "IX_BattlemapTokenAppearances_MarkerId",
                table: "BattlemapTokenAppearances");

            migrationBuilder.DropIndex(
                name: "IX_AttackAreas_MarkerId",
                table: "AttackAreas");
        }
    }
}
