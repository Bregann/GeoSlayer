using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeoSlayer.Domain.Database.Migrations
{
    /// <inheritdoc />
    public partial class WidenPlayerXpToLong : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "Xp",
                table: "Players",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Xp",
                table: "Players",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");
        }
    }
}
