using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Companion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogSearchText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                table: "CatalogSpells",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SearchText",
                table: "CatalogSpells",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SearchText",
                table: "CatalogItems",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NameEn",
                table: "CatalogSpells");

            migrationBuilder.DropColumn(
                name: "SearchText",
                table: "CatalogSpells");

            migrationBuilder.DropColumn(
                name: "SearchText",
                table: "CatalogItems");
        }
    }
}
