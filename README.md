# Card Collector

A personal Yu-Gi-Oh card collection tracker built with ASP.NET Core Razor Pages.

## Features

- **Dashboard** &mdash; collection progress overview with stats, including current market value and wishlist remaining cost-to-complete from the most recent snapshots

**Catalog**
- **Browse** &mdash; search and filter all ~14,000+ cards; link through to individual card detail
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
- **Stats** &mdash; breakdown of your collection by rarity, set, and acquisition method; track collection value and wishlist cost-to-complete over time with historical snapshots updated automatically each night; look up price history for any individual card with a per-card chart
- **Edition Audit** &mdash; flags owned entries whose recorded edition or print variant doesn&rsquo;t match (or can&rsquo;t be verified against) the live API data, with suggested fixes and inline editing to correct them

**Tournaments**
- **Formats** &mdash; metagame periods with a date range, notes, and ranked top strategies; add, edit, and delete them from the Tournaments menu. Date ranges can&rsquo;t overlap, an open-ended format shows as &ldquo;Ongoing&rdquo;, and the format containing today is badged &ldquo;Current&rdquo;
- **Events** &mdash; tournaments you&rsquo;ve played, with date, location, event type, deck, decklist link, finish, players, top cut, and notes; add, edit, and delete them from the Tournaments menu. Filter by date range, format, event type, location, and deck. Each event&rsquo;s **details** page shows its format (with that format&rsquo;s top strategies), its round-by-round matches, and the match, game, and dice-roll records

**Export**
- **Export** &mdash; download your collection or wishlist as a CSV

## Tech Stack

- **Framework**: ASP.NET Core Razor Pages (.NET 10)
- **ORM**: Entity Framework Core 10 + SQLite
- **Data parsing**: Newtonsoft.Json 13 (JSON), YamlDotNet (yaml-yugi card data)
- **UI**: Bootstrap 5 + Bootstrap Icons, Chart.js 4, and flatpickr (all CDN)
- **Auth**: BCrypt.Net-Next (cookie authentication)
- **Tests**: MSTest + Moq (C#), Vitest + jsdom (JS)

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

   On first run the app fetches card data from yaml-yugi, card images from the YGO Pro Deck API, and prices and print variants from tcgcsv, caching each locally (the caches warm up in the background, so the app is usable before they finish). The SQLite database (`Data/collection.db`) is created automatically, and later releases patch it in place at startup.

4. Open `https://localhost:5001` in your browser and log in.

## Project Structure

```
CardCollector/
├── APIEndpoints.cs          # Minimal-API handlers (printing price lookup, card price history)
├── Config/                  # RarityCorrections.json (rarity name fixes applied at card load)
├── Data/
│   ├── AppDbContext.cs
│   ├── Models/              # EF entities and enums
│   └── ...                  # Startup schema patching and cache helpers
├── DTO/                     # Card data structures (yaml-yugi, YGOProDeck, and tcgcsv pricing)
├── Extensions/              # Enum/set-code helpers and DI registration modules
├── Pages/                   # Razor Pages, one per feature (see Features above)
├── Repository/              # Data access and card-data merging
├── Services/                # Business logic (joins card data with collection state, pricing, background refresh)
├── ViewModels/              # Page-specific view models
├── scripts/                 # Deployment script and systemd unit
└── wwwroot/js/              # Page-specific JavaScript
```

`Data/` also holds the runtime card, image, and pricing caches and the SQLite database; those are gitignored.

## Testing

<!-- coverage:start -->
![C# Tests](https://img.shields.io/badge/C%23%20tests-929%20passing-brightgreen)
![C# Coverage](https://img.shields.io/badge/C%23%20coverage-91%25-brightgreen)
![JS Tests](https://img.shields.io/badge/JS%20tests-157%20passing-brightgreen)
![JS Coverage](https://img.shields.io/badge/JS%20coverage-94%25-brightgreen)
<!-- coverage:end -->

```
CardCollector.Tests/
├── Pages/          # PageModel tests, one file per page
├── Repository/     # EF Core repository and card-data mapper tests (InMemory provider)
├── Services/       # CardService, PricingService, catalog/pricing cache, and background service tests
├── ViewModels/     # ViewModel logic tests
├── DTO/            # DTO/enum-extension tests
├── Data/           # Entity/enum extension and startup-schema tests
├── Extensions/     # Enum, set-code, and edition-warning extension tests
├── APIEndpointsTests.cs
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

- Card data (names, stats, sets, rarities) is fetched from the [yaml-yugi](https://github.com/DawnbrandBots/yaml-yugi) dataset on startup and cached locally for 7 days (configurable via `CardDataSettings:CacheTtlDays`). Card images are fetched separately from the YGO Pro Deck API and cached for 30 days (`CardDataSettings:ImageCacheTtlDays`); the same response is also used to fill in cards and set printings yaml-yugi is missing. The caches fall back to stale data if the upstream source is unreachable.
- Prices and print variants (Extended Art, Alternate Art, Retail Exclusive) come from [tcgcsv](https://tcgcsv.com), a mirror of TCGPlayer&rsquo;s catalog, and are cached for 20 hours (`CardDataSettings:PricingCacheTtlHours`). A card&rsquo;s printing is identified by its card, set code, rarity, and print variant.
- Known rarity naming problems in the source data are patched at load time from `Config/RarityCorrections.json`.
- Tournament data lives in the SQLite database, not the card caches. An event&rsquo;s format is never stored: it is worked out from the event&rsquo;s date and the format date ranges, so editing a format&rsquo;s dates re-classifies its events. Filtering events by format is a date-range filter for the same reason. A match&rsquo;s result (`W`, `L`, or `T`) is stored as recorded rather than computed from the game score, so a manual override is kept. Decklist links only accept `http` and `https` addresses.
- Speed Duel sets are excluded from all card data at load time.
- Card images are loaded from CDN URLs; no images are stored locally.
- Collection value and wishlist cost-to-complete are both refreshed automatically each night at midnight US Eastern time by the same background service. It fetches live prices for all owned and wishlisted cards, persists the results as dated snapshots for each, and prunes old snapshot data (keeping daily granularity for the last 30 days and one snapshot per calendar month beyond that). The Dashboard and Stats page display values from the most recent snapshots.
