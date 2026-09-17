using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using System;

#nullable disable

namespace GeoSlayer.Domain.Database.Migrations
{
    /// <inheritdoc />
    public partial class Stage04SkillTraining : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlayerPoiVisits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    PoiId = table.Column<int>(type: "integer", nullable: false),
                    VisitCount = table.Column<int>(type: "integer", nullable: false),
                    TotalVisits = table.Column<int>(type: "integer", nullable: false),
                    LastVisitUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FirstVisitUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerPoiVisits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerPoiVisits_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerPoiVisits_PointsOfInterest_PoiId",
                        column: x => x.PoiId,
                        principalTable: "PointsOfInterest",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SkillDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SkillType = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Icon = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    UnlockLevel = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkillDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SkillTerrainMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SkillType = table.Column<int>(type: "integer", nullable: false),
                    Terrain = table.Column<int>(type: "integer", nullable: false),
                    XpPerCell = table.Column<double>(type: "double precision", nullable: false),
                    YieldMultiplier = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkillTerrainMappings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerPoiVisits_PlayerId",
                table: "PlayerPoiVisits",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerPoiVisits_PlayerId_PoiId",
                table: "PlayerPoiVisits",
                columns: new[] { "PlayerId", "PoiId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerPoiVisits_PoiId",
                table: "PlayerPoiVisits",
                column: "PoiId");

            migrationBuilder.CreateIndex(
                name: "IX_SkillDefinitions_SkillType",
                table: "SkillDefinitions",
                column: "SkillType",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SkillTerrainMappings_SkillType_Terrain",
                table: "SkillTerrainMappings",
                columns: new[] { "SkillType", "Terrain" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerPoiVisits");

            migrationBuilder.DropTable(
                name: "SkillDefinitions");

            migrationBuilder.DropTable(
                name: "SkillTerrainMappings");
        }
    }
}
