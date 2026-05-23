using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GolfAssociationCommunity.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthorizeNetRefundTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "RefundAmount",
                table: "SponsorshipPayments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefundTransactionId",
                table: "SponsorshipPayments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RefundedAtUtc",
                table: "SponsorshipPayments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RefundAmount",
                table: "Registrations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefundTransactionId",
                table: "Registrations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RefundedAtUtc",
                table: "Registrations",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RefundAmount",
                table: "SponsorshipPayments");

            migrationBuilder.DropColumn(
                name: "RefundTransactionId",
                table: "SponsorshipPayments");

            migrationBuilder.DropColumn(
                name: "RefundedAtUtc",
                table: "SponsorshipPayments");

            migrationBuilder.DropColumn(
                name: "RefundAmount",
                table: "Registrations");

            migrationBuilder.DropColumn(
                name: "RefundTransactionId",
                table: "Registrations");

            migrationBuilder.DropColumn(
                name: "RefundedAtUtc",
                table: "Registrations");
        }
    }
}
