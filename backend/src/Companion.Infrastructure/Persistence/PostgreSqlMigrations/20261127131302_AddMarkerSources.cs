using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Companion.Infrastructure.Persistence.PostgreSqlMigrations
{
    /// <inheritdoc />
    public partial class AddMarkerSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CatalogItemId",
                table: "BattlemapMarkers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatureId",
                table: "BattlemapMarkers",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CatalogItemId",
                table: "BattlemapMarkers");

            migrationBuilder.DropColumn(
                name: "CreatureId",
                table: "BattlemapMarkers");
        }
    }
}
