using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GolfAssociationCommunity.Migrations
{
    /// <inheritdoc />
    public partial class AddHomepageContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CharityDescription",
                table: "GolfAssociations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CharityName",
                table: "GolfAssociations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CharityUrl",
                table: "GolfAssociations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EstYear",
                table: "GolfAssociations",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HeroImageUrl",
                table: "GolfAssociations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Motto",
                table: "GolfAssociations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tagline",
                table: "GolfAssociations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AssociationMedia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GolfAssociationId = table.Column<int>(type: "INTEGER", nullable: false),
                    MediaType = table.Column<int>(type: "INTEGER", nullable: false),
                    Url = table.Column<string>(type: "TEXT", nullable: false),
                    Caption = table.Column<string>(type: "TEXT", nullable: true),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssociationMedia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssociationMedia_GolfAssociations_GolfAssociationId",
                        column: x => x.GolfAssociationId,
                        principalTable: "GolfAssociations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssociationSponsors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GolfAssociationId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: true),
                    LogoUrl = table.Column<string>(type: "TEXT", nullable: true),
                    Website = table.Column<string>(type: "TEXT", nullable: true),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssociationSponsors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssociationSponsors_GolfAssociations_GolfAssociationId",
                        column: x => x.GolfAssociationId,
                        principalTable: "GolfAssociations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssociationMedia_GolfAssociationId",
                table: "AssociationMedia",
                column: "GolfAssociationId");

            migrationBuilder.CreateIndex(
                name: "IX_AssociationSponsors_GolfAssociationId",
                table: "AssociationSponsors",
                column: "GolfAssociationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssociationMedia");

            migrationBuilder.DropTable(
                name: "AssociationSponsors");

            migrationBuilder.DropColumn(
                name: "CharityDescription",
                table: "GolfAssociations");

            migrationBuilder.DropColumn(
                name: "CharityName",
                table: "GolfAssociations");

            migrationBuilder.DropColumn(
                name: "CharityUrl",
                table: "GolfAssociations");

            migrationBuilder.DropColumn(
                name: "EstYear",
                table: "GolfAssociations");

            migrationBuilder.DropColumn(
                name: "HeroImageUrl",
                table: "GolfAssociations");

            migrationBuilder.DropColumn(
                name: "Motto",
                table: "GolfAssociations");

            migrationBuilder.DropColumn(
                name: "Tagline",
                table: "GolfAssociations");
        }
    }
}
