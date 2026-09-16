using GeoSlayer.Domain.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace GeoSlayer.Domain.Database.Context
{
    public partial class AppDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<EnvironmentalSetting> EnvironmentalSettings { get; set; } = null!;
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<UserRefreshToken> UserRefreshTokens { get; set; } = null!;
        public DbSet<Player> Players { get; set; } = null!;
        public DbSet<RevealedCell> RevealedCells { get; set; } = null!;
        public DbSet<ImportedRegion> ImportedRegions { get; set; } = null!;
        public DbSet<PointOfInterest> PointsOfInterest { get; set; } = null!;
        public DbSet<PlayerSkill> PlayerSkills { get; set; } = null!;
        public DbSet<PlayerUpgrade> PlayerUpgrades { get; set; } = null!;
        public DbSet<UnlockDefinition> UnlockDefinitions { get; set; } = null!;
        public DbSet<UpgradeDefinition> UpgradeDefinitions { get; set; } = null!;
        public DbSet<Material> Materials { get; set; } = null!;
        public DbSet<PlayerMaterial> PlayerMaterials { get; set; } = null!;
        public DbSet<CellTerrain> CellTerrains { get; set; } = null!;
        public DbSet<DropTableEntry> DropTableEntries { get; set; } = null!;
        public DbSet<SkillDefinition> SkillDefinitions { get; set; } = null!;
        public DbSet<SkillTerrainMapping> SkillTerrainMappings { get; set; } = null!;
        public DbSet<PlayerPoiVisit> PlayerPoiVisits { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.HasPostgresExtension("postgis");

            modelBuilder.Entity<Player>(entity =>
            {
                entity.HasIndex(p => p.UserId);
            });

            modelBuilder.Entity<PlayerSkill>(entity =>
            {
                // Row exists <=> skill unlocked, so this index is the unlock guarantee.
                entity.HasIndex(e => new { e.PlayerId, e.SkillType })
                      .IsUnique();
            });

            modelBuilder.Entity<PlayerUpgrade>(entity =>
            {
                entity.HasIndex(e => new { e.PlayerId, e.UpgradeKey })
                      .IsUnique();
            });

            modelBuilder.Entity<UnlockDefinition>(entity =>
            {
                entity.HasIndex(e => e.AdventurerLevel);

                // One rung per level per payload — re-seeding must not duplicate the ladder.
                entity.HasIndex(e => new { e.AdventurerLevel, e.Payload })
                      .IsUnique();
            });

            modelBuilder.Entity<UpgradeDefinition>(entity =>
            {
                entity.HasIndex(e => e.Key)
                      .IsUnique();
            });

            modelBuilder.Entity<Material>(entity =>
            {
                entity.HasIndex(e => e.Key).IsUnique();
                entity.HasIndex(e => new { e.Category, e.Tier });
            });

            modelBuilder.Entity<PlayerMaterial>(entity =>
            {
                entity.HasIndex(e => new { e.PlayerId, e.MaterialId }).IsUnique();
            });

            modelBuilder.Entity<CellTerrain>(entity =>
            {
                // Terrain is a property of the world, so one row per cell serves every
                // player — the unique index is what makes the cache correct under races.
                entity.HasIndex(e => new { e.GridLat, e.GridLng }).IsUnique();
            });

            modelBuilder.Entity<DropTableEntry>(entity =>
            {
                entity.HasIndex(e => e.Terrain);
                entity.HasIndex(e => new { e.Terrain, e.MaterialId }).IsUnique();
            });

            modelBuilder.Entity<SkillDefinition>(entity =>
            {
                entity.HasIndex(e => e.SkillType).IsUnique();
            });

            modelBuilder.Entity<SkillTerrainMapping>(entity =>
            {
                // One rate per (skill, terrain); a duplicate would make training
                // order-dependent.
                entity.HasIndex(e => new { e.SkillType, e.Terrain }).IsUnique();
            });

            modelBuilder.Entity<PlayerPoiVisit>(entity =>
            {
                entity.HasIndex(e => new { e.PlayerId, e.PoiId }).IsUnique();
                entity.HasIndex(e => e.PlayerId);
            });

            modelBuilder.Entity<RevealedCell>(entity =>
            {
                entity.HasIndex(e => new { e.PlayerId, e.GridLat, e.GridLng })
                      .IsUnique();

                entity.HasIndex(e => e.PlayerId);
            });

            modelBuilder.Entity<ImportedRegion>(entity =>
            {
                entity.HasIndex(e => new { e.CellLat, e.CellLng })
                      .IsUnique();
            });

            modelBuilder.Entity<PointOfInterest>(entity =>
            {
                entity.HasIndex(p => p.Location)
                      .HasMethod("gist");

                entity.HasIndex(p => new { p.OsmId, p.OsmType })
                      .IsUnique();
            });
        }
    }
}
