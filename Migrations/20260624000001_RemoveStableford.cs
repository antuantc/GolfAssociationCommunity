using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GolfAssociationCommunity.Migrations
{
    /// <inheritdoc />
    public partial class RemoveStableford : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"PlayerScores\" DROP COLUMN \"StablefordPoints\";");
            migrationBuilder.Sql("ALTER TABLE \"Leaderboards\" DROP COLUMN \"StablefordPoints\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StablefordPoints",
                table: "PlayerScores",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StablefordPoints",
                table: "Leaderboards",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }
    }
}
