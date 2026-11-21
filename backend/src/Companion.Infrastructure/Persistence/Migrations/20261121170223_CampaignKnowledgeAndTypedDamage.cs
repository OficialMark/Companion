using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Companion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CampaignKnowledgeAndTypedDamage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CampaignKnowledgeEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SharedDescription = table.Column<string>(type: "nvarchar(max)", maxLength: 12000, nullable: true),
                    IsPlayerVisible = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByParticipantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    ImageContent = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    ImageMediaType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    ImageFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignKnowledgeEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignKnowledgeEntries_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CampaignKnowledgeContributions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    CreatedByParticipantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignKnowledgeContributions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignKnowledgeContributions_CampaignKnowledgeEntries_EntryId",
                        column: x => x.EntryId,
                        principalTable: "CampaignKnowledgeEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CampaignKnowledgeMasterNotes",
                columns: table => new
                {
                    EntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 12000, nullable: false),
                    UpdatedByParticipantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignKnowledgeMasterNotes", x => x.EntryId);
                    table.ForeignKey(
                        name: "FK_CampaignKnowledgeMasterNotes_CampaignKnowledgeEntries_EntryId",
                        column: x => x.EntryId,
                        principalTable: "CampaignKnowledgeEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CampaignMapPoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MapEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    X = table.Column<decimal>(type: "decimal(8,7)", precision: 8, scale: 7, nullable: false),
                    Y = table.Column<decimal>(type: "decimal(8,7)", precision: 8, scale: 7, nullable: false),
                    CreatedByParticipantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignMapPoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignMapPoints_CampaignKnowledgeEntries_MapEntryId",
                        column: x => x.MapEntryId,
                        principalTable: "CampaignKnowledgeEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignKnowledgeContributions_EntryId_CreatedAt",
                table: "CampaignKnowledgeContributions",
                columns: new[] { "EntryId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignKnowledgeEntries_CampaignId_Kind_IsPlayerVisible_Title",
                table: "CampaignKnowledgeEntries",
                columns: new[] { "CampaignId", "Kind", "IsPlayerVisible", "Title" });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMapPoints_MapEntryId_CreatedAt",
                table: "CampaignMapPoints",
                columns: new[] { "MapEntryId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampaignKnowledgeContributions");

            migrationBuilder.DropTable(
                name: "CampaignKnowledgeMasterNotes");

            migrationBuilder.DropTable(
                name: "CampaignMapPoints");

            migrationBuilder.DropTable(
                name: "CampaignKnowledgeEntries");
        }
    }
}
