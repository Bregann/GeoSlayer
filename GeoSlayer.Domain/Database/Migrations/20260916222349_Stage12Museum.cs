using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GeoSlayer.Domain.Database.Migrations
{
    /// <inheritdoc />
    public partial class Stage12Museum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "Curation",
                table: "Players",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "GeoRegions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CellLat = table.Column<double>(type: "double precision", nullable: false),
                    CellLng = table.Column<double>(type: "double precision", nullable: false),
                    RegionKey = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeoRegions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MuseumEntryDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    Wing = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Rarity = table.Column<int>(type: "integer", nullable: false),
                    UnlockCondition = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MuseumEntryDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlayerMuseumEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    EntryKey = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    FirstAcquiredUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    AcquiredAtPoiId = table.Column<int>(type: "integer", nullable: true),
                    AcquiredAtName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    DonatedQuantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerMuseumEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerMuseumEntries_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GeoRegions_CellLat_CellLng",
                table: "GeoRegions",
                columns: new[] { "CellLat", "CellLng" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GeoRegions_RegionKey",
                table: "GeoRegions",
                column: "RegionKey");

            migrationBuilder.CreateIndex(
                name: "IX_MuseumEntryDefinitions_Key",
                table: "MuseumEntryDefinitions",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MuseumEntryDefinitions_Wing",
                table: "MuseumEntryDefinitions",
                column: "Wing");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerMuseumEntries_PlayerId_EntryKey",
                table: "PlayerMuseumEntries",
                columns: new[] { "PlayerId", "EntryKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GeoRegions");

            migrationBuilder.DropTable(
                name: "MuseumEntryDefinitions");

            migrationBuilder.DropTable(
                name: "PlayerMuseumEntries");

            migrationBuilder.DropColumn(
                name: "Curation",
                table: "Players");
        }
    }
}
