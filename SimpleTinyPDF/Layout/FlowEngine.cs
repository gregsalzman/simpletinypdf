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
        private readonly DebugOptions _debug;

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
            int[] sectionTotalPages = null, DebugOptions debug = null)
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
            _debug = debug;

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

        internal RenderResult Render(IEnumerable<LayoutElement> elements)
        {
            var sectionPageCounts = new List<int>();
            _sectionIndex = 0;
            _sectionPageNumber = 0;

            bool any = false;
            using (var enumerator = elements.GetEnumerator())
            {
                bool hasCurrent = enumerator.MoveNext();
                while (hasCurrent)
                {
                    var element = enumerator.Current;
                    bool hasNext = enumerator.MoveNext();
                    var next = hasNext ? enumerator.Current : null;
                    any = true;
                    ProcessElement(element, next, sectionPageCounts);
                    hasCurrent = hasNext;
                }
            }

            if (!any) return new RenderResult(0, new int[0]);

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

        private void ProcessElement(LayoutElement element, LayoutElement next,
            List<int> sectionPageCounts)
        {
            switch (element.Type)
            {
                case LayoutElementType.Paragraph:
                case LayoutElementType.RichParagraph:
                {
                    EnsurePageStarted();
                    var opts = EffectiveOptions(element.ParagraphOptions);
                    ApplyKeepWithNext(element, opts, next);
                    BeginElementBounds();
                    if (element.Type == LayoutElementType.Paragraph)
                        RenderParagraph(element);
                    else
                        RenderRichParagraph(element);
                    EndElementBounds(opts.SpaceBefore, opts.SpaceAfter);
                    break;
                }
                case LayoutElementType.Image:
                {
                    EnsurePageStarted();
                    var opts = element.ImageOptions ?? new ImageOptions();
                    BeginElementBounds();
                    RenderImage(element);
                    EndElementBounds(opts.SpaceBefore, opts.SpaceAfter);
                    break;
                }
                case LayoutElementType.Table:
                    EnsurePageStarted();
                    BeginElementBounds();
                    RenderTable(element);
                    EndElementBounds(0f, 0f);
                    break;
                case LayoutElementType.List:
                    EnsurePageStarted();
                    BeginElementBounds();
                    RenderList(element);
                    EndElementBounds(0f, 0f);
                    break;
                case LayoutElementType.HorizontalRule:
                {
                    EnsurePageStarted();
                    var opts = element.RuleOptions ?? new HorizontalRuleOptions();
                    BeginElementBounds();
                    RenderHorizontalRule(element);
                    EndElementBounds(opts.SpaceBefore, opts.SpaceAfter);
                    break;
                }
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
            DrawDebugPageOverlays();
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
                        BreakToNextColumnOrPage();
                    return;
                }
            }

            if (_currentY + height > _contentBottom)
                BreakToNextColumnOrPage();
        }

        private void BreakToNextColumnOrPage()
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

        // ── Keep Rules ─────────────────────────────────────────────

        /// <summary>
        /// Forces a column/page break before the element when KeepTogether is set
        /// and its content won't fit in the remaining space (but would fit on a
        /// fresh page). Call before advancing past SpaceBefore.
        /// </summary>
        private void ApplyKeepTogether(ParagraphOptions opts, float totalHeight)
        {
            if (!opts.KeepTogether) return;
            if (_currentY + totalHeight <= _contentBottom) return;

            float fullHeight = _contentBottom - _margins.Top;
            if (totalHeight <= fullHeight)
            {
                if (_currentY > _margins.Top)
                    BreakToNextColumnOrPage();
            }
            else
            {
                Warn("KeepTogether: paragraph is taller than one page/column and will be split.");
            }
        }

        private void ApplyKeepWithNext(LayoutElement element, ParagraphOptions opts,
            LayoutElement next)
        {
            if (!opts.KeepWithNext || next == null) return;
            if (_currentY <= _margins.Top) return; // already at top of a column/page

            float currentHeight = MeasureParagraphTotalHeight(element, opts);
            float nextFirstLine = MeasureFirstLineHeight(next);
            float combined = currentHeight + nextFirstLine;

            if (_currentY + combined > _contentBottom &&
                combined <= _contentBottom - _margins.Top)
            {
                BreakToNextColumnOrPage();
            }
        }

        /// <summary>Full height of a paragraph element including SpaceBefore/SpaceAfter.</summary>
        private float MeasureParagraphTotalHeight(LayoutElement element, ParagraphOptions opts)
        {
            float lineHeight = opts.FontSize * opts.LineSpacing;

            if (element.Type == LayoutElementType.RichParagraph)
            {
                float width = EffectiveParagraphWidth(opts);
                float height = 0;
                foreach (var richLine in FontMetrics.WrapRichText(element.Spans, width))
                {
                    float lh = richLine.MaxFontSize * opts.LineSpacing;
                    if (lh <= 0) lh = lineHeight;
                    height += lh;
                }
                return opts.SpaceBefore + height + opts.SpaceAfter;
            }

            var text = element.Text ?? "";
            int lineCount;
            if (opts.TabStops != null && opts.TabStops.Length > 0 && text.Contains("\t"))
                lineCount = text.Split('\n').Length;
            else
                lineCount = WrapParagraph(text, opts, EffectiveFont(opts)).Count;

            return opts.SpaceBefore + lineCount * lineHeight + opts.SpaceAfter;
        }

        /// <summary>Height of the first line of an element, including its SpaceBefore.</summary>
        private float MeasureFirstLineHeight(LayoutElement next)
        {
            switch (next.Type)
            {
                case LayoutElementType.Paragraph:
                {
                    var opts = EffectiveOptions(next.ParagraphOptions);
                    return opts.SpaceBefore + opts.FontSize * opts.LineSpacing;
                }
                case LayoutElementType.RichParagraph:
                {
                    var opts = EffectiveOptions(next.ParagraphOptions);
                    float maxFs = opts.FontSize;
                    if (next.Spans != null)
                        foreach (var span in next.Spans)
                            if (span.FontSize > maxFs) maxFs = span.FontSize;
                    return opts.SpaceBefore + maxFs * opts.LineSpacing;
                }
                case LayoutElementType.Image:
                {
                    var opts = next.ImageOptions ?? new ImageOptions();
                    ResolveImageSize(next.Image, opts, out float w, out float h);
                    if (w > _contentWidth) h *= _contentWidth / w;
                    return opts.SpaceBefore + h;
                }
                case LayoutElementType.HorizontalRule:
                {
                    var opts = next.RuleOptions ?? new HorizontalRuleOptions();
                    return opts.SpaceBefore + opts.Thickness;
                }
                default:
                    // Tables/lists: approximate with one default text line
                    return 12f * 1.2f;
            }
        }

        // ── Debug ──────────────────────────────────────────────────

        private int _boundsStartPage;
        private float _boundsStartY;

        private void Warn(string message) => _debug?.OnLayoutWarning?.Invoke(message);

        private void BeginElementBounds()
        {
            if (_debug == null || !_debug.ShowElementBounds) return;
            _boundsStartPage = _pageNumber;
            _boundsStartY = _currentY;
        }

        private void EndElementBounds(float topInset, float bottomInset)
        {
            if (_debug == null || !_debug.ShowElementBounds || _currentPage == null) return;

            // If the element spilled onto a new page/column, bound only the final part.
            float y0 = _boundsStartPage == _pageNumber
                ? _boundsStartY + topInset
                : _margins.Top;
            float y1 = _currentY - bottomInset;
            if (y1 <= y0) return;

            _currentPage.DrawRectangle(_contentLeft, y0, _contentWidth, y1 - y0,
                _debug.DebugColor, 0.5f);
        }

        private void DrawDebugPageOverlays()
        {
            if (_debug == null) return;

            float left = _margins.Left;
            float right = _currentPage.Width - _margins.Right;
            float top = _margins.Top;
            float bottom = _contentBottom;
            var color = _debug.DebugColor;

            if (_debug.ShowMargins)
            {
                DrawDashedLine(left, top, right, top, color);
                DrawDashedLine(left, bottom, right, bottom, color);
                DrawDashedLine(left, top, left, bottom, color);
                DrawDashedLine(right, top, right, bottom, color);
            }

            if (_debug.ShowColumns && _columns != null)
            {
                for (int i = 1; i < _columns.ColumnCount; i++)
                {
                    float x = left + i * (_columns.ColumnWidth + _columns.ColumnGap)
                        - _columns.ColumnGap / 2f;
                    DrawDashedLine(x, top, x, bottom, color);
                }
            }
        }

        private void DrawDashedLine(float x1, float y1, float x2, float y2, PdfColor color)
        {
            const float dashLength = 4f;
            const float gapLength = 3f;

            float dx = x2 - x1, dy = y2 - y1;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            if (length <= 0) return;

            float ux = dx / length, uy = dy / length;
            float pos = 0;
            while (pos < length)
            {
                float segment = Math.Min(dashLength, length - pos);
                _currentPage.DrawLine(
                    x1 + ux * pos, y1 + uy * pos,
                    x1 + ux * (pos + segment), y1 + uy * (pos + segment),
                    color, 0.5f);
                pos += dashLength + gapLength;
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

        /// <summary>Paragraph content width after left/right indentation.</summary>
        private float EffectiveParagraphWidth(ParagraphOptions opts)
        {
            float width = _contentWidth - opts.LeftIndent - opts.RightIndent;
            return width > 0 ? width : 1f;
        }

        private void RenderParagraph(LayoutElement element)
        {
            var opts = EffectiveOptions(element.ParagraphOptions);
            var text = element.Text ?? "";

            // Custom renderer override
            if (_renderer != null)
            {
                _currentY += opts.SpaceBefore;
                float? customY = _renderer.RenderParagraph(_currentPage, text,
                    _contentLeft, _currentY, _contentWidth, opts, MakeContext());
                if (customY.HasValue)
                {
                    _currentY = customY.Value + opts.SpaceAfter;
                    return;
                }
                _currentY -= opts.SpaceBefore; // undo — default will handle it
            }

            if (string.IsNullOrWhiteSpace(text))
                Warn("Empty paragraph.");

            var font = EffectiveFont(opts);

            // Tab stops: render without wrapping
            if (opts.TabStops != null && opts.TabStops.Length > 0 && text.Contains("\t"))
            {
                _currentY += opts.SpaceBefore;
                RenderTabbedParagraph(text, opts);
                _currentY += opts.SpaceAfter;
                return;
            }

            if (_contentWidth - opts.LeftIndent - opts.RightIndent <= 0)
                Warn("Indentation exceeds the content width; paragraph width clamped.");

            var lines = WrapParagraph(text, opts, font);
            float lineHeight = opts.FontSize * opts.LineSpacing;

            ApplyKeepTogether(opts, opts.SpaceBefore + lines.Count * lineHeight);

            _currentY += opts.SpaceBefore;

            for (int i = 0; i < lines.Count; i++)
            {
                EnsureSpace(lineHeight);
                bool isLastLine = (i == lines.Count - 1);

                // Recompute per line: a column break inside the paragraph
                // moves _contentLeft to the next column
                float x = _contentLeft + opts.LeftIndent;
                float width = EffectiveParagraphWidth(opts);
                if (lines[i].First)
                {
                    x += opts.FirstLineIndent;
                    width = Math.Max(1f, width - opts.FirstLineIndent);
                }

                RenderTextLine(lines[i].Text, opts, font, isLastLine, x, width);
                _currentY += lineHeight;
            }

            _currentY += opts.SpaceAfter;
        }

        /// <summary>
        /// Wraps paragraph text, honoring FirstLineIndent: the first line of each
        /// newline-separated paragraph wraps at a narrower/wider width than the rest.
        /// The First flag marks lines that receive the first-line indent.
        /// </summary>
        private List<(string Text, bool First)> WrapParagraph(string text,
            ParagraphOptions opts, PdfFontSource font)
        {
            var result = new List<(string Text, bool First)>();
            float baseWidth = EffectiveParagraphWidth(opts);

            if (opts.FirstLineIndent == 0f)
            {
                foreach (var line in FontMetrics.WrapText(text, font, opts.FontSize,
                    baseWidth, opts.CharacterSpacing))
                {
                    result.Add((line, false));
                }
                return result;
            }

            float firstWidth = Math.Max(1f, baseWidth - opts.FirstLineIndent);

            if (string.IsNullOrEmpty(text))
            {
                result.Add(("", true));
                return result;
            }

            foreach (var para in text.Split('\n'))
            {
                if (string.IsNullOrEmpty(para))
                {
                    result.Add(("", true));
                    continue;
                }

                var words = para.Split(' ');
                var currentLine = new System.Text.StringBuilder();
                float currentWidth = 0;
                float maxWidth = firstWidth;
                bool first = true;
                float spaceWidth = FontMetrics.MeasureString(" ", font, opts.FontSize,
                    opts.CharacterSpacing);

                foreach (var word in words)
                {
                    float wordWidth = FontMetrics.MeasureString(word, font, opts.FontSize,
                        opts.CharacterSpacing);

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
                        result.Add((currentLine.ToString(), first));
                        first = false;
                        maxWidth = baseWidth;
                        currentLine.Clear();
                        currentLine.Append(word);
                        currentWidth = wordWidth;
                    }
                }

                result.Add((currentLine.ToString(), first));
            }

            return result;
        }

        private void RenderTextLine(string line, ParagraphOptions opts, PdfFontSource font,
            bool isLastLine, float left, float width)
        {
            var color = EffectiveColor(opts);
            var alignment = opts.Alignment;

            // Justify: non-last lines get justified; last line is left-aligned
            if (alignment == TextAlignment.Justify)
            {
                if (!isLastLine)
                {
                    RenderJustifiedLine(line, opts, font, left, width);
                    return;
                }
                alignment = TextAlignment.Left;
            }

            float lineWidth = FontMetrics.MeasureString(line, font, opts.FontSize, opts.CharacterSpacing);
            float x;
            switch (alignment)
            {
                case TextAlignment.Center:
                    x = left + (width - lineWidth) / 2f;
                    break;
                case TextAlignment.Right:
                    x = left + width - lineWidth;
                    break;
                default:
                    x = left;
                    break;
            }

            _currentPage.DrawText(line, x, _currentY, font, opts.FontSize,
                color, TextAlignment.Left, opts.Underline, opts.Opacity,
                null, 0f, null, opts.LineSpacing, opts.CharacterSpacing,
                opts.Bold, opts.Italic);
        }

        private void RenderJustifiedLine(string line, ParagraphOptions opts, PdfFontSource font,
            float left, float width)
        {
            var color = EffectiveColor(opts);
            var words = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length <= 1)
            {
                _currentPage.DrawText(line, left, _currentY, font, opts.FontSize,
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

            float totalSpace = width - totalWordsWidth;
            float spacePerGap = totalSpace / (words.Length - 1);
            float x = left;

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

            if (_contentWidth - opts.LeftIndent - opts.RightIndent <= 0)
                Warn("Indentation exceeds the content width; paragraph width clamped.");

            var richLines = FontMetrics.WrapRichText(element.Spans, EffectiveParagraphWidth(opts));

            float totalHeight = 0;
            foreach (var richLine in richLines)
            {
                float lh = richLine.MaxFontSize * opts.LineSpacing;
                if (lh <= 0) lh = opts.FontSize * opts.LineSpacing;
                totalHeight += lh;
            }
            ApplyKeepTogether(opts, opts.SpaceBefore + totalHeight);

            _currentY += opts.SpaceBefore;

            for (int i = 0; i < richLines.Count; i++)
            {
                var richLine = richLines[i];
                float lineHeight = richLine.MaxFontSize * opts.LineSpacing;
                if (lineHeight <= 0) lineHeight = opts.FontSize * opts.LineSpacing;

                EnsureSpace(lineHeight);

                bool isLastLine = (i == richLines.Count - 1);
                // Recompute per line: a column break inside the paragraph
                // moves _contentLeft to the next column
                RenderRichLine(richLine, opts, isLastLine,
                    _contentLeft + opts.LeftIndent, EffectiveParagraphWidth(opts));
                _currentY += lineHeight;
            }

            _currentY += opts.SpaceAfter;
        }

        private void RenderRichLine(FontMetrics.RichLine richLine, ParagraphOptions opts,
            bool isLastLine, float left, float width)
        {
            var alignment = opts.Alignment;
            if (alignment == TextAlignment.Justify && isLastLine)
                alignment = TextAlignment.Left;

            float maxFs = richLine.MaxFontSize > 0 ? richLine.MaxFontSize : opts.FontSize;

            float x;
            if (alignment == TextAlignment.Justify)
            {
                RenderJustifiedRichLine(richLine, opts, maxFs, left, width);
                return;
            }

            switch (alignment)
            {
                case TextAlignment.Center:
                    x = left + (width - richLine.TotalWidth) / 2f;
                    break;
                case TextAlignment.Right:
                    x = left + width - richLine.TotalWidth;
                    break;
                default:
                    x = left;
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
            float maxFs, float left, float width)
        {
            int gapCount = 0;
            foreach (var word in richLine.Words)
                if (word.HasLeadingSpace) gapCount++;

            float extraSpace = width - richLine.TotalWidth;
            float extraPerGap = gapCount > 0 ? extraSpace / gapCount : 0;

            float x = left;
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

            float x = _contentLeft + opts.LeftIndent;

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
                Warn($"Image width {drawWidth:0.##}pt exceeds content width " +
                    $"{_contentWidth:0.##}pt; scaled to fit.");
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

        // ── Horizontal Rule ────────────────────────────────────────

        private void RenderHorizontalRule(LayoutElement element)
        {
            var opts = element.RuleOptions ?? new HorizontalRuleOptions();

            _currentY += opts.SpaceBefore;
            EnsureSpace(opts.Thickness);

            float x1 = _contentLeft + opts.LeftIndent;
            float x2 = _contentLeft + _contentWidth - opts.RightIndent;
            if (x2 > x1)
            {
                float y = _currentY + opts.Thickness / 2f;
                _currentPage.DrawLine(x1, y, x2, y, opts.Color ?? PdfColor.Black, opts.Thickness);
            }
            else
            {
                Warn("Horizontal rule indents exceed the content width; rule skipped.");
            }

            _currentY += opts.Thickness + opts.SpaceAfter;
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

            _currentY = _currentPage.DrawTable(element.Table, _contentLeft, _currentY,
                _margins.Bottom, _margins.Top);

            int pagesAfter = _doc.PageCount;
            if (pagesAfter > pagesBefore)
            {
                // Table created continuation pages. A page's footer is drawn when
                // the page ends: the start page ends here, and so does each
                // continuation page except the last, which stays current and gets
                // its footer at the next page break or end of render.
                DrawFooterOnCurrentPage();

                for (int i = pagesBefore; i < pagesAfter; i++)
                {
                    _pageNumber++;
                    _sectionPageNumber++;
                    var page = _doc.Pages[i];
                    DrawHeaderOnPage(page, _pageNumber);
                    if (i < pagesAfter - 1)
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
                // Same deferred-footer rule as RenderTable: the start page and
                // intermediate continuation pages end here; the last page's
                // footer is drawn when it ends.
                DrawFooterOnCurrentPage();

                for (int i = pagesBefore; i < pagesAfter; i++)
                {
                    _pageNumber++;
                    _sectionPageNumber++;
                    var page = _doc.Pages[i];
                    DrawHeaderOnPage(page, _pageNumber);
                    if (i < pagesAfter - 1)
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
