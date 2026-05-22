using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GolfAssociationCommunity.Migrations
{
    /// <inheritdoc />
    public partial class AddAssociationAuthorizeNetSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AuthorizeNetApiLoginId",
                table: "GolfAssociations",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuthorizeNetTransactionKey",
                table: "GolfAssociations",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AuthorizeNetUseSandbox",
                table: "GolfAssociations",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuthorizeNetApiLoginId",
                table: "GolfAssociations");

            migrationBuilder.DropColumn(
                name: "AuthorizeNetTransactionKey",
                table: "GolfAssociations");

            migrationBuilder.DropColumn(
                name: "AuthorizeNetUseSandbox",
                table: "GolfAssociations");
        }
    }
}
