namespace SimpleTinyPDF
{
    /// <summary>
    /// Controls how an image is scaled to fit within the target rectangle.
    /// </summary>
    public enum ImageScaleMode
    {
        /// <summary>Stretch to fill the entire area, ignoring aspect ratio.</summary>
        Stretch,

        /// <summary>Scale to fit inside the area, preserving aspect ratio, centered.</summary>
        Fit,

        /// <summary>Scale to cover the entire area, preserving aspect ratio, centered, overflow clipped.</summary>
        Fill
    }
}
