using CardCollector.Models;

namespace CardCollector.Tests.Models
{
    [TestClass]
    public sealed class BanlistTests
    {
        [TestMethod]
        public void GetLimit_KonamiIDAbsent_ReturnsUnlimited()
        {
            var banlist = new Banlist
            {
                EffectiveDate = new DateOnly(2024, 1, 1),
                LimitsByKonamiID = new Dictionary<int, BanlistLimit> { [123] = BanlistLimit.Limited }
            };

            var result = banlist.GetLimit(999);

            Assert.AreEqual(BanlistLimit.Unlimited, result);
        }

        [TestMethod]
        public void GetLimit_KonamiIDPresent_ReturnsItsLimit()
        {
            var banlist = new Banlist
            {
                EffectiveDate = new DateOnly(2024, 1, 1),
                LimitsByKonamiID = new Dictionary<int, BanlistLimit> { [123] = BanlistLimit.Limited }
            };

            var result = banlist.GetLimit(123);

            Assert.AreEqual(BanlistLimit.Limited, result);
        }
    }
}
