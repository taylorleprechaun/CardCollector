namespace CardCollector.Repository
{
    /// <summary>
    /// Runs a unit of work inside a single database transaction, committing on success and
    /// rolling back if the operation throws.
    /// </summary>
    public interface IUnitOfWork
    {
        Task ExecuteInTransactionAsync(Func<Task> operation);
    }
}
