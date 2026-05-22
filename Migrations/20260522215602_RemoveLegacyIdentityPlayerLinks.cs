using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GolfAssociationCommunity.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyIdentityPlayerLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Leaderboards_AspNetUsers_PlayerId",
                table: "Leaderboards");

            migrationBuilder.DropForeignKey(
                name: "FK_PlayerScores_AspNetUsers_PlayerId",
                table: "PlayerScores");

            migrationBuilder.DropForeignKey(
                name: "FK_Registrations_AspNetUsers_PlayerId",
                table: "Registrations");

            migrationBuilder.DropIndex(
                name: "IX_Registrations_PlayerId",
                table: "Registrations");

            migrationBuilder.DropIndex(
                name: "IX_PlayerScores_PlayerId",
                table: "PlayerScores");

            migrationBuilder.DropIndex(
                name: "IX_Leaderboards_PlayerId",
                table: "Leaderboards");

            migrationBuilder.DropColumn(
                name: "PlayerId",
                table: "Registrations");

            migrationBuilder.DropColumn(
                name: "PlayerId",
                table: "PlayerScores");

            migrationBuilder.DropColumn(
                name: "PlayerId",
                table: "Leaderboards");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PlayerId",
                table: "Registrations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlayerId",
                table: "PlayerScores",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlayerId",
                table: "Leaderboards",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Registrations_PlayerId",
                table: "Registrations",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerScores_PlayerId",
                table: "PlayerScores",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_Leaderboards_PlayerId",
                table: "Leaderboards",
                column: "PlayerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Leaderboards_AspNetUsers_PlayerId",
                table: "Leaderboards",
                column: "PlayerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PlayerScores_AspNetUsers_PlayerId",
                table: "PlayerScores",
                column: "PlayerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Registrations_AspNetUsers_PlayerId",
                table: "Registrations",
                column: "PlayerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
