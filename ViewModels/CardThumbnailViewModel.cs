namespace CardCollector.ViewModels
{
    /// <param name="Placeholder">Text shown in the grey box when there is no image; empty shows a plain box.</param>
    public record CardThumbnailViewModel(string? ImageURL, string AltText, string Placeholder = "");
}
