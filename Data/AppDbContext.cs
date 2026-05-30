using CardCollector.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CardCollector.Data
{
    public class AppDBContext : DbContext
    {
        public AppDBContext(DbContextOptions<AppDBContext> options) : base(options) { }

        public DbSet<CollectionEntry> CollectionEntries { get; set; }

        public DbSet<PreferredVersion> PreferredVersions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CollectionEntry>(entity =>
            {
                entity.HasKey(e => e.ID);
                entity.HasIndex(e => new { e.ImageID, e.SetCode });
                entity.Property(e => e.AcquisitionMethod).HasConversion<string>();
                entity.Property(e => e.Condition).HasConversion<string>();
                entity.Property(e => e.Edition).HasConversion<string>();
                entity.Property(e => e.Status).HasConversion<string>();
            });

            modelBuilder.Entity<PreferredVersion>(entity =>
            {
                entity.HasKey(e => e.ID);
                entity.HasIndex(e => e.ImageID).IsUnique();
            });
        }
    }
}
