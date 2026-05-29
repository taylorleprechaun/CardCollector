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
        private readonly IPreferredVersionRepository _preferredVersionRepository;

        public CardService(
            ICardDataRepository cardDataRepository,
            ICollectionRepository collectionRepository,
            IPreferredVersionRepository preferredVersionRepository)
        {
            _cardDataRepository = cardDataRepository;
            _collectionRepository = collectionRepository;
            _preferredVersionRepository = preferredVersionRepository;
        }

        public async Task<bool> AddEntryAsync(
            int cardID, int imageID, string setCode, CollectionStatus status,
            int quantity, CardCondition? condition, CardEdition? edition,
            AcquisitionMethod? acquisitionMethod, bool isPlaceholder,
            DateTime? purchaseDate, decimal? purchasePrice, decimal? marketPriceAtEntry = null)
        {
            if (await _collectionRepository.ExistsAsync(imageID, setCode))
                return false;

            var entry = new CollectionEntry
            {
                AcquisitionMethod = acquisitionMethod,
                CardID = cardID,
                Condition = condition,
                DateCreated = DateTime.UtcNow,
                DateModified = DateTime.UtcNow,
                Edition = edition,
                ImageID = imageID,
                IsPlaceholder = false,
                MarketPriceAtEntry = marketPriceAtEntry,
                PurchaseDate = purchaseDate,
                PurchasePrice = purchasePrice,
                Quantity = quantity < 1 ? 1 : quantity,
                SetCode = setCode,
                Status = status
            };

            await _collectionRepository.AddAsync(entry);
            return true;
        }

        public Card? GetCardByID(int cardID) => _cardDataRepository.GetCardByID(cardID);

        public IEnumerable<string> GetCardNameSuggestions(string query, int maxResults = 10) =>
            _cardDataRepository.GetAllCards()
                .Where(c => c.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c.Name)
                .Take(maxResults)
                .Select(c => c.Name);

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
            var entries = (await GetEnrichedByStatusAsync(CollectionStatus.Owned)).ToList();

            var imageIDs = entries.Select(e => e.ImageID).Distinct();
            var preferredVersions = await _preferredVersionRepository.GetByImageIDsAsync(imageIDs);

            return entries
                .GroupBy(e => (e.CardName, e.ImageURLSmall, e.SetCode, e.SetName, e.RarityCode))
                .Select(g =>
                {
                    var first = g.First();
                    var withPrice = g.Where(e => e.PurchasePrice.HasValue).ToList();
                    decimal? totalCost = withPrice.Any()
                        ? withPrice.Sum(e => e.Quantity * e.PurchasePrice!.Value)
                        : null;

                    var isPreferred = preferredVersions.TryGetValue(first.ImageID, out var pv)
                                      && pv.SetCode == first.SetCode;

                    return new CollectionGroupViewModel
                    {
                        CardID = first.CardID,
                        CardName = first.CardName,
                        Entries = g.ToList(),
                        ImageID = first.ImageID,
                        ImageURLSmall = first.ImageURLSmall,
                        IsPreferredVersion = isPreferred,
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

        public async Task<(Card Card, Image Image)?> GetRandomUncollectedAsync()
        {
            var preferredImageIDs = await _preferredVersionRepository.GetPreferredImageIDsAsync();

            var uncollected = _cardDataRepository.GetAllArtworks()
                .Where(a => !preferredImageIDs.Contains(a.Image.ID))
                .ToList();

            if (uncollected.Count == 0)
                return null;

            var index = Random.Shared.Next(uncollected.Count);
            return uncollected[index];
        }

        public async Task RemoveFromWishlistAsync(int imageID) =>
            await _preferredVersionRepository.DeleteAsync(imageID);

        public async Task SavePreferredVersionAsync(int cardID, int imageID, string setCode) =>
            await _preferredVersionRepository.AddOrUpdateAsync(cardID, imageID, setCode);

        public async Task<PagedResult<CollectionGroupViewModel>> SearchGroupedOwnedAsync(string? query, int page, int pageSize)
        {
            var allGroups = (await GetGroupedOwnedAsync()).ToList();

            var filtered = allGroups
                .Where(g => string.IsNullOrWhiteSpace(query) ||
                            g.CardName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            g.SetName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            g.SetCode.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            g.RarityCode.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var totalCount = filtered.Count;
            var items = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return new PagedResult<CollectionGroupViewModel>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task<PagedResult<CardListItemViewModel>> SearchCardsAsync(string? query, int page, int pageSize)
        {
            var filtered = _cardDataRepository.GetAllCards()
                .Where(c => string.IsNullOrWhiteSpace(query) ||
                            c.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c.Name)
                .ToList();

            var totalCount = filtered.Count;
            var slice = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

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

        public async Task<PagedResult<WishlistItemViewModel>> SearchWishlistAsync(string? query, int page, int pageSize)
        {
            var allItems = (await GetWishlistAsync()).ToList();

            var filtered = allItems
                .Where(item => string.IsNullOrWhiteSpace(query) ||
                               item.CardName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                               item.SetName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                               item.SetCode.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                               item.RarityName.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var totalCount = filtered.Count;
            var items = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return new PagedResult<WishlistItemViewModel>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task<IEnumerable<WishlistItemViewModel>> GetWishlistAsync()
        {
            var allPreferred = (await _preferredVersionRepository.GetAllAsync()).ToList();
            if (allPreferred.Count == 0)
                return [];

            var collectedPairs = await _collectionRepository.GetCollectedPairsAsync();

            var wishlistItems = allPreferred
                .Where(pv => !collectedPairs.Contains((pv.ImageID, pv.SetCode)))
                .ToList();

            var results = new List<WishlistItemViewModel>();
            foreach (var pv in wishlistItems)
            {
                var card = _cardDataRepository.GetCardByID(pv.CardID);
                var image = card?.CardImages?.FirstOrDefault(i => i.ID == pv.ImageID);
                var set = card?.CardSets?.FirstOrDefault(s => s.Code == pv.SetCode);

                results.Add(new WishlistItemViewModel
                {
                    CardID = pv.CardID,
                    CardName = card?.Name ?? "Unknown",
                    ImageID = pv.ImageID,
                    ImageURLSmall = image?.ImageURLSmall ?? string.Empty,
                    Price = set?.Price,
                    RarityCode = set?.RarityCode ?? string.Empty,
                    RarityName = set?.RarityName ?? string.Empty,
                    SetCode = pv.SetCode,
                    SetName = set?.Name ?? pv.SetCode
                });
            }

            return results.OrderBy(r => r.CardName).ThenBy(r => r.SetCode);
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
                    IsPlaceholder = false,
                    MarketPriceAtEntry = entry.MarketPriceAtEntry,
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
    }
}
