using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GeoSlayer.Domain.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerAntiCheatFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserStreetProgresses");

            migrationBuilder.DropTable(
                name: "Streets");

            migrationBuilder.DropIndex(
                name: "IX_Players_Location",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "StreetCount",
                table: "ImportedRegions");

            migrationBuilder.AddColumn<double>(
                name: "LastCellLat",
                table: "Players",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LastCellLng",
                table: "Players",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LastLatitude",
                table: "Players",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "LastLongitude",
                table: "Players",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSyncAtUtc",
                table: "Players",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RevealedCells",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    GridLat = table.Column<int>(type: "integer", nullable: false),
                    GridLng = table.Column<int>(type: "integer", nullable: false),
                    RevealedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevealedCells", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RevealedCells_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RevealedCells_PlayerId",
                table: "RevealedCells",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_RevealedCells_PlayerId_GridLat_GridLng",
                table: "RevealedCells",
                columns: new[] { "PlayerId", "GridLat", "GridLng" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RevealedCells");

            migrationBuilder.DropColumn(
                name: "LastCellLat",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "LastCellLng",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "LastLatitude",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "LastLongitude",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "LastSyncAtUtc",
                table: "Players");

            migrationBuilder.AddColumn<Point>(
                name: "Location",
                table: "Players",
                type: "geometry (point, 4326)",
                nullable: false);

            migrationBuilder.AddColumn<int>(
                name: "StreetCount",
                table: "ImportedRegions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Streets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    OsmId = table.Column<long>(type: "bigint", nullable: false),
                    Path = table.Column<LineString>(type: "geometry (linestring, 4326)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Streets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserStreetProgresses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    StreetId = table.Column<int>(type: "integer", nullable: false),
                    CoveredMaxFraction = table.Column<double>(type: "double precision", nullable: false),
                    CoveredMinFraction = table.Column<double>(type: "double precision", nullable: false),
                    IsConquered = table.Column<bool>(type: "boolean", nullable: false),
                    PercentComplete = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserStreetProgresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserStreetProgresses_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserStreetProgresses_Streets_StreetId",
                        column: x => x.StreetId,
                        principalTable: "Streets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Players_Location",
                table: "Players",
                column: "Location")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_Streets_OsmId",
                table: "Streets",
                column: "OsmId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Streets_Path",
                table: "Streets",
                column: "Path")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_UserStreetProgresses_PlayerId_StreetId",
                table: "UserStreetProgresses",
                columns: new[] { "PlayerId", "StreetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserStreetProgresses_StreetId",
                table: "UserStreetProgresses",
                column: "StreetId");
        }
    }
}
