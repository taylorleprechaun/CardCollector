namespace CardCollector.ViewModels
{
    /// <summary>The page-size rules every paged list shares.</summary>
    public static class Paging
    {
        public const int DEFAULT_PAGE_SIZE = 25;
        public const int MAX_PAGE_SIZE = 100;

        /// <summary>The page sizes a list offers to choose from.</summary>
        public static IReadOnlyList<int> PageSizeOptions { get; } = [10, 25, 50, 100];

        /// <summary>A page number of at least 1.</summary>
        public static int ClampPage(int page) => Math.Max(1, page);

        /// <summary>Any size from 1 to <see cref="MAX_PAGE_SIZE"/>; anything else becomes the default. For queries.</summary>
        public static int ClampPageSize(int pageSize) => pageSize is < 1 or > MAX_PAGE_SIZE ? DEFAULT_PAGE_SIZE : pageSize;

        /// <summary>One of <see cref="PageSizeOptions"/>; anything else becomes the default. For a page's query string.</summary>
        public static int NormalizePageSize(int pageSize) => PageSizeOptions.Contains(pageSize) ? pageSize : DEFAULT_PAGE_SIZE;
    }
}
