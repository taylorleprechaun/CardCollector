using CardCollector.Services;

namespace CardCollector.Tests.Services
{
    /// <summary>A warm-up service under test, with the token that stops it and the card-data load it is waiting on.</summary>
    internal sealed record WarmupTestContext(CatalogWarmupHostedService Service, CancellationTokenSource Started, TaskCompletionSource CardDataLoad);
}
