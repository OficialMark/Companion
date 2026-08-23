using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Companion.Infrastructure.Persistence.Migrations;

[DbContext(typeof(CompanionDbContext))]
[Migration("20260823170000_AddDiceRollReveal")]
public partial class AddDiceRollReveal : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "RevealedAt",
            table: "DiceRolls",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.Sql("UPDATE DiceRolls SET RevealedAt = CreatedAt WHERE RevealedAt IS NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "RevealedAt",
            table: "DiceRolls");
    }
}
