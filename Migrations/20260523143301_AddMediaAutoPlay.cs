using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GolfAssociationCommunity.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaAutoPlay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoPlay",
                table: "AssociationMedia",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoPlay",
                table: "AssociationMedia");
        }
    }
}
