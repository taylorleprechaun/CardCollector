using CardCollector.Data.Models.Catalog;

namespace CardCollector.Models.Catalog
{
    /// <summary>
    /// One real card as the sources report it this run: the yaml-yugi document (if any) together with every YGOProDeck
    /// card matched to it. Field values prefer yaml-yugi and fall back to YGOProDeck.
    /// </summary>
    /// <remarks>
    /// yaml-yugi has a separate document for a few alternate-art passcodes of one Konami card (Dark Magician 36996508,
    /// Polymerization 27847700). Those ride along in <see cref="AlternateYamlYugi"/>: their passcodes become aliases and
    /// their printings join the card's.
    /// </remarks>
    public sealed class ObservedCard
    {
        /// <summary>Every outside identifier the sources report for the card.</summary>
        public IReadOnlyList<(CatalogSource Source, CatalogAliasKind Kind, int Value)> Aliases
        {
            get
            {
                var aliases = new List<(CatalogSource, CatalogAliasKind, int)>();
                if (YamlYugi?.Passcode is int passcode)
                    aliases.Add((CatalogSource.YamlYugi, CatalogAliasKind.Passcode, passcode));
                if (YamlYugi?.FakePassword is int fakePassword)
                    aliases.Add((CatalogSource.YamlYugi, CatalogAliasKind.FakePassword, fakePassword));
                foreach (var alternate in AlternateYamlYugi)
                {
                    if (alternate.Passcode is int alternatePasscode)
                        aliases.Add((CatalogSource.YamlYugi, CatalogAliasKind.Passcode, alternatePasscode));
                    if (alternate.FakePassword is int alternateFakePassword)
                        aliases.Add((CatalogSource.YamlYugi, CatalogAliasKind.FakePassword, alternateFakePassword));
                }

                foreach (var card in YGOProDeck)
                {
                    if (card.YGOProDeckID is int id)
                        aliases.Add((CatalogSource.YGOProDeck, CatalogAliasKind.YGOProDeckID, id));
                    if (card.BetaID is int betaID)
                        aliases.Add((CatalogSource.YGOProDeck, CatalogAliasKind.BetaID, betaID));
                    aliases.AddRange(card.ArtworkIDs.Select(a => (CatalogSource.YGOProDeck, CatalogAliasKind.Artwork, a)));
                }

                return aliases.Distinct().ToList();
            }
        }

        /// <summary>Other yaml-yugi documents with the same Konami ID as <see cref="YamlYugi"/> (alternate-art passcodes).</summary>
        public IReadOnlyList<SourceCard> AlternateYamlYugi { get; }

        public string? Archetype => Prefer(c => c.Archetype);

        public int? ATK => Prefer(c => c.ATK);

        public string? Attribute => Prefer(c => c.Attribute);

        public string? CardType => Prefer(c => c.CardType);

        public int? DEF => Prefer(c => c.DEF);

        public string? Description => Prefer(c => c.Description);

        /// <summary>Artwork from the YGOProDeck card that carries the Konami ID, else the first one matched.</summary>
        public IReadOnlyList<DTO.Image> Images => PrimaryYGOProDeck?.Images ?? [];

        public int? KonamiID => YamlYugi?.KonamiID ?? YGOProDeck.Select(c => c.KonamiID).FirstOrDefault(k => k.HasValue);

        public int? Level => Prefer(c => c.Level);

        public string? LinkMarkers => Prefer(c => c.LinkMarkers);

        public int? LinkRating => Prefer(c => c.LinkRating);

        public string Name => YamlYugi?.Name ?? PrimaryYGOProDeck!.Name;

        /// <summary>The passcode to show: yaml-yugi's, then yaml-yugi's stand-in id, then YGOProDeck's id.</summary>
        public int? PreferredPasscode => YamlYugi?.Passcode ?? YamlYugi?.FakePassword ?? PrimaryYGOProDeck?.YGOProDeckID;

        /// <summary>The YGOProDeck card that carries the Konami ID, else the first one matched.</summary>
        public SourceCard? PrimaryYGOProDeck => YGOProDeck.FirstOrDefault(c => c.KonamiID.HasValue && c.KonamiID == YamlYugi?.KonamiID)
            ?? YGOProDeck.FirstOrDefault();

        /// <summary>Every (set code, rarity) any source lists, de-duplicated case-insensitively.</summary>
        public IReadOnlyList<SourcePrinting> Printings =>
            (YamlYugi?.Printings ?? [])
                .Concat(AlternateYamlYugi.SelectMany(c => c.Printings))
                .Concat(YGOProDeck.SelectMany(c => c.Printings))
                .GroupBy(p => (Code: p.SetCode.ToUpperInvariant(), Rarity: p.RarityName.ToUpperInvariant()))
                .Select(g => g.First())
                .ToList();

        public string? Type => Prefer(c => c.Type);

        public string? TypeLine => Prefer(c => c.TypeLine);

        public SourceCard? YamlYugi { get; }

        public IReadOnlyList<SourceCard> YGOProDeck { get; }

        public ObservedCard(SourceCard? yamlYugi, IReadOnlyList<SourceCard> ygoProDeck, IReadOnlyList<SourceCard>? alternateYamlYugi = null)
        {
            if (ygoProDeck is null) throw new ArgumentNullException(nameof(ygoProDeck));
            if (yamlYugi is null && ygoProDeck.Count == 0)
                throw new ArgumentException("An observed card needs at least one source card.", nameof(ygoProDeck));

            AlternateYamlYugi = alternateYamlYugi ?? [];
            YamlYugi = yamlYugi;
            YGOProDeck = ygoProDeck;
        }

        /// <summary>A short label for logs and review items: the name plus whichever id is known.</summary>
        public string Describe() =>
            KonamiID is int konamiID ? $"\"{Name}\" (Konami ID {konamiID})" : $"\"{Name}\" (passcode {PreferredPasscode})";

        private T? Prefer<T>(Func<SourceCard, T?> field) where T : class =>
            (YamlYugi is null ? null : field(YamlYugi)) ?? (PrimaryYGOProDeck is null ? null : field(PrimaryYGOProDeck));

        private T? Prefer<T>(Func<SourceCard, T?> field) where T : struct =>
            (YamlYugi is null ? null : field(YamlYugi)) ?? (PrimaryYGOProDeck is null ? null : field(PrimaryYGOProDeck));
    }
}
