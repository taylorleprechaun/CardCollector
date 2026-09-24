namespace CardCollector.Extensions
{
    public static class HttpRequestExtensions
    {
        /// <summary>True when the request came from the app's own <c>fetch</c> calls, which send <c>X-Requested-With: XMLHttpRequest</c>.</summary>
        public static bool IsAjaxRequest(this HttpRequest request)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));

            return request.Headers.XRequestedWith == "XMLHttpRequest";
        }
    }
}
