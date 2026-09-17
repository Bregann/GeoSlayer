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
        public DbSet<Claim> Claims { get; set; } = null!;
        public DbSet<Worker> Workers { get; set; } = null!;
        public DbSet<Recipe> Recipes { get; set; } = null!;
        public DbSet<RecipeInput> RecipeInputs { get; set; } = null!;
        public DbSet<Item> Items { get; set; } = null!;
        public DbSet<PlayerItem> PlayerItems { get; set; } = null!;
        public DbSet<PlayerCraft> PlayerCrafts { get; set; } = null!;
        public DbSet<MuseumEntryDefinition> MuseumEntryDefinitions { get; set; } = null!;
        public DbSet<PlayerMuseumEntry> PlayerMuseumEntries { get; set; } = null!;
        public DbSet<GeoRegion> GeoRegions { get; set; } = null!;
        public DbSet<PlayerClueScroll> PlayerClueScrolls { get; set; } = null!;
        public DbSet<ClueStep> ClueSteps { get; set; } = null!;
        public DbSet<WorkerExpedition> WorkerExpeditions { get; set; } = null!;
        public DbSet<BankedTransit> BankedTransits { get; set; } = null!;
        public DbSet<PatrolRoute> PatrolRoutes { get; set; } = null!;
        public DbSet<PatrolWaypoint> PatrolWaypoints { get; set; } = null!;
        public DbSet<DistrictDefinition> DistrictDefinitions { get; set; } = null!;
        public DbSet<ResourceSurge> ResourceSurges { get; set; } = null!;
        public DbSet<EncounterDefinition> EncounterDefinitions { get; set; } = null!;
        public DbSet<PlayerEncounter> PlayerEncounters { get; set; } = null!;

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

            modelBuilder.Entity<Claim>(entity =>
            {
                entity.HasIndex(e => e.PlayerId);

                // One Claim per centre cell per player — the overlap check guards the
                // wider case, but this makes a duplicate impossible at the storage layer.
                entity.HasIndex(e => new { e.PlayerId, e.CentreGridLat, e.CentreGridLng })
                      .IsUnique();
            });

            modelBuilder.Entity<Worker>(entity =>
            {
                entity.HasIndex(e => e.PlayerId);

                // Deleting a Claim must not delete the worker standing on it — it should
                // simply become unassigned.
                entity.HasOne(e => e.Claim)
                      .WithMany()
                      .HasForeignKey(e => e.ClaimId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<Recipe>(entity =>
            {
                entity.HasIndex(e => e.Key).IsUnique();
            });

            modelBuilder.Entity<RecipeInput>(entity =>
            {
                entity.HasIndex(e => new { e.RecipeId, e.MaterialId }).IsUnique();
            });

            modelBuilder.Entity<Item>(entity =>
            {
                entity.HasIndex(e => e.Key).IsUnique();
            });

            modelBuilder.Entity<PlayerItem>(entity =>
            {
                entity.HasIndex(e => new { e.PlayerId, e.ItemId }).IsUnique();

                // Removing a Claim must unplace its buildings, not delete them.
                entity.HasOne(e => e.Claim)
                      .WithMany()
                      .HasForeignKey(e => e.ClaimId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<PlayerCraft>(entity =>
            {
                entity.HasIndex(e => new { e.PlayerId, e.Collected });
            });

            modelBuilder.Entity<MuseumEntryDefinition>(entity =>
            {
                entity.HasIndex(e => e.Key).IsUnique();
                entity.HasIndex(e => e.Wing);
            });

            modelBuilder.Entity<PlayerMuseumEntry>(entity =>
            {
                // One plinth per player per entry. The unique index is what makes
                // "first-find counts" safe under a concurrent double-find.
                entity.HasIndex(e => new { e.PlayerId, e.EntryKey }).IsUnique();
            });

            modelBuilder.Entity<GeoRegion>(entity =>
            {
                // Shared across players: one lookup per cell, ever.
                entity.HasIndex(e => new { e.CellLat, e.CellLng }).IsUnique();
                entity.HasIndex(e => e.RegionKey);
            });

            modelBuilder.Entity<PlayerClueScroll>(entity =>
            {
                entity.HasIndex(e => new { e.PlayerId, e.Tier, e.CompletedUtc });
            });

            modelBuilder.Entity<ClueStep>(entity =>
            {
                entity.HasIndex(e => new { e.ScrollId, e.StepIndex }).IsUnique();
            });

            modelBuilder.Entity<WorkerExpedition>(entity =>
            {
                entity.HasIndex(e => new { e.PlayerId, e.Collected });
                entity.HasIndex(e => e.WorkerId);
            });

            modelBuilder.Entity<BankedTransit>(entity =>
            {
                // One banked row per cell per player: passing the same spot twice on one
                // commute should not bank it twice.
                entity.HasIndex(e => new { e.PlayerId, e.GridLat, e.GridLng }).IsUnique();
                entity.HasIndex(e => new { e.PlayerId, e.Redeemed });
            });

            modelBuilder.Entity<PatrolRoute>(entity =>
            {
                entity.HasIndex(e => e.PlayerId);
            });

            modelBuilder.Entity<PatrolWaypoint>(entity =>
            {
                entity.HasIndex(e => new { e.RouteId, e.Sequence }).IsUnique();
            });

            modelBuilder.Entity<DistrictDefinition>(entity =>
            {
                entity.HasIndex(e => e.Key).IsUnique();
            });

            modelBuilder.Entity<ResourceSurge>(entity =>
            {
                // One active surge per region cell — shared across players, since a surge
                // is a property of a place.
                entity.HasIndex(e => new { e.CellLat, e.CellLng, e.EndsUtc });
            });

            modelBuilder.Entity<EncounterDefinition>(entity =>
            {
                entity.HasIndex(e => e.Key).IsUnique();
            });

            modelBuilder.Entity<PlayerEncounter>(entity =>
            {
                // One encounter per definition per POI per player: the spawner is
                // deterministic and re-runs on every sync, so this is what stops a
                // repeated sync stacking duplicates of the same fight.
                entity.HasIndex(e => new { e.PlayerId, e.PoiId, e.DefinitionKey })
                      .IsUnique();

                // The open-encounters query: mine, unresolved.
                entity.HasIndex(e => new { e.PlayerId, e.ResolvedUtc });

                entity.HasOne(e => e.Player)
                      .WithMany()
                      .HasForeignKey(e => e.PlayerId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Poi)
                      .WithMany()
                      .HasForeignKey(e => e.PoiId)
                      .OnDelete(DeleteBehavior.Cascade);
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
