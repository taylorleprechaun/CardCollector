namespace CardCollector.ViewModels
{
    /// <summary>How often the dice roll was won or lost, displayed as <c>Won-Lost</c>. Unrecorded rolls are not counted.</summary>
    public sealed record DiceRecord(int Won, int Lost)
    {
        public override string ToString() => $"{Won}-{Lost}";
    }
}
