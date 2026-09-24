namespace CardCollector.Extensions
{
    public static class HttpClientExtensions
    {
        /// <summary>Identifies the app to the card, price and banlist sources it calls.</summary>
        public const string USER_AGENT = "CardCollector/1.0 (personal Yu-Gi-Oh collection tracker)";

        /// <summary>Applies the settings every named client shares: the request timeout and the app's User-Agent.</summary>
        public static HttpClient ApplyAppDefaults(this HttpClient client, TimeSpan timeout)
        {
            if (client is null) throw new ArgumentNullException(nameof(client));

            client.Timeout = timeout;
            client.DefaultRequestHeaders.Add("User-Agent", USER_AGENT);
            return client;
        }
    }
}
