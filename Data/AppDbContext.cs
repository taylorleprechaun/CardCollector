using CardCollector.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CardCollector.Data
{
    public class AppDBContext : DbContext
    {
        public AppDBContext(DbContextOptions<AppDBContext> options) : base(options) { }

        public DbSet<CheckedOutCard> CheckedOutCards { get; set; }

        public DbSet<CollectionEntry> CollectionEntries { get; set; }

        public DbSet<CollectionEntryValueSnapshot> CollectionEntryValueSnapshots { get; set; }

        public DbSet<CollectionValueSnapshot> CollectionValueSnapshots { get; set; }

        public DbSet<DismissedNewPrinting> DismissedNewPrintings { get; set; }

        public DbSet<Format> Formats { get; set; }

        public DbSet<FormatStrategy> FormatStrategies { get; set; }

        public DbSet<IgnoredCard> IgnoredCards { get; set; }

        public DbSet<PendingOrderLine> PendingOrderLines { get; set; }

        public DbSet<PreferredVersion> PreferredVersions { get; set; }

        public DbSet<WishlistValueSnapshot> WishlistValueSnapshots { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CheckedOutCard>(entity =>
            {
                entity.HasKey(e => e.ID);
                entity.HasIndex(e => new { e.CardID, e.SetCode, e.RarityName, e.PrintVariant }).IsUnique();
            });

            modelBuilder.Entity<CollectionEntry>(entity =>
            {
                entity.HasKey(e => e.ID);
                entity.HasIndex(e => new { e.CardID, e.SetCode });
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
                entity.HasIndex(e => new { e.CardID, e.SetCode, e.RarityName, e.PrintVariant }).IsUnique();
            });

            modelBuilder.Entity<Format>(entity =>
            {
                entity.HasKey(e => e.ID);
                entity.HasMany(e => e.Strategies)
                    .WithOne()
                    .HasForeignKey(s => s.FormatID)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<FormatStrategy>(entity =>
            {
                entity.HasKey(e => e.ID);
                entity.HasIndex(e => e.FormatID);
            });

            modelBuilder.Entity<IgnoredCard>(entity =>
            {
                entity.HasKey(e => e.ID);
                entity.HasIndex(e => e.CardID).IsUnique();
            });

            modelBuilder.Entity<PendingOrderLine>(entity =>
            {
                entity.HasKey(e => e.ID);
                entity.Property(e => e.AcquisitionMethod).HasConversion<string>();
                entity.Property(e => e.Condition).HasConversion<string>();
                entity.Property(e => e.Edition).HasConversion<string>();
            });

            modelBuilder.Entity<PreferredVersion>(entity =>
            {
                entity.HasKey(e => e.ID);
                entity.HasIndex(e => new { e.CardID, e.SetCode, e.RarityName, e.PrintVariant }).IsUnique();
            });
        }
    }
}
