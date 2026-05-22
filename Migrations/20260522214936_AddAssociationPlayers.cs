using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GolfAssociationCommunity.Migrations
{
    /// <inheritdoc />
    public partial class AddAssociationPlayers : Migration
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

            migrationBuilder.AddColumn<int>(
                name: "AssociationPlayerId",
                table: "Registrations",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PlayerId",
                table: "PlayerScores",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AddColumn<int>(
                name: "AssociationPlayerId",
                table: "PlayerScores",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PlayerId",
                table: "Leaderboards",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AddColumn<int>(
                name: "AssociationPlayerId",
                table: "Leaderboards",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AssociationPlayers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GolfAssociationId = table.Column<int>(type: "INTEGER", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    HandicapIndex = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssociationPlayers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssociationPlayers_GolfAssociations_GolfAssociationId",
                        column: x => x.GolfAssociationId,
                        principalTable: "GolfAssociations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(@"
INSERT INTO AssociationPlayers (GolfAssociationId, DisplayName, Email, HandicapIndex, IsActive, CreatedAt, UpdatedAt)
SELECT source.GolfAssociationId,
       source.DisplayName,
       source.Email,
       NULL,
       1,
       CURRENT_TIMESTAMP,
       CURRENT_TIMESTAMP
FROM (
    SELECT DISTINCT
        t.GolfAssociationId,
        TRIM(COALESCE(
            NULLIF(TRIM(COALESCE(u.FirstName, '') || ' ' || COALESCE(u.LastName, '')), ''),
            NULLIF(u.Email, ''),
            NULLIF(u.UserName, ''),
            'Unknown Player')) AS DisplayName,
        LOWER(COALESCE(NULLIF(u.Email, ''), 'user-' || u.Id || '@local.player')) AS Email
    FROM Registrations r
    INNER JOIN Tournaments t ON t.Id = r.TournamentId
    INNER JOIN AspNetUsers u ON u.Id = r.PlayerId
    WHERE r.PlayerId IS NOT NULL AND r.PlayerId <> ''

    UNION

    SELECT DISTINCT
        t.GolfAssociationId,
        TRIM(COALESCE(
            NULLIF(TRIM(COALESCE(u.FirstName, '') || ' ' || COALESCE(u.LastName, '')), ''),
            NULLIF(u.Email, ''),
            NULLIF(u.UserName, ''),
            'Unknown Player')) AS DisplayName,
        LOWER(COALESCE(NULLIF(u.Email, ''), 'user-' || u.Id || '@local.player')) AS Email
    FROM PlayerScores ps
    INNER JOIN Tournaments t ON t.Id = ps.TournamentId
    INNER JOIN AspNetUsers u ON u.Id = ps.PlayerId
    WHERE ps.PlayerId IS NOT NULL AND ps.PlayerId <> ''

    UNION

    SELECT DISTINCT
        t.GolfAssociationId,
        TRIM(COALESCE(
            NULLIF(TRIM(COALESCE(u.FirstName, '') || ' ' || COALESCE(u.LastName, '')), ''),
            NULLIF(u.Email, ''),
            NULLIF(u.UserName, ''),
            'Unknown Player')) AS DisplayName,
        LOWER(COALESCE(NULLIF(u.Email, ''), 'user-' || u.Id || '@local.player')) AS Email
    FROM Leaderboards l
    INNER JOIN Tournaments t ON t.Id = l.TournamentId
    INNER JOIN AspNetUsers u ON u.Id = l.PlayerId
    WHERE l.PlayerId IS NOT NULL AND l.PlayerId <> ''
) AS source
WHERE NOT EXISTS (
    SELECT 1
    FROM AssociationPlayers ap
    WHERE ap.GolfAssociationId = source.GolfAssociationId
      AND ap.Email = source.Email);

INSERT INTO AssociationPlayers (GolfAssociationId, DisplayName, Email, HandicapIndex, IsActive, CreatedAt, UpdatedAt)
SELECT DISTINCT
    t.GolfAssociationId,
    TRIM(COALESCE(NULLIF(r.GuestName, ''), NULLIF(r.GuestEmail, ''), 'Guest Player')) AS DisplayName,
    LOWER(COALESCE(NULLIF(r.GuestEmail, ''), 'guest-registration-' || r.Id || '@local.player')) AS Email,
    r.Handicap,
    1,
    CURRENT_TIMESTAMP,
    CURRENT_TIMESTAMP
FROM Registrations r
INNER JOIN Tournaments t ON t.Id = r.TournamentId
WHERE (r.PlayerId IS NULL OR r.PlayerId = '')
  AND NOT EXISTS (
      SELECT 1
      FROM AssociationPlayers ap
      WHERE ap.GolfAssociationId = t.GolfAssociationId
        AND ap.Email = LOWER(COALESCE(NULLIF(r.GuestEmail, ''), 'guest-registration-' || r.Id || '@local.player')));

UPDATE Registrations
SET AssociationPlayerId = (
    SELECT ap.Id
    FROM AssociationPlayers ap
    INNER JOIN Tournaments t ON t.Id = Registrations.TournamentId
    LEFT JOIN AspNetUsers u ON u.Id = Registrations.PlayerId
    WHERE ap.GolfAssociationId = t.GolfAssociationId
      AND ap.Email = LOWER(
          CASE
              WHEN Registrations.PlayerId IS NOT NULL AND Registrations.PlayerId <> ''
                  THEN COALESCE(NULLIF(u.Email, ''), 'user-' || u.Id || '@local.player')
              ELSE COALESCE(NULLIF(Registrations.GuestEmail, ''), 'guest-registration-' || Registrations.Id || '@local.player')
          END)
    LIMIT 1)
WHERE AssociationPlayerId IS NULL;

UPDATE PlayerScores
SET AssociationPlayerId = (
    SELECT ap.Id
    FROM AssociationPlayers ap
    INNER JOIN Tournaments t ON t.Id = PlayerScores.TournamentId
    INNER JOIN AspNetUsers u ON u.Id = PlayerScores.PlayerId
    WHERE ap.GolfAssociationId = t.GolfAssociationId
      AND ap.Email = LOWER(COALESCE(NULLIF(u.Email, ''), 'user-' || u.Id || '@local.player'))
    LIMIT 1)
WHERE AssociationPlayerId IS NULL;

UPDATE Leaderboards
SET AssociationPlayerId = (
    SELECT ap.Id
    FROM AssociationPlayers ap
    INNER JOIN Tournaments t ON t.Id = Leaderboards.TournamentId
    INNER JOIN AspNetUsers u ON u.Id = Leaderboards.PlayerId
    WHERE ap.GolfAssociationId = t.GolfAssociationId
      AND ap.Email = LOWER(COALESCE(NULLIF(u.Email, ''), 'user-' || u.Id || '@local.player'))
    LIMIT 1)
WHERE AssociationPlayerId IS NULL;
");

            migrationBuilder.AlterColumn<int>(
                name: "AssociationPlayerId",
                table: "PlayerScores",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "AssociationPlayerId",
                table: "Leaderboards",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Registrations_AssociationPlayerId",
                table: "Registrations",
                column: "AssociationPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerScores_AssociationPlayerId",
                table: "PlayerScores",
                column: "AssociationPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_Leaderboards_AssociationPlayerId",
                table: "Leaderboards",
                column: "AssociationPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_AssociationPlayers_GolfAssociationId_Email",
                table: "AssociationPlayers",
                columns: new[] { "GolfAssociationId", "Email" });

            migrationBuilder.AddForeignKey(
                name: "FK_Leaderboards_AspNetUsers_PlayerId",
                table: "Leaderboards",
                column: "PlayerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Leaderboards_AssociationPlayers_AssociationPlayerId",
                table: "Leaderboards",
                column: "AssociationPlayerId",
                principalTable: "AssociationPlayers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PlayerScores_AspNetUsers_PlayerId",
                table: "PlayerScores",
                column: "PlayerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PlayerScores_AssociationPlayers_AssociationPlayerId",
                table: "PlayerScores",
                column: "AssociationPlayerId",
                principalTable: "AssociationPlayers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Registrations_AssociationPlayers_AssociationPlayerId",
                table: "Registrations",
                column: "AssociationPlayerId",
                principalTable: "AssociationPlayers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Leaderboards_AspNetUsers_PlayerId",
                table: "Leaderboards");

            migrationBuilder.DropForeignKey(
                name: "FK_Leaderboards_AssociationPlayers_AssociationPlayerId",
                table: "Leaderboards");

            migrationBuilder.DropForeignKey(
                name: "FK_PlayerScores_AspNetUsers_PlayerId",
                table: "PlayerScores");

            migrationBuilder.DropForeignKey(
                name: "FK_PlayerScores_AssociationPlayers_AssociationPlayerId",
                table: "PlayerScores");

            migrationBuilder.DropForeignKey(
                name: "FK_Registrations_AssociationPlayers_AssociationPlayerId",
                table: "Registrations");

            migrationBuilder.DropTable(
                name: "AssociationPlayers");

            migrationBuilder.DropIndex(
                name: "IX_Registrations_AssociationPlayerId",
                table: "Registrations");

            migrationBuilder.DropIndex(
                name: "IX_PlayerScores_AssociationPlayerId",
                table: "PlayerScores");

            migrationBuilder.DropIndex(
                name: "IX_Leaderboards_AssociationPlayerId",
                table: "Leaderboards");

            migrationBuilder.DropColumn(
                name: "AssociationPlayerId",
                table: "Registrations");

            migrationBuilder.DropColumn(
                name: "AssociationPlayerId",
                table: "PlayerScores");

            migrationBuilder.DropColumn(
                name: "AssociationPlayerId",
                table: "Leaderboards");

            migrationBuilder.AlterColumn<string>(
                name: "PlayerId",
                table: "PlayerScores",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PlayerId",
                table: "Leaderboards",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Leaderboards_AspNetUsers_PlayerId",
                table: "Leaderboards",
                column: "PlayerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PlayerScores_AspNetUsers_PlayerId",
                table: "PlayerScores",
                column: "PlayerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
