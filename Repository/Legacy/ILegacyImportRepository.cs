namespace CardCollector.Repository.Legacy
{
    /// <summary>Writes migrated rows into the app's (new) database.</summary>
    public interface ILegacyImportRepository
    {
        /// <summary>Stages rows for insertion, keeping the ids they had in the legacy database.</summary>
        void AddRange<TEntity>(IEnumerable<TEntity> entities) where TEntity : class;

        /// <summary>
        /// Copies a table that needs no mapping straight from the legacy database, column for column (the columns both
        /// tables have), and returns the number of rows copied.
        /// </summary>
        Task<int> CopyTableAsync(string legacyDatabasePath, string table, CancellationToken cancellationToken = default);

        /// <summary>Creates the database with the full current schema when it doesn't exist yet.</summary>
        Task EnsureCreatedAsync(CancellationToken cancellationToken = default);

        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
