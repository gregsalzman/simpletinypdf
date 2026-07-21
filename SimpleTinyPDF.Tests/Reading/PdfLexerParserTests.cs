using System.Linq;
using System.Text;
using Xunit;

namespace SimpleTinyPDF.Tests
{
    public class PdfLexerParserTests
    {
        private static CosValue Parse(string pdfSyntax)
        {
            var parser = new PdfParser(Encoding.ASCII.GetBytes(pdfSyntax));
            return parser.ParseValue();
        }

        // ── Literal strings ─────────────────────────────────────────

        [Fact]
        public void LiteralString_EscapedParensAndBackslash()
        {
            var value = Assert.IsType<CosString>(Parse(@"(a\(b\)c\\d)"));
            Assert.Equal(@"a(b)c\d", value.AsText());
        }

        [Fact]
        public void LiteralString_NestedBalancedParens()
        {
            var value = Assert.IsType<CosString>(Parse("(outer (inner) tail)"));
            Assert.Equal("outer (inner) tail", value.AsText());
        }

        [Fact]
        public void LiteralString_OctalEscapes()
        {
            var value = Assert.IsType<CosString>(Parse(@"(\101\102\61)"));
            Assert.Equal("AB1", value.AsText());
        }

        [Fact]
        public void LiteralString_StandardEscapes()
        {
            var value = Assert.IsType<CosString>(Parse("(a\\n\\t\\r\\b\\fb)"));
            Assert.Equal(new byte[] { (byte)'a', 0x0A, 0x09, 0x0D, 0x08, 0x0C, (byte)'b' }, value.Raw);
        }

        [Fact]
        public void LiteralString_BackslashNewlineContinuation()
        {
            var value = Assert.IsType<CosString>(Parse("(ab\\\r\ncd)"));
            Assert.Equal("abcd", value.AsText());
        }

        [Fact]
        public void LiteralString_RawCrLfNormalizedToLf()
        {
            var value = Assert.IsType<CosString>(Parse("(a\r\nb\rc)"));
            Assert.Equal(new byte[] { (byte)'a', 0x0A, (byte)'b', 0x0A, (byte)'c' }, value.Raw);
        }

        [Fact]
        public void LiteralString_UnknownEscapeDropsBackslash()
        {
            var value = Assert.IsType<CosString>(Parse(@"(a\zb)"));
            Assert.Equal("azb", value.AsText());
        }

        // ── Hex strings ─────────────────────────────────────────────

        [Fact]
        public void HexString_Basic()
        {
            var value = Assert.IsType<CosString>(Parse("<48656C6C6F>"));
            Assert.Equal("Hello", value.AsText());
        }

        [Fact]
        public void HexString_WhitespaceInsideIgnored()
        {
            var value = Assert.IsType<CosString>(Parse("<48 65\n6C 6C 6F>"));
            Assert.Equal("Hello", value.AsText());
        }

        [Fact]
        public void HexString_OddLengthPadsZero()
        {
            var value = Assert.IsType<CosString>(Parse("<484>"));
            Assert.Equal(new byte[] { 0x48, 0x40 }, value.Raw);
        }

        [Fact]
        public void String_Utf16BeBomDecoded()
        {
            var value = Assert.IsType<CosString>(Parse("<FEFF00480069>"));
            Assert.Equal("Hi", value.AsText());
        }

        // ── Names ───────────────────────────────────────────────────

        [Fact]
        public void Name_HashEscapesDecoded()
        {
            var value = Assert.IsType<CosName>(Parse("/Name#20With#20Spaces"));
            Assert.Equal("Name With Spaces", value.Value);
        }

        [Fact]
        public void Name_MalformedHashKeptLiterally()
        {
            var value = Assert.IsType<CosName>(Parse("/A#GB"));
            Assert.Equal("A#GB", value.Value);
        }

        // ── Numbers and references ──────────────────────────────────

        [Theory]
        [InlineData("42", 42L)]
        [InlineData("-17", -17L)]
        [InlineData("+9", 9L)]
        public void Integer_Parsed(string text, long expected)
        {
            var value = Assert.IsType<CosInteger>(Parse(text));
            Assert.Equal(expected, value.Value);
        }

        [Theory]
        [InlineData("3.14", 3.14)]
        [InlineData("-.5", -0.5)]
        [InlineData("+.5", 0.5)]
        [InlineData("6.", 6.0)]
        public void Real_Parsed(string text, double expected)
        {
            var value = Assert.IsType<CosReal>(Parse(text));
            Assert.Equal(expected, value.Value, 6);
        }

        [Fact]
        public void Reference_Parsed()
        {
            var value = Assert.IsType<CosReference>(Parse("12 0 R"));
            Assert.Equal(12, value.Id.Number);
            Assert.Equal(0, value.Id.Generation);
        }

        [Fact]
        public void ThreeIntegersInArray_NotMistakenForReference()
        {
            var array = Assert.IsType<CosArray>(Parse("[1 0 5]"));
            Assert.Equal(3, array.Items.Count);
            Assert.All(array.Items, item => Assert.IsType<CosInteger>(item));
        }

        [Fact]
        public void ReferenceInsideArray_Parsed()
        {
            var array = Assert.IsType<CosArray>(Parse("[1 0 R 2 0 R 7]"));
            Assert.Equal(3, array.Items.Count);
            Assert.IsType<CosReference>(array.Items[0]);
            Assert.IsType<CosReference>(array.Items[1]);
            Assert.Equal(7, Assert.IsType<CosInteger>(array.Items[2]).Value);
        }

        // ── Keywords, dicts, arrays, comments ───────────────────────

        [Fact]
        public void Keywords_TrueFalseNull()
        {
            Assert.True(Assert.IsType<CosBool>(Parse("true")).Value);
            Assert.False(Assert.IsType<CosBool>(Parse("false")).Value);
            Assert.IsType<CosNull>(Parse("null"));
        }

        [Fact]
        public void Dict_NestedAndTyped()
        {
            var dict = Assert.IsType<CosDict>(Parse("<< /Type /Page /Count 3 /Kids [4 0 R] /Sub << /A (x) >> >>"));
            Assert.Equal("Page", dict.GetName("Type"));
            Assert.Equal(3L, dict.GetInteger("Count"));
            var kids = Assert.IsType<CosArray>(dict.Get("Kids"));
            Assert.IsType<CosReference>(kids.Items.Single());
            var sub = Assert.IsType<CosDict>(dict.Get("Sub"));
            Assert.Equal("x", Assert.IsType<CosString>(sub.Get("A")).AsText());
        }

        [Fact]
        public void Dict_DuplicateKeysLastWins()
        {
            var dict = Assert.IsType<CosDict>(Parse("<< /A 1 /A 2 >>"));
            Assert.Equal(2L, dict.GetInteger("A"));
            Assert.Single(dict.Entries);
        }

        [Fact]
        public void Comments_SkippedAsWhitespace()
        {
            var dict = Assert.IsType<CosDict>(Parse("<< % comment here\n /A % another\n 5 >>"));
            Assert.Equal(5L, dict.GetInteger("A"));
        }

        // ── Indirect objects and streams ────────────────────────────

        [Fact]
        public void IndirectObject_ParsedWithId()
        {
            var parser = new PdfParser(Encoding.ASCII.GetBytes("7 0 obj\n<< /X 1 >>\nendobj"));
            var body = parser.ParseIndirectObject(0, out var id);
            Assert.Equal(7, id.Number);
            Assert.Equal(0, id.Generation);
            Assert.Equal(1L, Assert.IsType<CosDict>(body).GetInteger("X"));
        }

        [Fact]
        public void Stream_CorrectLength()
        {
            var pdf = "5 0 obj\n<< /Length 5 >>\nstream\r\nHELLO\nendstream\nendobj";
            var parser = new PdfParser(Encoding.ASCII.GetBytes(pdf));
            var stream = Assert.IsType<CosStream>(parser.ParseIndirectObject(0, out _));
            Assert.Equal("HELLO", Encoding.ASCII.GetString(stream.RawData));
        }

        [Fact]
        public void Stream_WrongLengthRecoveredByScan()
        {
            var pdf = "5 0 obj\n<< /Length 999 >>\nstream\nHELLO\nendstream\nendobj";
            var parser = new PdfParser(Encoding.ASCII.GetBytes(pdf));
            var stream = Assert.IsType<CosStream>(parser.ParseIndirectObject(0, out _));
            Assert.Equal("HELLO", Encoding.ASCII.GetString(stream.RawData));
            Assert.Equal(5L, stream.GetInteger("Length"));
        }

        [Fact]
        public void Stream_IndirectLengthResolved()
        {
            var pdf = "5 0 obj\n<< /Length 6 0 R >>\nstream\nHELLO\nendstream\nendobj";
            var parser = new PdfParser(Encoding.ASCII.GetBytes(pdf));
            parser.LengthResolver = id => id.Number == 6 ? 5L : (long?)null;
            var stream = Assert.IsType<CosStream>(parser.ParseIndirectObject(0, out _));
            Assert.Equal("HELLO", Encoding.ASCII.GetString(stream.RawData));
        }

        [Fact]
        public void IndirectObject_BadHeaderThrows()
        {
            var parser = new PdfParser(Encoding.ASCII.GetBytes("not an object"));
            Assert.Throws<PdfParseException>(() => parser.ParseIndirectObject(0, out _));
        }
    }
}
