using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using System;

#nullable disable

namespace GeoSlayer.Domain.Database.Migrations
{
    /// <inheritdoc />
    public partial class Stage16CombatEncounters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EncounterDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    MinCombatLevel = table.Column<int>(type: "integer", nullable: false),
                    Tier = table.Column<int>(type: "integer", nullable: false),
                    IsTrainingGround = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncounterDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlayerEncounters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    DefinitionKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PoiId = table.Column<int>(type: "integer", nullable: false),
                    SpawnedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Won = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerEncounters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerEncounters_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerEncounters_PointsOfInterest_PoiId",
                        column: x => x.PoiId,
                        principalTable: "PointsOfInterest",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EncounterDefinitions_Key",
                table: "EncounterDefinitions",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerEncounters_PlayerId_PoiId_DefinitionKey",
                table: "PlayerEncounters",
                columns: new[] { "PlayerId", "PoiId", "DefinitionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerEncounters_PlayerId_ResolvedUtc",
                table: "PlayerEncounters",
                columns: new[] { "PlayerId", "ResolvedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerEncounters_PoiId",
                table: "PlayerEncounters",
                column: "PoiId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EncounterDefinitions");

            migrationBuilder.DropTable(
                name: "PlayerEncounters");
        }
    }
}
