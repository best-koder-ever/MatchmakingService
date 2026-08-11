using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MatchmakingService.Migrations
{
    /// <inheritdoc />
    public partial class ConnectionInsightV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConfidenceLevel",
                table: "MatchInsights",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ConnectionHookJson",
                table: "MatchInsights",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ConnectionSignalsJson",
                table: "MatchInsights",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PsychometricProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    KeycloakId = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SocialEnergy = table.Column<double>(type: "double", nullable: false),
                    Warmth = table.Column<double>(type: "double", nullable: false),
                    PlanningRhythm = table.Column<double>(type: "double", nullable: false),
                    Steadiness = table.Column<double>(type: "double", nullable: false),
                    Curiosity = table.Column<double>(type: "double", nullable: false),
                    SchemaVersion = table.Column<int>(type: "int", nullable: false),
                    ComputedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PsychometricProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PsychometricProfiles_UserProfiles_UserId",
                        column: x => x.UserId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_PsychometricProfile_KeycloakId",
                table: "PsychometricProfiles",
                column: "KeycloakId");

            migrationBuilder.CreateIndex(
                name: "IX_PsychometricProfile_UserId",
                table: "PsychometricProfiles",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PsychometricProfiles");

            migrationBuilder.DropColumn(
                name: "ConfidenceLevel",
                table: "MatchInsights");

            migrationBuilder.DropColumn(
                name: "ConnectionHookJson",
                table: "MatchInsights");

            migrationBuilder.DropColumn(
                name: "ConnectionSignalsJson",
                table: "MatchInsights");
        }
    }
}
