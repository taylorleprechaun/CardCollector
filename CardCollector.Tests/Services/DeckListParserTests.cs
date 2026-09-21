using System.Buffers.Binary;
using CardCollector.Services;

namespace CardCollector.Tests.Services
{
    [TestClass]
    public sealed class DeckListParserTests
    {
        [TestMethod]
        public void Parse_InputLongerThanLimit_ReturnsFailure()
        {
            var text = "#main\n" + new string('1', DeckListParser.MAX_INPUT_LENGTH);

            var result = DeckListParser.Parse(text);

            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains(result.Error, "too long");
        }

        [TestMethod]
        [DataRow(null, DisplayName = "Null text")]
        [DataRow("", DisplayName = "Empty text")]
        [DataRow("   \r\n  ", DisplayName = "Whitespace text")]
        public void Parse_NoText_ReturnsFailure(string? text)
        {
            var result = DeckListParser.Parse(text);

            Assert.IsFalse(result.Succeeded);
            Assert.IsNotNull(result.Error);
        }

        [TestMethod]
        public void Parse_SampleYdk_HasSameCardsInSameOrderAsSampleYdke()
        {
            var ydk = DeckListParser.Parse(ReadFixture("sample.ydk")).Deck!;
            var ydke = DeckListParser.Parse(ReadFixture("sample.ydke.txt")).Deck!;

            CollectionAssert.AreEqual(ydke.Main.ToList(), ydk.Main.ToList());
            CollectionAssert.AreEqual(ydke.Extra.ToList(), ydk.Extra.ToList());
            CollectionAssert.AreEqual(ydke.Side.ToList(), ydk.Side.ToList());
        }

        [TestMethod]
        public void Parse_SampleYdk_ReturnsSixtyFifteenFifteenAsYdk()
        {
            var result = DeckListParser.Parse(ReadFixture("sample.ydk"));

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(DeckListFormat.Ydk, result.Deck!.Format);
            Assert.AreEqual(60, result.Deck.Main.Count);
            Assert.AreEqual(15, result.Deck.Extra.Count);
            Assert.AreEqual(15, result.Deck.Side.Count);
        }

        [TestMethod]
        public void Parse_SampleYdke_ReturnsSixtyFifteenFifteenAsYdke()
        {
            var result = DeckListParser.Parse(ReadFixture("sample.ydke.txt"));

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(DeckListFormat.Ydke, result.Deck!.Format);
            Assert.AreEqual(60, result.Deck.Main.Count);
            Assert.AreEqual(15, result.Deck.Extra.Count);
            Assert.AreEqual(15, result.Deck.Side.Count);
        }

        [TestMethod]
        public void Parse_TextThatIsNeitherFormat_ReturnsFailure()
        {
            var result = DeckListParser.Parse("4 Sample Card\n3 Other Card");

            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains(result.Error, "YDKe");
        }

        [TestMethod]
        public void Parse_TotalOverCardLimit_ReturnsFailure()
        {
            var ydke = Ydke(Enumerable.Range(1, DeckListParser.MAX_CARDS + 1).ToArray(), [], []);

            var result = DeckListParser.Parse(ydke);

            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains(result.Error, "limit");
        }

        [TestMethod]
        public void Parse_YdkCommentLines_AreIgnored()
        {
            var result = DeckListParser.Parse("#created by someone\n#main\n# a note\n100\n#extra\n200\n!side\n300");

            CollectionAssert.AreEqual(new[] { 100 }, result.Deck!.Main.ToList());
            CollectionAssert.AreEqual(new[] { 200 }, result.Deck.Extra.ToList());
            CollectionAssert.AreEqual(new[] { 300 }, result.Deck.Side.ToList());
        }

        [TestMethod]
        public void Parse_YdkCrlfLineEndings_ParsesLikeLf()
        {
            var lf = DeckListParser.Parse("#main\n100\n101\n#extra\n200\n!side\n300\n").Deck!;

            var crlf = DeckListParser.Parse("#main\r\n100\r\n101\r\n#extra\r\n200\r\n!side\r\n300\r\n").Deck!;

            CollectionAssert.AreEqual(lf.Main.ToList(), crlf.Main.ToList());
            CollectionAssert.AreEqual(lf.Extra.ToList(), crlf.Extra.ToList());
            CollectionAssert.AreEqual(lf.Side.ToList(), crlf.Side.ToList());
        }

        [TestMethod]
        public void Parse_YdkeBadBase64_NamesTheSection()
        {
            var result = DeckListParser.Parse("ydke://" + Encode(100) + "!@@@@!!");

            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains(result.Error, "extra");
        }

        [TestMethod]
        public void Parse_YdkeEmptyMain_ReturnsFailure()
        {
            var result = DeckListParser.Parse("ydke://!" + Encode(200) + "!!");

            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains(result.Error, "main deck is empty");
        }

        [TestMethod]
        public void Parse_YdkeExtraSectionAfterSide_ReturnsFailure()
        {
            var result = DeckListParser.Parse($"ydke://{Encode(100)}!!!{Encode(200)}");

            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains(result.Error, "three sections");
        }

        [TestMethod]
        public void Parse_YdkeMissingPadding_Succeeds()
        {
            var unpadded = Encode(100).TrimEnd('=');

            var result = DeckListParser.Parse($"ydke://{unpadded}!!!");

            CollectionAssert.AreEqual(new[] { 100 }, result.Deck!.Main.ToList());
        }

        [TestMethod]
        public void Parse_YdkEmptyExtraAndSide_Succeeds()
        {
            var result = DeckListParser.Parse("#main\n100\n#extra\n!side\n");

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(0, result.Deck!.Extra.Count);
            Assert.AreEqual(0, result.Deck.Side.Count);
        }

        [TestMethod]
        public void Parse_YdkEmptyMain_ReturnsFailure()
        {
            var result = DeckListParser.Parse("#main\n#extra\n200\n");

            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains(result.Error, "main deck is empty");
        }

        [TestMethod]
        public void Parse_YdkeOnlyTwoSections_ReturnsFailure()
        {
            var result = DeckListParser.Parse($"ydke://{Encode(100)}!{Encode(200)}");

            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains(result.Error, "three sections");
        }

        [TestMethod]
        public void Parse_YdkePasscodeAboveInt32Range_ReturnsFailure()
        {
            var tooLarge = Convert.ToBase64String(BitConverter.GetBytes(uint.MaxValue));

            var result = DeckListParser.Parse($"ydke://{tooLarge}!!!");

            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains(result.Error, "invalid card number");
        }

        [TestMethod]
        public void Parse_YdkeSchemeInUpperCase_IsDetectedAsYdke()
        {
            var result = DeckListParser.Parse("YDKE://" + Encode(100) + "!!!");

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(DeckListFormat.Ydke, result.Deck!.Format);
        }

        [TestMethod]
        public void Parse_YdkeSectionLengthNotMultipleOfFour_ReturnsFailure()
        {
            var fiveBytes = Convert.ToBase64String(new byte[] { 1, 0, 0, 0, 1 });

            var result = DeckListParser.Parse($"ydke://{fiveBytes}!!!");

            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains(result.Error, "wrong length");
        }

        [TestMethod]
        public void Parse_YdkeUrlSafeAlphabet_DecodesLikeStandardAlphabet()
        {
            // 0x0FFFFFFB encodes to "+///Dw==" in the standard alphabet.
            var standard = DeckListParser.Parse("ydke://+///Dw==!!!").Deck!;

            var urlSafe = DeckListParser.Parse("ydke://-___Dw==!!!").Deck!;

            CollectionAssert.AreEqual(new[] { 0x0FFFFFFB }, standard.Main.ToList());
            CollectionAssert.AreEqual(standard.Main.ToList(), urlSafe.Main.ToList());
        }

        [TestMethod]
        public void Parse_YdkeWithoutTrailingBang_Succeeds()
        {
            var result = DeckListParser.Parse($"ydke://{Encode(100)}!{Encode(200)}!{Encode(300)}");

            Assert.IsTrue(result.Succeeded);
            CollectionAssert.AreEqual(new[] { 300 }, result.Deck!.Side.ToList());
        }

        [TestMethod]
        public void Parse_YdkeWithWhitespaceAndNewlines_IgnoresThem()
        {
            var encoded = Encode(100, 101);
            var wrapped = $"  ydke://{encoded[..4]}\r\n{encoded[4..]}!\n!\n!  ";

            var result = DeckListParser.Parse(wrapped);

            CollectionAssert.AreEqual(new[] { 100, 101 }, result.Deck!.Main.ToList());
        }

        [TestMethod]
        public void Parse_YdkLeadingZeros_ReadsThePasscode()
        {
            var result = DeckListParser.Parse("#main\n02563463\n");

            CollectionAssert.AreEqual(new[] { 2563463 }, result.Deck!.Main.ToList());
        }

        [TestMethod]
        public void Parse_YdkNonNumericLine_NamesTheLineNumber()
        {
            var result = DeckListParser.Parse("#main\n100\nnot a number\n200");

            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains(result.Error, "Line 3");
            StringAssert.Contains(result.Error, "not a number");
        }

        [TestMethod]
        public void Parse_YdkNumberBeforeAnySection_ReturnsFailure()
        {
            var result = DeckListParser.Parse("100\n#main\n200\n");

            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains(result.Error, "Line 1");
        }

        [TestMethod]
        public void Parse_YdkSideHeaderWithHash_IsTheSideDeck()
        {
            var result = DeckListParser.Parse("#main\n100\n#extra\n200\n#side\n300\n");

            CollectionAssert.AreEqual(new[] { 300 }, result.Deck!.Side.ToList());
            Assert.AreEqual(1, result.Deck.Extra.Count);
        }

        [TestMethod]
        public void Parse_YdkZeroPasscode_ReturnsFailure()
        {
            var result = DeckListParser.Parse("#main\n0\n");

            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains(result.Error, "Line 2");
        }
        private static string Encode(params int[] passcodes)
        {
            var bytes = new byte[passcodes.Length * 4];
            for (var index = 0; index < passcodes.Length; index++)
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), (uint)passcodes[index]);

            return Convert.ToBase64String(bytes);
        }

        private static string ReadFixture(string fileName) =>
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "Decks", fileName));

        private static string Ydke(int[] main, int[] extra, int[] side) =>
            $"ydke://{Encode(main)}!{Encode(extra)}!{Encode(side)}!";
    }
}
