namespace SimpleTinyPDF
{
    /// <summary>
    /// Styling options for a horizontal rule in a layout document.
    /// </summary>
    public class HorizontalRuleOptions
    {
        /// <summary>Line thickness in points. Default is 0.5.</summary>
        public float Thickness { get; set; } = 0.5f;

        /// <summary>Rule color. Default is black.</summary>
        public PdfColor? Color { get; set; }

        /// <summary>Space before the rule in points. Default is 6.</summary>
        public float SpaceBefore { get; set; } = 6f;

        /// <summary>Space after the rule in points. Default is 6.</summary>
        public float SpaceAfter { get; set; } = 6f;

        /// <summary>Left indentation in points. The rule starts this far from the content left edge.</summary>
        public float LeftIndent { get; set; }

        /// <summary>Right indentation in points. The rule ends this far from the content right edge.</summary>
        public float RightIndent { get; set; }
    }
}
