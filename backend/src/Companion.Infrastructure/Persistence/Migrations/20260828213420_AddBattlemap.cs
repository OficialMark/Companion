using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Companion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBattlemap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Battlemaps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EncounterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImageContent = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    ImageMediaType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ImageFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ImageRevision = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Columns = table.Column<int>(type: "int", nullable: false),
                    Rows = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Battlemaps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Battlemaps_Encounters_EncounterId",
                        column: x => x.EncounterId,
                        principalTable: "Encounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BattlemapTokenPositions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BattlemapId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Column = table.Column<int>(type: "int", nullable: false),
                    Row = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BattlemapTokenPositions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BattlemapTokenPositions_Battlemaps_BattlemapId",
                        column: x => x.BattlemapId,
                        principalTable: "Battlemaps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BattlemapTokenPositions_Participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "Participants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Battlemaps_EncounterId",
                table: "Battlemaps",
                column: "EncounterId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BattlemapTokenPositions_BattlemapId",
                table: "BattlemapTokenPositions",
                column: "BattlemapId");

            migrationBuilder.CreateIndex(
                name: "IX_BattlemapTokenPositions_BattlemapId_ParticipantId",
                table: "BattlemapTokenPositions",
                columns: new[] { "BattlemapId", "ParticipantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BattlemapTokenPositions_ParticipantId",
                table: "BattlemapTokenPositions",
                column: "ParticipantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BattlemapTokenPositions");

            migrationBuilder.DropTable(
                name: "Battlemaps");
        }
    }
}
