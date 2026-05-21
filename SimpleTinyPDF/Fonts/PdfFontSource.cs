using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace SimpleTinyPDF
{
    /// <summary>
    /// Represents a font source — either one of the 14 standard PDF Type 1 fonts
    /// or a custom TrueType/OpenType font loaded from a file.
    /// </summary>
    /// <remarks>
    /// Implicit conversion from <see cref="PdfFont"/> allows existing code to work unchanged:
    /// <code>page.DrawText("Hello", 50, 50, PdfFont.Helvetica, 12f);</code>
    /// Custom fonts are loaded via factory methods:
    /// <code>
    /// var font = PdfFontSource.FromFile("Roboto-Regular.ttf");
    /// page.DrawText("Hello", 50, 50, font, 12f);
    /// </code>
    /// </remarks>
    public sealed class PdfFontSource : IEquatable<PdfFontSource>
    {
        private readonly PdfFont? _builtIn;
        private readonly TrueTypeFont _custom;

        private PdfFontSource(PdfFont font)
        {
            _builtIn = font;
        }

        private PdfFontSource(TrueTypeFont font)
        {
            _custom = font ?? throw new ArgumentNullException(nameof(font));
        }

        /// <summary>Whether this is one of the 14 standard PDF Type 1 fonts.</summary>
        internal bool IsBuiltIn => _builtIn.HasValue;

        /// <summary>The built-in font enum value. Only valid when <see cref="IsBuiltIn"/> is true.</summary>
        internal PdfFont BuiltInFont => _builtIn.Value;

        /// <summary>The parsed custom font. Only valid when <see cref="IsBuiltIn"/> is false.</summary>
        internal TrueTypeFont CustomFont => _custom;

        /// <summary>Implicit conversion from PdfFont enum for backward compatibility.</summary>
        public static implicit operator PdfFontSource(PdfFont font) => new PdfFontSource(font);

        /// <summary>Loads a TrueType (.ttf) or OpenType (.otf) font from a file path.</summary>
        public static PdfFontSource FromFile(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            var data = File.ReadAllBytes(path);
            return new PdfFontSource(new TrueTypeFont(data));
        }

        /// <summary>Loads a TrueType (.ttf) or OpenType (.otf) font from a byte array.</summary>
        public static PdfFontSource FromBytes(byte[] fontData)
        {
            if (fontData == null) throw new ArgumentNullException(nameof(fontData));
            return new PdfFontSource(new TrueTypeFont(fontData));
        }

        /// <summary>Loads a TrueType (.ttf) or OpenType (.otf) font from a stream.</summary>
        public static PdfFontSource FromStream(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                return new PdfFontSource(new TrueTypeFont(ms.ToArray()));
            }
        }

        /// <inheritdoc />
        public bool Equals(PdfFontSource other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            if (IsBuiltIn && other.IsBuiltIn)
                return _builtIn.Value == other._builtIn.Value;
            if (!IsBuiltIn && !other.IsBuiltIn)
                return ReferenceEquals(_custom, other._custom);
            return false;
        }

        /// <inheritdoc />
        public override bool Equals(object obj) => Equals(obj as PdfFontSource);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return IsBuiltIn
                ? _builtIn.GetHashCode()
                : RuntimeHelpers.GetHashCode(_custom);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return IsBuiltIn
                ? _builtIn.Value.ToString()
                : _custom.PostScriptName ?? "CustomFont";
        }
    }
}
