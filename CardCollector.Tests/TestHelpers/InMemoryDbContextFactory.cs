using CardCollector.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CardCollector.Tests.TestHelpers
{
    // Fresh, isolated in-memory EF Core context per call — used to test Repository classes' query/grouping
    // logic directly without a real SQLite file or the ASP.NET pipeline.
    internal static class InMemoryDbContextFactory
    {
        public static AppDBContext Create()
        {
            var options = new DbContextOptionsBuilder<AppDBContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                // The InMemory provider doesn't support real transactions; without this it throws on
                // BeginTransactionAsync instead of treating it as a no-op the way UnitOfWork expects.
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            return new AppDBContext(options);
        }
    }
}
