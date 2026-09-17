using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using System;

#nullable disable

namespace GeoSlayer.Domain.Database.Migrations
{
    /// <inheritdoc />
    public partial class Stage14Retention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BankedTransits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    GridLat = table.Column<int>(type: "integer", nullable: false),
                    GridLng = table.Column<int>(type: "integer", nullable: false),
                    BankedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Weight = table.Column<double>(type: "double precision", nullable: false),
                    Redeemed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankedTransits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankedTransits_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DistrictDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    RequiredTerrains = table.Column<int>(type: "integer", nullable: false),
                    MinimumClaims = table.Column<int>(type: "integer", nullable: false),
                    OutputBonus = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DistrictDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PatrolRoutes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastCompletedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletionCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatrolRoutes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatrolRoutes_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ResourceSurges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CellLat = table.Column<double>(type: "double precision", nullable: false),
                    CellLng = table.Column<double>(type: "double precision", nullable: false),
                    TargetTerrain = table.Column<int>(type: "integer", nullable: true),
                    TargetSkill = table.Column<int>(type: "integer", nullable: true),
                    Multiplier = table.Column<double>(type: "double precision", nullable: false),
                    Description = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    StartsUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceSurges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkerExpeditions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    WorkerId = table.Column<int>(type: "integer", nullable: false),
                    PoiId = table.Column<int>(type: "integer", nullable: false),
                    PoiName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DispatchedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReturnsUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DistanceMetres = table.Column<double>(type: "double precision", nullable: false),
                    Collected = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkerExpeditions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkerExpeditions_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkerExpeditions_PointsOfInterest_PoiId",
                        column: x => x.PoiId,
                        principalTable: "PointsOfInterest",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkerExpeditions_Workers_WorkerId",
                        column: x => x.WorkerId,
                        principalTable: "Workers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PatrolWaypoints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RouteId = table.Column<int>(type: "integer", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatrolWaypoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatrolWaypoints_PatrolRoutes_RouteId",
                        column: x => x.RouteId,
                        principalTable: "PatrolRoutes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankedTransits_PlayerId_GridLat_GridLng",
                table: "BankedTransits",
                columns: new[] { "PlayerId", "GridLat", "GridLng" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankedTransits_PlayerId_Redeemed",
                table: "BankedTransits",
                columns: new[] { "PlayerId", "Redeemed" });

            migrationBuilder.CreateIndex(
                name: "IX_DistrictDefinitions_Key",
                table: "DistrictDefinitions",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PatrolRoutes_PlayerId",
                table: "PatrolRoutes",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PatrolWaypoints_RouteId_Sequence",
                table: "PatrolWaypoints",
                columns: new[] { "RouteId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResourceSurges_CellLat_CellLng_EndsUtc",
                table: "ResourceSurges",
                columns: new[] { "CellLat", "CellLng", "EndsUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkerExpeditions_PlayerId_Collected",
                table: "WorkerExpeditions",
                columns: new[] { "PlayerId", "Collected" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkerExpeditions_PoiId",
                table: "WorkerExpeditions",
                column: "PoiId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkerExpeditions_WorkerId",
                table: "WorkerExpeditions",
                column: "WorkerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankedTransits");

            migrationBuilder.DropTable(
                name: "DistrictDefinitions");

            migrationBuilder.DropTable(
                name: "PatrolWaypoints");

            migrationBuilder.DropTable(
                name: "ResourceSurges");

            migrationBuilder.DropTable(
                name: "WorkerExpeditions");

            migrationBuilder.DropTable(
                name: "PatrolRoutes");
        }
    }
}
