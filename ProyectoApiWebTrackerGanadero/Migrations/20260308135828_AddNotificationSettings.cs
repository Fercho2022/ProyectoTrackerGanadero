using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ApiWebTrackerGanado.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotificationSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    NotificationEmail = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EnableEmailNotifications = table.Column<bool>(type: "boolean", nullable: false),
                    EnableWhatsAppNotifications = table.Column<bool>(type: "boolean", nullable: false),
                    AlertNoSignal = table.Column<bool>(type: "boolean", nullable: false),
                    AlertWeakSignal = table.Column<bool>(type: "boolean", nullable: false),
                    AlertAbruptDisconnection = table.Column<bool>(type: "boolean", nullable: false),
                    AlertNightMovement = table.Column<bool>(type: "boolean", nullable: false),
                    AlertSuddenExit = table.Column<bool>(type: "boolean", nullable: false),
                    AlertUnusualSpeed = table.Column<bool>(type: "boolean", nullable: false),
                    AlertTrackerManipulation = table.Column<bool>(type: "boolean", nullable: false),
                    AlertOutOfBounds = table.Column<bool>(type: "boolean", nullable: false),
                    AlertImmobility = table.Column<bool>(type: "boolean", nullable: false),
                    AlertLowActivity = table.Column<bool>(type: "boolean", nullable: false),
                    AlertHighActivity = table.Column<bool>(type: "boolean", nullable: false),
                    AlertPossibleHeat = table.Column<bool>(type: "boolean", nullable: false),
                    AlertBatteryLow = table.Column<bool>(type: "boolean", nullable: false),
                    AlertBatteryCritical = table.Column<bool>(type: "boolean", nullable: false),
                    AlertInvalidCoordinates = table.Column<bool>(type: "boolean", nullable: false),
                    AlertLocationJump = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationSettings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationSettings_UserId",
                table: "NotificationSettings",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationSettings");
        }
    }
}
