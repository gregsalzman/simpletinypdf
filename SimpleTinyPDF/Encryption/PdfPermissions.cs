using System;

namespace SimpleTinyPDF
{
    /// <summary>
    /// Specifies user access permissions for an encrypted PDF document.
    /// Combine with bitwise OR to grant multiple permissions.
    /// </summary>
    [Flags]
    public enum PdfPermissions
    {
        /// <summary>No permissions granted.</summary>
        None = 0,

        /// <summary>Print the document (may be low-quality if <see cref="HighQualityPrint"/> is not set).</summary>
        Print = 1 << 2,

        /// <summary>Modify document contents.</summary>
        ModifyContents = 1 << 3,

        /// <summary>Copy or extract text and graphics.</summary>
        ExtractText = 1 << 4,

        /// <summary>Add or modify annotations and fill form fields.</summary>
        AnnotateAndForms = 1 << 5,

        /// <summary>Fill in existing form fields.</summary>
        FillForms = 1 << 8,

        /// <summary>Extract text and graphics for accessibility purposes.</summary>
        ExtractForAccessibility = 1 << 9,

        /// <summary>Assemble the document (insert, rotate, delete pages, bookmarks).</summary>
        Assemble = 1 << 10,

        /// <summary>Print at high quality.</summary>
        HighQualityPrint = 1 << 11,

        /// <summary>All permissions granted.</summary>
        All = Print | ModifyContents | ExtractText | AnnotateAndForms
            | FillForms | ExtractForAccessibility | Assemble | HighQualityPrint
    }
}
