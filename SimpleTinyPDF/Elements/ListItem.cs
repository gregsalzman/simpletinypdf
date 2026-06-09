using System;
using System.Collections.Generic;

namespace SimpleTinyPDF
{
    /// <summary>
    /// Represents a single item in a list, optionally containing nested child items.
    /// </summary>
    public class ListItem
    {
        /// <summary>The text content of this list item.</summary>
        public string Text { get; }

        /// <summary>Child items nested under this item.</summary>
        public IReadOnlyList<ListItem> Children { get; }

        /// <summary>
        /// When non-null, overrides the list style used to render this item's children.
        /// </summary>
        public ListStyle? ChildrenStyle { get; }

        /// <summary>
        /// When non-null, overrides the bullet symbol used to render this item's children.
        /// Specify a <see cref="TextSpan"/> to use a different font or symbol (e.g. ZapfDingbats, Symbol).
        /// Only used when the effective children style is <see cref="ListStyle.Bullet"/>.
        /// </summary>
        public TextSpan ChildrenBullet { get; }

        /// <summary>Creates a list item whose children inherit the style from the parent list.</summary>
        public ListItem(string text, params ListItem[] children)
            : this(text, null, null, children) { }

        /// <summary>Creates a list item that overrides the style used to render its children.</summary>
        public ListItem(string text, ListStyle childrenStyle, params ListItem[] children)
            : this(text, (ListStyle?)childrenStyle, null, children) { }

        /// <summary>Creates a list item that overrides both the style and bullet symbol for its children.</summary>
        public ListItem(string text, ListStyle childrenStyle, TextSpan childrenBullet,
            params ListItem[] children)
            : this(text, (ListStyle?)childrenStyle, childrenBullet, children) { }

        private ListItem(string text, ListStyle? childrenStyle, TextSpan childrenBullet,
            ListItem[] children)
        {
            Text = text ?? "";
            ChildrenStyle = childrenStyle;
            ChildrenBullet = childrenBullet;
            Children = children ?? (IReadOnlyList<ListItem>)Array.Empty<ListItem>();
        }
    }

    /// <summary>Controls how list item markers are rendered.</summary>
    public enum ListStyle
    {
        /// <summary>Items are marked with a bullet symbol.</summary>
        Bullet,
        /// <summary>Items are marked with sequential numbers.</summary>
        Numbered,
        /// <summary>Items are marked with lowercase Roman numerals (i, ii, iii …).</summary>
        RomanLower,
        /// <summary>Items are marked with uppercase Roman numerals (I, II, III …).</summary>
        RomanUpper
    }
}
