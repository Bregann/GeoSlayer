using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace GeoSlayer.Domain.Database.Migrations
{
    /// <inheritdoc />
    public partial class Stage17CoinEconomy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The two dropped columns are why EF warns about data loss, and the loss is the
            // point: stack caps were removed with the coin economy (§5.4), so StackCap and
            // DustPerOverflow describe a rule that no longer exists. No player state is
            // touched — PlayerMaterials.Quantity is untouched and uncapped, so every holding
            // survives exactly as it was.
            migrationBuilder.DropColumn(
                name: "DustPerOverflow",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "StackCap",
                table: "Materials");

            migrationBuilder.AddColumn<long>(
                name: "Coin",
                table: "Players",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "CoinDeposited",
                table: "Players",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTime>(
                name: "InterestSettledUtc",
                table: "Players",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Coin",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "CoinDeposited",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "InterestSettledUtc",
                table: "Players");

            migrationBuilder.AddColumn<int>(
                name: "DustPerOverflow",
                table: "Materials",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StackCap",
                table: "Materials",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
