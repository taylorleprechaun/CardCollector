# Card Collector

A personal Yu-Gi-Oh card collection tracker built with ASP.NET Core Razor Pages.

## Features

- **Dashboard** &mdash; collection progress overview with stats
- **Browse** &mdash; search and filter all ~12,000+ cards from the YGO Pro Deck dataset; link through to individual card detail
- **Card detail** &mdash; view printings for a specific card and add them to your collection
- **Collection** &mdash; owned cards grouped by card, searchable and paginated
- **Discover** &mdash; surface a random uncollected artwork and pick a preferred printing
- **Wishlist** &mdash; preferred versions you haven&rsquo;t ordered or owned yet, with search and sort
- **Orders** &mdash; manage cards you&rsquo;ve ordered and mark them as received
- **Export** &mdash; download your collection or wishlist as a CSV

Collection is tracked at the artwork level &mdash; a card with multiple artworks counts as separate collectibles.

## Tech Stack

- **Framework**: ASP.NET Core Razor Pages (.NET 10)
- **ORM**: Entity Framework Core 9 + SQLite
- **JSON parsing**: Newtonsoft.Json 13
- **UI**: Bootstrap 5 (CDN)

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### Setup

1. Clone the repository.

2. Run the app:

   ```bash
   dotnet run --project CardCollector
   ```

   On first run the app fetches card data from the YGO Pro Deck API and caches it locally. The SQLite database (`Data/collection.db`) is created automatically.

3. Open `https://localhost:5001` in your browser.

## Project Structure

```
CardCollector/
├── Data/
│   ├── AppDBContext.cs
│   └── Models/              # EF entities and enums
├── DTO/                     # Card data deserialized from the YGOProDeck API
├── Extensions/              # EnumExtensions (.GetDisplayName())
├── Pages/                   # Razor Pages (Dashboard, Browse, Card, Collection, Discover, Wishlist, Orders, Export)
├── Repository/              # Data access (CardDataRepository, CollectionRepository, PreferredVersionRepository)
├── Services/                # CardService (joins JSON + SQLite), PricingService (live TCGPlayer prices)
├── ViewModels/              # Page-specific view models
└── wwwroot/js/              # Page-specific JavaScript
```

## Data Notes

- Card data is fetched from the YGO Pro Deck API on startup and cached locally for 7 days (configurable via `CardDataSettings:CacheTtlDays`). The app falls back to the stale cache if the API is unreachable.
- Speed Duel sets are excluded from all card data at load time.
- Card images are loaded from CDN URLs embedded in the JSON; no images are stored locally.
- Live pricing data is fetched from the YGO Pro Deck pricing endpoint per card when needed.
