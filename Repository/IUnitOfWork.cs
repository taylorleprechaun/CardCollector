namespace CardCollector.Repository
{
    /// <summary>
    /// Runs a unit of work inside a single database transaction, committing on success and
    /// rolling back if the operation throws.
    /// </summary>
    public interface IUnitOfWork
    {
        /// <summary>
        /// Runs <paramref name="operation"/> in a transaction. The token cancels starting and committing it; a failed or
        /// cancelled operation is always rolled back.
        /// </summary>
        Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken = default);
    }
}
