using System;
using System.Collections.Generic;

namespace SimpleTinyPDF
{
    internal class FlowEngine
    {
        private readonly PdfDocument _doc;
        private readonly PageSize _defaultPageSize;
        private readonly PdfMargins _defaultMargins;
        private readonly HeaderFooterOptions _defaultHeaderFooter;
        private readonly ParagraphOptions _defaultOptions;
        private readonly int _totalPages;
        private readonly List<object> _eventHandlers;
        private readonly CustomRenderer _renderer;
        private readonly int[] _sectionTotalPages;

        // Current section settings (resolved from section options + defaults)
        private PageSize _pageSize;
        private PdfMargins _margins;
        private HeaderFooterOptions _headerFooter;
        private ColumnFlowEngine _columns;

        private PdfPage _currentPage;
        private float _currentY;
        private float _columnY; // Y position at top of column (for column resets)
        private int _pageNumber;
        private int _sectionIndex;
        private int _sectionPageNumber;
        private float _contentLeft;
        private float _contentWidth;
        private float _contentBottom;
        private bool _pageStarted;

        internal FlowEngine(PdfDocument doc, PageSize pageSize, PdfMargins margins,
            HeaderFooterOptions headerFooter, ParagraphOptions defaultOptions, int totalPages,
            List<object> eventHandlers = null, CustomRenderer renderer = null,
            int[] sectionTotalPages = null)
        {
            _doc = doc;
            _defaultPageSize = pageSize ?? PageSize.A4;
            _defaultMargins = margins ?? new PdfMargins(72);
            _defaultHeaderFooter = headerFooter ?? new HeaderFooterOptions();
            _defaultOptions = defaultOptions;
            _totalPages = totalPages;
            _eventHandlers = eventHandlers ?? new List<object>();
            _renderer = renderer;
            _sectionTotalPages = sectionTotalPages;

            // Initialize with defaults for implicit first section
            _pageSize = _defaultPageSize;
            _margins = _defaultMargins;
            _headerFooter = _defaultHeaderFooter;
        }

        private bool _sectionStarted;

        private void EnsurePageStarted()
        {
            if (!_pageStarted)
            {
                StartNewPage();
                if (!_sectionStarted)
                {
                    _sectionStarted = true;
                    FireEvent(PageEventType.SectionStarted);
                }
            }
        }

        internal RenderResult Render(IReadOnlyList<LayoutElement> elements)
        {
            if (elements.Count == 0) return new RenderResult(0, new int[0]);

            var sectionPageCounts = new List<int>();
            _sectionIndex = 0;
            _sectionPageNumber = 0;

            foreach (var element in elements)
            {
                switch (element.Type)
                {
                    case LayoutElementType.Paragraph:
                        EnsurePageStarted();
                        RenderParagraph(element);
                        break;
                    case LayoutElementType.RichParagraph:
                        EnsurePageStarted();
                        RenderRichParagraph(element);
                        break;
                    case LayoutElementType.Image:
                        EnsurePageStarted();
                        RenderImage(element);
                        break;
                    case LayoutElementType.Table:
                        EnsurePageStarted();
                        RenderTable(element);
                        break;
                    case LayoutElementType.List:
                        EnsurePageStarted();
                        RenderList(element);
                        break;
                    case LayoutElementType.PageBreak:
                        EnsurePageStarted();
                        StartNewPage();
                        break;
                    case LayoutElementType.SectionBreak:
                        // Finish current section if it was started
                        if (_sectionStarted)
                        {
                            DrawFooterOnCurrentPage();
                            FireEvent(PageEventType.PageFinished);
                            FireEvent(PageEventType.SectionFinished);
                            sectionPageCounts.Add(_sectionPageNumber);
                            _pageStarted = false;
                        }
                        else
                        {
                            // No pages rendered yet for implicit first section
                            sectionPageCounts.Add(0);
                        }

                        // Start new section
                        _sectionIndex++;
                        _sectionStarted = false;
                        ApplySectionOptions(element.SectionOptions);
                        break;
                    case LayoutElementType.ColumnBreak:
                        EnsurePageStarted();
                        HandleColumnBreak();
                        break;
                }
            }

            if (_pageStarted)
            {
                DrawFooterOnCurrentPage();
                FireEvent(PageEventType.PageFinished);
            }
            if (_sectionStarted)
                FireEvent(PageEventType.SectionFinished);
            sectionPageCounts.Add(_sectionPageNumber);

            return new RenderResult(_pageNumber, sectionPageCounts.ToArray());
        }

        private void ApplySectionOptions(SectionOptions options)
        {
            _pageSize = options.PageSize ?? _defaultPageSize;
            _margins = options.Margins ?? _defaultMargins;
            _headerFooter = options.HeaderFooter ?? _defaultHeaderFooter;

            if (options.RestartPageNumbers)
                _sectionPageNumber = 0;

            if (options.ColumnCount > 1)
            {
                float totalWidth = _pageSize.Width - _margins.Left - _margins.Right;
                _columns = new ColumnFlowEngine(options.ColumnCount, options.ColumnGap,
                    _margins.Left, totalWidth);
            }
            else
            {
                _columns = null;
            }
        }

        private void HandleColumnBreak()
        {
            if (_columns == null)
            {
                // No columns — treat as page break
                StartNewPage();
                return;
            }

            if (_columns.NextColumn())
            {
                // Moved to next column on same page — reset Y to column top
                _currentY = _columnY;
                UpdateContentBoundsForColumn();
            }
            else
            {
                // Was on last column — new page
                StartNewPage();
            }
        }

        private void StartNewPage()
        {
            if (_pageStarted)
            {
                DrawFooterOnCurrentPage();
                FireEvent(PageEventType.PageFinished);
            }

            _currentPage = _doc.AddPage(_pageSize);
            _pageNumber++;
            _sectionPageNumber++;
            _pageStarted = true;

            _contentBottom = _currentPage.Height - _margins.Bottom;
            _currentY = _margins.Top;
            _columnY = _margins.Top;

            if (_columns != null)
            {
                _columns.Reset();
                UpdateContentBoundsForColumn();
            }
            else
            {
                _contentLeft = _margins.Left;
                _contentWidth = _currentPage.Width - _margins.Left - _margins.Right;
            }

            DrawHeaderOnPage(_currentPage, _pageNumber);
            FireEvent(PageEventType.PageCreated);
        }

        private void UpdateContentBoundsForColumn()
        {
            _contentLeft = _columns.ColumnX;
            _contentWidth = _columns.ColumnWidth;
        }

        private void EnsureSpace(float height)
        {
            // Custom renderer override
            if (_renderer != null)
            {
                float remaining = _contentBottom - _currentY;
                bool? custom = _renderer.ShouldBreakPage(remaining, height, MakeContext());
                if (custom.HasValue)
                {
                    if (custom.Value)
                    {
                        if (_columns != null && _columns.NextColumn())
                        {
                            _currentY = _columnY;
                            UpdateContentBoundsForColumn();
                        }
                        else
                        {
                            if (_columns != null) _columns.Reset();
                            StartNewPage();
                        }
                    }
                    return;
                }
            }

            if (_currentY + height > _contentBottom)
            {
                if (_columns != null && _columns.NextColumn())
                {
                    _currentY = _columnY;
                    UpdateContentBoundsForColumn();
                }
                else
                {
                    StartNewPage();
                }
            }
        }

        // ── Events ──────────────────────────────────────────────────

        private void FireEvent(PageEventType eventType)
        {
            if (_eventHandlers.Count == 0) return;

            var ctx = MakeContext();
            foreach (var handler in _eventHandlers)
            {
                if (handler is Action<PageEventType, PdfPage, PageContext> action)
                    action(eventType, _currentPage, ctx);
                else if (handler is IPageEventHandler iHandler)
                    iHandler.HandleEvent(eventType, _currentPage, ctx);
            }
        }

        // ── Header / Footer ────────────────────────────────────────

        private PageContext MakeContext() => MakeContext(_pageNumber);

        private PageContext MakeContext(int pageNumber) =>
            new PageContext
            {
                PageNumber = pageNumber,
                TotalPages = _totalPages,
                SectionPageNumber = _sectionPageNumber,
                SectionTotalPages = _sectionTotalPages != null && _sectionIndex < _sectionTotalPages.Length
                    ? _sectionTotalPages[_sectionIndex]
                    : 0,
                SectionIndex = _sectionIndex
            };

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

            // Custom renderer override
            if (_renderer != null)
            {
                _currentY += opts.SpaceBefore;
                float? customY = _renderer.RenderParagraph(_currentPage, element.Text ?? "",
                    _contentLeft, _currentY, _contentWidth, opts, MakeContext());
                if (customY.HasValue)
                {
                    _currentY = customY.Value + opts.SpaceAfter;
                    return;
                }
                _currentY -= opts.SpaceBefore; // undo — default will handle it
            }

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

            // Custom renderer override
            if (_renderer != null)
            {
                _currentY += opts.SpaceBefore;
                float? customY = _renderer.RenderRichParagraph(_currentPage, element.Spans,
                    _contentLeft, _currentY, _contentWidth, opts, MakeContext());
                if (customY.HasValue)
                {
                    _currentY = customY.Value + opts.SpaceAfter;
                    return;
                }
                _currentY -= opts.SpaceBefore;
            }

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

            // Custom renderer override
            if (_renderer != null)
            {
                _currentY += opts.SpaceBefore;
                float? customY = _renderer.RenderImage(_currentPage, image,
                    _contentLeft, _currentY, _contentWidth, opts, MakeContext());
                if (customY.HasValue)
                {
                    _currentY = customY.Value + opts.SpaceAfter;
                    return;
                }
                _currentY -= opts.SpaceBefore;
            }

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
            // Custom renderer override
            if (_renderer != null)
            {
                float? customY = _renderer.RenderTable(_currentPage, element.Table,
                    _contentLeft, _currentY, MakeContext());
                if (customY.HasValue)
                {
                    _currentY = customY.Value;
                    return;
                }
            }

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
                    _sectionPageNumber++;
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
                    _sectionPageNumber++;
                    var page = _doc.Pages[i];
                    DrawHeaderOnPage(page, _pageNumber);
                    DrawFooterOnPage(page, _pageNumber);
                }

                _currentPage = resultPage;
            }
        }

        // ── Result ─────────────────────────────────────────────────

        internal class RenderResult
        {
            internal int TotalPages { get; }
            internal int[] SectionPageCounts { get; }

            internal RenderResult(int totalPages, int[] sectionPageCounts)
            {
                TotalPages = totalPages;
                SectionPageCounts = sectionPageCounts;
            }
        }
    }
}
