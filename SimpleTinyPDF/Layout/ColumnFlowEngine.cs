namespace SimpleTinyPDF
{
    /// <summary>
    /// Manages multi-column layout within a page. Tracks the current column
    /// and computes column positions and widths.
    /// </summary>
    internal class ColumnFlowEngine
    {
        private readonly int _columnCount;
        private readonly float _columnGap;
        private readonly float _contentLeft;
        private readonly float _totalWidth;

        internal int CurrentColumn { get; private set; }
        internal int ColumnCount => _columnCount;
        internal float ColumnGap => _columnGap;
        internal float ColumnWidth { get; }
        internal float ColumnX => _contentLeft + CurrentColumn * (ColumnWidth + _columnGap);

        internal ColumnFlowEngine(int columnCount, float columnGap, float contentLeft, float totalWidth)
        {
            _columnCount = columnCount < 1 ? 1 : columnCount;
            _columnGap = columnGap;
            _contentLeft = contentLeft;
            _totalWidth = totalWidth;

            ColumnWidth = _columnCount > 1
                ? (_totalWidth - (_columnCount - 1) * _columnGap) / _columnCount
                : _totalWidth;
        }

        /// <summary>
        /// Advances to the next column. Returns true if moved to next column
        /// on the same page; false if a new page is needed (was on last column).
        /// </summary>
        internal bool NextColumn()
        {
            if (CurrentColumn + 1 < _columnCount)
            {
                CurrentColumn++;
                return true;
            }
            return false;
        }

        /// <summary>Resets to the first column (called on new page).</summary>
        internal void Reset()
        {
            CurrentColumn = 0;
        }
    }
}
