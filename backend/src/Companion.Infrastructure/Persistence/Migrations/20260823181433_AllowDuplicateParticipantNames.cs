using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Companion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowDuplicateParticipantNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Participants_CampaignId_DisplayName",
                table: "Participants");

            migrationBuilder.CreateIndex(
                name: "IX_Participants_CampaignId_DisplayName",
                table: "Participants",
                columns: new[] { "CampaignId", "DisplayName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Participants_CampaignId_DisplayName",
                table: "Participants");

            migrationBuilder.CreateIndex(
                name: "IX_Participants_CampaignId_DisplayName",
                table: "Participants",
                columns: new[] { "CampaignId", "DisplayName" },
                unique: true);
        }
    }
}
