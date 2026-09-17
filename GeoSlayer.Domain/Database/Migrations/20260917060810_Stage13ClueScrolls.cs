using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GeoSlayer.Domain.Database.Migrations
{
    /// <inheritdoc />
    public partial class Stage13ClueScrolls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlayerClueScrolls",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    Tier = table.Column<int>(type: "integer", nullable: false),
                    CurrentStep = table.Column<int>(type: "integer", nullable: false),
                    StartedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SkipUsed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerClueScrolls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerClueScrolls_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClueSteps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ScrollId = table.Column<int>(type: "integer", nullable: false),
                    StepIndex = table.Column<int>(type: "integer", nullable: false),
                    StepType = table.Column<int>(type: "integer", nullable: false),
                    TargetPoiId = table.Column<int>(type: "integer", nullable: true),
                    TargetSkill = table.Column<int>(type: "integer", nullable: true),
                    TargetLat = table.Column<double>(type: "double precision", nullable: true),
                    TargetLng = table.Column<double>(type: "double precision", nullable: true),
                    TargetRadius = table.Column<double>(type: "double precision", nullable: true),
                    RiddleText = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    SolvedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WasSkipped = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClueSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClueSteps_PlayerClueScrolls_ScrollId",
                        column: x => x.ScrollId,
                        principalTable: "PlayerClueScrolls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClueSteps_ScrollId_StepIndex",
                table: "ClueSteps",
                columns: new[] { "ScrollId", "StepIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerClueScrolls_PlayerId_Tier_CompletedUtc",
                table: "PlayerClueScrolls",
                columns: new[] { "PlayerId", "Tier", "CompletedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClueSteps");

            migrationBuilder.DropTable(
                name: "PlayerClueScrolls");
        }
    }
}
