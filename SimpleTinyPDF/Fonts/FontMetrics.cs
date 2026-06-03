using System.Collections.Generic;

namespace SimpleTinyPDF
{
    internal static class FontMetrics
    {
        /// <summary>
        /// Returns the width of a character in 1/1000 em units (PdfFontSource overload).
        /// </summary>
        internal static int GetCharWidth(PdfFontSource font, char c)
        {
            if (font.IsBuiltIn)
                return GetCharWidth(font.BuiltInFont, c);
            return font.CustomFont.GetCharWidth(c);
        }

        /// <summary>
        /// Measures the width of a string in points (PdfFontSource overload).
        /// </summary>
        internal static float MeasureString(string text, PdfFontSource font, float fontSize,
            float charSpacing = 0f)
        {
            if (font.IsBuiltIn)
                return MeasureString(text, font.BuiltInFont, fontSize, charSpacing);
            if (string.IsNullOrEmpty(text)) return 0;
            int total = 0;
            int glyphCount = 0;
            for (int i = 0; i < text.Length; i++)
            {
                int cp;
                if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    cp = char.ConvertToUtf32(text[i], text[i + 1]);
                    i++;
                }
                else
                {
                    cp = text[i];
                }
                total += font.CustomFont.GetCharWidth(cp);
                glyphCount++;
            }
            float width = total * fontSize / 1000f;
            if (charSpacing != 0f && glyphCount > 0)
                width += glyphCount * charSpacing;
            return width;
        }

        /// <summary>
        /// Word-wraps text to fit within the specified width (PdfFontSource overload).
        /// </summary>
        internal static List<string> WrapText(string text, PdfFontSource font, float fontSize,
            float maxWidth, float charSpacing = 0f)
        {
            if (font.IsBuiltIn)
                return WrapText(text, font.BuiltInFont, fontSize, maxWidth, charSpacing);

            var result = new List<string>();
            if (string.IsNullOrEmpty(text))
            {
                result.Add("");
                return result;
            }

            var paragraphs = text.Split('\n');
            foreach (var para in paragraphs)
            {
                if (string.IsNullOrEmpty(para))
                {
                    result.Add("");
                    continue;
                }

                var words = para.Split(' ');
                var currentLine = new System.Text.StringBuilder();
                float currentWidth = 0;
                float spaceWidth = MeasureString(" ", font, fontSize, charSpacing);

                foreach (var word in words)
                {
                    float wordWidth = MeasureString(word, font, fontSize, charSpacing);

                    if (currentLine.Length == 0)
                    {
                        currentLine.Append(word);
                        currentWidth = wordWidth;
                    }
                    else if (currentWidth + spaceWidth + wordWidth <= maxWidth)
                    {
                        currentLine.Append(' ').Append(word);
                        currentWidth += spaceWidth + wordWidth;
                    }
                    else
                    {
                        result.Add(currentLine.ToString());
                        currentLine.Clear();
                        currentLine.Append(word);
                        currentWidth = wordWidth;
                    }
                }

                result.Add(currentLine.ToString());
            }

            return result;
        }

        /// <summary>
        /// Returns the width of a character in 1/1000 em units.
        /// </summary>
        internal static int GetCharWidth(PdfFont font, char c)
        {
            int code = (int)c;
            // Map common Unicode characters to WinAnsiEncoding codes
            if (code > 255 && PdfStringHelper.UnicodeToWinAnsi.TryGetValue(c, out byte winAnsi))
                code = winAnsi;
            // Map extended European characters to their base character width
            if (code > 255 && GlyphMapping.UnicodeToGlyphName.TryGetValue(c, out string glyphName))
                code = (glyphName == "dotlessi") ? 'i' : glyphName[0];
            if (code < 0 || code > 255) return 500; // fallback for out-of-range

            switch (font)
            {
                case PdfFont.Courier:
                case PdfFont.CourierBold:
                case PdfFont.CourierOblique:
                case PdfFont.CourierBoldOblique:
                    return 600;

                case PdfFont.Helvetica:
                case PdfFont.HelveticaOblique:
                    return HelveticaWidths[code];

                case PdfFont.HelveticaBold:
                case PdfFont.HelveticaBoldOblique:
                    return HelveticaBoldWidths[code];

                case PdfFont.TimesRoman:
                    return TimesRomanWidths[code];

                case PdfFont.TimesBold:
                    return TimesBoldWidths[code];

                case PdfFont.TimesItalic:
                    return TimesItalicWidths[code];

                case PdfFont.TimesBoldItalic:
                    return TimesBoldItalicWidths[code];

                case PdfFont.Symbol:
                    return SymbolWidths[code];

                case PdfFont.ZapfDingbats:
                    return ZapfDingbatsWidths[code];

                default:
                    return HelveticaWidths[code];
            }
        }

        /// <summary>
        /// Measures the width of a string in points.
        /// </summary>
        internal static float MeasureString(string text, PdfFont font, float fontSize,
            float charSpacing = 0f)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            int total = 0;
            foreach (char c in text)
                total += GetCharWidth(font, c);
            float width = total * fontSize / 1000f;
            if (charSpacing != 0f && text.Length > 0)
                width += text.Length * charSpacing;
            return width;
        }

        /// <summary>
        /// Word-wraps text to fit within the specified width. Returns a list of lines.
        /// Handles explicit \n line breaks.
        /// </summary>
        internal static List<string> WrapText(string text, PdfFont font, float fontSize,
            float maxWidth, float charSpacing = 0f)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(text))
            {
                result.Add("");
                return result;
            }

            // Split on explicit newlines first
            var paragraphs = text.Split('\n');
            foreach (var para in paragraphs)
            {
                if (string.IsNullOrEmpty(para))
                {
                    result.Add("");
                    continue;
                }

                var words = para.Split(' ');
                var currentLine = new System.Text.StringBuilder();
                float currentWidth = 0;
                float spaceWidth = MeasureString(" ", font, fontSize, charSpacing);

                foreach (var word in words)
                {
                    float wordWidth = MeasureString(word, font, fontSize, charSpacing);

                    if (currentLine.Length == 0)
                    {
                        // First word on line — always add it even if it exceeds width
                        currentLine.Append(word);
                        currentWidth = wordWidth;
                    }
                    else if (currentWidth + spaceWidth + wordWidth <= maxWidth)
                    {
                        currentLine.Append(' ').Append(word);
                        currentWidth += spaceWidth + wordWidth;
                    }
                    else
                    {
                        result.Add(currentLine.ToString());
                        currentLine.Clear();
                        currentLine.Append(word);
                        currentWidth = wordWidth;
                    }
                }

                result.Add(currentLine.ToString());
            }

            return result;
        }

        // ── Rich text wrapping ─────────────────────────────────────────────────

        internal class StyledWord
        {
            internal string Text;
            internal PdfFontSource Font;
            internal float FontSize;
            internal PdfColor Color;
            internal float Width;
            internal bool StartsNewLine;
            internal bool HasLeadingSpace;
            internal PdfFontSource SpaceFont;
            internal float SpaceFontSize;
            internal float SpaceWidth;
            internal bool Underline;
            internal bool SpaceUnderline;
            internal float Opacity;
            internal string Link;
            internal string SpaceLink;
            internal float CharacterSpacing;
            internal bool Bold;
            internal bool Italic;
        }

        internal class RichLine
        {
            internal readonly List<StyledWord> Words = new List<StyledWord>();
            internal float TotalWidth;
            internal float MaxFontSize;
        }

        /// <summary>
        /// Word-wraps a sequence of TextSpans to fit within the specified width.
        /// Returns a list of RichLines, each containing styled words.
        /// </summary>
        internal static List<RichLine> WrapRichText(IEnumerable<TextSpan> spans, float maxWidth)
        {
            // Phase 1: Tokenize all spans into a flat list of StyledWords.
            var allWords = new List<StyledWord>();
            bool pendingSpace = false;
            PdfFontSource pendingSpaceFont = PdfFont.Helvetica;
            float pendingSpaceFontSize = 12f;
            bool pendingSpaceUnderline = false;
            string pendingSpaceLink = null;
            float pendingSpaceCharSpacing = 0f;

            foreach (var span in spans)
            {
                if (string.IsNullOrEmpty(span.Text))
                    continue;

                var paragraphs = span.Text.Split('\n');

                for (int pIdx = 0; pIdx < paragraphs.Length; pIdx++)
                {
                    bool forceNewLine = (pIdx > 0);
                    var para = paragraphs[pIdx];

                    if (forceNewLine)
                    {
                        pendingSpace = false;
                    }

                    if (para.Length == 0)
                    {
                        if (forceNewLine)
                        {
                            allWords.Add(new StyledWord
                            {
                                Text = "",
                                Font = span.Font,
                                FontSize = span.FontSize,
                                Color = span.Color,
                                Width = 0,
                                StartsNewLine = true,
                                HasLeadingSpace = false,
                                SpaceWidth = 0,
                                Underline = span.Underline,
                                Opacity = span.Opacity,
                                Link = span.Link,
                                CharacterSpacing = span.CharacterSpacing,
                                Bold = span.Bold,
                                Italic = span.Italic
                            });
                        }
                        continue;
                    }

                    int charIdx = 0;
                    bool firstWordInPara = true;

                    while (charIdx < para.Length)
                    {
                        // Skip spaces
                        bool foundSpace = false;
                        while (charIdx < para.Length && para[charIdx] == ' ')
                        {
                            foundSpace = true;
                            charIdx++;
                        }

                        if (charIdx >= para.Length)
                        {
                            // Trailing spaces — record for next span
                            if (foundSpace)
                            {
                                pendingSpace = true;
                                pendingSpaceFont = span.Font;
                                pendingSpaceFontSize = span.FontSize;
                                pendingSpaceUnderline = span.Underline;
                                pendingSpaceLink = span.Link;
                                pendingSpaceCharSpacing = span.CharacterSpacing;
                            }
                            break;
                        }

                        // Extract word
                        int wordStart = charIdx;
                        while (charIdx < para.Length && para[charIdx] != ' ')
                            charIdx++;
                        string wordText = para.Substring(wordStart, charIdx - wordStart);
                        float wordWidth = MeasureString(wordText, span.Font, span.FontSize, span.CharacterSpacing);

                        // Determine leading space
                        bool hasSpace = false;
                        PdfFontSource spaceFont = span.Font;
                        float spaceFontSize = span.FontSize;
                        bool spaceUnderline = span.Underline;
                        string spaceLink = span.Link;
                        float spaceCharSpacing = span.CharacterSpacing;

                        if (foundSpace && !firstWordInPara)
                        {
                            // Space within this paragraph, from this span
                            hasSpace = true;
                        }
                        else if (foundSpace && firstWordInPara)
                        {
                            // Leading space at start of paragraph fragment
                            // This means the span text started with space (e.g. " world")
                            if (allWords.Count > 0 && !forceNewLine)
                            {
                                hasSpace = true;
                            }
                        }
                        else if (pendingSpace && firstWordInPara && !forceNewLine && allWords.Count > 0)
                        {
                            // Previous span ended with a trailing space
                            hasSpace = true;
                            spaceFont = pendingSpaceFont;
                            spaceFontSize = pendingSpaceFontSize;
                            spaceUnderline = pendingSpaceUnderline;
                            spaceLink = pendingSpaceLink;
                            spaceCharSpacing = pendingSpaceCharSpacing;
                        }

                        float spaceWidth = hasSpace
                            ? MeasureString(" ", spaceFont, spaceFontSize, spaceCharSpacing)
                            : 0;

                        allWords.Add(new StyledWord
                        {
                            Text = wordText,
                            Font = span.Font,
                            FontSize = span.FontSize,
                            Color = span.Color,
                            Width = wordWidth,
                            StartsNewLine = forceNewLine && firstWordInPara,
                            HasLeadingSpace = hasSpace,
                            SpaceFont = spaceFont,
                            SpaceFontSize = spaceFontSize,
                            SpaceWidth = spaceWidth,
                            Underline = span.Underline,
                            SpaceUnderline = spaceUnderline,
                            Opacity = span.Opacity,
                            Link = span.Link,
                            SpaceLink = spaceLink,
                            CharacterSpacing = span.CharacterSpacing,
                            Bold = span.Bold,
                            Italic = span.Italic
                        });

                        pendingSpace = false;
                        forceNewLine = false;
                        firstWordInPara = false;
                    }

                    // If paragraph had no words (all spaces) and was a forced break
                    if (firstWordInPara && forceNewLine)
                    {
                        allWords.Add(new StyledWord
                        {
                            Text = "",
                            Font = span.Font,
                            FontSize = span.FontSize,
                            Color = span.Color,
                            Width = 0,
                            StartsNewLine = true,
                            HasLeadingSpace = false,
                            SpaceWidth = 0,
                            Underline = span.Underline,
                            Opacity = span.Opacity,
                            Link = span.Link,
                            CharacterSpacing = span.CharacterSpacing,
                            Bold = span.Bold,
                            Italic = span.Italic
                        });
                    }
                }
            }

            // Phase 2: Wrap styled words into lines using greedy algorithm.
            var lines = new List<RichLine>();

            if (allWords.Count == 0)
            {
                lines.Add(new RichLine());
                return lines;
            }

            var currentLine = new RichLine();
            float currentLineWidth = 0;

            for (int i = 0; i < allWords.Count; i++)
            {
                var word = allWords[i];

                if (word.StartsNewLine && (currentLine.Words.Count > 0 || lines.Count > 0))
                {
                    lines.Add(currentLine);
                    currentLine = new RichLine();
                    currentLineWidth = 0;
                }

                if (currentLine.Words.Count == 0)
                {
                    // First word on line — always place it
                    word.HasLeadingSpace = false;
                    word.SpaceWidth = 0;
                    currentLine.Words.Add(word);
                    currentLineWidth = word.Width;
                    if (word.FontSize > currentLine.MaxFontSize)
                        currentLine.MaxFontSize = word.FontSize;
                }
                else
                {
                    float neededWidth = (word.HasLeadingSpace ? word.SpaceWidth : 0) + word.Width;

                    if (currentLineWidth + neededWidth <= maxWidth)
                    {
                        currentLine.Words.Add(word);
                        currentLineWidth += neededWidth;
                        if (word.FontSize > currentLine.MaxFontSize)
                            currentLine.MaxFontSize = word.FontSize;
                    }
                    else
                    {
                        lines.Add(currentLine);
                        currentLine = new RichLine();
                        word.HasLeadingSpace = false;
                        word.SpaceWidth = 0;
                        currentLine.Words.Add(word);
                        currentLineWidth = word.Width;
                        if (word.FontSize > currentLine.MaxFontSize)
                            currentLine.MaxFontSize = word.FontSize;
                    }
                }
            }

            lines.Add(currentLine);

            // Phase 3: Compute TotalWidth for each line.
            foreach (var line in lines)
            {
                float w = 0;
                foreach (var word in line.Words)
                {
                    if (word.HasLeadingSpace)
                        w += word.SpaceWidth;
                    w += word.Width;
                }
                line.TotalWidth = w;
            }

            return lines;
        }

        // ── Width arrays for standard 14 fonts (WinAnsiEncoding, codes 0-255) ──
        // Values are in 1/1000 of an em unit, sourced from Adobe AFM files.

        private static readonly ushort[] HelveticaWidths = new ushort[256]
        {
            // 0-31: control characters
            0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
            // 32-47: space ! " # $ % & ' ( ) * + , - . /
            278,278,355,556,556,889,667,191,333,333,389,584,278,333,278,278,
            // 48-63: 0-9 : ; < = > ?
            556,556,556,556,556,556,556,556,556,556,278,278,584,584,584,556,
            // 64-79: @ A-O
            1015,667,667,722,722,667,611,778,722,278,500,667,556,833,722,778,
            // 80-95: P-Z [ \ ] ^ _
            667,778,722,667,611,722,667,944,667,667,611,278,278,278,469,556,
            // 96-111: ` a-o
            222,556,556,500,556,556,278,556,556,222,222,500,222,833,556,556,
            // 112-127: p-z { | } ~ DEL
            556,556,333,500,278,556,500,722,500,500,500,334,260,334,584,0,
            // 128-143
            556,0,222,556,333,1000,556,556,333,1000,667,333,1000,0,611,0,
            // 144-159
            0,222,222,333,333,350,556,1000,333,1000,500,333,944,0,500,667,
            // 160-175
            278,333,556,556,556,556,260,556,333,737,370,556,584,333,737,333,
            // 176-191
            400,584,333,333,333,556,537,278,333,333,365,556,834,834,834,611,
            // 192-207
            667,667,667,667,667,667,1000,722,667,667,667,667,278,278,278,278,
            // 208-223
            722,722,778,778,778,778,778,584,778,722,722,722,722,667,667,611,
            // 224-239
            556,556,556,556,556,556,889,500,556,556,556,556,278,278,278,278,
            // 240-255
            556,556,556,556,556,556,556,584,611,556,556,556,556,500,556,500
        };

        private static readonly ushort[] HelveticaBoldWidths = new ushort[256]
        {
            0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
            278,333,474,556,556,889,722,238,333,333,389,584,278,333,278,278,
            556,556,556,556,556,556,556,556,556,556,333,333,584,584,584,611,
            975,722,722,722,722,667,611,778,722,278,556,722,611,833,722,778,
            667,778,722,667,611,722,667,944,667,667,611,333,278,333,584,556,
            222,556,611,556,611,556,333,611,611,278,278,556,278,889,611,611,
            611,611,389,556,333,611,556,778,556,556,500,389,280,389,584,0,
            556,0,278,556,500,1000,556,556,333,1000,667,333,1000,0,611,0,
            0,278,278,500,500,350,556,1000,333,1000,556,333,944,0,500,667,
            278,333,556,556,556,556,280,556,333,737,370,556,584,333,737,333,
            400,584,333,333,333,611,556,278,333,333,365,556,834,834,834,611,
            722,722,722,722,722,722,1000,722,667,667,667,667,278,278,278,278,
            722,722,778,778,778,778,778,584,778,722,722,722,722,667,667,611,
            556,556,556,556,556,556,889,556,556,556,556,556,278,278,278,278,
            611,611,611,611,611,611,611,584,611,611,611,611,611,556,611,556
        };

        private static readonly ushort[] TimesRomanWidths = new ushort[256]
        {
            0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
            250,333,408,500,500,833,778,180,333,333,500,564,250,333,250,278,
            500,500,500,500,500,500,500,500,500,500,278,278,564,564,564,444,
            921,722,667,667,722,611,556,722,722,333,389,722,611,889,722,722,
            556,722,667,556,611,722,722,944,722,722,611,333,278,333,469,500,
            333,444,500,444,500,444,333,500,500,278,278,500,278,778,500,500,
            500,500,333,389,278,500,500,722,500,500,444,480,200,480,541,0,
            500,0,333,500,444,1000,500,500,333,1000,556,333,889,0,611,0,
            0,333,333,444,444,350,500,1000,333,980,389,333,722,0,444,722,
            250,333,500,500,500,500,200,500,333,760,276,500,564,333,760,333,
            400,564,300,300,333,500,453,250,333,300,310,500,750,750,750,444,
            722,722,722,722,722,722,889,667,611,611,611,611,333,333,333,333,
            722,722,722,722,722,722,722,564,722,722,722,722,722,722,556,500,
            444,444,444,444,444,444,667,444,444,444,444,444,278,278,278,278,
            500,500,500,500,500,500,500,564,500,500,500,500,500,500,500,500
        };

        private static readonly ushort[] TimesBoldWidths = new ushort[256]
        {
            0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
            250,333,555,500,500,1000,833,278,333,333,500,570,250,333,250,278,
            500,500,500,500,500,500,500,500,500,500,333,333,570,570,570,500,
            930,722,667,722,722,667,611,778,778,389,500,778,667,944,722,778,
            611,778,722,556,667,722,722,1000,722,722,667,333,278,333,581,500,
            333,500,556,444,556,444,333,500,556,278,333,556,278,833,556,500,
            556,556,444,389,333,556,500,722,500,500,444,394,220,394,520,0,
            500,0,333,500,500,1000,500,500,333,1000,556,333,1000,0,667,0,
            0,333,333,500,500,350,500,1000,333,1000,389,333,722,0,444,722,
            250,333,500,500,500,500,220,500,333,747,300,500,570,333,747,333,
            400,570,300,300,333,556,540,250,333,300,330,500,750,750,750,500,
            722,722,722,722,722,722,1000,722,667,667,667,667,389,389,389,389,
            722,722,778,778,778,778,778,570,778,722,722,722,722,722,611,556,
            500,500,500,500,500,500,722,444,444,444,444,444,278,278,278,278,
            500,556,500,500,500,500,500,570,500,556,556,556,556,500,556,500
        };

        private static readonly ushort[] TimesItalicWidths = new ushort[256]
        {
            0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
            250,333,420,500,500,833,778,214,333,333,500,675,250,333,250,278,
            500,500,500,500,500,500,500,500,500,500,333,333,675,675,675,500,
            920,611,611,667,722,611,611,722,722,333,444,667,556,833,667,722,
            611,722,611,500,556,722,611,833,611,556,556,389,278,389,422,500,
            333,500,500,444,500,444,278,500,500,278,278,444,278,722,500,500,
            500,500,389,389,278,500,444,667,444,444,389,400,275,400,541,0,
            500,0,333,500,556,889,500,500,333,1000,500,333,944,0,556,0,
            0,333,333,556,556,350,500,889,333,980,389,333,667,0,389,556,
            250,389,500,500,500,500,275,500,333,760,276,500,675,333,760,333,
            400,675,300,300,333,500,523,250,333,300,310,500,750,750,750,500,
            611,611,611,611,611,611,889,667,611,611,611,611,333,333,333,333,
            722,667,722,722,722,722,722,675,722,722,722,722,722,556,611,500,
            500,500,500,500,500,500,667,444,444,444,444,444,278,278,278,278,
            500,500,500,500,500,500,500,675,500,500,500,500,500,444,500,444
        };

        private static readonly ushort[] TimesBoldItalicWidths = new ushort[256]
        {
            0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
            250,389,555,500,500,833,778,278,333,333,500,570,250,333,250,278,
            500,500,500,500,500,500,500,500,500,500,333,333,570,570,570,500,
            832,667,667,667,722,667,667,722,778,389,500,667,611,889,722,722,
            611,722,667,556,611,722,667,889,667,611,611,333,278,333,570,500,
            333,500,500,444,500,444,333,500,556,278,278,500,278,778,556,500,
            500,500,389,389,278,556,444,667,500,444,389,348,220,348,570,0,
            500,0,333,500,500,1000,500,500,333,1000,556,333,944,0,611,0,
            0,333,333,500,500,350,500,1000,333,1000,389,333,722,0,389,611,
            250,389,500,500,500,500,220,500,333,747,266,500,606,333,747,333,
            400,570,300,300,333,576,500,250,333,300,300,500,750,750,750,500,
            667,667,667,667,667,667,944,667,667,667,667,667,389,389,389,389,
            722,722,722,722,722,722,722,570,722,722,722,722,722,611,611,500,
            500,500,500,500,500,500,722,444,444,444,444,444,278,278,278,278,
            500,556,500,500,500,500,500,570,500,556,556,556,556,444,500,444
        };

        private static readonly ushort[] SymbolWidths = new ushort[256]
        {
            0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
            250,333,713,500,549,833,778,439,333,333,500,549,250,549,250,278,
            500,500,500,500,500,500,500,500,500,500,278,278,549,549,549,444,
            549,722,667,722,612,611,763,603,722,333,631,722,686,889,722,722,
            768,741,556,592,611,690,439,768,645,795,611,333,863,333,658,500,
            500,631,549,549,494,439,521,411,603,329,603,549,549,576,521,549,
            549,521,549,603,439,576,713,686,493,686,494,480,200,480,549,0,
            0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
            750,620,247,549,167,713,500,753,753,753,753,1042,987,603,987,603,
            400,549,411,549,549,713,494,460,549,549,549,549,1000,603,1000,658,
            823,686,795,987,768,768,823,768,768,713,713,713,713,713,713,713,
            768,713,790,790,890,823,549,250,713,603,603,1042,987,603,987,603,
            494,329,790,790,786,713,384,384,384,384,384,384,494,494,494,494,
            0,329,274,686,686,686,384,384,384,384,384,384,494,494,494,0
        };

        private static readonly ushort[] ZapfDingbatsWidths = new ushort[256]
        {
            0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
            278,974,961,974,980,719,789,790,791,690,960,939,549,855,911,933,
            911,945,974,755,846,762,761,571,677,763,760,759,754,494,552,537,
            577,692,786,788,788,790,793,794,816,823,789,841,823,833,816,831,
            923,744,723,749,790,792,695,776,768,792,759,707,708,682,701,826,
            815,789,789,707,687,696,689,786,787,713,791,785,791,873,761,762,
            762,759,759,892,892,788,784,438,138,277,415,392,392,668,668,0,
            390,390,317,317,276,276,509,509,410,410,234,234,334,334,0,0,
            0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
            278,732,544,544,910,667,760,760,776,595,694,626,788,788,788,788,
            788,788,788,788,788,788,788,788,788,788,788,788,788,788,788,788,
            788,788,788,788,788,788,788,788,788,788,788,788,788,788,788,788,
            788,788,788,788,894,838,1016,458,748,924,748,918,927,928,928,834,
            873,828,924,924,917,930,931,463,883,836,836,867,867,696,696,874,
            0,874,760,946,771,865,771,888,967,888,831,873,927,970,918,0
        };
    }
}
