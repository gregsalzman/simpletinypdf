using System;
using System.Collections.Generic;

namespace SimpleTinyPDF
{
    /// <summary>
    /// Represents a bookmark (outline item) in the PDF document's navigation panel.
    /// Bookmarks can be nested to create a hierarchical table of contents.
    /// </summary>
    public sealed class PdfBookmark
    {
        private readonly List<PdfBookmark> _children = new List<PdfBookmark>();

        /// <summary>The display title of this bookmark.</summary>
        internal string Title { get; }

        /// <summary>The page this bookmark navigates to.</summary>
        internal PdfPage Page { get; }

        /// <summary>
        /// Optional Y position on the page (in the page's coordinate system).
        /// When null, the bookmark fits the entire page.
        /// When set, scrolls to this vertical position.
        /// </summary>
        internal float? Y { get; }

        /// <summary>The child bookmarks of this node.</summary>
        internal IReadOnlyList<PdfBookmark> Children => _children;

        internal PdfBookmark(string title, PdfPage page, float? y)
        {
            if (string.IsNullOrEmpty(title))
                throw new ArgumentException("Bookmark title cannot be null or empty.", nameof(title));
            if (page == null)
                throw new ArgumentNullException(nameof(page));
            Title = title;
            Page = page;
            Y = y;
        }

        /// <summary>
        /// Adds a child bookmark under this bookmark.
        /// </summary>
        /// <param name="title">The display title shown in the bookmark panel.</param>
        /// <param name="page">The page to navigate to when clicked.</param>
        /// <param name="y">Optional Y position on the page (in the page's coordinate system).
        /// When omitted, the bookmark navigates to fit the entire page.</param>
        /// <returns>The newly created child bookmark, which can have its own children.</returns>
        public PdfBookmark AddBookmark(string title, PdfPage page, float? y = null)
        {
            var child = new PdfBookmark(title, page, y);
            _children.Add(child);
            return child;
        }
    }
}
