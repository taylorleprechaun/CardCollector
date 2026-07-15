using CardCollector.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CardCollector.Data
{
    public class AppDBContext : DbContext
    {
        public DbSet<CheckedOutCard> CheckedOutCards { get; set; }

        public DbSet<CollectionEntry> CollectionEntries { get; set; }

        public DbSet<CollectionEntryValueSnapshot> CollectionEntryValueSnapshots { get; set; }

        public DbSet<CollectionValueSnapshot> CollectionValueSnapshots { get; set; }

        public DbSet<DismissedNewPrinting> DismissedNewPrintings { get; set; }

        public DbSet<IgnoredCard> IgnoredCards { get; set; }

        public DbSet<PreferredVersion> PreferredVersions { get; set; }

        public AppDBContext(DbContextOptions<AppDBContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CheckedOutCard>(entity =>
            {
                entity.HasKey(e => e.ID);
                entity.HasIndex(e => new { e.ImageID, e.SetCode, e.RarityName }).IsUnique();
            });

            modelBuilder.Entity<CollectionEntry>(entity =>
            {
                entity.HasKey(e => e.ID);
                entity.HasIndex(e => new { e.ImageID, e.SetCode });
                entity.Property(e => e.AcquisitionMethod).HasConversion<string>();
                entity.Property(e => e.Condition).HasConversion<string>();
                entity.Property(e => e.Edition).HasConversion<string>();
                entity.Property(e => e.Status).HasConversion<string>();
            });

            modelBuilder.Entity<CollectionEntryValueSnapshot>(entity =>
            {
                entity.HasKey(e => e.ID);
                entity.Property(e => e.Edition).HasConversion<string>();
            });

            modelBuilder.Entity<DismissedNewPrinting>(entity =>
            {
                entity.HasKey(e => e.ID);
                entity.HasIndex(e => new { e.CardID, e.SetCode, e.RarityName }).IsUnique();
            });

            modelBuilder.Entity<IgnoredCard>(entity =>
            {
                entity.HasKey(e => e.ID);
                entity.HasIndex(e => e.CardID).IsUnique();
            });

            modelBuilder.Entity<PreferredVersion>(entity =>
            {
                entity.HasKey(e => e.ID);
                entity.HasIndex(e => e.ImageID).IsUnique();
            });
        }
    }
}
