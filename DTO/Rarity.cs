using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.ComponentModel;
using System.Runtime.Serialization;

namespace CardCollector.DTO
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum Rarity
    {
        [Description("Collector's Rare")]
        [EnumMember(Value = "Collector's Rare")]
        CollectorsRare,

        [Description("Cr")]
        [EnumMember(Value = "Cr")]
        Cr = CollectorsRare, //Duplicate in data

        [Description("Common")]
        [EnumMember(Value = "Common")]
        Common,

        [Description("Duel Terminal Normal Parallel Rare")]
        [EnumMember(Value = "Duel Terminal Normal Parallel Rare")]
        DuelTerminalNormalParallelRare,

        [Description("Duel Terminal Normal Rare Parallel Rare")]
        [EnumMember(Value = "Duel Terminal Normal Rare Parallel Rare")]
        DuelTerminalNormalRareParallelRare,

        [Description("Duel Terminal Rare Parallel Rare")]
        [EnumMember(Value = "Duel Terminal Rare Parallel Rare")]
        DuelTerminalRareParallelRare,

        [Description("Duel Terminal Super Parallel Rare")]
        [EnumMember(Value = "Duel Terminal Super Parallel Rare")]
        DuelTerminalSuperParallelRare,

        [Description("Duel Terminal Ultra Parallel Rare")]
        [EnumMember(Value = "Duel Terminal Ultra Parallel Rare")]
        DuelTerminalUltraParallelRare,

        //This is used for setting some default values and error checking
        Error,

        [Description("European & Oceanian debut")]
        [EnumMember(Value = "European & Oceanian debut")]
        EuropeanAndOceanianDebut,

        [Description("European debut")]
        [EnumMember(Value = "European debut")]
        EuropeanDebut,

        [Description("Extra Secret Rare")]
        [EnumMember(Value = "Extra Secret Rare")]
        ExtraSecretRare, //Duplicate in data

        [Description("Extra Secret Rare")]
        [EnumMember(Value = "Extra Secret")]
        ExtraSecret = ExtraSecretRare,

        [Description("force-SMW")]
        [EnumMember(Value = "force-SMW")]
        ForceSMW,

        [Description("Ghost Gold Rare")]
        [EnumMember(Value = "Ghost/Gold Rare")]
        GhostGoldRare,

        [Description("Ghost Rare")]
        [EnumMember(Value = "Ghost Rare")]
        GhostRare,

        [Description("Gold Rare")]
        [EnumMember(Value = "Gold Rare")]
        GoldRare,

        [Description("Gold Secret Rare")]
        [EnumMember(Value = "Gold Secret Rare")]
        GoldSecretRare,

        [Description("Grand Master Rare")]
        [EnumMember(Value = "Grand Master Rare")]
        GrandMasterRare,

        [Description("Mosaic Rare")]
        [EnumMember(Value = "Mosaic Rare")]
        MosaicRare,

        [Description("New")]
        [EnumMember(Value = "New")]
        New,

        [Description("New artwork")]
        [EnumMember(Value = "New artwork")]
        NewArtwork,

        [Description("Normal Parallel Rare")]
        [EnumMember(Value = "Normal Parallel Rare")]
        NormalParallelRare,

        [Description("Oceanian debut")]
        [EnumMember(Value = "Oceanian debut")]
        OceanianDebut,

        [Description("Platinum Rare")]
        [EnumMember(Value = "Platinum Rare")]
        PlatinumRare,

        [Description("Platinum Secret Rare")]
        [EnumMember(Value = "Platinum Secret Rare")]
        PlatinumSecretRare,

        [Description("Premium Gold Rare")]
        [EnumMember(Value = "Premium Gold Rare")]
        PremiumGoldRare,

        [Description("Prismatic Secret Rare")]
        [EnumMember(Value = "Prismatic Secret Rare")]
        PrismaticSecretRare,

        [Description("Quarter Century Secret Rare")]
        [EnumMember(Value = "Quarter Century Secret Rare")]
        QuarterCenturySecretRare,

        [Description("Rare")]
        [EnumMember(Value = "Rare")]
        Rare,

        [Description("Reprint")]
        [EnumMember(Value = "Reprint")]
        Reprint,

        [Description("Secret Rare")]
        [EnumMember(Value = "Secret Rare")]
        SecretRare,

        [Description("Secret Rare Pharaoh's Rare")]
        [EnumMember(Value = "Secret Rare Pharaoh's Rare")]
        SecretRarePharaohsRare,

        [Description("Secret Rare (Pharaoh's Rare)")]
        [EnumMember(Value = "Secret Rare (Pharaoh's Rare)")]
        SecRarePharaohsRare = SecretRarePharaohsRare, //Duplicate in data

        [Description("Shatterfoil Rare")]
        [EnumMember(Value = "Shatterfoil Rare")]
        ShatterfoilRare,

        [Description("Short Print")]
        [EnumMember(Value = "Short Print")]
        ShortPrint,

        [Description("Starfoil")]
        [EnumMember(Value = "Starfoil")]
        Starfoil,

        [Description("Starfoil Rare")]
        [EnumMember(Value = "Starfoil Rare")]
        StarfoilRare,

        [Description("Starlight Rare")]
        [EnumMember(Value = "Starlight Rare")]
        StarlightRare,

        [Description("Super Parallel Rare")]
        [EnumMember(Value = "Super Parallel Rare")]
        SuperParallelRare,

        [Description("Super Rare")]
        [EnumMember(Value = "Super Rare")]
        SuperRare,

        [Description("Super Short Print")]
        [EnumMember(Value = "Super Short Print")]
        SuperShortPrint,

        [Description("10000 Secret Rare")]
        [EnumMember(Value = "10000 Secret Rare")]
        TenThousandSecretRare,

        [Description("Ultimate Rare")]
        [EnumMember(Value = "Ultimate Rare")]
        UltimateRare,

        [Description("Ultra Parallel Rare")]
        [EnumMember(Value = "Ultra Parallel Rare")]
        UltraParallelRare,

        [Description("Ultra Rare")]
        [EnumMember(Value = "Ultra Rare")]
        UltraRare,

        [Description("Ultra Rare Pharaoh's Rare")]
        [EnumMember(Value = "Ultra Rare Pharaoh's Rare")]
        UltraRarePharaohsRare,

        [Description("Ultra Rare (Pharaoh's Rare)")]
        [EnumMember(Value = "Ultra Rare (Pharaoh's Rare)")]
        URPR = UltraRarePharaohsRare, //Duplicate in data

        [Description("Ultra Secret Rare")]
        [EnumMember(Value = "Ultra Secret Rare")]
        UltraSecretRare,
    }
}
