using Microsoft.EntityFrameworkCore.Migrations;

namespace AIStudyHub.Data.Migrations;

/// <summary>
/// Phase 1 + 2 + 3 + 4: Adds trash-bin lifecycle fields, per-user document sharing,
/// token ledger for AI quota tracking, recommendation table, and flashcard lapses counter.
/// Run: dotnet ef database update
/// </summary>
public partial class AddTrashBinShareLevelRecommendationTokenWalletLapses : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // All schema objects were already applied by the preceding partial run.
        // DocumentShare, TokenLedger, and Recommendations tables already exist,
        // along with all column additions stripped in the prior fix.
        // This migration is registered purely to advance __EFMigrationsHistory.
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Down() intentionally left empty: this migration cannot be safely rolled back
        // as the DB already contains all the objects it describes.
    }
}
