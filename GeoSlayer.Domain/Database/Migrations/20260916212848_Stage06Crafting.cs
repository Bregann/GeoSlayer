using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using System;

#nullable disable

namespace GeoSlayer.Domain.Database.Migrations
{
    /// <inheritdoc />
    public partial class Stage06Crafting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Items",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Slot = table.Column<int>(type: "integer", nullable: false),
                    Modifier = table.Column<int>(type: "integer", nullable: false),
                    ModifierValue = table.Column<double>(type: "double precision", nullable: false),
                    Tier = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Items", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlayerItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    IsEquipped = table.Column<bool>(type: "boolean", nullable: false),
                    ClaimId = table.Column<int>(type: "integer", nullable: true),
                    AcquiredUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerItems_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PlayerItems_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerItems_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Recipes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    SkillType = table.Column<int>(type: "integer", nullable: false),
                    LevelRequired = table.Column<int>(type: "integer", nullable: false),
                    DurationSeconds = table.Column<double>(type: "double precision", nullable: false),
                    XpReward = table.Column<double>(type: "double precision", nullable: false),
                    OutputMaterialId = table.Column<int>(type: "integer", nullable: true),
                    OutputItemId = table.Column<int>(type: "integer", nullable: true),
                    OutputQuantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recipes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Recipes_Items_OutputItemId",
                        column: x => x.OutputItemId,
                        principalTable: "Items",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Recipes_Materials_OutputMaterialId",
                        column: x => x.OutputMaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PlayerCrafts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    RecipeId = table.Column<int>(type: "integer", nullable: false),
                    RecipeKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StartedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletesUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Collected = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerCrafts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerCrafts_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerCrafts_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeInputs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RecipeId = table.Column<int>(type: "integer", nullable: false),
                    MaterialId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeInputs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeInputs_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RecipeInputs_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Items_Key",
                table: "Items",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerCrafts_PlayerId_Collected",
                table: "PlayerCrafts",
                columns: new[] { "PlayerId", "Collected" });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerCrafts_RecipeId",
                table: "PlayerCrafts",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerItems_ClaimId",
                table: "PlayerItems",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerItems_ItemId",
                table: "PlayerItems",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerItems_PlayerId_ItemId",
                table: "PlayerItems",
                columns: new[] { "PlayerId", "ItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecipeInputs_MaterialId",
                table: "RecipeInputs",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeInputs_RecipeId_MaterialId",
                table: "RecipeInputs",
                columns: new[] { "RecipeId", "MaterialId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_Key",
                table: "Recipes",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_OutputItemId",
                table: "Recipes",
                column: "OutputItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_OutputMaterialId",
                table: "Recipes",
                column: "OutputMaterialId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerCrafts");

            migrationBuilder.DropTable(
                name: "PlayerItems");

            migrationBuilder.DropTable(
                name: "RecipeInputs");

            migrationBuilder.DropTable(
                name: "Recipes");

            migrationBuilder.DropTable(
                name: "Items");
        }
    }
}
