using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using System;

#nullable disable

namespace GeoSlayer.Domain.Database.Migrations
{
    /// <inheritdoc />
    public partial class Stage03MaterialsInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CellTerrains",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GridLat = table.Column<int>(type: "integer", nullable: false),
                    GridLng = table.Column<int>(type: "integer", nullable: false),
                    Terrain = table.Column<int>(type: "integer", nullable: false),
                    ClassifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CellTerrains", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Materials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Tier = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    SkillType = table.Column<int>(type: "integer", nullable: true),
                    StackCap = table.Column<int>(type: "integer", nullable: false),
                    IsUnique = table.Column<bool>(type: "boolean", nullable: false),
                    LevelRequired = table.Column<int>(type: "integer", nullable: false),
                    BaseGatherSeconds = table.Column<double>(type: "double precision", nullable: false),
                    XpPerUnit = table.Column<double>(type: "double precision", nullable: false),
                    DustPerOverflow = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Materials", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DropTableEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Terrain = table.Column<int>(type: "integer", nullable: false),
                    MaterialId = table.Column<int>(type: "integer", nullable: false),
                    Weight = table.Column<int>(type: "integer", nullable: false),
                    MinQuantity = table.Column<int>(type: "integer", nullable: false),
                    MaxQuantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DropTableEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DropTableEntries_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerMaterials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    MaterialId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerMaterials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerMaterials_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerMaterials_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CellTerrains_GridLat_GridLng",
                table: "CellTerrains",
                columns: new[] { "GridLat", "GridLng" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DropTableEntries_MaterialId",
                table: "DropTableEntries",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_DropTableEntries_Terrain",
                table: "DropTableEntries",
                column: "Terrain");

            migrationBuilder.CreateIndex(
                name: "IX_DropTableEntries_Terrain_MaterialId",
                table: "DropTableEntries",
                columns: new[] { "Terrain", "MaterialId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Materials_Category_Tier",
                table: "Materials",
                columns: new[] { "Category", "Tier" });

            migrationBuilder.CreateIndex(
                name: "IX_Materials_Key",
                table: "Materials",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerMaterials_MaterialId",
                table: "PlayerMaterials",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerMaterials_PlayerId_MaterialId",
                table: "PlayerMaterials",
                columns: new[] { "PlayerId", "MaterialId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CellTerrains");

            migrationBuilder.DropTable(
                name: "DropTableEntries");

            migrationBuilder.DropTable(
                name: "PlayerMaterials");

            migrationBuilder.DropTable(
                name: "Materials");
        }
    }
}
