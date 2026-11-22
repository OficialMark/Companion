using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Companion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AttachCampaignKnowledgePdf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "PdfContent",
                table: "CampaignKnowledgeEntries",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PdfFileName",
                table: "CampaignKnowledgeEntries",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PdfContent",
                table: "CampaignKnowledgeEntries");

            migrationBuilder.DropColumn(
                name: "PdfFileName",
                table: "CampaignKnowledgeEntries");
        }
    }
}
