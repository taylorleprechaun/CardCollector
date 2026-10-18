namespace CardCollector.Models.Legacy
{
    /// <summary>What the migration did with one legacy table.</summary>
    public sealed class LegacyTableReport
    {
        /// <summary>Rows moved to another card because only one card has their printing (passcode drift).</summary>
        public int CardCorrected { get; set; }

        /// <summary>Rows whose card id matched the printing's card directly.</summary>
        public int Exact { get; set; }

        /// <summary>Rows that duplicated another row's catalog printing after mapping and were folded into it.</summary>
        public int Merged { get; set; }

        public int Read { get; set; }

        public string Table { get; init; } = string.Empty;

        /// <summary>Rows that found no printing or card; each is a review item, not dropped.</summary>
        public int Unmapped { get; set; }

        /// <summary>Rows without a variant moved onto their card's only (variant) printing of that set code and rarity.</summary>
        public int VariantFilled { get; set; }

        public int Written { get; set; }
    }
}
