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
- **Edition Audit** &mdash; flags owned and ordered entries whose recorded edition isn&rsquo;t one TCGPlayer lists for that printing, with inline editing to correct them. A printing with no linked TCGPlayer product can&rsquo;t be checked; it links to its card in the catalog admin so the product can be linked

**Tournaments**
- **Formats** &mdash; metagame periods with a date range, notes, and ranked top strategies. Ranges can&rsquo;t overlap; an open-ended format shows as &ldquo;Ongoing&rdquo; and the one containing today is badged &ldquo;Current&rdquo;
- **Events** &mdash; tournaments you&rsquo;ve played (date, location, type, deck, decklist link, finish, players, top cut, notes), filterable by date, format, type, location, and deck. An event&rsquo;s page shows its format and top strategies, its rounds, and its match, game, and dice-roll records. Rounds are entered right there, built for a phone between matches: the next round is pre-filled, opponent decks autocomplete, common scores are one tap (or enter a custom score), the result follows the score until you override it, and out-of-order rounds get a one-click sort
- **Decks** &mdash; paste a YDKe code or YDK file from DuelingBook to add a deck to an event (and optionally to other events sharing its decklist link). The viewer shows every card&rsquo;s image by section, sorted by type then name, with monster/spell/trap counts and a Forbidden/Limited/Semi-Limited badge on each restricted card, checked against the list in effect at the event or today (pickers switch the event or list). Decks can be renamed or deleted from the Decks page
- **Analytics** &mdash; records and win % filtered by format, deck, opponent, date, event type, and location. Tabs for deck &times; format, matchups, a per-format trend chart, deck, format, event type, and dice roll

**Export**
- **Export** &mdash; download your collection or wishlist as a CSV

**Catalog admin**
- **Review inbox** &mdash; changes the nightly catalog sync couldn&rsquo;t apply on its own (a card or printing a source stopped listing, a renamed card, a TCGPlayer product that matches no printing, a legacy row the migration couldn&rsquo;t place). Each item offers its fixes; decided items can be reopened
- **Cards** &mdash; search the catalog, edit a card&rsquo;s fields (an edited field is locked so the sync won&rsquo;t overwrite it), add, link, merge, or retire printings, and see how many collection rows use each printing
- **Sync history** &mdash; every catalog sync run with what each source returned and what changed, warning when a source keeps failing

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

   On first run the app downloads yaml-yugi, YGOProDeck, and tcgcsv, caches each locally, and builds the card catalog in a new SQLite database at `Data/cardcollector.db`. When a pre-catalog database exists at `LegacyMigration:LegacyDatabasePath` (`Data/collection.db`) and `Data/cardcollector.db` doesn&rsquo;t, the app first migrates it: it builds the catalog, points every collection row at a catalog printing, and writes `Data/migration-report.txt`. The legacy file is only read, so it stays as the rollback. Rows it can&rsquo;t place land in the review inbox instead of being dropped.

4. Open `http://localhost:5000` (the default launch profile) in your browser and log in.

## Project Structure

```
CardCollector/
├── APIEndpoints.cs          # Minimal-API handlers (printing price lookup, card price history)
├── Config/                  # RarityCorrections.json (rarity name fixes applied during the catalog sync)
├── Data/
│   ├── AppDbContext.cs
│   ├── Models/              # EF entities and enums
│   └── ...                  # Cache helpers
├── DTO/                     # Card data structures (yaml-yugi, YGOProDeck, and tcgcsv pricing)
├── Extensions/              # Enum/set-code helpers and DI registration modules
├── Models/                  # Domain, request, and result types (not EF entities)
├── Pages/                   # Razor Pages, one per feature (see Features above)
├── Repository/              # Data access: catalog, card sources, collection, and the legacy import
├── Rules/                   # Pure business rules, parsers, and calculators (including the catalog sync and legacy mapping)
├── Services/                # Business logic (joins card data with collection state, pricing, background refresh)
├── ViewModels/              # Page-specific view models
├── scripts/                 # Deployment script and systemd unit
└── wwwroot/js/              # Page-specific JavaScript
```

`Data/` also holds the runtime card, image, and pricing caches and the SQLite database; those are gitignored.

## Testing

<!-- coverage:start -->
![C# Tests](https://img.shields.io/badge/C%23%20tests-1463%20passing-brightgreen)
![C# Coverage](https://img.shields.io/badge/C%23%20coverage-93%25-brightgreen)
![JS Tests](https://img.shields.io/badge/JS%20tests-269%20passing-brightgreen)
![JS Coverage](https://img.shields.io/badge/JS%20coverage-96%25-brightgreen)
<!-- coverage:end -->

```
CardCollector.Tests/
├── Pages/          # PageModel tests, one file per page
├── Repository/     # EF Core repository and card-data mapper tests (InMemory provider)
├── Rules/          # Pure rule, parser, and calculator tests
├── Models/         # Domain-type tests
├── Services/       # CardService, PricingService, catalog/pricing cache, and background service tests
├── ViewModels/     # ViewModel logic tests
├── DTO/            # DTO/enum-extension tests
├── Data/           # Entity/enum extension and startup-schema tests
├── Extensions/     # Extension-method tests
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

- The app keeps its own card catalog in the database: cards and printings with permanent IDs, built from three sources. [yaml-yugi](https://github.com/DawnbrandBots/yaml-yugi) supplies card data, sets, and rarities (cached 7 days, `CardDataSettings:CacheTtlDays`). The YGO Pro Deck API supplies images and the cards yaml-yugi lacks (cached 30 days, `CardDataSettings:ImageCacheTtlDays`). [tcgcsv](https://tcgcsv.com), a mirror of TCGPlayer&rsquo;s catalog, supplies prices and print variants such as Extended Art (cached 20 hours, `CardDataSettings:PricingCacheTtlHours`). The caches fall back to stale data if a source is unreachable.
- Every collection, wishlist, checked-out, cart, and dismissed row points at a catalog printing by its ID, so a source renaming a card, changing a passcode, or reshuffling rarities doesn&rsquo;t orphan your data. A printing is a set code, a rarity, and an optional print variant.
- The catalog syncs at startup (skipped when no source has changed since the last run) and again each night. A sync applies new cards and printings on its own and sends anything ambiguous or destructive to the review inbox. Its guardrails live in `CatalogSync`: a source returning less than `MinSourceRatio` of its last good count is ignored for that run, a run that would open more than `MaxNewActionableItems` inbox items is rolled back and recorded as one anomaly, and the sync history warns once a source has failed `SourceFailureWarningRuns` runs in a row.
- Known rarity naming problems in the source data are corrected during the sync from `Config/RarityCorrections.json`.
- Two command-line modes run without starting the web app: `catalog-sync --db <path> [--offline]` builds or updates a catalog database, and `migrate-legacy --from <legacy path> --to <new path> [--offline] [--overwrite]` builds a new database from a pre-catalog one and prints the migration report. `--offline` uses only the local source caches.
- Per-card price history on the Stats page follows the catalog card, so it survives a card being renamed. History migrated from a pre-catalog database is linked to its card through its collection entry, or by exact card name when the entry is gone.
- Tournament data lives in the SQLite database. An event&rsquo;s format isn&rsquo;t stored; it comes from the event&rsquo;s date, so editing a format&rsquo;s dates re-classifies its events. Round results (`W`, `L`, `T`) are stored as recorded rather than computed from the score, so manual overrides stick. Win % is `(W + T/2) / (W + L + T)`.
- A round allows at most two game wins and two losses, and a bye is stored as a win with no games. New rounds are placed by their label: numbered rounds, then Top 8, Top 4, and Finals.
- Decks are pasted, since DuelingBook deck pages can&rsquo;t be fetched. Every passcode, including alternate-art and YGOProDeck-only ones, resolves to its catalog card; a passcode the catalog doesn&rsquo;t know is kept as pasted, shown as an unknown card, and resolved again each time the deck is read.
- Forbidden &amp; Limited lists come from [yaml-yugi-limit-regulation](https://github.com/DawnbrandBots/yaml-yugi-limit-regulation), keyed by Konami ID and cached for 7 days (`BanlistSettings:CacheTtlDays`). `BanlistSettings:EffectiveDateOverrides` moves two lists recorded under a regional date to the AMER date they took effect.
- Speed Duel sets are excluded from the catalog during the sync.
- Card images are loaded from CDN URLs; no images are stored locally.
- Collection value and wishlist cost-to-complete are both refreshed automatically each night at midnight US Eastern time by the same background service. It fetches live prices for all owned and wishlisted cards, persists the results as dated snapshots for each, and prunes old snapshot data (keeping daily granularity for the last 30 days and one snapshot per calendar month beyond that). The Dashboard and Stats page display values from the most recent snapshots.
