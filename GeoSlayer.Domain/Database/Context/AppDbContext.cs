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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.HasPostgresExtension("postgis");

            modelBuilder.Entity<Player>(entity =>
            {
                entity.HasIndex(p => p.UserId);
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
