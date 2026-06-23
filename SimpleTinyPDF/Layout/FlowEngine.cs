using System;
using System.Collections.Generic;

namespace SimpleTinyPDF
{
    internal class FlowEngine
    {
        private readonly PdfDocument _doc;
        private readonly PageSize _pageSize;
        private readonly PdfMargins _margins;
        private readonly HeaderFooterOptions _headerFooter;
        private readonly ParagraphOptions _defaultOptions;
        private readonly int _totalPages;

        private PdfPage _currentPage;
        private float _currentY;
        private int _pageNumber;
        private float _contentLeft;
        private float _contentWidth;
        private float _contentBottom;
        private bool _pageStarted;

        internal FlowEngine(PdfDocument doc, PageSize pageSize, PdfMargins margins,
            HeaderFooterOptions headerFooter, ParagraphOptions defaultOptions, int totalPages)
        {
            _doc = doc;
            _pageSize = pageSize ?? PageSize.A4;
            _margins = margins ?? new PdfMargins(72);
            _headerFooter = headerFooter ?? new HeaderFooterOptions();
            _defaultOptions = defaultOptions;
            _totalPages = totalPages;
        }

        internal int Render(IReadOnlyList<LayoutElement> elements)
        {
            if (elements.Count == 0) return 0;

            StartNewPage();

            foreach (var element in elements)
            {
                switch (element.Type)
                {
                    case LayoutElementType.Paragraph:
                        RenderParagraph(element);
                        break;
                    case LayoutElementType.RichParagraph:
                        RenderRichParagraph(element);
                        break;
                    case LayoutElementType.Image:
                        RenderImage(element);
                        break;
                    case LayoutElementType.Table:
                        RenderTable(element);
                        break;
                    case LayoutElementType.List:
                        RenderList(element);
                        break;
                    case LayoutElementType.PageBreak:
                        StartNewPage();
                        break;
                }
            }

            DrawFooterOnCurrentPage();
            return _pageNumber;
        }

        private void StartNewPage()
        {
            if (_pageStarted)
                DrawFooterOnCurrentPage();

            _currentPage = _doc.AddPage(_pageSize);
            _pageNumber++;
            _pageStarted = true;

            _contentLeft = _margins.Left;
            _contentWidth = _currentPage.Width - _margins.Left - _margins.Right;
            _contentBottom = _currentPage.Height - _margins.Bottom;
            _currentY = _margins.Top;

            DrawHeaderOnPage(_currentPage, _pageNumber);
        }

        private void EnsureSpace(float height)
        {
            if (_currentY + height > _contentBottom)
                StartNewPage();
        }

        // ── Header / Footer ────────────────────────────────────────

        private PageContext MakeContext(int pageNumber) =>
            new PageContext { PageNumber = pageNumber, TotalPages = _totalPages };

        private void DrawHeaderOnPage(PdfPage page, int pageNumber)
        {
            var ctx = MakeContext(pageNumber);
            Action<PdfPage, PageContext> action = null;

            if (ctx.IsFirstPage && _headerFooter.FirstPageHeader != null)
                action = _headerFooter.FirstPageHeader;
            else if (ctx.IsEvenPage && _headerFooter.EvenPageHeader != null)
                action = _headerFooter.EvenPageHeader;
            else
                action = _headerFooter.Header;

            action?.Invoke(page, ctx);
        }

        private void DrawFooterOnPage(PdfPage page, int pageNumber)
        {
            var ctx = MakeContext(pageNumber);
            Action<PdfPage, PageContext> action = null;

            if (ctx.IsFirstPage && _headerFooter.FirstPageFooter != null)
                action = _headerFooter.FirstPageFooter;
            else if (ctx.IsEvenPage && _headerFooter.EvenPageFooter != null)
                action = _headerFooter.EvenPageFooter;
            else
                action = _headerFooter.Footer;

            action?.Invoke(page, ctx);
        }

        private void DrawFooterOnCurrentPage()
        {
            if (_pageStarted)
                DrawFooterOnPage(_currentPage, _pageNumber);
        }

        // ── Paragraph ──────────────────────────────────────────────

        private ParagraphOptions EffectiveOptions(ParagraphOptions element) =>
            element ?? _defaultOptions ?? new ParagraphOptions();

        private PdfFontSource EffectiveFont(ParagraphOptions opts) =>
            opts.Font ?? (PdfFontSource)PdfFont.Helvetica;

        private PdfColor EffectiveColor(ParagraphOptions opts) =>
            opts.Color ?? PdfColor.Black;

        private void RenderParagraph(LayoutElement element)
        {
            var opts = EffectiveOptions(element.ParagraphOptions);
            var font = EffectiveFont(opts);
            var text = element.Text ?? "";

            _currentY += opts.SpaceBefore;

            // Tab stops: render without wrapping
            if (opts.TabStops != null && opts.TabStops.Length > 0 && text.Contains("\t"))
            {
                RenderTabbedParagraph(text, opts);
                _currentY += opts.SpaceAfter;
                return;
            }

            var lines = FontMetrics.WrapText(text, font, opts.FontSize, _contentWidth, opts.CharacterSpacing);
            float lineHeight = opts.FontSize * opts.LineSpacing;

            for (int i = 0; i < lines.Count; i++)
            {
                EnsureSpace(lineHeight);
                bool isLastLine = (i == lines.Count - 1);
                RenderTextLine(lines[i], opts, font, lineHeight, isLastLine);
                _currentY += lineHeight;
            }

            _currentY += opts.SpaceAfter;
        }

        private void RenderTextLine(string line, ParagraphOptions opts, PdfFontSource font,
            float lineHeight, bool isLastLine)
        {
            var color = EffectiveColor(opts);
            var alignment = opts.Alignment;

            // Justify: non-last lines get justified; last line is left-aligned
            if (alignment == TextAlignment.Justify)
            {
                if (!isLastLine)
                {
                    RenderJustifiedLine(line, opts, font);
                    return;
                }
                alignment = TextAlignment.Left;
            }

            float lineWidth = FontMetrics.MeasureString(line, font, opts.FontSize, opts.CharacterSpacing);
            float x;
            switch (alignment)
            {
                case TextAlignment.Center:
                    x = _contentLeft + (_contentWidth - lineWidth) / 2f;
                    break;
                case TextAlignment.Right:
                    x = _contentLeft + _contentWidth - lineWidth;
                    break;
                default:
                    x = _contentLeft;
                    break;
            }

            _currentPage.DrawText(line, x, _currentY, font, opts.FontSize,
                color, TextAlignment.Left, opts.Underline, opts.Opacity,
                null, 0f, null, opts.LineSpacing, opts.CharacterSpacing,
                opts.Bold, opts.Italic);
        }

        private void RenderJustifiedLine(string line, ParagraphOptions opts, PdfFontSource font)
        {
            var color = EffectiveColor(opts);
            var words = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length <= 1)
            {
                _currentPage.DrawText(line, _contentLeft, _currentY, font, opts.FontSize,
                    color, TextAlignment.Left, opts.Underline, opts.Opacity,
                    null, 0f, null, opts.LineSpacing, opts.CharacterSpacing,
                    opts.Bold, opts.Italic);
                return;
            }

            float totalWordsWidth = 0;
            var wordWidths = new float[words.Length];
            for (int i = 0; i < words.Length; i++)
            {
                wordWidths[i] = FontMetrics.MeasureString(words[i], font, opts.FontSize, opts.CharacterSpacing);
                totalWordsWidth += wordWidths[i];
            }

            float totalSpace = _contentWidth - totalWordsWidth;
            float spacePerGap = totalSpace / (words.Length - 1);
            float x = _contentLeft;

            for (int i = 0; i < words.Length; i++)
            {
                _currentPage.DrawText(words[i], x, _currentY, font, opts.FontSize,
                    color, TextAlignment.Left, opts.Underline, opts.Opacity,
                    null, 0f, null, opts.LineSpacing, opts.CharacterSpacing,
                    opts.Bold, opts.Italic);
                x += wordWidths[i] + spacePerGap;
            }
        }

        // ── Rich Paragraph ─────────────────────────────────────────

        private void RenderRichParagraph(LayoutElement element)
        {
            var opts = EffectiveOptions(element.ParagraphOptions);
            _currentY += opts.SpaceBefore;

            var richLines = FontMetrics.WrapRichText(element.Spans, _contentWidth);

            for (int i = 0; i < richLines.Count; i++)
            {
                var richLine = richLines[i];
                float lineHeight = richLine.MaxFontSize * opts.LineSpacing;
                if (lineHeight <= 0) lineHeight = opts.FontSize * opts.LineSpacing;

                EnsureSpace(lineHeight);

                bool isLastLine = (i == richLines.Count - 1);
                RenderRichLine(richLine, opts, isLastLine);
                _currentY += lineHeight;
            }

            _currentY += opts.SpaceAfter;
        }

        private void RenderRichLine(FontMetrics.RichLine richLine, ParagraphOptions opts, bool isLastLine)
        {
            var alignment = opts.Alignment;
            if (alignment == TextAlignment.Justify && isLastLine)
                alignment = TextAlignment.Left;

            float maxFs = richLine.MaxFontSize > 0 ? richLine.MaxFontSize : opts.FontSize;

            float x;
            if (alignment == TextAlignment.Justify)
            {
                RenderJustifiedRichLine(richLine, opts, maxFs);
                return;
            }

            switch (alignment)
            {
                case TextAlignment.Center:
                    x = _contentLeft + (_contentWidth - richLine.TotalWidth) / 2f;
                    break;
                case TextAlignment.Right:
                    x = _contentLeft + _contentWidth - richLine.TotalWidth;
                    break;
                default:
                    x = _contentLeft;
                    break;
            }

            foreach (var word in richLine.Words)
            {
                if (word.HasLeadingSpace)
                    x += word.SpaceWidth;

                // Offset Y so all words share the same baseline (align to maxFontSize)
                float wordY = _currentY + (maxFs - word.FontSize);

                _currentPage.DrawText(word.Text, x, wordY, word.Font, word.FontSize,
                    word.Color, TextAlignment.Left, word.Underline, word.Opacity,
                    word.Link, 0f, null, opts.LineSpacing, word.CharacterSpacing,
                    word.Bold, word.Italic);
                x += word.Width;
            }
        }

        private void RenderJustifiedRichLine(FontMetrics.RichLine richLine, ParagraphOptions opts,
            float maxFs)
        {
            int gapCount = 0;
            foreach (var word in richLine.Words)
                if (word.HasLeadingSpace) gapCount++;

            float extraSpace = _contentWidth - richLine.TotalWidth;
            float extraPerGap = gapCount > 0 ? extraSpace / gapCount : 0;

            float x = _contentLeft;
            foreach (var word in richLine.Words)
            {
                if (word.HasLeadingSpace)
                    x += word.SpaceWidth + extraPerGap;

                float wordY = _currentY + (maxFs - word.FontSize);

                _currentPage.DrawText(word.Text, x, wordY, word.Font, word.FontSize,
                    word.Color, TextAlignment.Left, word.Underline, word.Opacity,
                    word.Link, 0f, null, opts.LineSpacing, word.CharacterSpacing,
                    word.Bold, word.Italic);
                x += word.Width;
            }
        }

        // ── Tab Stops ──────────────────────────────────────────────

        private void RenderTabbedParagraph(string text, ParagraphOptions opts)
        {
            var lines = text.Split('\n');
            float lineHeight = opts.FontSize * opts.LineSpacing;

            foreach (var line in lines)
            {
                EnsureSpace(lineHeight);
                RenderTabbedLine(line, opts);
                _currentY += lineHeight;
            }
        }

        private void RenderTabbedLine(string line, ParagraphOptions opts)
        {
            var font = EffectiveFont(opts);
            var color = EffectiveColor(opts);
            var segments = line.Split('\t');
            var tabStops = opts.TabStops;

            float x = _contentLeft;

            for (int i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                float segWidth = FontMetrics.MeasureString(segment, font, opts.FontSize, opts.CharacterSpacing);

                if (i > 0 && tabStops != null && i - 1 < tabStops.Length)
                {
                    var tab = tabStops[i - 1];
                    float tabX = _contentLeft + tab.Position;

                    // Draw leader characters
                    if (tab.Leader.HasValue)
                        DrawLeader(tab.Leader.Value, x, tabX, font, opts);

                    switch (tab.Alignment)
                    {
                        case TabAlignment.Left:
                            x = tabX;
                            break;
                        case TabAlignment.Center:
                            x = tabX - segWidth / 2f;
                            break;
                        case TabAlignment.Right:
                            x = tabX - segWidth;
                            break;
                        case TabAlignment.Decimal:
                            int dotIdx = segment.IndexOf(tab.DecimalChar);
                            if (dotIdx >= 0)
                            {
                                float beforeDot = FontMetrics.MeasureString(
                                    segment.Substring(0, dotIdx), font, opts.FontSize, opts.CharacterSpacing);
                                x = tabX - beforeDot;
                            }
                            else
                            {
                                x = tabX - segWidth;
                            }
                            break;
                    }
                }

                _currentPage.DrawText(segment, x, _currentY, font, opts.FontSize,
                    color, TextAlignment.Left, opts.Underline, opts.Opacity,
                    null, 0f, null, opts.LineSpacing, opts.CharacterSpacing,
                    opts.Bold, opts.Italic);

                x += segWidth;
            }
        }

        private void DrawLeader(char leader, float startX, float endX, PdfFontSource font,
            ParagraphOptions opts)
        {
            string leaderStr = leader.ToString();
            float leaderWidth = FontMetrics.MeasureString(leaderStr, font, opts.FontSize, opts.CharacterSpacing);
            if (leaderWidth <= 0) return;

            var color = EffectiveColor(opts);
            float x = startX;
            while (x + leaderWidth <= endX)
            {
                _currentPage.DrawText(leaderStr, x, _currentY, font, opts.FontSize,
                    color, TextAlignment.Left, false, opts.Opacity);
                x += leaderWidth;
            }
        }

        // ── Image ──────────────────────────────────────────────────

        private void RenderImage(LayoutElement element)
        {
            var opts = element.ImageOptions ?? new ImageOptions();
            var image = element.Image;

            _currentY += opts.SpaceBefore;

            // Calculate display dimensions
            float drawWidth, drawHeight;
            ResolveImageSize(image, opts, out drawWidth, out drawHeight);

            // Cap to content width
            if (drawWidth > _contentWidth)
            {
                float scale = _contentWidth / drawWidth;
                drawWidth = _contentWidth;
                drawHeight *= scale;
            }

            EnsureSpace(drawHeight);

            // Horizontal alignment
            float x;
            switch (opts.Alignment)
            {
                case TextAlignment.Center:
                    x = _contentLeft + (_contentWidth - drawWidth) / 2f;
                    break;
                case TextAlignment.Right:
                    x = _contentLeft + _contentWidth - drawWidth;
                    break;
                default:
                    x = _contentLeft;
                    break;
            }

            _currentPage.DrawImage(image, x, _currentY, drawWidth, drawHeight,
                opts.Opacity, opts.ScaleMode);

            _currentY += drawHeight;
            _currentY += opts.SpaceAfter;
        }

        private void ResolveImageSize(PdfImage image, ImageOptions opts,
            out float width, out float height)
        {
            float pixelW = image.PixelWidth;
            float pixelH = image.PixelHeight;
            float aspect = pixelH > 0 ? pixelW / pixelH : 1f;

            if (opts.Width.HasValue && opts.Height.HasValue)
            {
                width = opts.Width.Value;
                height = opts.Height.Value;
            }
            else if (opts.Width.HasValue)
            {
                width = opts.Width.Value;
                height = width / aspect;
            }
            else if (opts.Height.HasValue)
            {
                height = opts.Height.Value;
                width = height * aspect;
            }
            else
            {
                // Fit to content width
                width = _contentWidth;
                height = width / aspect;
            }
        }

        // ── Table ──────────────────────────────────────────────────

        private void RenderTable(LayoutElement element)
        {
            int pagesBefore = _doc.PageCount;
            int pageNumBefore = _pageNumber;

            _currentY = _currentPage.DrawTable(element.Table, _contentLeft, _currentY,
                _margins.Bottom, _margins.Top);

            int pagesAfter = _doc.PageCount;
            if (pagesAfter > pagesBefore)
            {
                // Table created continuation pages — apply headers/footers
                for (int i = pagesBefore; i < pagesAfter; i++)
                {
                    _pageNumber++;
                    var page = _doc.Pages[i];
                    DrawHeaderOnPage(page, _pageNumber);
                    DrawFooterOnPage(page, _pageNumber);
                }

                _currentPage = _doc.Pages[pagesAfter - 1];
            }
        }

        // ── List ───────────────────────────────────────────────────

        private void RenderList(LayoutElement element)
        {
            int pagesBefore = _doc.PageCount;

            var (resultPage, resultY) = _currentPage.DrawList(
                element.ListItems, _contentLeft, _currentY, _contentWidth,
                element.ListStyle, _margins.Bottom,
                continuationY: _margins.Top);

            _currentY = resultY;

            int pagesAfter = _doc.PageCount;
            if (pagesAfter > pagesBefore)
            {
                for (int i = pagesBefore; i < pagesAfter; i++)
                {
                    _pageNumber++;
                    var page = _doc.Pages[i];
                    DrawHeaderOnPage(page, _pageNumber);
                    DrawFooterOnPage(page, _pageNumber);
                }

                _currentPage = resultPage;
            }
        }
    }
}
