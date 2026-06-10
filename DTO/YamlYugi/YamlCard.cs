namespace CardCollector.DTO.YamlYugi
{
    public sealed class YamlCard
    {
        public string? Atk { get; set; }
        public string? Attribute { get; set; }
        public string? CardType { get; set; }
        public string? Def { get; set; }
        public int? Level { get; set; }
        public List<string>? LinkArrows { get; set; }
        public string? MonsterTypeLine { get; set; }
        public YamlLocalizedString? Name { get; set; }
        public int? Password { get; set; }
        public string? Property { get; set; }
        public int? Rank { get; set; }
        // Images intentionally omitted — card images come from the YGOProDeck image cache.
        // IgnoreUnmatchedProperties handles skipping the images block.
        public YamlCardSets? Sets { get; set; }
        public YamlLocalizedString? Text { get; set; }
    }

    public sealed class YamlLocalizedString
    {
        public string? En { get; set; }
    }

    public sealed class YamlCardSets
    {
        public List<YamlSetEntry>? En { get; set; }
    }

    public sealed class YamlSetEntry
    {
        public List<string>? Rarities { get; set; }
        public string? SetName { get; set; }
        public string? SetNumber { get; set; }
    }
}
