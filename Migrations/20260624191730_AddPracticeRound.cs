using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GolfAssociationCommunity.Migrations
{
    /// <inheritdoc />
    public partial class AddPracticeRound : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasPracticeRound",
                table: "Tournaments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "PracticeRoundFee",
                table: "Tournaments",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IncludesPracticeRound",
                table: "Registrations",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasPracticeRound",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "PracticeRoundFee",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "IncludesPracticeRound",
                table: "Registrations");
        }
    }
}
