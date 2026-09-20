namespace CardCollector.Data.Models
{
    /// <summary>One of a format's top strategies; <see cref="Position"/> keeps them in ranked order.</summary>
    public class FormatStrategy
    {
        public int FormatID { get; set; }

        public int ID { get; set; }

        public string Name { get; set; } = string.Empty;

        public int Position { get; set; }
    }
}
