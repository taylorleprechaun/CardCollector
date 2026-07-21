# Card Collector

A personal Yu-Gi-Oh card collection tracker built with ASP.NET Core Razor Pages.

## Features

- **Dashboard** &mdash; collection progress overview with stats, including current market value from the most recent snapshot

**Catalog**
- **Browse** &mdash; search and filter all ~12,000+ cards; link through to individual card detail
- **Card detail** &mdash; view all printings for a card and add them to your collection
- **Discover** &mdash; surface a random card without a preferred printing and pick one
- **New Printings** &mdash; cards where a newer set printing exists for your preferred version; dismiss or upgrade in one click

**Collection**
- **Collection** &mdash; owned cards grouped by printing, searchable and paginated
- **Checked Out** &mdash; cards currently lent out, with check-in/check-out controls on the Collection page
- **Ignore card** &mdash; exclude a specific card from Dashboard progress tracking (toggle from Card detail, Collection, Discover, or New Printings)

**Shopping**
- **Wishlist** &mdash; preferred versions you haven&rsquo;t ordered or owned yet, with search and sort, live In Cart / Ordered count badges, and an Order button that stages into the Cart
- **Buy List** &mdash; ranks your wishlist by budget and price cap to plan what to buy next, with live In Cart / Ordered count badges and a copy-to-clipboard export formatted for TCGPlayer's mass entry tool
- **Cart** &mdash; stage purchases from Buy List or Wishlist across multiple browsing sessions (just a quantity to start), then fill in condition, edition, and price before submitting them all at once to Orders
- **Orders** &mdash; manage cards you&rsquo;ve ordered and mark them as received

**Insights**
- **Stats** &mdash; breakdown of your collection by rarity, set, and acquisition method; track collection value over time with historical snapshots updated automatically each night; look up price history for any individual card with a per-card chart
- **Edition Audit** &mdash; flags owned entries whose recorded edition doesn&rsquo;t match (or can&rsquo;t be verified against) the live API data, with inline editing to correct them

**Export**
- **Export** &mdash; download your collection or wishlist as a CSV

## Tech Stack

- **Framework**: ASP.NET Core Razor Pages (.NET 10)
- **ORM**: Entity Framework Core 9 + SQLite
- **JSON parsing**: Newtonsoft.Json 13
- **UI**: Bootstrap 5 + Chart.js 4 (CDN)
- **Auth**: BCrypt.Net-Next (cookie authentication)

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### Setup

1. Clone the repository.

2. Create `appsettings-private.json` in the project root (this file is gitignored):

   ```json
   {
     "Auth": {
       "Username": "your-username",
       "PasswordHash": "your-bcrypt-hash"
     }
   }
   ```

   Generate a BCrypt hash of your chosen password (cost factor 11) and paste it as the `PasswordHash` value.

3. Run the app:

   ```bash
   dotnet run --project CardCollector
   ```

   On first run the app fetches card data from yaml-yugi and card images from the YGO Pro Deck API, caching both locally. The SQLite database (`Data/collection.db`) is created automatically.

4. Open `https://localhost:5001` in your browser and log in.

## Project Structure

```
CardCollector/
├── Data/
│   ├── AppDBContext.cs
│   └── Models/              # EF entities and enums
├── DTO/                     # Card data structures (yaml-yugi card data + YGOProDeck image data)
├── Extensions/              # EnumExtensions (.GetDisplayName())
├── Pages/                   # Razor Pages, one per feature (see Features above)
├── Repository/              # Data access (CardDataRepository, CollectionRepository, PreferredVersionRepository)
├── Services/                # CardService (joins JSON + SQLite), PricingService (live TCGPlayer prices)
├── ViewModels/              # Page-specific view models
└── wwwroot/js/              # Page-specific JavaScript
```

## Testing

![C# Tests](https://img.shields.io/badge/C%23%20tests-548%20passing-brightgreen)
![C# Coverage](https://img.shields.io/badge/C%23%20coverage-90%25-brightgreen)
![JS Tests](https://img.shields.io/badge/JS%20tests-129%20passing-brightgreen)
![JS Coverage](https://img.shields.io/badge/JS%20coverage-93%25-brightgreen)

```
CardCollector.Tests/
├── Pages/          # PageModel tests, one file per page
├── Repository/     # EF Core repository tests (InMemory provider)
├── Services/       # CardService, PricingService, background service tests
├── ViewModels/     # ViewModel logic tests
├── DTO/            # DTO/enum-extension tests
├── Data/Models/    # Entity/enum extension tests
└── TestHelpers/    # InMemoryDbContextFactory, PageContextFactory

tests/              # JS (Vitest) tests, mirroring wwwroot/js/ one-to-one
```

```bash
# C# tests
dotnet test CardCollector.Tests/CardCollector.Tests.csproj

# C# tests with coverage
dotnet test CardCollector.Tests/CardCollector.Tests.csproj -p:CollectCoverage=true -p:CoverletOutputFormat=cobertura -p:CoverletOutput=./coverage/

# JS tests
npm test

# JS tests with coverage
npm run test:coverage
```

C# coverage excludes compiled Razor views and the `Program.cs` bootstrap (via `[ExcludeFromCodeCoverage]` and coverlet config) since that's markup/bootstrap rather than logic &mdash; the percentage reflects testable application code, not a raw line count across the whole project.

## Data Notes

- Card data (names, stats, sets, rarities) is fetched from the [yaml-yugi](https://github.com/DawnbrandBots/yaml-yugi) dataset on startup and cached locally for 7 days (configurable via `CardDataSettings:CacheTtlDays`). Card images are fetched separately from the YGO Pro Deck API and cached for 30 days (`CardDataSettings:ImageCacheTtlDays`). Both caches fall back to stale data if the upstream source is unreachable.
- Speed Duel sets are excluded from all card data at load time.
- Card images are loaded from CDN URLs; no images are stored locally.
- Live pricing data is fetched from the YGO Pro Deck pricing endpoint per card when needed.
- Collection value is refreshed automatically each night at midnight US Eastern time by a background service. It fetches live prices for all owned cards, persists the results as dated snapshots, and prunes old snapshot data (keeping daily granularity for the last 30 days and one snapshot per calendar month beyond that). The Dashboard and Stats page display values from the most recent snapshot.
