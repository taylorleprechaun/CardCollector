# Card Collector

A personal Yu-Gi-Oh card collection tracker built with ASP.NET Core Razor Pages.

## Features

- **Dashboard** — collection progress overview with stats
- **Browse** — search and filter all ~12,000+ cards from the YGO Pro Deck dataset
- **Collection** — view owned cards grouped by status, condition, and edition
- **Discover** — surface a random uncollected artwork and add it to your order list or mark it as owned
- **Orders** — manage cards you've ordered and mark them as received

Collection is tracked at the artwork level — a card with multiple artworks counts as separate collectibles.

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

2. Download card data from the YGO Pro Deck API and place it at the project root:

   ```
   https://db.ygoprodeck.com/api/v7/cardinfo.php
   ```

   Save the response as `cardinfo.php.json` in the `CardCollector/` project folder.

3. Run the app:

   ```bash
   dotnet run --project CardCollector
   ```

   The SQLite database (`Data/collection.db`) is created automatically on first run.

4. Open `https://localhost:5001` in your browser.

## Project Structure

```
CardCollector/
├── Data/
│   ├── AppDbContext.cs
│   └── Models/              # EF entities and enums
├── DTO/                     # Deserialized card data from JSON
├── Extensions/              # EnumExtensions (.GetDisplayName())
├── Pages/                   # Razor Pages (Dashboard, Browse, Collection, Discover, Orders, Card)
├── Repository/              # Data access (CardDataRepository, CollectionRepository)
├── Services/                # CardService — joins JSON data with SQLite state
├── ViewModels/              # Page-specific view models
└── cardinfo.php.json        # Source card data (copied to output directory)
```

## Data Notes

- `cardinfo.php.json` is read-only reference data and is never written to by the app.
- Card images are loaded from CDN URLs embedded in the JSON; no images are stored locally.
- The `cardinfo.php.json` file is not committed to the repository due to its size (~50MB). Download a fresh copy from the YGO Pro Deck API when setting up.
