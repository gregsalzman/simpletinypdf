using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SimpleTinyPDF
{
    /// <summary>
    /// Represents a single page and provides all drawing operations.
    /// By default coordinates are top-down (Y=0 is the top of the page).
    /// Set <see cref="CoordinateOrigin"/> to <see cref="SimpleTinyPDF.CoordinateOrigin.BottomUp"/>
    /// to use native PDF coordinates (Y=0 at the bottom).
    /// </summary>
    public sealed class PdfPage
    {
        private readonly StringBuilder _content = new StringBuilder();
        private readonly Dictionary<string, PdfFont> _usedFonts = new Dictionary<string, PdfFont>();
        private readonly Dictionary<string, PdfImage> _usedImages = new Dictionary<string, PdfImage>();
        private readonly Dictionary<float, string> _usedGraphicsStates = new Dictionary<float, string>();
        private readonly Dictionary<PdfFont, EncodingExtension> _encodingExtensions = new Dictionary<PdfFont, EncodingExtension>();
        private int _nextFontId = 1;
        private int _nextImageId = 1;
        private int _nextGsId = 1;
        private readonly List<LinkAnnotation> _linkAnnotations = new List<LinkAnnotation>();
        internal PdfDocument Document { get; set; }

        /// <summary>Page width in points.</summary>
        public float Width { get; }

        /// <summary>Page height in points.</summary>
        public float Height { get; }

        /// <summary>
        /// Controls the Y-axis direction. TopDown (default) means Y=0 is the top of the page.
        /// BottomUp uses native PDF coordinates where Y=0 is the bottom.
        /// </summary>
        public CoordinateOrigin CoordinateOrigin { get; set; } = CoordinateOrigin.TopDown;

        internal PdfPage(float width, float height)
        {
            Width = width;
            Height = height;
        }

        internal Dictionary<string, PdfFont> GetUsedFonts() => _usedFonts;
        internal Dictionary<string, PdfImage> GetUsedImages() => _usedImages;
        internal Dictionary<float, string> GetUsedGraphicsStates() => _usedGraphicsStates;
        internal string GetContentStream() => _content.ToString();
        internal IReadOnlyList<LinkAnnotation> GetLinkAnnotations() => _linkAnnotations;

        internal EncodingExtension GetOrCreateEncodingExtension(PdfFont font)
        {
            if (!_encodingExtensions.TryGetValue(font, out var ext))
            {
                ext = new EncodingExtension();
                _encodingExtensions[font] = ext;
            }
            return ext;
        }

        internal EncodingExtension GetEncodingExtension(PdfFont font)
        {
            _encodingExtensions.TryGetValue(font, out var ext);
            return ext;
        }

        private string EnsureFont(PdfFont font)
        {
            foreach (var kv in _usedFonts)
            {
                if (kv.Value.Equals(font))
                    return kv.Key;
            }
            var id = "F" + _nextFontId++;
            _usedFonts[id] = font;
            return id;
        }

        private string EnsureImage(PdfImage image)
        {
            foreach (var kv in _usedImages)
            {
                if (kv.Value.Equals(image))
                    return kv.Key;
            }
            var id = "Im" + _nextImageId++;
            _usedImages[id] = image;
            return id;
        }

        private string EnsureGraphicsState(float opacity)
        {
            if (_usedGraphicsStates.TryGetValue(opacity, out var existing))
                return existing;
            var id = "GS" + _nextGsId++;
            _usedGraphicsStates[opacity] = id;
            return id;
        }

        private void AppendOpacity(float opacity)
        {
            if (opacity >= 1f) return;
            opacity = Math.Max(0f, Math.Min(1f, opacity));
            var gsId = EnsureGraphicsState(opacity);
            _content.AppendFormat("/{0} gs\n", gsId);
        }

        private static string F(float v) => v.ToString("0.####", CultureInfo.InvariantCulture);

        private void AppendRotation(float angleDegrees, float pdfOriginX, float pdfOriginY)
        {
            if (Math.Abs(angleDegrees) < 0.001f) return;
            // Negate: user positive = clockwise, PDF positive = counter-clockwise
            float radians = -angleDegrees * (float)Math.PI / 180f;
            float cos = (float)Math.Cos(radians);
            float sin = (float)Math.Sin(radians);
            // Translate-rotate-translate for rotation around arbitrary point
            _content.AppendFormat("1 0 0 1 {0} {1} cm\n", F(pdfOriginX), F(pdfOriginY));
            _content.AppendFormat("{0} {1} {2} {3} 0 0 cm\n", F(cos), F(sin), F(-sin), F(cos));
            _content.AppendFormat("1 0 0 1 {0} {1} cm\n", F(-pdfOriginX), F(-pdfOriginY));
        }

        private void AppendColorFill(PdfColor color)
        {
            if (color.IsCmyk)
                _content.AppendFormat(CultureInfo.InvariantCulture,
                    "{0} {1} {2} {3} k\n", F(color.C), F(color.M), F(color.Y), F(color.K));
            else
                _content.AppendFormat(CultureInfo.InvariantCulture,
                    "{0} {1} {2} rg\n", F(color.R), F(color.G), F(color.B));
        }

        private void AppendColorStroke(PdfColor color)
        {
            if (color.IsCmyk)
                _content.AppendFormat(CultureInfo.InvariantCulture,
                    "{0} {1} {2} {3} K\n", F(color.C), F(color.M), F(color.Y), F(color.K));
            else
                _content.AppendFormat(CultureInfo.InvariantCulture,
                    "{0} {1} {2} RG\n", F(color.R), F(color.G), F(color.B));
        }

        /// <summary>Measures the width of a string in points for the given font and size.</summary>
        public float MeasureText(string text, PdfFont font, float fontSize) =>
            FontMetrics.MeasureString(text, font, fontSize);

        private void AppendUnderline(float x, float pdfY, float width, float fontSize, PdfColor color)
        {
            float ulY = pdfY - 100f * fontSize / 1000f;
            float ulH = 50f * fontSize / 1000f;
            AppendColorFill(color);
            _content.AppendFormat("{0} {1} {2} {3} re f\n", F(x), F(ulY), F(width), F(ulH));
        }

        // ── Text ──────────────────────────────────────────────────

        /// <summary>
        /// Draws a single line of text at the specified position.
        /// </summary>
        public void DrawText(string text, float x, float y,
            PdfFont font = PdfFont.Helvetica, float fontSize = 12f,
            PdfColor? color = null, TextAlignment alignment = TextAlignment.Left,
            bool underline = false, float opacity = 1f,
            string link = null, float rotation = 0f)
        {
            if (string.IsNullOrEmpty(text)) return;
            var c = color ?? PdfColor.Black;
            var fontId = EnsureFont(font);
            float pdfY = CoordinateOrigin == CoordinateOrigin.TopDown
                ? Height - y - fontSize : y;

            float drawX = x;
            if (alignment == TextAlignment.Center)
                drawX = x - MeasureText(text, font, fontSize) / 2f;
            else if (alignment == TextAlignment.Right)
                drawX = x - MeasureText(text, font, fontSize);

            _content.Append("q\n");
            AppendOpacity(opacity);
            AppendRotation(rotation, x, pdfY);
            _content.Append("BT\n");
            _content.AppendFormat("/{0} {1} Tf\n", fontId, F(fontSize));
            AppendColorFill(c);
            _content.AppendFormat("{0} {1} Td\n", F(drawX), F(pdfY));
            _content.AppendFormat("{0} Tj\n", PdfStringHelper.Escape(text, GetOrCreateEncodingExtension(font)));
            _content.Append("ET\n");
            if (underline)
                AppendUnderline(drawX, pdfY, MeasureText(text, font, fontSize), fontSize, c);
            _content.Append("Q\n");
            AddLinkAnnotation(link, drawX, pdfY, MeasureText(text, font, fontSize), fontSize);
        }

        /// <summary>
        /// Draws a single line of mixed-format text at the specified position.
        /// Each TextSpan can have its own font, font size, and color.
        /// </summary>
        public void DrawRichText(IEnumerable<TextSpan> spans, float x, float y,
            TextAlignment alignment = TextAlignment.Left, float rotation = 0f)
        {
            if (spans == null) return;

            var spanList = new List<TextSpan>();
            float totalWidth = 0;
            float maxFontSize = 0;
            bool hasUnderline = false;
            foreach (var span in spans)
            {
                if (string.IsNullOrEmpty(span.Text)) continue;
                spanList.Add(span);
                totalWidth += MeasureText(span.Text, span.Font, span.FontSize);
                if (span.FontSize > maxFontSize)
                    maxFontSize = span.FontSize;
                if (span.Underline) hasUnderline = true;
            }

            if (spanList.Count == 0) return;

            float drawX = x;
            if (alignment == TextAlignment.Center)
                drawX = x - totalWidth / 2f;
            else if (alignment == TextAlignment.Right)
                drawX = x - totalWidth;

            float pdfY = CoordinateOrigin == CoordinateOrigin.TopDown
                ? Height - y - maxFontSize : y;

            _content.Append("q\n");
            AppendRotation(rotation, x, pdfY);
            _content.Append("BT\n");
            _content.AppendFormat("{0} {1} Td\n", F(drawX), F(pdfY));

            PdfFont? lastFont = null;
            float lastFontSize = -1;
            PdfColor? lastColor = null;
            float lastOpacity = -1f;

            // Track span positions for underline and links
            List<(float x, float width, float fontSize, PdfColor color)> ulSpans = null;
            if (hasUnderline)
                ulSpans = new List<(float, float, float, PdfColor)>();
            List<(float x, float width, float fontSize, string url)> linkSpans = null;
            float cursorX = drawX;

            foreach (var span in spanList)
            {
                if (span.Opacity != lastOpacity)
                {
                    if (span.Opacity < 1f)
                        AppendOpacity(span.Opacity);
                    else if (lastOpacity >= 0f && lastOpacity < 1f)
                    {
                        // Reset to full opacity
                        var gsId = EnsureGraphicsState(1f);
                        _content.AppendFormat("/{0} gs\n", gsId);
                    }
                    lastOpacity = span.Opacity;
                }

                if (lastFont == null || !lastFont.Value.Equals(span.Font) || lastFontSize != span.FontSize)
                {
                    var fontId = EnsureFont(span.Font);
                    _content.AppendFormat("/{0} {1} Tf\n", fontId, F(span.FontSize));
                    lastFont = span.Font;
                    lastFontSize = span.FontSize;
                }

                if (lastColor == null || !lastColor.Value.Equals(span.Color))
                {
                    AppendColorFill(span.Color);
                    lastColor = span.Color;
                }

                float spanWidth = MeasureText(span.Text, span.Font, span.FontSize);
                _content.AppendFormat("{0} Tj\n", PdfStringHelper.Escape(span.Text, GetOrCreateEncodingExtension(span.Font)));
                if (span.Underline)
                    ulSpans?.Add((cursorX, spanWidth, span.FontSize, span.Color));
                if (!string.IsNullOrEmpty(span.Link))
                {
                    if (linkSpans == null)
                        linkSpans = new List<(float, float, float, string)>();
                    linkSpans.Add((cursorX, spanWidth, span.FontSize, span.Link));
                }
                cursorX += spanWidth;
            }

            _content.Append("ET\n");
            if (ulSpans != null)
            {
                foreach (var ul in ulSpans)
                    AppendUnderline(ul.x, pdfY, ul.width, ul.fontSize, ul.color);
            }
            _content.Append("Q\n");
            if (linkSpans != null)
            {
                foreach (var ls in linkSpans)
                    AddLinkAnnotation(ls.url, ls.x, pdfY, ls.width, ls.fontSize);
            }
        }

        /// <summary>
        /// Draws text wrapped within a box. Returns the Y position after the last line.
        /// </summary>
        public float DrawTextBox(string text, float x, float y, float width,
            PdfFont font = PdfFont.Helvetica, float fontSize = 12f,
            float lineSpacing = 1.2f, PdfColor? color = null,
            TextAlignment alignment = TextAlignment.Left, bool underline = false,
            float opacity = 1f, string link = null, float rotation = 0f)
        {
            if (string.IsNullOrEmpty(text)) return y;
            var c = color ?? PdfColor.Black;
            var fontId = EnsureFont(font);
            float lineHeight = fontSize * lineSpacing;

            var lines = FontMetrics.WrapText(text, font, fontSize, width);
            float currentY = y;

            // Collect underline positions to draw after the text block
            List<(float drawX, float pdfY, float lineWidth)> ulLines = null;
            if (underline)
                ulLines = new List<(float, float, float)>();

            bool topDown = CoordinateOrigin == CoordinateOrigin.TopDown;

            _content.Append("q\n");
            AppendOpacity(opacity);
            float rotOriginY = topDown ? Height - y - fontSize : y;
            AppendRotation(rotation, x, rotOriginY);
            _content.Append("BT\n");
            _content.AppendFormat("/{0} {1} Tf\n", fontId, F(fontSize));
            AppendColorFill(c);

            foreach (var line in lines)
            {
                float pdfY = topDown ? Height - currentY - fontSize : currentY;
                float drawX = x;
                float lineW = MeasureText(line, font, fontSize);
                if (alignment == TextAlignment.Center)
                    drawX = x + (width - lineW) / 2f;
                else if (alignment == TextAlignment.Right)
                    drawX = x + width - lineW;

                // Use Tm (text matrix) for absolute positioning instead of Td (relative)
                _content.AppendFormat("1 0 0 1 {0} {1} Tm\n", F(drawX), F(pdfY));
                _content.AppendFormat("{0} Tj\n", PdfStringHelper.Escape(line, GetOrCreateEncodingExtension(font)));
                ulLines?.Add((drawX, pdfY, lineW));
                if (!string.IsNullOrEmpty(link))
                    AddLinkAnnotation(link, drawX, pdfY, lineW, fontSize);
                currentY += topDown ? lineHeight : -lineHeight;
            }

            _content.Append("ET\n");
            if (ulLines != null)
            {
                foreach (var ul in ulLines)
                    AppendUnderline(ul.drawX, ul.pdfY, ul.lineWidth, fontSize, c);
            }
            _content.Append("Q\n");
            return currentY;
        }

        /// <summary>
        /// Draws mixed-format text wrapped within a box. Each TextSpan can have its own
        /// font, font size, and color. Returns the Y position after the last line.
        /// </summary>
        public float DrawRichTextBox(IEnumerable<TextSpan> spans, float x, float y,
            float width, float lineSpacing = 1.2f,
            TextAlignment alignment = TextAlignment.Left, float rotation = 0f)
        {
            if (spans == null) return y;

            // Filter out empty spans before wrapping
            var spanList = new List<TextSpan>();
            foreach (var s in spans)
            {
                if (s != null && !string.IsNullOrEmpty(s.Text))
                    spanList.Add(s);
            }
            if (spanList.Count == 0) return y;

            var lines = FontMetrics.WrapRichText(spanList, width);
            if (lines.Count == 0) return y;

            float currentY = y;

            // Collect underline segments to draw after the text block
            var ulSegments = new List<(float x, float pdfY, float width, float fontSize, PdfColor color)>();

            bool topDown = CoordinateOrigin == CoordinateOrigin.TopDown;
            float firstMaxFs = lines[0].MaxFontSize;
            if (firstMaxFs <= 0) firstMaxFs = 12f;
            float rotOriginY = topDown ? Height - y - firstMaxFs : y;
            _content.Append("q\n");
            AppendRotation(rotation, x, rotOriginY);
            _content.Append("BT\n");

            PdfFont? lastFont = null;
            float lastFontSize = -1;
            PdfColor? lastColor = null;
            float lastOpacity = -1f;

            foreach (var line in lines)
            {
                float maxFontSize = line.MaxFontSize;
                if (maxFontSize <= 0) maxFontSize = 12f;
                float lineHeight = maxFontSize * lineSpacing;

                float pdfY = topDown ? Height - currentY - maxFontSize : currentY;

                float drawX = x;
                if (alignment == TextAlignment.Center)
                    drawX = x + (width - line.TotalWidth) / 2f;
                else if (alignment == TextAlignment.Right)
                    drawX = x + width - line.TotalWidth;

                _content.AppendFormat("1 0 0 1 {0} {1} Tm\n", F(drawX), F(pdfY));

                float cursorX = drawX;

                // Track contiguous link segments within this line
                string currentLinkUrl = null;
                float linkStartX = 0;
                float linkMaxFontSize = 0;

                foreach (var word in line.Words)
                {
                    if (string.IsNullOrEmpty(word.Text) && !word.HasLeadingSpace)
                        continue;

                    if (word.Opacity != lastOpacity)
                    {
                        if (word.Opacity < 1f)
                            AppendOpacity(word.Opacity);
                        else if (lastOpacity >= 0f && lastOpacity < 1f)
                        {
                            var gsId = EnsureGraphicsState(1f);
                            _content.AppendFormat("/{0} gs\n", gsId);
                        }
                        lastOpacity = word.Opacity;
                    }

                    if (word.HasLeadingSpace)
                    {
                        if (lastFont == null || !lastFont.Value.Equals(word.SpaceFont) ||
                            lastFontSize != word.SpaceFontSize)
                        {
                            var spaceFontId = EnsureFont(word.SpaceFont);
                            _content.AppendFormat("/{0} {1} Tf\n", spaceFontId, F(word.SpaceFontSize));
                            lastFont = word.SpaceFont;
                            lastFontSize = word.SpaceFontSize;
                        }
                        _content.AppendFormat("{0} Tj\n", PdfStringHelper.Escape(" ", GetOrCreateEncodingExtension(word.SpaceFont)));
                        if (word.SpaceUnderline)
                            ulSegments.Add((cursorX, pdfY, word.SpaceWidth, word.SpaceFontSize, word.Color));

                        // Handle link for the space portion
                        if (word.SpaceLink != currentLinkUrl)
                        {
                            if (currentLinkUrl != null)
                                AddLinkAnnotation(currentLinkUrl, linkStartX, pdfY, cursorX - linkStartX, linkMaxFontSize);
                            if (word.SpaceLink != null)
                            {
                                currentLinkUrl = word.SpaceLink;
                                linkStartX = cursorX;
                                linkMaxFontSize = word.SpaceFontSize;
                            }
                            else
                                currentLinkUrl = null;
                        }
                        else if (currentLinkUrl != null && word.SpaceFontSize > linkMaxFontSize)
                            linkMaxFontSize = word.SpaceFontSize;

                        cursorX += word.SpaceWidth;
                    }

                    if (string.IsNullOrEmpty(word.Text))
                        continue;

                    if (lastFont == null || !lastFont.Value.Equals(word.Font) ||
                        lastFontSize != word.FontSize)
                    {
                        var fontId = EnsureFont(word.Font);
                        _content.AppendFormat("/{0} {1} Tf\n", fontId, F(word.FontSize));
                        lastFont = word.Font;
                        lastFontSize = word.FontSize;
                    }

                    if (lastColor == null || !lastColor.Value.Equals(word.Color))
                    {
                        AppendColorFill(word.Color);
                        lastColor = word.Color;
                    }

                    _content.AppendFormat("{0} Tj\n", PdfStringHelper.Escape(word.Text, GetOrCreateEncodingExtension(word.Font)));
                    if (word.Underline)
                        ulSegments.Add((cursorX, pdfY, word.Width, word.FontSize, word.Color));

                    // Handle link for the word portion
                    if (word.Link != currentLinkUrl)
                    {
                        if (currentLinkUrl != null)
                            AddLinkAnnotation(currentLinkUrl, linkStartX, pdfY, cursorX - linkStartX, linkMaxFontSize);
                        if (word.Link != null)
                        {
                            currentLinkUrl = word.Link;
                            linkStartX = cursorX;
                            linkMaxFontSize = word.FontSize;
                        }
                        else
                            currentLinkUrl = null;
                    }
                    else if (currentLinkUrl != null && word.FontSize > linkMaxFontSize)
                        linkMaxFontSize = word.FontSize;

                    cursorX += word.Width;
                }

                // Finalize any pending link at end of line
                if (currentLinkUrl != null)
                    AddLinkAnnotation(currentLinkUrl, linkStartX, pdfY, cursorX - linkStartX, linkMaxFontSize);

                currentY += topDown ? lineHeight : -lineHeight;
            }

            _content.Append("ET\n");
            foreach (var ul in ulSegments)
                AppendUnderline(ul.x, ul.pdfY, ul.width, ul.fontSize, ul.color);
            _content.Append("Q\n");
            return currentY;
        }

        // ── Lists ─────────────────────────────────────────────────

        /// <summary>
        /// Draws a bulleted list. Returns the Y position after the last item.
        /// </summary>
        public float DrawBulletList(string[] items, float x, float y, float width,
            PdfFont font = PdfFont.Helvetica, float fontSize = 12f,
            float lineSpacing = 1.2f, PdfColor? color = null,
            string bulletChar = "\u2022", float indent = 15f)
        {
            if (items == null || items.Length == 0) return y;
            bool topDown = CoordinateOrigin == CoordinateOrigin.TopDown;
            float currentY = y;
            foreach (var item in items)
            {
                DrawText(bulletChar, x, currentY, font, fontSize, color);
                currentY = DrawTextBox(item, x + indent, currentY, width - indent,
                    font, fontSize, lineSpacing, color);
                currentY += (topDown ? 1 : -1) * fontSize * 0.3f;
            }
            return currentY;
        }

        /// <summary>
        /// Draws a numbered list. Returns the Y position after the last item.
        /// </summary>
        public float DrawNumberedList(string[] items, float x, float y, float width,
            PdfFont font = PdfFont.Helvetica, float fontSize = 12f,
            float lineSpacing = 1.2f, PdfColor? color = null,
            int startNumber = 1, float indent = 20f)
        {
            if (items == null || items.Length == 0) return y;
            bool topDown = CoordinateOrigin == CoordinateOrigin.TopDown;
            float currentY = y;
            for (int i = 0; i < items.Length; i++)
            {
                string num = (startNumber + i) + ".";
                DrawText(num, x, currentY, font, fontSize, color);
                currentY = DrawTextBox(items[i], x + indent, currentY, width - indent,
                    font, fontSize, lineSpacing, color);
                currentY += (topDown ? 1 : -1) * fontSize * 0.3f;
            }
            return currentY;
        }

        // ── Shapes ────────────────────────────────────────────────

        /// <summary>Draws a straight line between two points.</summary>
        public void DrawLine(float x1, float y1, float x2, float y2,
            PdfColor? color = null, float lineWidth = 1f, float rotation = 0f)
        {
            var c = color ?? PdfColor.Black;
            bool topDown = CoordinateOrigin == CoordinateOrigin.TopDown;
            float py1 = topDown ? Height - y1 : y1;
            float py2 = topDown ? Height - y2 : y2;
            _content.Append("q\n");
            AppendRotation(rotation, x1, py1);
            _content.AppendFormat("{0} w\n", F(lineWidth));
            AppendColorStroke(c);
            _content.AppendFormat("{0} {1} m {2} {3} l S\n", F(x1), F(py1), F(x2), F(py2));
            _content.Append("Q\n");
        }

        /// <summary>Draws a rectangle outline (stroke only).</summary>
        public void DrawRectangle(float x, float y, float width, float height,
            PdfColor? strokeColor = null, float lineWidth = 1f, float rotation = 0f)
        {
            var c = strokeColor ?? PdfColor.Black;
            float pdfY = CoordinateOrigin == CoordinateOrigin.TopDown
                ? Height - y - height : y;
            _content.Append("q\n");
            AppendRotation(rotation, x, pdfY + height);
            _content.AppendFormat("{0} w\n", F(lineWidth));
            AppendColorStroke(c);
            _content.AppendFormat("{0} {1} {2} {3} re S\n", F(x), F(pdfY), F(width), F(height));
            _content.Append("Q\n");
        }

        /// <summary>Draws a filled rectangle, optionally with a border.</summary>
        public void DrawFilledRectangle(float x, float y, float width, float height,
            PdfColor fillColor, PdfColor? strokeColor = null, float lineWidth = 0f,
            float rotation = 0f)
        {
            float pdfY = CoordinateOrigin == CoordinateOrigin.TopDown
                ? Height - y - height : y;
            _content.Append("q\n");
            AppendRotation(rotation, x, pdfY + height);
            AppendColorFill(fillColor);
            if (strokeColor.HasValue && lineWidth > 0)
            {
                _content.AppendFormat("{0} w\n", F(lineWidth));
                AppendColorStroke(strokeColor.Value);
                _content.AppendFormat("{0} {1} {2} {3} re B\n", F(x), F(pdfY), F(width), F(height));
            }
            else
            {
                _content.AppendFormat("{0} {1} {2} {3} re f\n", F(x), F(pdfY), F(width), F(height));
            }
            _content.Append("Q\n");
        }

        // ── Images ────────────────────────────────────────────────

        /// <summary>Draws an image at the specified position and size.</summary>
        public void DrawImage(PdfImage image, float x, float y, float width, float height,
            float opacity = 1f, ImageScaleMode scaleMode = ImageScaleMode.Stretch,
            float rotation = 0f)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image));
            if (width <= 0)
                throw new ArgumentException("Width must be positive.", nameof(width));
            if (height <= 0)
                throw new ArgumentException("Height must be positive.", nameof(height));

            // Auto-register with the document and get the canonical instance
            if (Document != null)
                image = Document.AddImage(image);

            var imgId = EnsureImage(image);

            // Compute actual draw rectangle based on scale mode
            float drawX = x, drawY = y, drawW = width, drawH = height;
            bool clip = false;

            if (scaleMode != ImageScaleMode.Stretch && image.PixelWidth > 0 && image.PixelHeight > 0)
            {
                float imgAspect = (float)image.PixelWidth / image.PixelHeight;
                float boxAspect = width / height;

                float scale;
                if (scaleMode == ImageScaleMode.Fit)
                    scale = imgAspect > boxAspect ? width / image.PixelWidth : height / image.PixelHeight;
                else // Fill
                    scale = imgAspect > boxAspect ? height / image.PixelHeight : width / image.PixelWidth;

                drawW = image.PixelWidth * scale;
                drawH = image.PixelHeight * scale;
                drawX = x + (width - drawW) / 2f;
                drawY = y + (height - drawH) / 2f;
                clip = scaleMode == ImageScaleMode.Fill;
            }

            bool topDown = CoordinateOrigin == CoordinateOrigin.TopDown;
            float pdfY = topDown ? Height - drawY - drawH : drawY;
            _content.Append("q\n");

            // For Fill mode, clip to the original target rectangle
            if (clip)
            {
                float clipPdfY = topDown ? Height - y - height : y;
                _content.AppendFormat("{0} {1} {2} {3} re W n\n",
                    F(x), F(clipPdfY), F(width), F(height));
            }

            AppendOpacity(opacity);

            // Apply user rotation around the user-provided (x, y) position
            float rotPdfY = topDown ? Height - y : y;
            AppendRotation(rotation, x, rotPdfY);

            // Apply CTM based on EXIF orientation
            switch (image.ExifOrientation)
            {
                case 2: // Mirror horizontal
                    _content.AppendFormat("{0} 0 0 {1} {2} {3} cm\n",
                        F(-drawW), F(drawH), F(drawX + drawW), F(pdfY));
                    break;
                case 3: // Rotate 180
                    _content.AppendFormat("{0} 0 0 {1} {2} {3} cm\n",
                        F(-drawW), F(-drawH), F(drawX + drawW), F(pdfY + drawH));
                    break;
                case 4: // Mirror vertical
                    _content.AppendFormat("{0} 0 0 {1} {2} {3} cm\n",
                        F(drawW), F(-drawH), F(drawX), F(pdfY + drawH));
                    break;
                case 5: // Transpose
                    _content.AppendFormat("0 {0} {1} 0 {2} {3} cm\n",
                        F(-drawH), F(-drawW), F(drawX + drawW), F(pdfY + drawH));
                    break;
                case 6: // Rotate 90 CW
                    _content.AppendFormat("0 {0} {1} 0 {2} {3} cm\n",
                        F(-drawH), F(drawW), F(drawX), F(pdfY + drawH));
                    break;
                case 7: // Transverse
                    _content.AppendFormat("0 {0} {1} 0 {2} {3} cm\n",
                        F(drawH), F(drawW), F(drawX), F(pdfY));
                    break;
                case 8: // Rotate 90 CCW
                    _content.AppendFormat("0 {0} {1} 0 {2} {3} cm\n",
                        F(drawH), F(-drawW), F(drawX + drawW), F(pdfY));
                    break;
                default: // Orientation 1 (normal) or unknown
                    _content.AppendFormat("{0} 0 0 {1} {2} {3} cm\n",
                        F(drawW), F(drawH), F(drawX), F(pdfY));
                    break;
            }

            _content.AppendFormat("/{0} Do\n", imgId);
            _content.Append("Q\n");
        }

        // ── Tables ────────────────────────────────────────────────

        /// <summary>
        /// Draws a table starting at the specified position. If the table exceeds
        /// the page, continuation pages are automatically created.
        /// Returns the Y position below the table on the final page.
        /// </summary>
        /// <param name="continuationY">Y position for the table on continuation pages. If null, uses the same Y as the first page.</param>
        public float DrawTable(PdfTable table, float x, float y, float bottomMargin = 50f, float? continuationY = null)
        {
            return table.Render(this, x, y, bottomMargin, continuationY);
        }
        // ── Link annotations ──────────────────────────────────────

        private void AddLinkAnnotation(string url, float drawX, float pdfY, float width, float fontSize)
        {
            if (string.IsNullOrEmpty(url)) return;
            _linkAnnotations.Add(new LinkAnnotation
            {
                X0 = drawX,
                Y0 = pdfY - fontSize * 0.1f,
                X1 = drawX + width,
                Y1 = pdfY + fontSize,
                Url = url
            });
        }
    }

    internal struct LinkAnnotation
    {
        internal float X0, Y0, X1, Y1;
        internal string Url;
    }
}
