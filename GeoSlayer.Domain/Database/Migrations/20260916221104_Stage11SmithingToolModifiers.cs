using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeoSlayer.Domain.Database.Migrations
{
    /// <inheritdoc />
    public partial class Stage11SmithingToolModifiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SecondaryModifier",
                table: "Items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SecondaryModifierValue",
                table: "Items",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SecondaryModifier",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "SecondaryModifierValue",
                table: "Items");
        }
    }
}
