using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GolfAssociationCommunity.Migrations
{
    /// <inheritdoc />
    public partial class AddFlights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Flight",
                table: "Registrations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Flight",
                table: "Leaderboards",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Flight",
                table: "Registrations");

            migrationBuilder.DropColumn(
                name: "Flight",
                table: "Leaderboards");
        }
    }
}
