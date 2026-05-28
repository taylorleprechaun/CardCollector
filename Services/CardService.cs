using CardCollector.Data.Models;
using CardCollector.DTO;
using CardCollector.Repository;
using CardCollector.ViewModels;

namespace CardCollector.Services
{
    public class CardService : ICardService
    {
        private readonly ICardDataRepository _cardDataRepository;
        private readonly ICollectionRepository _collectionRepository;

        public CardService(ICardDataRepository cardDataRepository, ICollectionRepository collectionRepository)
        {
            _cardDataRepository = cardDataRepository;
            _collectionRepository = collectionRepository;
        }

        public Card? GetCardByID(int cardID) => _cardDataRepository.GetCardByID(cardID);

        public async Task<DashboardStats> GetDashboardStatsAsync()
        {
            var totalArtworks = _cardDataRepository.GetAllArtworks().Count();
            var groups = await GetGroupedOwnedAsync();
            var ordered = await _collectionRepository.GetByStatusAsync(CollectionStatus.Ordered);

            return new DashboardStats
            {
                IncompleteSetCount = groups.Count(g => g.CompletionStatus == CollectionCompletionStatus.Incomplete),
                OwnedCount = groups.Count(g => g.CompletionStatus == CollectionCompletionStatus.Complete),
                OrderedCount = ordered.Count(),
                PlaceholderSetCount = groups.Count(g => g.CompletionStatus == CollectionCompletionStatus.Placeholder),
                TotalArtworks = totalArtworks
            };
        }

        public Task<IEnumerable<OrderEntryViewModel>> GetEnrichedOrdersAsync()
            => GetEnrichedByStatusAsync(CollectionStatus.Ordered);

        public Task<IEnumerable<OrderEntryViewModel>> GetEnrichedOwnedAsync()
            => GetEnrichedByStatusAsync(CollectionStatus.Owned);

        public async Task<IEnumerable<CollectionGroupViewModel>> GetGroupedOwnedAsync()
        {
            var entries = await GetEnrichedByStatusAsync(CollectionStatus.Owned);

            return entries
                .GroupBy(e => (e.CardName, e.ImageURLSmall, e.SetCode, e.SetName, e.RarityCode))
                .Select(g =>
                {
                    var first = g.First();
                    var withPrice = g.Where(e => e.PurchasePrice.HasValue).ToList();
                    decimal? totalCost = withPrice.Any()
                        ? withPrice.Sum(e => e.Quantity * e.PurchasePrice!.Value)
                        : null;

                    return new CollectionGroupViewModel
                    {
                        CardID = first.CardID,
                        CardName = first.CardName,
                        Entries = g.ToList(),
                        ImageID = first.ImageID,
                        ImageURLSmall = first.ImageURLSmall,
                        RarityCode = first.RarityCode,
                        SetCode = first.SetCode,
                        SetName = first.SetName,
                        TotalCost = totalCost,
                        TotalQuantity = g.Sum(e => e.Quantity)
                    };
                })
                .OrderBy(g => g.CardName)
                .ThenBy(g => g.SetCode)
                .ToList();
        }

        private async Task<IEnumerable<OrderEntryViewModel>> GetEnrichedByStatusAsync(CollectionStatus status)
        {
            var entries = await _collectionRepository.GetByStatusAsync(status);
            var viewModels = new List<OrderEntryViewModel>();

            foreach (var entry in entries)
            {
                var card = _cardDataRepository.GetCardByID(entry.CardID);
                var image = card?.CardImages?.FirstOrDefault(i => i.ID == entry.ImageID);
                var set = card?.CardSets?.FirstOrDefault(s => s.Code == entry.SetCode);

                viewModels.Add(new OrderEntryViewModel
                {
                    AcquisitionMethod = entry.AcquisitionMethod,
                    CardID = entry.CardID,
                    CardName = card?.Name ?? "Unknown",
                    Condition = entry.Condition,
                    DateCreated = entry.DateCreated,
                    Edition = entry.Edition,
                    EntryID = entry.ID,
                    ImageID = entry.ImageID,
                    ImageURLSmall = image?.ImageURLSmall ?? string.Empty,
                    IsPlaceholder = entry.IsPlaceholder,
                    PurchaseDate = entry.PurchaseDate,
                    PurchasePrice = entry.PurchasePrice,
                    Quantity = entry.Quantity,
                    RarityCode = set?.RarityCode ?? string.Empty,
                    SetCode = entry.SetCode,
                    SetName = set?.Name ?? entry.SetCode
                });
            }

            return viewModels;
        }

        public async Task<PagedResult<CardListItemViewModel>> SearchCardsAsync(string? query, int page, int pageSize)
        {
            var all = _cardDataRepository.GetAllCards().OrderBy(c => c.Name).AsEnumerable();

            if (!string.IsNullOrWhiteSpace(query))
                all = all.Where(c => c.Name.Contains(query, StringComparison.OrdinalIgnoreCase));

            var totalCount = all.Count();
            var slice = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            var cardIDs = slice.Select(c => c.ID).ToList();
            var statusMap = await _collectionRepository.GetStatusByCardIDsAsync(cardIDs);
            var placeholderIDs = await _collectionRepository.GetPlaceholderCardIDsAsync(cardIDs);

            var items = slice.Select(c => new CardListItemViewModel
            {
                Attribute = c.Attribute ?? string.Empty,
                CardID = c.ID,
                CardType = c.CardType ?? string.Empty,
                ImageURLSmall = c.CardImages?.FirstOrDefault()?.ImageURLSmall ?? string.Empty,
                IsPlaceholder = placeholderIDs.Contains(c.ID),
                Name = c.Name ?? string.Empty,
                Status = statusMap.TryGetValue(c.ID, out var s) ? s : null,
                Type = c.Type ?? string.Empty
            }).ToList();

            return new PagedResult<CardListItemViewModel>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task<(Card Card, Image Image)?> GetRandomUncollectedAsync()
        {
            var collectedImageIDs = (await _collectionRepository.GetCollectedImageIDsAsync()).ToHashSet();

            var uncollected = _cardDataRepository.GetAllArtworks()
                .Where(a => !collectedImageIDs.Contains(a.Image.ID))
                .ToList();

            if (uncollected.Count == 0)
                return null;

            var index = Random.Shared.Next(uncollected.Count);
            return uncollected[index];
        }
    }
}
