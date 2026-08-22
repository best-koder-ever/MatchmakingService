using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MatchmakingService.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// NEUTRALIZED 2026-08-20: this migration is a backdated duplicate. The MatchInsights
    /// table is already created by 20260511185257_AddMatchInsights (which is applied).
    /// Because this migration's timestamp (2026-05-05) is older than the applied
    /// 2026-05-11 migration, EF tried to run it on startup and crashed with
    /// "Table 'MatchInsights' already exists". It is recorded as applied in
    /// __EFMigrationsHistory but performs no work.
    /// </remarks>
    public partial class AddMatchInsight : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Intentionally a no-op — see class remarks.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally a no-op — see class remarks.
        }
    }
}
