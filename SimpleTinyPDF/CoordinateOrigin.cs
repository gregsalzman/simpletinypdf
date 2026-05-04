namespace SimpleTinyPDF
{
    /// <summary>
    /// Controls the Y-axis direction for page coordinates.
    /// </summary>
    public enum CoordinateOrigin
    {
        /// <summary>Y=0 is the top of the page, Y increases downward (default).</summary>
        TopDown,

        /// <summary>Y=0 is the bottom of the page, Y increases upward (native PDF coordinates).</summary>
        BottomUp
    }
}
