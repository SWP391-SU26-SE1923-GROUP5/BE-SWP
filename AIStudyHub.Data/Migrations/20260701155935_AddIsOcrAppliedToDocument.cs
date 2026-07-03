using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIStudyHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIsOcrAppliedToDocument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsOcrApplied",
                table: "Document",
                type: "bit",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsOcrApplied",
                table: "Document");
        }
    }
}
