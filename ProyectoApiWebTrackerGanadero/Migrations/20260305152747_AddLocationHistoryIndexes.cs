using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiWebTrackerGanado.Migrations
{
    /// <inheritdoc />
    public partial class AddLocationHistoryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LocationHistories_AnimalId",
                table: "LocationHistories");

            migrationBuilder.DropIndex(
                name: "IX_LocationHistories_TrackerId",
                table: "LocationHistories");

            migrationBuilder.CreateIndex(
                name: "IX_LocationHistories_AnimalId_Timestamp",
                table: "LocationHistories",
                columns: new[] { "AnimalId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_LocationHistories_TrackerId_Timestamp",
                table: "LocationHistories",
                columns: new[] { "TrackerId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LocationHistories_AnimalId_Timestamp",
                table: "LocationHistories");

            migrationBuilder.DropIndex(
                name: "IX_LocationHistories_TrackerId_Timestamp",
                table: "LocationHistories");

            migrationBuilder.CreateIndex(
                name: "IX_LocationHistories_AnimalId",
                table: "LocationHistories",
                column: "AnimalId");

            migrationBuilder.CreateIndex(
                name: "IX_LocationHistories_TrackerId",
                table: "LocationHistories",
                column: "TrackerId");
        }
    }
}
