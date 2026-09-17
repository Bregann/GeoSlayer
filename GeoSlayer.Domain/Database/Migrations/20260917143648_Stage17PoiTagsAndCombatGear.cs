using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeoSlayer.Domain.Database.Migrations
{
    /// <inheritdoc />
    public partial class Stage17PoiTagsAndCombatGear : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // defaultValueSql, not the scaffolded defaultValue: "" is not valid JSON and
            // Postgres rejects it on the cast to jsonb.
            //
            // A default is required at all, rather than cosmetic, because PointsOfInterest is
            // already populated in every environment and a NOT NULL column with no default
            // cannot be added to a non-empty table. Existing rows get an empty tag set, which
            // Cryptic generation reads as "no distinguishing detail" and falls back to a
            // Category step — so this is safe to run long before a re-import backfills tags.
            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "PointsOfInterest",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Tags",
                table: "PointsOfInterest");
        }
    }
}
