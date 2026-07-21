using System;

namespace SimpleTinyPDF
{
    /// <summary>
    /// Thrown when an existing PDF file cannot be parsed, even after attempting
    /// cross-reference repair.
    /// </summary>
    public class PdfParseException : Exception
    {
        /// <summary>Creates a new parse exception.</summary>
        public PdfParseException(string message) : base(message) { }

        /// <summary>Creates a new parse exception with an inner exception.</summary>
        public PdfParseException(string message, Exception innerException) : base(message, innerException) { }
    }
}
