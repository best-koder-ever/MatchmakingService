using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MatchmakingService.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateSystemFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PreferredGender",
                table: "UserProfiles",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<double>(
                name: "DesirabilityScore",
                table: "UserProfiles",
                type: "double",
                nullable: false,
                defaultValue: 50.0);

            migrationBuilder.AddColumn<bool>(
                name: "IsVerified",
                table: "UserProfiles",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastActiveAt",
                table: "UserProfiles",
                type: "datetime(6)",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP(6)");

            migrationBuilder.AddColumn<string>(
                name: "LookingFor",
                table: "UserProfiles",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfile_ActiveSearch",
                table: "UserProfiles",
                columns: new[] { "IsActive", "Gender", "Age", "LastActiveAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserProfile_Desirability",
                table: "UserProfiles",
                columns: new[] { "IsActive", "DesirabilityScore" });

            migrationBuilder.CreateIndex(
                name: "IX_UserProfile_PreferredGenderActive",
                table: "UserProfiles",
                columns: new[] { "PreferredGender", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_UserInteraction_CreatedAt",
                table: "UserInteractions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserInteraction_UserLookup",
                table: "UserInteractions",
                columns: new[] { "UserId", "TargetUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchScore_Lookup_Valid",
                table: "MatchScores",
                columns: new[] { "UserId", "TargetUserId", "IsValid", "CalculatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchScore_UserIdValid_Score",
                table: "MatchScores",
                columns: new[] { "UserId", "IsValid", "OverallScore" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchingAlgorithmMetric_User_Date",
                table: "MatchingAlgorithmMetrics",
                columns: new[] { "UserId", "CalculatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Match_User1Id_IsActive",
                table: "Matches",
                columns: new[] { "User1Id", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Match_User2Id_IsActive",
                table: "Matches",
                columns: new[] { "User2Id", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserProfile_ActiveSearch",
                table: "UserProfiles");

            migrationBuilder.DropIndex(
                name: "IX_UserProfile_Desirability",
                table: "UserProfiles");

            migrationBuilder.DropIndex(
                name: "IX_UserProfile_PreferredGenderActive",
                table: "UserProfiles");

            migrationBuilder.DropIndex(
                name: "IX_UserInteraction_CreatedAt",
                table: "UserInteractions");

            migrationBuilder.DropIndex(
                name: "IX_UserInteraction_UserLookup",
                table: "UserInteractions");

            migrationBuilder.DropIndex(
                name: "IX_MatchScore_Lookup_Valid",
                table: "MatchScores");

            migrationBuilder.DropIndex(
                name: "IX_MatchScore_UserIdValid_Score",
                table: "MatchScores");

            migrationBuilder.DropIndex(
                name: "IX_MatchingAlgorithmMetric_User_Date",
                table: "MatchingAlgorithmMetrics");

            migrationBuilder.DropIndex(
                name: "IX_Match_User1Id_IsActive",
                table: "Matches");

            migrationBuilder.DropIndex(
                name: "IX_Match_User2Id_IsActive",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "DesirabilityScore",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "IsVerified",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "LastActiveAt",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "LookingFor",
                table: "UserProfiles");

            migrationBuilder.AlterColumn<string>(
                name: "PreferredGender",
                table: "UserProfiles",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
