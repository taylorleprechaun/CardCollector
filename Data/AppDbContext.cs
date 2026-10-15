using CardCollector.Data.Models;
using CardCollector.Data.Models.Catalog;
using Microsoft.EntityFrameworkCore;

namespace CardCollector.Data
{
    public class AppDBContext : DbContext
    {
        public AppDBContext(DbContextOptions<AppDBContext> options) : base(options) { }

        public DbSet<CatalogCardAlias> CatalogCardAliases { get; set; }

        public DbSet<CatalogCardImage> CatalogCardImages { get; set; }

        public DbSet<CatalogCard> CatalogCards { get; set; }

        public DbSet<CatalogPrinting> CatalogPrintings { get; set; }

        public DbSet<CatalogPrintingSource> CatalogPrintingSources { get; set; }

        public DbSet<CatalogReviewItem> CatalogReviewItems { get; set; }

        public DbSet<CatalogSet> CatalogSets { get; set; }

        public DbSet<CatalogSyncRun> CatalogSyncRuns { get; set; }

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
            modelBuilder.Entity<CatalogCard>(entity =>
            {
                entity.HasKey(e => e.ID);
                entity.HasIndex(e => e.KonamiID).IsUnique();
                entity.HasIndex(e => e.Name);
                entity.Property(e => e.Status).HasConversion<string>();
                entity.HasMany(e => e.Aliases)
                    .WithOne()
                    .HasForeignKey(a => a.CatalogCardID)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasMany(e => e.Images)
                    .WithOne()
                    .HasForeignKey(i => i.CatalogCardID)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasMany(e => e.Printings)
                    .WithOne(p => p.Card)
                    .HasForeignKey(p => p.CatalogCardID)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<CatalogCardAlias>(entity =>
            {
                entity.HasKey(e => e.ID);
                entity.HasIndex(e => new { e.Source, e.Kind, e.Value, e.CatalogCardID }).IsUnique();
                entity.HasIndex(e => e.Value);
                entity.Property(e => e.Kind).HasConversion<string>();
                entity.Property(e => e.Source).HasConversion<string>();
            });

            modelBuilder.Entity<CatalogCardImage>(entity =>
            {
                entity.HasKey(e => e.ID);
                entity.HasIndex(e => new { e.CatalogCardID, e.ImageID }).IsUnique();
            });

            modelBuilder.Entity<CatalogPrinting>(entity =>
            {
                entity.HasKey(e => e.ID);
                entity.HasIndex(e => new { e.CatalogCardID, e.SetCode, e.RarityName, e.PrintVariant }).IsUnique();
                entity.HasIndex(e => e.SetCode);
                entity.Property(e => e.Status).HasConversion<string>();
                entity.HasOne(e => e.Set)
                    .WithMany()
                    .HasForeignKey(e => e.CatalogSetID)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasMany(e => e.Sources)
                    .WithOne()
                    .HasForeignKey(s => s.CatalogPrintingID)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<CatalogPrintingSource>(entity =>
            {
                entity.HasKey(e => e.ID);
                entity.HasIndex(e => new { e.Source, e.ExternalKey }).IsUnique();
                entity.Property(e => e.Source).HasConversion<string>();
            });

            modelBuilder.Entity<CatalogReviewItem>(entity =>
            {
                entity.HasKey(e => e.ID);
                entity.HasIndex(e => new { e.Kind, e.SubjectKey }).IsUnique();
                entity.HasIndex(e => e.Status);
                entity.Property(e => e.Kind).HasConversion<string>();
                entity.Property(e => e.Status).HasConversion<string>();
            });

            modelBuilder.Entity<CatalogSet>(entity =>
            {
                entity.HasKey(e => e.ID);
                entity.HasIndex(e => e.Prefix).IsUnique();
            });

            modelBuilder.Entity<CatalogSyncRun>(entity =>
            {
                entity.HasKey(e => e.ID);
                entity.Property(e => e.Status).HasConversion<string>();
                entity.Property(e => e.TCGPlayerStatus).HasConversion<string>();
                entity.Property(e => e.YamlYugiStatus).HasConversion<string>();
                entity.Property(e => e.YGOProDeckStatus).HasConversion<string>();
            });

            modelBuilder.Entity<CheckedOutCard>(entity =>
            {
                entity.HasKey(e => e.ID);
                entity.HasIndex(e => e.CatalogPrintingID).IsUnique();
                entity.HasOne(e => e.CatalogPrinting).WithMany().HasForeignKey(e => e.CatalogPrintingID).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<CollectionEntry>(entity =>
            {
                entity.HasKey(e => e.ID);
                entity.HasOne(e => e.CatalogPrinting).WithMany().HasForeignKey(e => e.CatalogPrintingID).OnDelete(DeleteBehavior.Restrict);
                entity.Property(e => e.AcquisitionMethod).HasConversion<string>();
                entity.Property(e => e.Condition).HasConversion<string>();
                entity.Property(e => e.Edition).HasConversion<string>();
                entity.Property(e => e.Status).HasConversion<string>();
            });

            modelBuilder.Entity<CollectionEntryValueSnapshot>(entity =>
            {
                entity.HasKey(e => e.ID);
                entity.HasIndex(e => e.CatalogCardID);
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
                entity.HasIndex(e => new { e.DeckID, e.PastedPasscode, e.Section }).IsUnique();
                entity.Property(e => e.Section).HasConversion<string>();
            });

            modelBuilder.Entity<DismissedNewPrinting>(entity =>
            {
                entity.HasKey(e => e.ID);
                entity.HasIndex(e => e.CatalogPrintingID).IsUnique();
                entity.HasOne(e => e.CatalogPrinting).WithMany().HasForeignKey(e => e.CatalogPrintingID).OnDelete(DeleteBehavior.Restrict);
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
                entity.HasIndex(e => e.CatalogCardID).IsUnique();
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
                entity.HasOne(e => e.CatalogPrinting).WithMany().HasForeignKey(e => e.CatalogPrintingID).OnDelete(DeleteBehavior.Restrict);
                entity.Property(e => e.AcquisitionMethod).HasConversion<string>();
                entity.Property(e => e.Condition).HasConversion<string>();
                entity.Property(e => e.Edition).HasConversion<string>();
            });

            modelBuilder.Entity<PreferredVersion>(entity =>
            {
                entity.HasKey(e => e.ID);
                entity.HasIndex(e => e.CatalogPrintingID).IsUnique();
                entity.HasOne(e => e.CatalogPrinting).WithMany().HasForeignKey(e => e.CatalogPrintingID).OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
