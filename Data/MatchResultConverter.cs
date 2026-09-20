using CardCollector.Data.Models;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CardCollector.Data
{
    /// <summary>Stores <see cref="MatchResult"/> as <c>L</c>, <c>T</c> or <c>W</c> so ad-hoc SQL stays readable.</summary>
    public sealed class MatchResultConverter : ValueConverter<MatchResult, string>
    {
        public MatchResultConverter() : base(result => ToLetter(result), letter => FromLetter(letter))
        {
        }

        private static MatchResult FromLetter(string letter) => letter switch
        {
            "L" => MatchResult.Loss,
            "T" => MatchResult.Tie,
            "W" => MatchResult.Win,
            _ => throw new InvalidOperationException($"'{letter}' is not a valid match result.")
        };

        private static string ToLetter(MatchResult result) => result switch
        {
            MatchResult.Loss => "L",
            MatchResult.Tie => "T",
            MatchResult.Win => "W",
            _ => throw new InvalidOperationException($"{result} is not a valid match result.")
        };
    }
}
