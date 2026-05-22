using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GolfAssociationCommunity.Migrations
{
    /// <inheritdoc />
    public partial class AddSponsorshipPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SponsorshipPayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GolfAssociationId = table.Column<int>(type: "INTEGER", nullable: false),
                    SponsorshipPackageId = table.Column<int>(type: "INTEGER", nullable: true),
                    PackageName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    SponsorName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    SponsorEmail = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    SponsorCompany = table.Column<string>(type: "TEXT", nullable: true),
                    AmountPaid = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    PaymentConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    PaidAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AuthorizeNetTransactionId = table.Column<string>(type: "TEXT", nullable: true),
                    CardLast4 = table.Column<string>(type: "TEXT", nullable: true),
                    BillingAddressLine1 = table.Column<string>(type: "TEXT", nullable: false),
                    BillingCity = table.Column<string>(type: "TEXT", nullable: false),
                    BillingState = table.Column<string>(type: "TEXT", nullable: false),
                    BillingZipCode = table.Column<string>(type: "TEXT", nullable: false),
                    BillingCountry = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SponsorshipPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SponsorshipPayments_GolfAssociations_GolfAssociationId",
                        column: x => x.GolfAssociationId,
                        principalTable: "GolfAssociations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SponsorshipPayments_SponsorshipPackages_SponsorshipPackageId",
                        column: x => x.SponsorshipPackageId,
                        principalTable: "SponsorshipPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SponsorshipPayments_GolfAssociationId",
                table: "SponsorshipPayments",
                column: "GolfAssociationId");

            migrationBuilder.CreateIndex(
                name: "IX_SponsorshipPayments_PaidAtUtc",
                table: "SponsorshipPayments",
                column: "PaidAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SponsorshipPayments_SponsorshipPackageId",
                table: "SponsorshipPayments",
                column: "SponsorshipPackageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SponsorshipPayments");
        }
    }
}
