using System;
using GeoSlayer.Domain.Enums;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GeoSlayer.Domain.Database.Migrations
{
    /// <inheritdoc />
    public partial class Stage02ProgressionCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Xp",
                table: "Players",
                newName: "AdventurerXp");

            // EF's scaffolder guessed this as a rename of "Level" -> "RespecCount".
            // It is not: Level is a derived cache of Xp, and RespecCount is new state that
            // must start at zero.  Drop the old column and recompute the level below.
            migrationBuilder.DropColumn(
                name: "Level",
                table: "Players");

            migrationBuilder.AddColumn<int>(
                name: "RespecCount",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AdventurerLevel",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "BonusPointsEarned",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BonusPointsSpent",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "PlayerSkills",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    SkillType = table.Column<int>(type: "integer", nullable: false),
                    Xp = table.Column<long>(type: "bigint", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    UnlockedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerSkills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerSkills_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerUpgrades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    UpgradeKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerUpgrades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerUpgrades_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UnlockDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AdventurerLevel = table.Column<int>(type: "integer", nullable: false),
                    UnlockType = table.Column<int>(type: "integer", nullable: false),
                    Payload = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnlockDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UpgradeDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    MaxRank = table.Column<int>(type: "integer", nullable: false),
                    CostCurve = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EffectPerRank = table.Column<double>(type: "double precision", nullable: false),
                    MinAdventurerLevel = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UpgradeDefinitions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerSkills_PlayerId_SkillType",
                table: "PlayerSkills",
                columns: new[] { "PlayerId", "SkillType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerUpgrades_PlayerId_UpgradeKey",
                table: "PlayerUpgrades",
                columns: new[] { "PlayerId", "UpgradeKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnlockDefinitions_AdventurerLevel",
                table: "UnlockDefinitions",
                column: "AdventurerLevel");

            migrationBuilder.CreateIndex(
                name: "IX_UnlockDefinitions_AdventurerLevel_Payload",
                table: "UnlockDefinitions",
                columns: new[] { "AdventurerLevel", "Payload" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UpgradeDefinitions_Key",
                table: "UpgradeDefinitions",
                column: "Key",
                unique: true);

            // ── One-off rescale onto the Adventurer curve (Stage 02 task 1) ──────────
            //
            // Before this migration, Player.Xp was raw Exploration XP: every revealed cell
            // paid its full 2 XP into the one global pool.  That pool is now the Adventurer
            // pool, which receives only GLOBAL_XP_RATIO (0.25) of skill XP, so existing
            // balances are 4x too large and would leave players far up the unlock ladder
            // they never climbed.
            //
            // The old total is preserved as Exploration skill XP, then the Adventurer
            // balance is rescaled to the 0.25 cut.  A player's visible progress is
            // therefore unchanged in the skill they actually earned it in.
            migrationBuilder.Sql($@"
                INSERT INTO ""PlayerSkills"" (""PlayerId"", ""SkillType"", ""Xp"", ""Level"", ""UnlockedAtUtc"")
                SELECT p.""Id"", {(int)SkillType.Exploration}, p.""AdventurerXp"", 1, NOW() AT TIME ZONE 'UTC'
                FROM ""Players"" p
                WHERE p.""AdventurerXp"" > 0;
            ");

            migrationBuilder.Sql(@"
                UPDATE ""Players""
                SET ""AdventurerXp"" = FLOOR(""AdventurerXp"" * 0.25)
                WHERE ""AdventurerXp"" > 0;
            ");

            // Both cached levels are derived, so recompute rather than trusting the old
            // value.  The RS curve has no closed form, so this walks the same cumulative
            // sum the C# XpCurve uses: XP(L) = floor( (1/4) * sum(n<L) floor(n + 300*2^(n/7)) ).
            migrationBuilder.Sql(@"
                WITH RECURSIVE curve(level, points, cumulative) AS (
                    SELECT 1, 0::numeric, 0::bigint
                    UNION ALL
                    SELECT level + 1,
                           points + FLOOR(level + 300 * POWER(2, level / 7.0)),
                           FLOOR((points + FLOOR(level + 300 * POWER(2, level / 7.0))) / 4)::bigint
                    FROM curve
                    WHERE level < 200
                )
                UPDATE ""Players"" p
                SET ""AdventurerLevel"" = COALESCE((
                        SELECT MAX(c.level) FROM curve c WHERE c.cumulative <= p.""AdventurerXp""
                    ), 1);
            ");

            migrationBuilder.Sql(@"
                WITH RECURSIVE curve(level, points, cumulative) AS (
                    SELECT 1, 0::numeric, 0::bigint
                    UNION ALL
                    SELECT level + 1,
                           points + FLOOR(level + 300 * POWER(2, level / 7.0)),
                           FLOOR((points + FLOOR(level + 300 * POWER(2, level / 7.0))) / 4)::bigint
                    FROM curve
                    WHERE level < 200
                )
                UPDATE ""PlayerSkills"" s
                SET ""Level"" = COALESCE((
                        SELECT MAX(c.level) FROM curve c WHERE c.cumulative <= s.""Xp""
                    ), 1);
            ");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerSkills");

            migrationBuilder.DropTable(
                name: "PlayerUpgrades");

            migrationBuilder.DropTable(
                name: "UnlockDefinitions");

            migrationBuilder.DropTable(
                name: "UpgradeDefinitions");

            migrationBuilder.DropColumn(
                name: "AdventurerLevel",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "BonusPointsEarned",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "BonusPointsSpent",
                table: "Players");

            migrationBuilder.RenameColumn(
                name: "RespecCount",
                table: "Players",
                newName: "Level");

            migrationBuilder.RenameColumn(
                name: "AdventurerXp",
                table: "Players",
                newName: "Xp");
        }
    }
}
