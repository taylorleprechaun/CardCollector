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

        public DbSet<DeckCard> DeckCards { get; set; }

        public DbSet<Deck> Decks { get; set; }

        public DbSet<DismissedNewPrinting> DismissedNewPrintings { get; set; }

        public DbSet<Event> Events { get; set; }

        public DbSet<Format> Formats { get; set; }

        public DbSet<FormatStrategy> FormatStrategies { get; set; }

        public DbSet<IgnoredCard> IgnoredCards { get; set; }

        public DbSet<Match> Matches { get; set; }

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

            modelBuilder.Entity<Deck>(entity =>
            {
                entity.HasKey(e => e.ID);
                entity.HasMany(e => e.Cards)
                    .WithOne()
                    .HasForeignKey(c => c.DeckID)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<DeckCard>(entity =>
            {
                entity.HasKey(e => e.ID);
                entity.HasIndex(e => new { e.DeckID, e.CardID, e.Section }).IsUnique();
                entity.Property(e => e.Section).HasConversion<string>();
            });

            modelBuilder.Entity<DismissedNewPrinting>(entity =>
            {
                entity.HasKey(e => e.ID);
                entity.HasIndex(e => new { e.CardID, e.SetCode, e.RarityName, e.PrintVariant }).IsUnique();
            });

            modelBuilder.Entity<Event>(entity =>
            {
                entity.HasKey(e => e.ID);
                entity.HasIndex(e => e.Date);
                entity.Property(e => e.EventType).HasConversion<string>();
                entity.HasMany(e => e.Matches)
                    .WithOne()
                    .HasForeignKey(m => m.EventID)
                    .OnDelete(DeleteBehavior.Cascade);
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

            modelBuilder.Entity<Match>(entity =>
            {
                entity.HasKey(e => e.ID);
                entity.HasIndex(e => e.EventID);
                entity.Property(e => e.Result).HasConversion(new MatchResultConverter());
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
