# Card Collector

A personal Yu-Gi-Oh card collection tracker built with ASP.NET Core Razor Pages.

## Features

- **Dashboard** &mdash; collection progress overview with stats, including current market value from the most recent snapshot
- **Browse** &mdash; search and filter all ~12,000+ cards; link through to individual card detail
- **Card detail** &mdash; view all printings for a card and add them to your collection
- **Collection** &mdash; owned cards grouped by printing, searchable and paginated
- **Discover** &mdash; surface a random card without a preferred printing and pick one
- **Wishlist** &mdash; preferred versions you haven&rsquo;t ordered or owned yet, with search and sort
- **New Printings** &mdash; cards where a newer set printing exists for your preferred version; dismiss or upgrade in one click
- **Orders** &mdash; manage cards you&rsquo;ve ordered and mark them as received
- **Stats** &mdash; breakdown of your collection by rarity, set, and acquisition method; calculate current market value with a live pricing fetch and track value over time with historical snapshots
- **Export** &mdash; download your collection or wishlist as a CSV

## Tech Stack

- **Framework**: ASP.NET Core Razor Pages (.NET 10)
- **ORM**: Entity Framework Core 9 + SQLite
- **JSON parsing**: Newtonsoft.Json 13
- **UI**: Bootstrap 5 + Chart.js 4 (CDN)

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### Setup

1. Clone the repository.

2. Run the app:

   ```bash
   dotnet run --project CardCollector
   ```

   On first run the app fetches card data from yaml-yugi and card images from the YGO Pro Deck API, caching both locally. The SQLite database (`Data/collection.db`) is created automatically.

3. Open `https://localhost:5001` in your browser.

## Project Structure

```
CardCollector/
├── Data/
│   ├── AppDBContext.cs
│   └── Models/              # EF entities and enums
├── DTO/                     # Card data structures (yaml-yugi card data + YGOProDeck image data)
├── Extensions/              # EnumExtensions (.GetDisplayName())
├── Pages/                   # Razor Pages (Dashboard, Browse, Card, Collection, Discover, Wishlist, NewPrintings, Orders, Export)
├── Repository/              # Data access (CardDataRepository, CollectionRepository, PreferredVersionRepository)
├── Services/                # CardService (joins JSON + SQLite), PricingService (live TCGPlayer prices)
├── ViewModels/              # Page-specific view models
└── wwwroot/js/              # Page-specific JavaScript
```

## Data Notes

- Card data (names, stats, sets, rarities) is fetched from the [yaml-yugi](https://github.com/DawnbrandBots/yaml-yugi) dataset on startup and cached locally for 7 days (configurable via `CardDataSettings:CacheTtlDays`). Card images are fetched separately from the YGO Pro Deck API and cached for 30 days (`CardDataSettings:ImageCacheTtlDays`). Both caches fall back to stale data if the upstream source is unreachable.
- Speed Duel sets are excluded from all card data at load time.
- Card images are loaded from CDN URLs; no images are stored locally.
- Live pricing data is fetched from the YGO Pro Deck pricing endpoint per card when needed.
- Collection value is calculated on demand from the Stats page, which fetches live prices for all owned cards (up to 5 concurrent requests) and persists the results as dated snapshots. The Dashboard displays the value from the most recent snapshot.
