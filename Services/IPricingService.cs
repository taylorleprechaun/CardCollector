namespace CardCollector.Services
{
    public interface IPricingService
    {
        Task<decimal?> GetPrintingPriceAsync(int cardID, string setCode, string rarityName);
    }
}
