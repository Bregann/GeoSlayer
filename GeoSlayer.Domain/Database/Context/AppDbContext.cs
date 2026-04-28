using GeoSlayer.Domain.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace GeoSlayer.Domain.Database.Context
{
    public partial class AppDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<EnvironmentalSetting> EnvironmentalSettings { get; set; } = null!;
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<UserRefreshToken> UserRefreshTokens { get; set; } = null!;
        public DbSet<Street> Streets { get; set; } = null!;
        public DbSet<Player> Players { get; set; } = null!;
        public DbSet<UserStreetProgress> UserStreetProgresses { get; set; } = null!;
        public DbSet<ImportedRegion> ImportedRegions { get; set; } = null!;
        public DbSet<PointOfInterest> PointsOfInterest { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.HasPostgresExtension("postgis");

            modelBuilder.Entity<Street>(entity =>
            {
                entity.HasIndex(s => s.Path)
                      .HasMethod("gist");

                entity.HasIndex(s => s.OsmId)
                      .IsUnique();
            });

            modelBuilder.Entity<Player>(entity =>
            {
                entity.HasIndex(p => p.Location)
                      .HasMethod("gist");
            });

            modelBuilder.Entity<UserStreetProgress>(entity =>
            {
                entity.HasIndex(e => new { e.PlayerId, e.StreetId })
                      .IsUnique();
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
