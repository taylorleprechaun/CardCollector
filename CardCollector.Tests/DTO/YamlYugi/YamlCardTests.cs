using CardCollector.DTO.YamlYugi;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CardCollector.Tests.DTO.YamlYugi
{
    [TestClass]
    public sealed class YamlCardTests
    {
        [TestMethod]
        public void Deserialize_KonamiIDAbsent_IsNull()
        {
            const string yaml = "password: 483";

            var card = Deserialize(yaml);

            Assert.IsNull(card.KonamiID);
        }

        [TestMethod]
        public void Deserialize_KonamiIDPresent_MapsFromUnderscoredKey()
        {
            const string yaml = """
                konami_id: 21470
                password: 483
                """;

            var card = Deserialize(yaml);

            Assert.AreEqual(21470, card.KonamiID);
            Assert.AreEqual(483, card.Password);
        }

        // Mirrors CardDataRepository.ParseYamlCards: UnderscoredNamingConvention with unmatched
        // properties ignored, since real yaml-yugi documents carry many fields this DTO doesn't map.
        private static YamlCard Deserialize(string yaml)
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            return deserializer.Deserialize<YamlCard>(yaml);
        }
    }
}
