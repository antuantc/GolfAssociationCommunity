using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GolfAssociationCommunity.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentFlights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TournamentFlightId",
                table: "Registrations",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TournamentFlights",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TournamentId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentFlights", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TournamentFlights_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Registrations_TournamentFlightId",
                table: "Registrations",
                column: "TournamentFlightId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentFlights_TournamentId",
                table: "TournamentFlights",
                column: "TournamentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Registrations_TournamentFlights_TournamentFlightId",
                table: "Registrations",
                column: "TournamentFlightId",
                principalTable: "TournamentFlights",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Registrations_TournamentFlights_TournamentFlightId",
                table: "Registrations");

            migrationBuilder.DropTable(
                name: "TournamentFlights");

            migrationBuilder.DropIndex(
                name: "IX_Registrations_TournamentFlightId",
                table: "Registrations");

            migrationBuilder.DropColumn(
                name: "TournamentFlightId",
                table: "Registrations");
        }
    }
}
