using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MatchmakingService.Migrations
{
    /// <inheritdoc />
    public partial class EnhancedMatchmakingModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "CompatibilityScore",
                table: "Matches",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Matches",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastMessageAt",
                table: "Matches",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastMessageByUserId",
                table: "Matches",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MatchSource",
                table: "Matches",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "UnmatchedAt",
                table: "Matches",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UnmatchedByUserId",
                table: "Matches",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MatchingAlgorithmMetrics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SuggestionsGenerated = table.Column<int>(type: "int", nullable: false),
                    SwipesReceived = table.Column<int>(type: "int", nullable: false),
                    LikesReceived = table.Column<int>(type: "int", nullable: false),
                    MatchesCreated = table.Column<int>(type: "int", nullable: false),
                    SuccessRate = table.Column<double>(type: "double", nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchingAlgorithmMetrics", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "MatchPreferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    PreferenceType = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PreferenceValue = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Weight = table.Column<double>(type: "double", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchPreferences", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "MatchScores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    TargetUserId = table.Column<int>(type: "int", nullable: false),
                    OverallScore = table.Column<double>(type: "double", nullable: false),
                    LocationScore = table.Column<double>(type: "double", nullable: false),
                    AgeScore = table.Column<double>(type: "double", nullable: false),
                    InterestsScore = table.Column<double>(type: "double", nullable: false),
                    EducationScore = table.Column<double>(type: "double", nullable: false),
                    LifestyleScore = table.Column<double>(type: "double", nullable: false),
                    ActivityScore = table.Column<double>(type: "double", nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IsValid = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchScores", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    SenderId = table.Column<int>(type: "int", nullable: false),
                    ReceiverId = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SentAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "UserProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Gender = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Age = table.Column<int>(type: "int", nullable: false),
                    Latitude = table.Column<double>(type: "double", nullable: false),
                    Longitude = table.Column<double>(type: "double", nullable: false),
                    City = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    State = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Country = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PreferredGender = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MinAge = table.Column<int>(type: "int", nullable: false),
                    MaxAge = table.Column<int>(type: "int", nullable: false),
                    MaxDistance = table.Column<double>(type: "double", nullable: false),
                    Interests = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Education = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Occupation = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Height = table.Column<int>(type: "int", nullable: false),
                    Religion = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Ethnicity = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    WantsChildren = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    HasChildren = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SmokingStatus = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DrinkingStatus = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LocationWeight = table.Column<double>(type: "double", nullable: false),
                    AgeWeight = table.Column<double>(type: "double", nullable: false),
                    InterestsWeight = table.Column<double>(type: "double", nullable: false),
                    EducationWeight = table.Column<double>(type: "double", nullable: false),
                    LifestyleWeight = table.Column<double>(type: "double", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfiles", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Match_CreatedAt",
                table: "Matches",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Match_User1Id",
                table: "Matches",
                column: "User1Id");

            migrationBuilder.CreateIndex(
                name: "IX_Match_User1Id_User2Id",
                table: "Matches",
                columns: new[] { "User1Id", "User2Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Match_User2Id",
                table: "Matches",
                column: "User2Id");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Match_UserOrder",
                table: "Matches",
                sql: "User1Id < User2Id");

            migrationBuilder.CreateIndex(
                name: "IX_MatchingAlgorithmMetric_CalculatedAt",
                table: "MatchingAlgorithmMetrics",
                column: "CalculatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MatchingAlgorithmMetric_UserId",
                table: "MatchingAlgorithmMetrics",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchPreference_UserId",
                table: "MatchPreferences",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchPreference_UserId_Type",
                table: "MatchPreferences",
                columns: new[] { "UserId", "PreferenceType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchScore_CalculatedAt",
                table: "MatchScores",
                column: "CalculatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MatchScore_OverallScore",
                table: "MatchScores",
                column: "OverallScore");

            migrationBuilder.CreateIndex(
                name: "IX_MatchScore_UserId",
                table: "MatchScores",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchScore_UserId_TargetUserId",
                table: "MatchScores",
                columns: new[] { "UserId", "TargetUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserProfile_Age",
                table: "UserProfiles",
                column: "Age");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfile_Gender",
                table: "UserProfiles",
                column: "Gender");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfile_Location",
                table: "UserProfiles",
                columns: new[] { "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_UserProfile_UserId",
                table: "UserProfiles",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchingAlgorithmMetrics");

            migrationBuilder.DropTable(
                name: "MatchPreferences");

            migrationBuilder.DropTable(
                name: "MatchScores");

            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "UserProfiles");

            migrationBuilder.DropIndex(
                name: "IX_Match_CreatedAt",
                table: "Matches");

            migrationBuilder.DropIndex(
                name: "IX_Match_User1Id",
                table: "Matches");

            migrationBuilder.DropIndex(
                name: "IX_Match_User1Id_User2Id",
                table: "Matches");

            migrationBuilder.DropIndex(
                name: "IX_Match_User2Id",
                table: "Matches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Match_UserOrder",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "CompatibilityScore",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "LastMessageAt",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "LastMessageByUserId",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "MatchSource",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "UnmatchedAt",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "UnmatchedByUserId",
                table: "Matches");
        }
    }
}
