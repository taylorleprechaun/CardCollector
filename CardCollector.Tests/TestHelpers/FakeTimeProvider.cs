namespace CardCollector.Tests.TestHelpers
{
    /// <summary>A clock that stands still until a test moves it, for code that reads the time through <see cref="TimeProvider"/>.</summary>
    internal sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public FakeTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public void Advance(TimeSpan by) => _utcNow += by;

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
