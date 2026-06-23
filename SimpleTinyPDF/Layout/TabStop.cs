namespace SimpleTinyPDF
{
    /// <summary>
    /// Controls how text is aligned at a tab stop position.
    /// </summary>
    public enum TabAlignment
    {
        /// <summary>Text starts at the tab position.</summary>
        Left,

        /// <summary>Text is centered at the tab position.</summary>
        Center,

        /// <summary>Text ends at the tab position.</summary>
        Right,

        /// <summary>The decimal point aligns at the tab position.</summary>
        Decimal
    }

    /// <summary>
    /// Defines a tab stop position and alignment within a paragraph.
    /// </summary>
    public class TabStop
    {
        /// <summary>Position of the tab stop in points, relative to the content area left edge.</summary>
        public float Position { get; }

        /// <summary>How text is aligned at this tab stop.</summary>
        public TabAlignment Alignment { get; }

        /// <summary>Optional leader character repeated to fill the gap before this tab stop.</summary>
        public char? Leader { get; }

        /// <summary>The character used for decimal alignment (default is '.').</summary>
        public char DecimalChar { get; }

        /// <summary>Creates a tab stop at the specified position.</summary>
        public TabStop(float position, TabAlignment alignment = TabAlignment.Left,
            char? leader = null, char decimalChar = '.')
        {
            Position = position;
            Alignment = alignment;
            Leader = leader;
            DecimalChar = decimalChar;
        }
    }
}
