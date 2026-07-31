# SimpleTinyPDF

[![NuGet](https://img.shields.io/nuget/v/SimpleTinyPDF)](https://www.nuget.org/packages/SimpleTinyPDF)

A small-but-mighty zero-dependency PDF generation library for .NET.  It is faster and uses less memory than most alternative packages.  SimpleTinyPDF acheives these impressive results by not doing everything... but there's still a good chance it does what you need!

 SimpleTinyPDF targets .NET Standard 2.0, so it works with .NET Framework 4.6.1+, .NET Core 2.0+, and .NET 5+. 

## Table of Contents

- [Features](#features)
- [Quick Start](#quick-start)
- [Pros and Cons](#pros-and-cons)
- [Size Comparison](#size-comparison)
- [Performance Comparison](#performance-comparison)
- [API Reference](#api-reference)
  - [PdfDocument](#pdfdocument)
  - [PdfPage](#pdfpage) — [Text](#text) · [Right-to-Left Text](#right-to-left-text-arabic-hebrew) · [Shapes](#shapes) · [Images](#images) · [Tables](#tables) · [Lists](#lists)
  - [Document Layout](#document-layout)
  - [PdfColor](#pdfcolor)
  - [PageSize](#pagesize)
  - [PdfUnit](#pdfunit)
  - [Bookmarks](#bookmarks)
  - [Annotations](#annotations)
  - [Encryption](#encryption)
  - [Barcodes](#barcodes)
  - [Form Fields (AcroForms)](#form-fields-acroforms)
  - [Digital Signatures](#digital-signatures)
  - [Editing Existing PDFs](#editing-existing-pdfs)
- [Example: Invoice with Company Logo](#example-invoice-with-company-logo)
- [Example: CSV to Table Report](#example-csv-to-table-report)
- [Version History](#version-history)
- [License](#license)

## Features

- **Text** — single-line, wrapped text boxes, rich text with mixed fonts/sizes/colors, alignment (left, center, right, justify), underline, hyperlinks, opacity, character spacing, faux bold, faux italic
- **Right-to-left text** — Arabic and Hebrew render correctly with no extra code: built-in Unicode Bidirectional Algorithm (UAX #9) for mixed RTL/LTR lines, Arabic contextual letter joining and lam-alef ligatures via presentation forms, mirrored brackets, and wrapped/justified RTL paragraphs — all in pure C# with zero dependencies
- **Images** — JPEG and PNG (with transparency), EXIF auto-orientation, scaling modes (Stretch, Fit, Fill), opacity
- **Tables** — styled headers, column alignment, alternate row shading, auto-pagination with repeated headers, CSV import
- **Barcodes** — Code 128, Code 39, EAN-13, UPC-A, and QR Code; pure vector rendering (no images); configurable colors, quiet zones, human-readable text, rotation, and opacity
- **Document Layout** — automatic content flow and pagination via `PdfDocumentLayout`. Add paragraphs, rich text, images, tables, and lists that flow across pages automatically. Headers and footers with page numbering (including accurate total page counts via two-pass rendering), first-page and even-page overrides, tab stops with leaders and decimal alignment, manual page breaks, document sections with independent page size/margins/headers per section, multi-column text flow, page lifecycle events, and custom renderers for pluggable layout behavior
- **Lists** — bullet, numbered, lowercase Roman (i, ii, iii…), and uppercase Roman (I, II, III…); unlimited nesting with per-level style overrides and custom bullet symbols; automatic text wrapping and multi-page flow
- **Shapes** — lines, rectangles (stroke and/or fill), with optional rotation
- **Bookmarks** — hierarchical outline / table of contents
- **Annotations** — text (sticky notes), markup (highlight, underline, strikeout), stamps (Approved, Draft, Confidential, etc.), and internal links (navigate to another page)
- **Encryption** — AES-128 and AES-256 password protection with configurable user/owner passwords and granular permission flags (print, copy, modify, annotate, etc.)
- **Form Fields (AcroForms)** — text fields (single/multi-line, password), checkboxes, radio buttons, dropdowns (combo boxes), listboxes (single/multi-select), and push buttons; editable in Adobe Acrobat and other PDF viewers
- **Digital Signatures** — PKCS#7 detached signatures with X.509 certificates; visible or invisible; optional RFC 3161 timestamping from built-in or custom TSA servers; HSM/smart card/cloud KMS support via custom signer delegate
- **Editing existing PDFs** — open any unencrypted PDF (including files with cross-reference streams and object streams) and merge, split, extract, delete, rearrange, or insert pages; mix imported pages with generated ones; encrypt or sign the result; automatic repair of files with broken cross-reference tables
- **Fonts** — 14 standard PDF Type 1 fonts (Helvetica, Times, Courier, Symbol, ZapfDingbats) plus TrueType (.ttf) and OpenType (.otf) font embedding with full Unicode support including supplementary planes (CJK, Cyrillic, Greek, Arabic, CJK Extension B, enclosed alphanumerics, etc.). TrueType fonts are automatically subsetted to include only the glyphs used in the document
- **Colors** — RGB, CMYK, grayscale, and spot colors (Separation) with CMYK fallback and tint control
- **Unit conversion** — convert between points, inches, centimeters, and millimeters; fractional inch support ("1-1/8", "1 1/8", "3/4")
- **Page sizes** — A3, A4, A5, Letter, Legal, custom dimensions, landscape
- **Coordinates** — top-down (default) or native PDF bottom-up
- **Metadata** — document title and author
- **Zero dependencies** — no NuGet packages, no native libraries
- **Runs in Blazor WebAssembly** — pure managed code with no native binaries means PDFs can be generated, opened, and merged entirely in the browser; encryption and digital signatures remain server-only (the browser runtime lacks AES/RSA)

## Quick Start

```
dotnet add package SimpleTinyPDF
```

```csharp
using SimpleTinyPDF;

var doc = new PdfDocument { Title = "Hello World" };
var page = doc.AddPage();

page.DrawText("Hello, World!", 50, 50, PdfFont.HelveticaBold, 24);
page.DrawText("Generated with SimpleTinyPDF.", 50, 80);

doc.Save("hello.pdf");
```

See the full [Invoice example](#example-invoice-with-company-logo) and [CSV Table Report example](#example-csv-to-table-report) for more realistic usage.

## Pros and Cons

**Use SimpleTinyPDF when you want to:**

- Generate invoices, reports, receipts, or labels from code
- Avoid pulling in a large dependency tree for simple PDF output
- Run in constrained environments (Azure Functions, Docker, CI pipelines, Blazor WebAssembly) without native libraries
- Ship a self-contained app with no runtime font or image dependencies

**Look elsewhere if you need:**

- HTML-to-PDF conversion
- Full OpenType shaping — font-specific ligatures, precise diacritic positioning, or Nastaliq-style Urdu (standard Arabic and Hebrew are supported; see [Right-to-Left Text](#right-to-left-text-arabic-hebrew))
- Content-level editing of existing PDFs — changing text, replacing images, or stamping onto existing pages (page-level operations — merge, split, extract, delete, rearrange — are supported; see [Editing Existing PDFs](#editing-existing-pdfs))
- Extracting text or images from existing PDFs

## Size Comparison

One of SimpleTinyPDF's goals is to stay tiny. Here's how it compares to other popular .NET PDF libraries:

| Library | NuGet Package | Total Footprint | vs SimpleTinyPDF | Native Binaries? |
|---|---|---|---|---|
| **SimpleTinyPDF** | **~259 KB** | **~259 KB** | **1x** | No |
| **PDFsharp + MigraDoc** 6.2.4 | ~6.1 MB | ~6.2 MB | 25x | No |
| **iText** 9.6.0 | ~5.0 MB | ~13–15 MB | 55x | No (BouncyCastle ~8 MB) |
| **QuestPDF** 2026.2.4 | ~36 MB | ~36 MB | 143x | Yes (bundled Skia) |
| **IronPDF** 2026.5 | ~19 MB | ~250+ MB | 990x | Yes (bundled Chromium) |

PDFsharp is shown with MigraDoc (its document-model layer) since most real-world usage relies on MigraDoc for tables, paragraphs, and auto-pagination — raw PDFsharp alone is ~4.4 MB. IronPDF's footprint includes the Chromium rendering engine downloaded at build/runtime. QuestPDF bundles custom Skia native binaries for cross-platform rendering.

The v0.90 PDF reader/editor (parser, cross-reference handling, page importer) added about 31 KB to the assembly — the whole editing feature costs less than half of one percent of the next-smallest library's footprint.

## Performance Comparison

Benchmarked with [BenchmarkDotNet](https://benchmarkdotnet.org/) on Windows 11, Intel Core 7 150U (10 cores), .NET 9.0.18. All rows in all three tables come from a single benchmark session on an idle, freshly rebooted machine (July 2026, at v0.90). All output is generated in-memory (`byte[]`). IronPDF could not be benchmarked (requires a commercial license in Release builds). PDFsharp is benchmarked via MigraDoc (its document-model layer with tables, paragraphs, and auto-pagination) for a fair API-level comparison. Because iText is AGPL-licensed, it is not referenced by the benchmark project by default — its benchmarks are gated behind an opt-in MSBuild property (set the `IncludeIText=true` environment variable) so this MIT repo stays free of AGPL packages; the iText rows were measured in the same session with that flag enabled. See the [SimpleTinyPDF.Benchmarks](SimpleTinyPDF.Benchmarks/) project to run these yourself.

**Scenario 1 — 10,000-row CSV table** (landscape, auto-paginated with header repeat and alternate row shading):

| Library | Mean | Allocated | vs SimpleTinyPDF |
|---|---|---|---|
| **SimpleTinyPDF** | **183 ms** | **173 MB** | **1x** |
| **PDFsharp + MigraDoc** 6.2.4 | 4,441 ms | 845 MB | 24x slower |
| **iText** 9.6.0 | 7,849 ms | 1,986 MB | 43x slower |
| **QuestPDF** 2026.5 | 1,342 ms | 284 MB | 7x slower |
| **IronPDF** 2026.5 | N/A | N/A | — |

**Scenario 2 — 1,000-invoice batch** (each invoice: header, details, 3-row line items table, totals, footer):

| Library | Mean | Allocated | vs SimpleTinyPDF |
|---|---|---|---|
| **SimpleTinyPDF** | **114 ms** | **75 MB** | **1x** |
| **PDFsharp + MigraDoc** 6.2.4 | 1,048 ms | 559 MB | 9x slower |
| **iText** 9.6.0 | 2,768 ms | 1,062 MB | 24x slower |
| **QuestPDF** 2026.5 | 1,011 ms | 224 MB | 9x slower |
| **IronPDF** 2026.5 | N/A | N/A | — |

**Scenario 3 — Multi-page flowing report** (50 paragraphs with chapter headings, headers/footers with page numbers, summary table; uses `PdfDocumentLayout` with sections, columns, and page events):

| Library | Mean | Allocated | vs SimpleTinyPDF |
|---|---|---|---|
| **SimpleTinyPDF** (Layout) | **1.21 ms** | **1,785 KB** | **1x** |
| **PDFsharp + MigraDoc** 6.2.4 | 13.13 ms | 19,466 KB | 11x slower |
| **QuestPDF** 2026.5 | 19.00 ms | 838 KB | 16x slower |
| **IronPDF** 2026.5 | N/A | N/A | — |

SimpleTinyPDF is the fastest library in all three scenarios. In the layout report benchmark (Scenario 3), SimpleTinyPDF's `PdfDocumentLayout` engine is 11x faster than MigraDoc and 16x faster than QuestPDF while using a fraction of the memory compared to MigraDoc. QuestPDF achieves lower memory allocation but is significantly slower. In the data-heavy scenarios (1 and 2), SimpleTinyPDF leads by 7–43x in speed with substantially lower memory allocation.


## API Reference

### PdfDocument

The main entry point. Create one, add pages, then save.

```csharp
var doc = new PdfDocument();
doc.Title = "My Report";
doc.Author = "Jane Doe";

var page = doc.AddPage();                         // A4 by default
var page2 = doc.AddPage(PageSize.Letter);         // US Letter
var page3 = doc.AddPage(PageSize.A4.Landscape()); // A4 landscape
var cover = doc.InsertPage(1);                    // insert at position 1

doc.Save("output.pdf");       // save to file
doc.Save(stream);             // write to stream
byte[] bytes = doc.ToArray(); // get as byte array
```

| Property / Method | Description |
|---|---|
| `Title` | Document title (PDF metadata) |
| `Author` | Document author (PDF metadata) |
| `Pages` | Read-only list of all pages |
| `PageCount` | Number of pages |
| `FirstPage` / `LastPage` | First or last page (null if empty) |
| `GetPage(int pageNumber)` | Get a page by 1-based number |
| `GetPageNumber(PdfPage page)` | Get the 1-based number of a page |
| `AddPage(PageSize)` | Append a new page (defaults to A4) |
| `InsertPage(int, PageSize)` | Insert a page at a 1-based position |
| `AddImage(PdfImage)` | Register an image (deduplicates identical content) |
| `ImportPage(...)` / `ImportPages(...)` | Copy pages from an existing PDF (see [Editing Existing PDFs](#editing-existing-pdfs)) |
| `RemovePage(int)` / `MovePage(int, int)` | Delete or reorder pages |
| `Merge(...)` | Static: combine existing PDFs into one document |
| `Save(string)` / `Save(Stream)` | Write PDF to file or stream |
| `ToArray()` | Return PDF as a byte array |

### PdfPage

All drawing happens on a page. Coordinates are in points. By default the coordinate system is top-down (Y=0 is the top of the page). Set `CoordinateOrigin` to use native PDF bottom-up coordinates instead.

```csharp
// Default: Y=0 at top, Y increases downward
page.CoordinateOrigin = CoordinateOrigin.TopDown;

// Native PDF: Y=0 at bottom, Y increases upward
page.CoordinateOrigin = CoordinateOrigin.BottomUp;
```

In `BottomUp` mode, Y values map directly to PDF coordinates: text Y is the baseline, and rectangle/image Y is the bottom-left corner. Tables (`DrawTable`) always use top-down layout internally.

#### Text

```csharp
page.DrawText("Hello", 50, 50);
page.DrawText("Bold red", 50, 70, PdfFont.HelveticaBold, 14, PdfColor.Red, TextAlignment.Left, underline: true);

// Hyperlink — text becomes clickable
page.DrawText("Visit Example", 50, 90, PdfFont.Helvetica, 12, PdfColor.Blue,
    underline: true, link: "https://example.com");

// Wrapped text — pass width to enable word wrap, returns Y after last line
float nextY = page.DrawText("Long paragraph...", 50, 110,
    PdfFont.TimesRoman, 11, width: 400, lineSpacing: 1.4f);

// Wrapped text with hyperlink (each wrapped line is clickable)
float nextY1 = page.DrawText("Click here for full terms and conditions", 50, 160,
    link: "https://example.com/terms", width: 200);

// Rich text (mixed fonts/sizes/colors on one line)
page.DrawText(new[] {
    new TextSpan("Normal "),
    new TextSpan("bold", PdfFont.HelveticaBold),
    new TextSpan(" and ", PdfFont.Helvetica, 12, PdfColor.DarkGray),
    new TextSpan("red", PdfFont.Helvetica, 12, PdfColor.Red, underline: true)
}, 50, 200);

// Rich text with hyperlinks — individual spans can be links
page.DrawText(new[] {
    new TextSpan("See "),
    new TextSpan("our docs", PdfFont.Helvetica, 12, PdfColor.Blue,
        underline: true, link: "https://docs.example.com"),
    new TextSpan(" for details.")
}, 50, 220);

// Rich text with word wrap (pass width)
float nextY2 = page.DrawText(spans, 50, 240, width: 400);

// Measure text width
float w = page.MeasureText("Hello", PdfFont.Helvetica, 12);

// Rotated text — angle in degrees, clockwise
page.DrawText("Rotated 45°", 300, 100, fontSize: 16, rotation: 45);
page.DrawText("Rotated text box", 300, 200, width: 150, rotation: 90);

// Character spacing — extra space between each character
page.DrawText("SPACED OUT", 50, 250, characterSpacing: 3f);

// Faux bold and italic — simulated styles for any font (built-in or custom)
page.DrawText("Faux bold", 50, 270, bold: true);
page.DrawText("Faux italic", 50, 290, italic: true);
page.DrawText("Both", 50, 310, bold: true, italic: true);

// Full justification — distributes words evenly across the line width
float nextY3 = page.DrawText("Justified paragraph text...", 50, 340,
    alignment: TextAlignment.Justify, width: 400);
```

| Method | Returns | Description |
|---|---|---|
| `DrawText(string, ...)` | float | Text; pass `width` for word wrap. Returns Y after text |
| `DrawText(spans, ...)` | float | Rich text (mixed formatting); pass `width` for word wrap |
| `MeasureText(text, font, size)` | float | Width of text in points |

All text methods accept an optional `rotation` parameter — angle in degrees, clockwise (matching CSS convention), rotating around the element's `(x, y)` position.

**TextSpan** is used with the spans overload of `DrawText` for mixed-format text:

```csharp
new TextSpan("hello")                                          // defaults: Helvetica 12pt black
new TextSpan("bold", PdfFont.HelveticaBold, 14)                // custom font/size
new TextSpan("fancy", PdfFont.TimesItalic, 12, PdfColor.Blue, underline: true, opacity: 0.8f)
new TextSpan("click me", PdfFont.Helvetica, 12, PdfColor.Blue, underline: true,
    link: "https://example.com")                               // hyperlink
new TextSpan("bold", bold: true)                               // faux bold
new TextSpan("italic", italic: true)                           // faux italic
new TextSpan("wide", characterSpacing: 2f)                     // character spacing
```

**TextAlignment:** `TextAlignment.Left` (default), `TextAlignment.Center`, `TextAlignment.Right`, `TextAlignment.Justify`

**Built-in Fonts**

SimpleTinyPDF includes the 14 standard PDF Type 1 fonts. These are built into every PDF viewer and require no embedding, keeping output files small.

| Family | Variants |
|---|---|
| Helvetica | Regular, Bold, Oblique, BoldOblique |
| Times | Roman, Bold, Italic, BoldItalic |
| Courier | Regular, Bold, Oblique, BoldOblique |
| Symbol | (symbol characters) |
| ZapfDingbats | (decorative symbols) |

Use them via the `PdfFont` enum:

```csharp
page.DrawText("Hello", 50, 50, PdfFont.HelveticaBold, 14);
```

**Custom Fonts (TrueType / OpenType)**

Load any `.ttf` or `.otf` font file and use it anywhere a built-in font is accepted. The font binary is embedded in the PDF using CID (composite) fonts with Identity-H encoding, enabling full Unicode support — including BMP (CJK, Cyrillic, Greek) and supplementary planes (CJK Extension B, enclosed alphanumerics, etc.). UTF-16 surrogate pairs are handled transparently.

```csharp
// Load from file, byte array, or stream
var roboto = PdfFontSource.FromFile("Roboto-Regular.ttf");
var mono = PdfFontSource.FromBytes(File.ReadAllBytes("SourceCodePro-Regular.otf"));
var serif = PdfFontSource.FromStream(fontStream);

// Use with any drawing method — Latin, CJK, Cyrillic, etc.
page.DrawText("Custom font text", 50, 50, roboto, 14);
page.DrawText("Кириллица", 50, 70, roboto, 14);      // Cyrillic

var cjkFont = PdfFontSource.FromFile("NotoSansJP-Regular.ttf");
page.DrawText("こんにちは世界", 50, 90, cjkFont, 14); // Japanese

// Mix with built-in fonts in rich text
page.DrawText(new[] {
    new TextSpan("Hello ", PdfFont.HelveticaBold, 14),
    new TextSpan("世界", cjkFont, 14, PdfColor.Red)
}, 50, 120);

// Use in tables
table.HeaderFont = roboto;
table.CellFont = roboto;
```

Built-in `PdfFont` values are automatically converted to `PdfFontSource`, so all existing code continues to work unchanged. The same `PdfFontSource` instance can be used across multiple pages — the font data is embedded only once in the PDF. TrueType fonts are automatically subsetted to include only the glyphs used in the document. Text rendered with custom fonts is selectable and copyable in PDF viewers (via an embedded ToUnicode CMap).

**Character Support**

*Built-in fonts:*
- WinAnsiEncoding coverage (~256 Latin characters, digits, punctuation)
- Extended European diacritics (e.g. ą, ć, ę, ł, ň, ř, š, ž, ő, ű) via encoding extensions

*Custom fonts (TrueType / OpenType):*
- Full Unicode Basic Multilingual Plane (BMP) — U+0000 to U+FFFF
- Supplementary Unicode planes — U+10000 to U+10FFFF (CJK Extension B, enclosed alphanumerics, etc.)
- CJK characters (Chinese, Japanese, Korean)
- Cyrillic, Greek, and other scripts
- Arabic and Hebrew with automatic right-to-left layout and Arabic letter joining (see [Right-to-Left Text](#right-to-left-text-arabic-hebrew))
- Any character the font contains a glyph for

**Font Subsetting**

TrueType (`.ttf`) fonts are automatically subsetted when embedded in the PDF. Only the glyphs actually used in the document are included, dramatically reducing file size — especially for large CJK fonts (e.g. a 16 MB font embedding just a few characters produces a PDF well under 1 MB). Composite glyphs (like accented characters built from components) are handled correctly; all referenced component glyphs are automatically retained.

To disable subsetting and embed the full font file, set `Subset` to `false`:

```csharp
var font = PdfFontSource.FromFile("Roboto-Regular.ttf");
font.Subset = false; // embed the full font binary
```

OpenType (`.otf`) fonts with CFF outlines are currently embedded in full.

#### Right-to-Left Text (Arabic, Hebrew)

Arabic and Hebrew work automatically — pass text in normal logical order (the order you type it) and SimpleTinyPDF handles the rest. The Unicode Bidirectional Algorithm (UAX #9) reorders each line to visual order, Arabic letters are joined via contextual presentation forms (isolated/initial/medial/final plus the mandatory lam-alef ligatures), brackets are mirrored inside RTL runs, and Arabic diacritics (tashkeel) stay attached to their letters.

```csharp
// Any font with the right coverage works. DejaVu Sans covers both scripts;
// Amiri and Noto Naskh Arabic cover Arabic only.
var rtlFont = PdfFontSource.FromFile("DejaVuSans.ttf");

// Tip: keep RTL literals in variables — mixing them inline with more
// arguments is valid C#, but bidi-aware editors display the line scrambled.
string arabicGreeting = "السلام عليكم";
string hebrewGreeting = "שלום עולם";
string mixedLine = "Invoice 42 فاتورة (2026)";
string arabicParagraph = "السلام عليكم ورحمة الله وبركاته السلام عليكم ورحمة الله وبركاته";

// Right-align RTL text so it reads naturally from the right margin
page.DrawText(arabicGreeting, 545, 50, rtlFont, 18, alignment: TextAlignment.Right);
page.DrawText(hebrewGreeting, 545, 80, rtlFont, 18, alignment: TextAlignment.Right);

// Mixed directions just work — Latin and digits stay left-to-right
page.DrawText(mixedLine, 50, 110, rtlFont, 12);

// Wrapped and justified RTL paragraphs
page.DrawText(arabicParagraph, 50, 140, rtlFont, 12,
    width: 300, alignment: TextAlignment.Right);
```

Requirements and notes:

- A custom font whose cmap includes the Arabic Presentation Forms blocks is required for Arabic (most Arabic fonts qualify: Amiri, Noto Naskh Arabic, Arial, Tahoma, Times New Roman, DejaVu Sans, Traditional Arabic, ...). Hebrew needs no special glyphs — any font with Hebrew coverage works.
- The 14 built-in PDF fonts contain no Arabic or Hebrew glyphs; a custom font is required.
- Shaping is presentation-form based, not an OpenType GSUB engine: font-specific optional ligatures and precise GPOS diacritic positioning are not applied. Naskh-style fonts render well; calligraphic Nastaliq styles (common for Urdu) will not look right.
- Shadda+vowel mark combinations are substituted with the precomposed stacked forms (U+FC60–U+FC62) when the font provides those glyphs (Tahoma, Arial, Times New Roman, and most Arabic fonts do), so the vowel renders correctly above the shadda. In fonts without them (e.g. DejaVu Sans) the two marks overlap.
- Text extraction (copy/paste) from the PDF yields visual-order presentation forms rather than the original logical-order text.
- In rich text (`TextSpan`), each span/word is processed independently, so a multi-word RTL run inside *wrapped* rich text keeps logical word order. Plain `DrawText` (with or without `width`) handles multi-word RTL correctly.

**What Is Not Supported**

- OpenType GSUB/GPOS shaping (font-specific ligatures, precise diacritic positioning, Nastaliq-style Urdu) — Arabic shaping uses Unicode presentation forms instead
- CFF/OpenType font subsetting (the full font file is embedded; TrueType fonts are subsetted)
- WOFF/WOFF2 web font formats

#### Shapes

```csharp
page.DrawLine(50, 100, 500, 100, PdfColor.Black, lineWidth: 0.5f);
page.DrawRectangle(50, 110, 200, 80, PdfColor.DarkGray, lineWidth: 1f);
page.DrawFilledRectangle(50, 200, 200, 80, PdfColor.Rgb(230, 240, 255), PdfColor.Black, lineWidth: 0.5f);

// Rotated shapes — angle in degrees, clockwise
page.DrawFilledRectangle(300, 100, 80, 40, PdfColor.Blue, rotation: 45);
page.DrawRectangle(300, 200, 80, 80, PdfColor.Green, lineWidth: 2, rotation: 30);
page.DrawLine(100, 300, 300, 300, PdfColor.Red, lineWidth: 2, rotation: 90);
```

All shape methods accept an optional `rotation` parameter — angle in degrees, clockwise, rotating around the element's `(x, y)` position.

#### Images

```csharp
var img = PdfImage.FromFile("photo.jpg");
var img = PdfImage.FromBytes(byteArray);
var img = PdfImage.FromStream(stream);

int w = img.PixelWidth;   // display width (EXIF-adjusted)
int h = img.PixelHeight;  // display height (EXIF-adjusted)
```

```csharp
page.DrawImage(logo, x: 50, y: 30, width: 120, height: 40);
page.DrawImage(logo, x: 50, y: 30, width: 120, height: 40, opacity: 0.5f);
page.DrawImage(logo, x: 50, y: 30, width: 120, height: 40, scaleMode: ImageScaleMode.Fit);

// Rotated image — angle in degrees, clockwise
page.DrawImage(logo, x: 200, y: 100, width: 120, height: 40, rotation: 90);
```

JPEG and PNG are supported (auto-detected). PNG transparency is preserved. EXIF orientation tags are automatically applied.

The `scaleMode` parameter controls how the image is scaled to fit the target rectangle:

| Mode | Aspect Ratio | Description |
|---|---|---|
| `Stretch` (default) | Ignored | Fills the entire area exactly; image may be distorted |
| `Fit` | Preserved | Scales to fit inside the area; image is centered with possible letterboxing |
| `Fill` | Preserved | Scales to cover the entire area; image is centered and overflow is clipped |

`DrawImage` also accepts an optional `rotation` parameter — angle in degrees, clockwise, rotating around the element's `(x, y)` position.

#### Tables

Build tables with a fluent API or import from CSV.

```csharp
var table = new PdfTable(100, 200, 100, 100)  // column widths in points
    .SetHeaders("ID", "Description", "Qty", "Price")
    .AddRow("1001", "Widget", "5", "$12.50")
    .AddRow("1002", "Gadget", "2", "$24.00");

table.SetColumnAlignment(2, TextAlignment.Right);
table.SetColumnAlignment(3, TextAlignment.Right);
table.AlternateRowShading = true;
```

| Property | Default | Description |
|---|---|---|
| `HeaderFont` | HelveticaBold | Font for header row |
| `HeaderFontSize` | 10 | Font size for header row |
| `CellFont` | Helvetica | Font for body cells |
| `CellFontSize` | 10 | Font size for body cells |
| `HeaderBackground` | LightGray | Header row background color |
| `HeaderTextColor` | Black | Header text color |
| `BorderColor` | Black | Border color |
| `BorderWidth` | 0.5 | Border line width in points |
| `CellPadding` | 4 | Padding inside cells in points |
| `TextColor` | Black | Body text color |
| `AlternateRowShading` | false | Enable zebra-stripe rows |
| `AlternateRowColor` | RGB(0.95, 0.95, 0.95) | Alternate row background |
| `LineSpacing` | 1.2 | Line spacing multiplier for cell text |

**CSV import:**

```csharp
var table = PdfTable.FromCsv("data.csv");
var table = PdfTable.FromCsv("data.csv", firstRowIsHeader: true, delimiter: ',',
    columnWidths: new float[] { 80, 200, 80, 80 });
var table = PdfTable.FromCsvString(csvContent, totalWidth: 500);
```

**Drawing tables:**

```csharp
float endY = page.DrawTable(table, x: 50, y: 200, bottomMargin: 50);
```

Tables that exceed the page height automatically continue on new pages with repeated headers. By default, continuation pages start the table at the same Y position as the first page. Use `continuationY` to start higher on subsequent pages:

```csharp
// Table starts at y=300 on page 1, but at y=50 on continuation pages
page.DrawTable(table, x: 50, y: 300, continuationY: 50);
```

#### Lists

`DrawList` renders a hierarchical list and returns a `(PdfPage page, float y)` tuple indicating where rendering ended, so you can continue drawing below it — including on a different page if the list overflowed.

```csharp
var items = new[]
{
    new ListItem("Introduction"),
    new ListItem("Installation",
        new ListItem("Windows"),
        new ListItem("macOS"),
        new ListItem("Linux")),
    new ListItem("Configuration")
};

// Bullet list (default)
var (nextPage, nextY) = page.DrawList(items, x: 50, y: 100, width: 450);

// Numbered list
var (nextPage, nextY) = page.DrawList(items, x: 50, y: 100, width: 450,
    style: ListStyle.Numbered);

// Lowercase Roman numerals (i, ii, iii…)
var (nextPage, nextY) = page.DrawList(items, x: 50, y: 100, width: 450,
    style: ListStyle.RomanLower);

// Uppercase Roman numerals (I, II, III…)
var (nextPage, nextY) = page.DrawList(items, x: 50, y: 100, width: 450,
    style: ListStyle.RomanUpper);

// Multi-page list — automatically flows to new pages
var (lastPage, endY) = page.DrawList(items, x: 50, y: 100, width: 450,
    bottomMargin: 50, continuationY: 50);
```

Each item can override the style and bullet symbol used for its children:

```csharp
var items = new[]
{
    // Children use numbered style
    new ListItem("Chapter 1", ListStyle.Numbered,
        new ListItem("Section 1.1"),
        new ListItem("Section 1.2")),

    // Children use Roman numerals with a custom bullet symbol at the next level
    new ListItem("Chapter 2", ListStyle.RomanUpper,
        new ListItem("Part I", ListStyle.RomanLower,
            new ListItem("Sub-part a"))),
};
```

To use a custom bullet symbol from Symbol or ZapfDingbats, pass a `TextSpan` as the bullet:

```csharp
// Custom top-level bullet
var (nextPage, nextY) = page.DrawList(items, x: 50, y: 100, width: 450,
    bullet: new TextSpan("»", PdfFont.Helvetica));

// Per-level custom symbols via ChildrenBullet
new ListItem("Top item", ListStyle.Bullet, new TextSpan("‣", PdfFont.Helvetica),
    new ListItem("Child item"))
```

| Parameter | Default | Description |
|---|---|---|
| `items` | required | Array of `ListItem` to render |
| `x` | required | Left edge in points |
| `y` | required | Top of first item in points |
| `width` | required | Available width for text wrapping |
| `style` | `Bullet` | `Bullet`, `Numbered`, `RomanLower`, or `RomanUpper` |
| `bottomMargin` | 0 | Distance from page bottom before a new page is added |
| `font` | Helvetica | Font for item text and numbered markers |
| `fontSize` | 12 | Font size in points |
| `lineSpacing` | 1.2 | Line spacing multiplier |
| `color` | Black | Text and marker color |
| `bullet` | `•` | Bullet symbol (used when style is `Bullet`) |
| `startNumber` | 1 | Starting counter value (numbered/Roman styles) |
| `indentPerLevel` | 20 | Horizontal indent per nesting level in points |
| `continuationY` | same as `y` | Y position on continuation pages |

### PdfColor

```csharp
var c1 = PdfColor.Rgb(51, 102, 204);        // 0-255 integers
var c2 = PdfColor.Rgb(0.2f, 0.4f, 0.8f);    // 0.0-1.0 floats
var c3 = PdfColor.Cmyk(1f, 0f, 0f, 0f);     // CMYK
var c4 = PdfColor.Gray(0.5f);               // grayscale
```

**Spot Colors (Separation)**

Spot colors represent named inks used in professional printing (e.g., PANTONE colors). Each spot color has a name and a CMYK fallback for on-screen display. Spot colors work everywhere a regular `PdfColor` is accepted — text, shapes, tables, etc.

```csharp
// Define a spot color with CMYK display fallback
var pantone = PdfColor.Spot("PANTONE 185 C", c: 0f, m: 0.91f, y: 0.76f, k: 0f);

// Use anywhere a PdfColor is accepted
page.DrawFilledRectangle(50, 50, 200, 100, pantone);
page.DrawText("Spot color text", 50, 200, color: pantone);

// Tint control (0.0 = no ink, 1.0 = full ink)
var light = pantone.WithTint(0.5f);          // 50% tint of the same ink
var custom = PdfColor.Spot("Brand Blue", 1f, 0.5f, 0f, 0f, tint: 0.75f);
```

In the generated PDF, spot colors are written as `/Separation` color spaces with a Type 2 tint transform function. Different tints of the same ink share a single color space object, and the same spot color is deduplicated across pages.

Predefined: `Black`, `White`, `Red`, `Green`, `Blue`, `Yellow`, `Cyan`, `Magenta`, `Orange`, `Purple`, `Pink`, `Brown`, `Gold`, `Navy`, `Teal`, `Maroon`, `Olive`, `Coral`, `Crimson`, `Indigo`, `Silver`, `MediumGray`, `LightGray`, `DarkGray`

<details>
<summary>Predefined color values</summary>

| Name | Value | Color Space |
|---|---|---|
| `Black` | K=1 | CMYK |
| `White` | 255, 255, 255 | RGB |
| `Red` | 255, 0, 0 | RGB |
| `Green` | 0, 255, 0 | RGB |
| `Blue` | 0, 0, 255 | RGB |
| `Yellow` | Y=1 | CMYK |
| `Cyan` | C=1 | CMYK |
| `Magenta` | M=1 | CMYK |
| `Orange` | 255, 165, 0 | RGB |
| `Purple` | 128, 0, 128 | RGB |
| `Pink` | 255, 192, 203 | RGB |
| `Brown` | 139, 69, 19 | RGB |
| `Gold` | 255, 215, 0 | RGB |
| `Navy` | 0, 0, 128 | RGB |
| `Teal` | 0, 128, 128 | RGB |
| `Maroon` | 128, 0, 0 | RGB |
| `Olive` | 128, 128, 0 | RGB |
| `Coral` | 255, 127, 80 | RGB |
| `Crimson` | 220, 20, 60 | RGB |
| `Indigo` | 75, 0, 130 | RGB |
| `Silver` | 192, 192, 192 | RGB |
| `MediumGray` | 128, 128, 128 | RGB |
| `LightGray` | 212, 212, 212 | RGB |
| `DarkGray` | 84, 84, 84 | RGB |

</details>

### PageSize

Predefined: `PageSize.A4`, `A3`, `A5`, `Letter`, `Legal`

```csharp
var landscape = PageSize.A4.Landscape();
var custom = new PageSize(400, 600);  // width x height in points
```

### PdfUnit

Convert between PDF points and common measurement units. PDF uses points as its native unit (72 points = 1 inch).

```csharp
// Inches, centimeters, millimeters → points
float x = PdfUnit.InchesToPoints(2.5f);    // 180
float y = PdfUnit.CmToPoints(10f);         // 283.46
float w = PdfUnit.MmToPoints(50f);         // 141.73

// Points → other units
float inches = PdfUnit.PointsToInches(72f);  // 1.0
float cm     = PdfUnit.PointsToCm(72f);      // 2.54
float mm     = PdfUnit.PointsToMm(72f);      // 25.4

// Fractional inches — great for imperial measurements
float a = PdfUnit.InchesToPoints("1-1/8");   // 81  (hyphen separator)
float b = PdfUnit.InchesToPoints("1 1/8");   // 81  (space separator)
float c = PdfUnit.InchesToPoints("3/4");     // 54  (fraction only)
float d = PdfUnit.InchesToPoints(1, 1, 8);   // 81  (whole, numerator, denominator)

// Parse fractional inches to a float value
float val = PdfUnit.ParseInches("1-1/8");    // 1.125
```

| Method | Description |
|---|---|
| `InchesToPoints(float)` | Convert inches to points |
| `PointsToInches(float)` | Convert points to inches |
| `CmToPoints(float)` | Convert centimeters to points |
| `PointsToCm(float)` | Convert points to centimeters |
| `MmToPoints(float)` | Convert millimeters to points |
| `PointsToMm(float)` | Convert points to millimeters |
| `InchesToPoints(string)` | Parse fractional inches (e.g. "1-1/8") and convert to points |
| `InchesToPoints(int, int, int)` | Convert whole + fraction (numerator/denominator) inches to points |
| `ParseInches(string)` | Parse fractional inches string to a float value in inches |

### Bookmarks

Add bookmarks (outlines) that appear in the PDF viewer's navigation panel. Bookmarks can be nested to create a hierarchical table of contents.

```csharp
// Top-level bookmarks
var ch1 = doc.AddBookmark("Chapter 1", page1);
var ch2 = doc.AddBookmark("Chapter 2", page2);

// Nested bookmarks — point to a specific Y position on the page
ch1.AddBookmark("Installation", page1, y: 150);
ch1.AddBookmark("Configuration", page1, y: 350);

// Deeper nesting
var advanced = ch2.AddBookmark("Advanced Topics", page3);
advanced.AddBookmark("Performance Tuning", page3, y: 200);
```

| Method | Description |
|---|---|
| `PdfDocument.AddBookmark(title, page, y?)` | Add a top-level bookmark. Returns the bookmark for nesting children. |
| `PdfBookmark.AddBookmark(title, page, y?)` | Add a child bookmark under this one. |

When `y` is omitted the bookmark fits the entire page. When `y` is provided the viewer scrolls to that vertical position (in the page's coordinate system).

### Annotations

Add interactive annotations to pages — sticky notes, text markup, stamps, and internal document links. All annotation types support both TopDown and BottomUp coordinate systems.

**Text Annotations (Sticky Notes)**

```csharp
// Basic sticky note
page.AddTextAnnotation(100, 100, "Please review this section.");

// With title, icon, color, and open state
page.AddTextAnnotation(100, 200, "Needs revision", title: "Reviewer",
    icon: TextAnnotationIcon.Note, color: PdfColor.Red, open: true);
```

| Parameter | Default | Description |
|---|---|---|
| `x`, `y` | required | Position of the annotation icon (24x24 points) |
| `contents` | required | Note text shown in the popup |
| `title` | null | Author or title shown in the popup header |
| `icon` | `Comment` | Icon style: `Comment`, `Note`, `Key`, `Help`, `NewParagraph`, `Paragraph`, `Insert` |
| `color` | null | Icon color (viewer default if null) |
| `open` | false | Whether the popup starts open |

**Markup Annotations (Highlight, Underline, StrikeOut)**

```csharp
// Highlight a region
page.AddMarkupAnnotation(50, 50, 200, 14, MarkupAnnotationType.Highlight,
    color: PdfColor.Rgb(1f, 1f, 0f));

// Underline with a comment
page.AddMarkupAnnotation(50, 80, 200, 14, MarkupAnnotationType.Underline,
    color: PdfColor.Green, contents: "Good point");

// Strikeout
page.AddMarkupAnnotation(50, 110, 200, 14, MarkupAnnotationType.StrikeOut,
    color: PdfColor.Red);
```

| Parameter | Default | Description |
|---|---|---|
| `x`, `y` | required | Top-left corner of the marked region |
| `width`, `height` | required | Size of the marked region |
| `type` | `Highlight` | `Highlight`, `Underline`, or `StrikeOut` |
| `color` | null | Markup color (viewer default if null) |
| `contents` | null | Comment text shown in a popup |
| `title` | null | Author name |

**Stamp Annotations**

```csharp
page.AddStampAnnotation(100, 100, 200, 60, stamp: StampType.Approved);
page.AddStampAnnotation(100, 200, 200, 60, stamp: StampType.Confidential,
    contents: "Internal use only");
```

Available stamp types: `Approved`, `Experimental`, `NotApproved`, `AsIs`, `Expired`, `NotForPublicRelease`, `Confidential`, `Final`, `Sold`, `Departmental`, `ForComment`, `TopSecret`, `Draft`, `ForPublicRelease`

| Parameter | Default | Description |
|---|---|---|
| `x`, `y` | required | Top-left corner of the stamp |
| `width`, `height` | required | Size of the stamp |
| `stamp` | `Draft` | Predefined stamp type |
| `contents` | null | Tooltip text |
| `title` | null | Author name |
| `color` | null | Stamp color |

**Internal Links (GoTo)**

Create clickable regions that navigate to another page in the same document.

```csharp
var page1 = doc.AddPage();
var page2 = doc.AddPage();

// Link that fits the target page
page1.AddLinkToPage(50, 50, 120, 20, page2);

// Link that scrolls to a specific Y position
page1.AddLinkToPage(50, 80, 120, 20, page2, targetY: 200);
```

| Parameter | Default | Description |
|---|---|---|
| `x`, `y` | required | Top-left corner of the clickable area |
| `width`, `height` | required | Size of the clickable area |
| `targetPage` | required | The page to navigate to |
| `targetY` | null | Y position on the target page (fits entire page if null) |

### Encryption

Protect PDF documents with password-based encryption. Supports AES-128 (PDF 1.6) and AES-256 (PDF 2.0) with user passwords, owner passwords, and granular permission flags. All cryptography uses built-in `System.Security.Cryptography` — no external dependencies.

```csharp
// Owner password only — opens without a password, but restricts actions
doc.Encryption = new PdfEncryptionOptions
{
    OwnerPassword = "admin-secret",
    Permissions = PdfPermissions.Print | PdfPermissions.ExtractText
};

// User + owner password — requires password to open
doc.Encryption = new PdfEncryptionOptions
{
    UserPassword = "open-me",
    OwnerPassword = "full-access",
    Level = PdfEncryptionLevel.Aes256,
    Permissions = PdfPermissions.Print
};

// All permissions, AES-128 (default level)
doc.Encryption = new PdfEncryptionOptions
{
    UserPassword = "secret",
    OwnerPassword = "owner"
};
```

**PdfEncryptionOptions:**

| Property | Default | Description |
|---|---|---|
| `UserPassword` | `""` | Password to open the document. Empty means no open password required |
| `OwnerPassword` | `""` | Password for full owner access (change permissions, unrestricted operations) |
| `Level` | `Aes128` | `Aes128` (PDF 1.6, V4/R4) or `Aes256` (PDF 2.0, V5/R6) |
| `Permissions` | `All` | User access permissions (see below) |

**PdfPermissions** (combine with `|`):

| Flag | Description |
|---|---|
| `Print` | Print the document (may be low-quality without `HighQualityPrint`) |
| `ModifyContents` | Modify document contents |
| `ExtractText` | Copy or extract text and graphics |
| `AnnotateAndForms` | Add or modify annotations and fill form fields |
| `FillForms` | Fill in existing form fields |
| `ExtractForAccessibility` | Extract text and graphics for accessibility |
| `Assemble` | Insert, rotate, delete pages, and bookmarks |
| `HighQualityPrint` | Print at high quality |
| `All` | All permissions granted |
| `None` | No permissions granted |

### Barcodes

Draw 1D barcodes and QR codes as crisp vector graphics (PDF rectangles, not images). All encoding, check digits, and error correction are computed internally with zero dependencies.

```csharp
// EAN-13 barcode (check digit computed automatically)
page.DrawBarcode("590123412345", BarcodeType.Ean13, 50, 100, 200, 80);

// QR code with high error correction
page.DrawBarcode("https://example.com", BarcodeType.QrCode, 50, 200, 150, 150,
    new BarcodeOptions { QrErrorCorrectionLevel = QrErrorCorrection.High });

// Code 128 with human-readable text below
page.DrawBarcode("ABC-12345", BarcodeType.Code128, 50, 400, 250, 60,
    new BarcodeOptions { ShowText = true });

// UPC-A (12-digit North American retail)
page.DrawBarcode("01234567890", BarcodeType.UpcA, 50, 500, 200, 80);

// Custom colors and rotation
page.DrawBarcode("HELLO", BarcodeType.Code39, 300, 100, 200, 60,
    new BarcodeOptions
    {
        ForegroundColor = PdfColor.Navy,
        BackgroundColor = PdfColor.Rgb(245, 245, 255),
        ShowText = true,
        Rotation = 90
    });
```

**Supported barcode types:**

| Type | Characters | Use Cases |
|---|---|---|
| `Code128` | ASCII 0-127 | Shipping, logistics, general-purpose |
| `Code39` | 0-9, A-Z, - . $ / + % SPACE | Industrial, warehouse, military |
| `Ean13` | 12-13 digits | Retail products (international) |
| `UpcA` | 11-12 digits | Retail products (North America) |
| `QrCode` | Any text (UTF-8) | URLs, payments, WiFi, general data |

**BarcodeOptions:**

| Property | Default | Description |
|---|---|---|
| `ForegroundColor` | Black | Bar / module color |
| `BackgroundColor` | White | Background color |
| `DrawBackground` | true | Whether to fill the background |
| `IncludeQuietZone` | true | Add required white-space margin (within specified width/height) |
| `ShowText` | false | Human-readable text below 1D barcodes |
| `TextFont` | Courier | Font for human-readable text |
| `TextFontSize` | 8 | Font size for human-readable text |
| `QrErrorCorrectionLevel` | Medium | `Low`, `Medium`, `Quartile`, or `High` (QR only) |
| `Rotation` | 0 | Clockwise rotation in degrees |
| `Opacity` | 1.0 | 0.0 (transparent) to 1.0 (opaque) |

### Form Fields (AcroForms)

Add interactive form fields that users can fill in with Adobe Acrobat or other PDF viewers. Fields are editable — values persist when the user saves the PDF.

```csharp
var doc = new PdfDocument();
var page = doc.AddPage();

// Text field
page.AddTextField("name", 100, 100, 200, 25, new TextFieldOptions
{
    Value = "John Doe",
    FontSize = 12
});

// Multi-line text field
page.AddTextField("notes", 100, 140, 200, 80, new TextFieldOptions
{
    MultiLine = true,
    FontSize = 10
});

// Password field
page.AddTextField("password", 100, 240, 200, 25, new TextFieldOptions
{
    Password = true
});

// Checkbox
page.AddCheckbox("agree", 100, 280, 15, new CheckboxOptions
{
    Checked = true,
    CheckColor = PdfColor.Blue
});

// Radio button group
var group = doc.CreateRadioGroup("color", new RadioGroupOptions
{
    SelectedValue = "green"
});
page.AddRadioButton(group, "red", 100, 310, 12);
page.AddRadioButton(group, "green", 100, 330, 12);
page.AddRadioButton(group, "blue", 100, 350, 12);

// Dropdown (combo box)
page.AddDropdown("country", 100, 380, 200, 25,
    new[] { "United States", "Canada", "United Kingdom" },
    new DropdownOptions { SelectedValue = "Canada" });

// Listbox (multi-select)
page.AddListbox("colors", 100, 420, 200, 100,
    new[] { "Red", "Green", "Blue", "Yellow" },
    new ListboxOptions
    {
        SelectedValues = new[] { "Green", "Blue" },
        MultiSelect = true
    });

// Push button
page.AddButton("submit", "Submit", 100, 540, 100, 30);

doc.Save("form.pdf");
```

**PdfPage methods:**

| Method | Description |
|---|---|
| `AddTextField(name, x, y, w, h, options?)` | Single or multi-line text input |
| `AddCheckbox(name, x, y, size, options?)` | Checkbox with checkmark |
| `AddRadioButton(group, value, x, y, size)` | Radio button (use with `CreateRadioGroup`) |
| `AddDropdown(name, x, y, w, h, items, options?)` | Dropdown / combo box |
| `AddListbox(name, x, y, w, h, items, options?)` | Scrollable list (single or multi-select) |
| `AddButton(name, label, x, y, w, h, options?)` | Push button |

**PdfDocument methods:**

| Method | Description |
|---|---|
| `CreateRadioGroup(name, options?)` | Create a radio button group. Returns `PdfRadioGroup` for use with `AddRadioButton`. |

**Option classes** — all fields are optional with sensible defaults:

| Class | Key Properties |
|---|---|
| `TextFieldOptions` | `Value`, `FontSize`, `MultiLine`, `Password`, `ReadOnly`, `Required`, `MaxLength`, `Alignment`, `TextColor`, `BackgroundColor`, `BorderColor` |
| `CheckboxOptions` | `Checked`, `ExportValue`, `CheckColor`, `ReadOnly`, `Required`, `BackgroundColor`, `BorderColor` |
| `RadioGroupOptions` | `SelectedValue`, `DotColor`, `ReadOnly`, `Required`, `BackgroundColor`, `BorderColor` |
| `DropdownOptions` | `SelectedValue`, `FontSize`, `Editable`, `ReadOnly`, `Required`, `TextColor`, `BackgroundColor`, `BorderColor` |
| `ListboxOptions` | `SelectedValues`, `FontSize`, `MultiSelect`, `ReadOnly`, `Required`, `TextColor`, `BackgroundColor`, `BorderColor` |
| `ButtonOptions` | `FontSize`, `TextColor`, `BackgroundColor`, `BorderColor` |

### Digital Signatures

Sign PDF documents with X.509 certificates (PKCS#7 detached signatures). Signed documents display "Document has not been modified since this signature was applied" in Adobe Acrobat. Supports visible or invisible signatures, optional RFC 3161 timestamping, and external signing via HSM/smart card/cloud KMS delegates.

```csharp
using System.Security.Cryptography.X509Certificates;

var doc = new PdfDocument();
var page = doc.AddPage();
page.DrawText("Signed document", 50, 50);

// Invisible signature (no visual appearance)
doc.Signature = new PdfSignatureOptions
{
    Certificate = new X509Certificate2("cert.pfx", "password"),
    Reason = "Approval",
    Location = "New York"
};

// Or load from file path
doc.Signature = new PdfSignatureOptions
{
    CertificatePath = "cert.pfx",
    CertificatePassword = "password"
};

doc.Save("signed.pdf");
```

**Visible signature** — displays signer name, date, reason, and location in a rectangle on the page:

```csharp
doc.Signature = new PdfSignatureOptions
{
    Certificate = cert,
    Page = page,
    X = 50, Y = 700,
    Width = 200, Height = 60,
    Reason = "Reviewed and approved",
    Location = "New York"
};
```

**RFC 3161 timestamping** — embeds a cryptographic timestamp from a trusted Time Stamp Authority, proving when the document was signed (instead of relying on the signer's local clock):

```csharp
doc.Signature = new PdfSignatureOptions
{
    Certificate = cert,
    TimestampServer = TimestampServer.DigiCert  // or Sectigo, FreeTSA
};

// Or a custom TSA URL
doc.Signature = new PdfSignatureOptions
{
    Certificate = cert,
    TimestampServerUrl = "http://timestamp.example.com/tsa"
};
```

**Custom signer** — for HSM, smart card, or cloud KMS scenarios where the private key is not directly accessible:

```csharp
doc.Signature = new PdfSignatureOptions
{
    Certificate = cert,  // public key only — private key not used
    CustomSigner = (dataToSign) =>
    {
        // Sign with your HSM/KMS and return PKCS#1 v1.5 RSA signature bytes
        return myHsm.Sign(dataToSign);
    }
};
```

**PdfSignatureOptions:**

| Property | Default | Description |
|---|---|---|
| `Certificate` | required | `X509Certificate2` with private key (or public key only when using `CustomSigner`) |
| `CertificatePath` | null | Path to a PKCS#12 (.pfx/.p12) file (alternative to `Certificate`) |
| `CertificatePassword` | null | Password for the PKCS#12 file |
| `CustomSigner` | null | External signing delegate for HSM/smart card/cloud KMS |
| `Reason` | null | Reason for signing |
| `Location` | null | Location where the document was signed |
| `ContactInfo` | null | Contact information of the signer |
| `HashAlgorithm` | SHA256 | Hash algorithm (`SHA256`, `SHA384`, `SHA512`) |
| `Page` | null | Page for visible signature (null = invisible) |
| `X`, `Y` | 0 | Position of visible signature rectangle |
| `Width` | 150 | Width of visible signature rectangle |
| `Height` | 50 | Height of visible signature rectangle |
| `AdditionalCertificates` | null | Intermediate CA certificates to include in the PKCS#7 structure |
| `TimestampServer` | `None` | Built-in TSA: `None`, `DigiCert`, `Sectigo`, `FreeTSA` |
| `TimestampServerUrl` | null | Custom RFC 3161 TSA URL |

### Editing Existing PDFs

Open an existing PDF with `PdfReadDocument.Open`, then copy pages into a `PdfDocument` with `ImportPage` / `ImportPages`. Imported pages keep their full appearance — content, fonts, images, vector graphics, links — and can be freely mixed with generated pages.

```csharp
// Merge several PDFs into one
var merged = PdfDocument.Merge("chapter1.pdf", "chapter2.pdf", "chapter3.pdf");
merged.Save("book.pdf");

// Extract pages 2-4 into a new document
using (var source = PdfReadDocument.Open("report.pdf"))
{
    var extract = new PdfDocument();
    extract.ImportPages(source, 2, 4);
    extract.Save("excerpt.pdf");
}

// Split a PDF into single-page files
using (var source = PdfReadDocument.Open("input.pdf"))
{
    for (int i = 1; i <= source.PageCount; i++)
    {
        var single = new PdfDocument();
        single.ImportPage(source, i);
        single.Save($"page-{i}.pdf");
    }
}

// Add a generated cover page in front of an existing document
using (var source = PdfReadDocument.Open("existing.pdf"))
{
    var doc = new PdfDocument();
    var cover = doc.AddPage();
    cover.DrawText("Cover Page", 50, 100, PdfFont.HelveticaBold, 32);
    doc.ImportPages(source, 1, source.PageCount);
    doc.Save("with-cover.pdf");
}

// Delete and reorder pages
using (var source = PdfReadDocument.Open("input.pdf"))
{
    var doc = new PdfDocument();
    doc.ImportPages(source, 1, source.PageCount);
    doc.RemovePage(2);      // delete page 2
    doc.MovePage(3, 1);     // move page 3 to the front
    doc.Save("rearranged.pdf");
}
```

**`PdfReadDocument`**

| Member | Description |
|---|---|
| `Open(string)` / `Open(Stream)` / `Open(byte[])` | Open an existing PDF for reading |
| `PageCount` | Number of pages |
| `GetPageSize(int)` | Size of a page in points (1-based) |
| `Title` / `Author` | Document metadata |
| `Dispose()` | Release the in-memory copy (already-imported pages stay valid) |

**`PdfDocument` page operations**

| Member | Description |
|---|---|
| `ImportPage(source, pageNumber)` | Append one page from an existing PDF |
| `ImportPage(source, pageNumber, insertAt)` | Insert an imported page at a 1-based position |
| `ImportPages(source, first, last)` | Import a consecutive page range |
| `ImportPages(source, params int[])` | Import specific pages in a given order (repeats allowed) |
| `RemovePage(pageNumber)` | Delete a page (generated or imported) |
| `MovePage(from, to)` | Reorder pages |
| `Merge(params PdfReadDocument[])` | Static: combine opened documents |
| `Merge(params string[])` | Static: combine PDF files by path |

Notes and current limitations:

- **Saving is a full rewrite.** The output is a brand-new, cleanly built PDF. Any digital signatures present in a *source* file are therefore not carried over (you can freshly sign the result with `doc.Signature`). Encrypting the merged result with `doc.Encryption` works normally.
- Pages imported from the same `PdfReadDocument` share their fonts, images, and other resources in the output — importing 10 pages that use the same font embeds that font once.
- Reads classic cross-reference tables, cross-reference streams, object streams (PDF 1.5+), incremental updates, and hybrid-reference files. Files with a damaged cross-reference table are repaired automatically by scanning when possible.
- PDF/X identity is preserved: the source's `/OutputIntents` (e.g. `GTS_PDFX`) and XMP metadata are carried onto the output catalog, so viewers like Acrobat keep simulating overprint for imported prepress content. When merging multiple sources, the first source that provides them wins.
- Form-field widgets and in-document navigation (bookmarks, GoTo links) are not copied from source files; URI links, text notes, markup, and stamps on imported pages are preserved.
- Password-protected source files are not supported yet (`Open` throws `NotSupportedException`).
- Drawing on an imported page (stamping/watermarking) is not supported yet — saving such a document throws `InvalidOperationException`.

### Document Layout

`PdfDocumentLayout` provides automatic content flow, pagination, headers/footers with page numbering, and two-pass rendering for accurate total page counts. Add paragraphs, rich text, images, tables, and lists — content flows across pages automatically.

**Basic report:**

```csharp
using SimpleTinyPDF;

var layout = new PdfDocumentLayout();
layout.PageSize = PageSize.Letter;
layout.Margins = new PdfMargins(72);  // 1 inch on all sides

// Headers and footers with page numbers
layout.HeaderFooter.Header = (page, ctx) =>
{
    page.DrawText("Monthly Report", page.Width / 2, 30,
        PdfFont.HelveticaBold, 9, PdfColor.DarkGray, TextAlignment.Center);
    page.DrawLine(72, 45, page.Width - 72, 45, PdfColor.LightGray);
};
layout.HeaderFooter.Footer = (page, ctx) =>
{
    page.DrawText($"Page {ctx.PageNumber} of {ctx.TotalPages}",
        page.Width / 2, page.Height - 35,
        PdfFont.Helvetica, 8, PdfColor.DarkGray, TextAlignment.Center);
};

// Title
layout.AddParagraph("Monthly Performance Report", new ParagraphOptions
{
    Font = PdfFont.HelveticaBold, FontSize = 22,
    Alignment = TextAlignment.Center, SpaceAfter = 20
});

// Body paragraphs — automatically flow across pages
layout.AddParagraph("Lorem ipsum dolor sit amet...", new ParagraphOptions
{
    SpaceAfter = 6
});

// Section heading
layout.AddParagraph("Financial Summary", new ParagraphOptions
{
    Font = PdfFont.HelveticaBold, FontSize = 16,
    SpaceBefore = 15, SpaceAfter = 8
});

// Tables flow across pages with repeated headers
var table = new PdfTable(150, 100, 100, 100)
    .SetHeaders("Quarter", "Revenue", "Expenses", "Profit")
    .AddRow("Q1", "$1.2M", "$0.9M", "$0.3M")
    .AddRow("Q2", "$1.5M", "$1.0M", "$0.5M");
table.HeaderBackground = PdfColor.Rgb(51, 51, 51);
table.HeaderTextColor = PdfColor.White;
layout.AddTable(table);

// Save
layout.Save("report.pdf");
```

**Rich text, images, lists, and page breaks:**

```csharp
// Rich text paragraphs
layout.AddParagraph(new[] {
    new TextSpan("Important: ", PdfFont.HelveticaBold, 12, PdfColor.Red),
    new TextSpan("This section contains critical information.")
}, new ParagraphOptions { SpaceAfter = 10 });

// Images
var logo = PdfImage.FromFile("chart.png");
layout.AddImage(logo, new ImageOptions
{
    Width = 300, Height = 200,
    Alignment = TextAlignment.Center,
    SpaceAfter = 10
});

// Lists
layout.AddList(new[] {
    new ListItem("First item"),
    new ListItem("Second item",
        new ListItem("Nested item")),
    new ListItem("Third item")
}, ListStyle.Numbered);

// Manual page break
layout.AddPageBreak();
```

**First-page and even-page header/footer overrides:**

```csharp
layout.HeaderFooter.FirstPageHeader = (page, ctx) =>
{
    page.DrawText("COVER PAGE", page.Width / 2, 30,
        PdfFont.HelveticaBold, 14, TextAlignment.Center);
};
layout.HeaderFooter.EvenPageHeader = (page, ctx) =>
{
    page.DrawText("Report — Even Page", 72, 30, PdfFont.Helvetica, 9);
};
```

**Wrapping an existing document:**

```csharp
var doc = new PdfDocument { Title = "My Report", Author = "Jane Doe" };
var layout = new PdfDocumentLayout(doc);  // layout pages are appended
layout.AddParagraph("Content...");
layout.Save("output.pdf");
```

**PdfDocumentLayout:**

| Property / Method | Description |
|---|---|
| `PageSize` | Page size for new pages (default A4) |
| `Margins` | Page margins (default 72pt / 1 inch all sides) |
| `HeaderFooter` | Header and footer configuration |
| `DefaultParagraphOptions` | Default options applied to paragraphs without explicit options |
| `Document` | The underlying `PdfDocument` (available after `Generate`) |
| `AddParagraph(text, options?)` | Add a plain-text paragraph |
| `AddParagraph(spans, options?)` | Add a rich-text paragraph |
| `AddImage(image, options?)` | Add an image at the current flow position |
| `AddTable(table)` | Add a table at the current flow position |
| `AddList(items, style?)` | Add a list at the current flow position |
| `AddPageBreak()` | Force a page break |
| `AddSection(options?)` | Start a new section with independent page settings |
| `AddColumnBreak()` | Force a column break (next column, or next page if last column) |
| `AddEventHandler(action)` | Add a page event handler (delegate or `IPageEventHandler`) |
| `Renderer` | Custom renderer for overriding default element rendering |
| `Generate()` | Render all elements and return the `PdfDocument` |
| `Save(string)` / `Save(Stream)` | Generate and save to file or stream |
| `ToArray()` | Generate and return PDF as byte array |

**PdfMargins:**

```csharp
new PdfMargins(72)              // 72pt on all sides
new PdfMargins(72, 54)          // 72pt top/bottom, 54pt left/right
new PdfMargins(72, 54, 72, 54)  // top, right, bottom, left
```

**HeaderFooterOptions:**

| Property | Default | Description |
|---|---|---|
| `Header` | null | Header callback for every page (unless overridden) |
| `Footer` | null | Footer callback for every page (unless overridden) |
| `FirstPageHeader` | null | Header for the first page only |
| `FirstPageFooter` | null | Footer for the first page only |
| `EvenPageHeader` | null | Header for even-numbered pages |
| `EvenPageFooter` | null | Footer for even-numbered pages |
| `HeaderDistance` | 36 | Distance from top edge where headers are drawn (points) |
| `FooterDistance` | 36 | Distance from bottom edge where footers are drawn (points) |

Each callback receives the `PdfPage` and a `PageContext` with:

| Property | Description |
|---|---|
| `PageNumber` | Current page number (1-based) |
| `TotalPages` | Total number of pages (accurate via two-pass rendering) |
| `IsFirstPage` | True if this is the first page |
| `IsEvenPage` | True if the page number is even |
| `SectionIndex` | Zero-based section index |
| `SectionPageNumber` | Page number within the current section (1-based) |
| `SectionTotalPages` | Total pages in the current section (accurate via two-pass) |

**ParagraphOptions:**

| Property | Default | Description |
|---|---|---|
| `Font` | Helvetica | Font (built-in `PdfFont` or custom `PdfFontSource`) |
| `FontSize` | 12 | Font size in points |
| `Color` | Black | Text color |
| `Alignment` | Left | `Left`, `Center`, `Right`, or `Justify` |
| `LineSpacing` | 1.2 | Line spacing multiplier |
| `SpaceBefore` | 0 | Space before the paragraph in points |
| `SpaceAfter` | 0 | Space after the paragraph in points |
| `Underline` | false | Underline text |
| `Bold` | false | Faux bold effect |
| `Italic` | false | Faux italic effect |
| `CharacterSpacing` | 0 | Extra space between characters in points |
| `Opacity` | 1.0 | Text opacity (0.0–1.0) |
| `TabStops` | null | Tab stop definitions for tab-delimited text |
| `LeftIndent` | 0 | Left indentation in points |
| `RightIndent` | 0 | Right indentation in points |
| `FirstLineIndent` | 0 | Extra first-line indent in points (negative = hanging indent) |
| `KeepTogether` | false | Move the paragraph to the next page instead of splitting it |
| `KeepWithNext` | false | Keep the paragraph on the same page as the start of the next element |

**ImageOptions:**

| Property | Default | Description |
|---|---|---|
| `Width` | null | Display width in points (auto-calculated if null) |
| `Height` | null | Display height in points (auto-calculated if null) |
| `SpaceBefore` | 0 | Space before the image in points |
| `SpaceAfter` | 0 | Space after the image in points |
| `Alignment` | Left | Horizontal alignment (`Left`, `Center`, `Right`) |
| `Opacity` | 1.0 | Image opacity (0.0–1.0) |
| `ScaleMode` | Fit | How the image is scaled within its bounds |

**TabStop:**

Tab stops control alignment of tab-delimited text within paragraphs.

```csharp
layout.AddParagraph("Item\t$100.00\tPaid", new ParagraphOptions
{
    TabStops = new[]
    {
        new TabStop(200, TabAlignment.Left),
        new TabStop(350, TabAlignment.Right),
        new TabStop(450, TabAlignment.Center, leader: '.')  // dot leader
    }
});
```

| `TabAlignment` | Description |
|---|---|
| `Left` | Text starts at the tab position |
| `Center` | Text is centered at the tab position |
| `Right` | Text ends at the tab position |
| `Decimal` | The decimal point aligns at the tab position |

**Sections:**

Sections let you change page size, margins, and headers/footers mid-document. Each section starts on a new page with its own settings. Null properties inherit from the parent layout.

```csharp
var layout = new PdfDocumentLayout();
layout.AddParagraph("Section 1 — A4 portrait");

// Switch to landscape Letter with narrow margins
layout.AddSection(new SectionOptions
{
    PageSize = PageSize.Letter.Landscape(),
    Margins = new PdfMargins(36),
    RestartPageNumbers = true,  // page numbers restart at 1
    HeaderFooter = new HeaderFooterOptions
    {
        Header = (page, ctx) =>
            page.DrawText($"Appendix — Page {ctx.SectionPageNumber}",
                72, 20, PdfFont.Helvetica, 9, PdfColor.DarkGray)
    }
});
layout.AddParagraph("Section 2 — Letter landscape with custom header");
```

**SectionOptions:**

| Property | Default | Description |
|---|---|---|
| `PageSize` | null (inherit) | Page size for this section |
| `Margins` | null (inherit) | Page margins for this section |
| `HeaderFooter` | null (inherit) | Header/footer configuration for this section |
| `RestartPageNumbers` | false | Reset page number to 1 at the start of this section |
| `ColumnCount` | 1 | Number of text columns (1 = single column) |
| `ColumnGap` | 18 | Gap between columns in points |

**Multi-column layout:**

Sections can have multiple columns. Content flows down each column, then to the next column, then to a new page.

```csharp
// Newsletter-style 2-column layout
layout.AddSection(new SectionOptions { ColumnCount = 2, ColumnGap = 18 });
layout.AddParagraph("This text flows across two columns automatically...");

// Force next column (or next page if on the last column)
layout.AddColumnBreak();
layout.AddParagraph("This starts in the second column.");

// 3-column layout with a narrow gap
layout.AddSection(new SectionOptions { ColumnCount = 3, ColumnGap = 12 });
layout.AddParagraph("Three-column newspaper-style text...");
```

**Page events:**

Page events let you hook into the page lifecycle — for example, to add a watermark on every page or draw page borders.

```csharp
// Delegate-based event handler
layout.AddEventHandler((eventType, page, ctx) =>
{
    if (eventType == PageEventType.PageCreated)
    {
        // Draw a watermark on every page
        page.DrawText("DRAFT", page.Width / 2 - 50, page.Height / 2,
            PdfFont.HelveticaBold, 48, PdfColor.Rgb(230, 230, 230));
    }
});

// Interface-based event handler
layout.AddEventHandler(new MyPageEventHandler());
```

| `PageEventType` | Description |
|---|---|
| `PageCreated` | A new page has been created and its header drawn |
| `PageFinished` | A page is about to have its footer drawn and be finalized |
| `SectionStarted` | A new section has started |
| `SectionFinished` | A section has finished |

```csharp
// IPageEventHandler interface
public class BorderHandler : IPageEventHandler
{
    public void HandleEvent(PageEventType eventType, PdfPage page, PageContext context)
    {
        if (eventType == PageEventType.PageCreated)
            page.DrawRectangle(36, 36, page.Width - 72, page.Height - 72,
                PdfColor.LightGray, stroke: true, fill: false);
    }
}
```

**Custom renderer:**

Override default element rendering by subclassing `CustomRenderer`. Return a new Y position to replace default rendering, or return `null` to use the default.

```csharp
layout.Renderer = new HighlightRenderer();

public class HighlightRenderer : CustomRenderer
{
    public override float? RenderParagraph(PdfPage page, string text, float x, float y,
        float width, ParagraphOptions options, PageContext context)
    {
        // Draw a yellow background behind every paragraph
        float height = 20;
        page.DrawRectangle(x, y, width, height, PdfColor.Rgb(255, 255, 200));
        page.DrawText(text, x + 4, y + 2, options.Font ?? PdfFont.Helvetica,
            options.FontSize, options.Color ?? PdfColor.Black);
        return y + height;  // return new Y position
    }
}
```

| Method | Description |
|---|---|
| `RenderParagraph(...)` | Override plain-text paragraph rendering |
| `RenderRichParagraph(...)` | Override rich-text paragraph rendering |
| `RenderImage(...)` | Override image rendering |
| `RenderTable(...)` | Override table rendering |
| `ShouldBreakPage(remainingHeight, elementHeight, context)` | Override page break decision (return `true` to force, `false` to prevent, `null` for default) |

**Indentation:**

```csharp
// Block indentation
layout.AddParagraph("Indented block quote...", new ParagraphOptions
{
    LeftIndent = 36,
    RightIndent = 36
});

// First-line indent (classic book typography)
layout.AddParagraph("The first line is indented...", new ParagraphOptions
{
    FirstLineIndent = 18
});

// Hanging indent (bibliography style)
layout.AddParagraph("Salzman, G. (2026). SimpleTinyPDF...", new ParagraphOptions
{
    LeftIndent = 36,
    FirstLineIndent = -36  // first line back at the margin
});
```

**Keep-together and keep-with-next:**

```csharp
// A heading never gets orphaned at the bottom of a page
layout.AddParagraph("Chapter 3", new ParagraphOptions
{
    Font = PdfFont.HelveticaBold, FontSize = 16,
    KeepWithNext = true  // stays with the paragraph below
});
layout.AddParagraph("Chapter body text...");

// A block quote never splits across pages (best effort — blocks
// taller than one page still split)
layout.AddParagraph("Long block quote...", new ParagraphOptions
{
    KeepTogether = true
});
```

**Horizontal rules:**

```csharp
layout.AddHorizontalRule();  // thin full-width divider

layout.AddHorizontalRule(new HorizontalRuleOptions
{
    Thickness = 2f,
    Color = PdfColor.DarkGray,
    SpaceBefore = 12, SpaceAfter = 12,
    LeftIndent = 100, RightIndent = 100
});
```

| `HorizontalRuleOptions` | Default | Description |
|---|---|---|
| `Thickness` | 0.5 | Line thickness in points |
| `Color` | Black | Rule color |
| `SpaceBefore` / `SpaceAfter` | 6 | Vertical spacing in points |
| `LeftIndent` / `RightIndent` | 0 | Horizontal insets from the content edges |

**Lazy rendering:**

For very large documents, skip the page-counting pass and render in a single streaming pass. Trade-off: `PageContext.TotalPages` and `SectionTotalPages` stay 0, so "Page X of Y" footers are unavailable.

```csharp
var layout = new PdfDocumentLayout { LazyRendering = true };
```

**Debug helpers:**

```csharp
layout.Debug = new DebugOptions
{
    ShowMargins = true,        // dashed lines at margin boundaries
    ShowColumns = true,        // dashed lines between columns
    ShowElementBounds = true,  // rectangle around each element
    OnLayoutWarning = msg => Console.WriteLine(msg)
};
```

`OnLayoutWarning` reports layout problems: oversized images scaled to fit, empty paragraphs, indents wider than the content area, and keep-together blocks too tall to fit on one page.

## Example: Invoice with Company Logo

![Invoice example output](docs/example-invoice.png)

```csharp
using SimpleTinyPDF;

var doc = new PdfDocument { Title = "Invoice #1042" };
var page = doc.AddPage(PageSize.Letter);

// Company logo
var logo = PdfImage.FromFile("company-logo.png");
page.DrawImage(logo, 50, 40, 150, 50);

// Company info (right-aligned)
page.DrawText("Acme Corp", 562, 40, PdfFont.HelveticaBold, 14, alignment: TextAlignment.Right);
page.DrawText("123 Main Street, Springfield", 562, 58, PdfFont.Helvetica, 9,
    PdfColor.DarkGray, TextAlignment.Right);
page.DrawText("Tel: (555) 123-4567", 562, 70, PdfFont.Helvetica, 9,
    PdfColor.DarkGray, TextAlignment.Right);

// Divider
page.DrawLine(50, 100, 562, 100, PdfColor.LightGray, 1f);

// Invoice title
page.DrawText("INVOICE", 50, 120, PdfFont.HelveticaBold, 24, PdfColor.Rgb(51, 51, 51));

// Invoice details
page.DrawText("Invoice #: 1042", 50, 160, PdfFont.Helvetica, 10);
page.DrawText("Date: April 16, 2026", 50, 175, PdfFont.Helvetica, 10);
page.DrawText("Due: May 16, 2026", 50, 190, PdfFont.Helvetica, 10);

// Bill to
page.DrawText("Bill To:", 350, 160, PdfFont.HelveticaBold, 10);
page.DrawText("John Smith", 350, 175, PdfFont.Helvetica, 10);
page.DrawText("456 Oak Avenue", 350, 190, PdfFont.Helvetica, 10);
page.DrawText("Shelbyville, IL 62565", 350, 205, PdfFont.Helvetica, 10);

// Line items table
var table = new PdfTable(240, 80, 80, 112)
    .SetHeaders("Description", "Quantity", "Unit Price", "Amount")
    .AddRow("Web Development Services", "40 hrs", "$75.00", "$3,000.00")
    .AddRow("UI/UX Design", "16 hrs", "$85.00", "$1,360.00")
    .AddRow("Hosting Setup", "1", "$200.00", "$200.00");

table.SetColumnAlignment(1, TextAlignment.Center);
table.SetColumnAlignment(2, TextAlignment.Right);
table.SetColumnAlignment(3, TextAlignment.Right);
table.HeaderBackground = PdfColor.Rgb(51, 51, 51);
table.HeaderTextColor = PdfColor.White;
table.AlternateRowShading = true;

float tableEndY = page.DrawTable(table, 50, 240);

// Totals
float totalsX = 370;
float totalsY = tableEndY + 15;
page.DrawText("Subtotal:", totalsX, totalsY, PdfFont.Helvetica, 10);
page.DrawText("$4,560.00", 562, totalsY, PdfFont.Helvetica, 10, alignment: TextAlignment.Right);
page.DrawText("Tax (8%):", totalsX, totalsY + 18, PdfFont.Helvetica, 10);
page.DrawText("$364.80", 562, totalsY + 18, PdfFont.Helvetica, 10, alignment: TextAlignment.Right);
page.DrawLine(totalsX, totalsY + 34, 562, totalsY + 34, PdfColor.Black, 0.5f);
page.DrawText("Total Due:", totalsX, totalsY + 42, PdfFont.HelveticaBold, 12);
page.DrawText("$4,924.80", 562, totalsY + 42, PdfFont.HelveticaBold, 12, alignment: TextAlignment.Right);

// QR code for online payment
page.DrawBarcode("https://github.com/gregsalzman/simpletinypdf", BarcodeType.QrCode,
    50, totalsY, 80, 80);
page.DrawText("Scan to pay online.", 90, totalsY + 85, PdfFont.Helvetica, 8,
    PdfColor.DarkGray, TextAlignment.Center);

// Footer note
page.DrawText("Payment is due within 30 days. Thank you for your business!",
    306, 720, PdfFont.HelveticaOblique, 9, PdfColor.DarkGray, TextAlignment.Center);

doc.Save("invoice-1042.pdf");
```

## Example: CSV to Table Report

![CSV report example output](docs/example-csv-report.png)

```csharp
using SimpleTinyPDF;

// Suppose "sales-data.csv" contains:
//   Region,Product,Q1,Q2,Q3,Q4
//   North,Widgets,1200,1350,1100,1500
//   South,Widgets,980,1050,1200,1180
//   ...

var doc = new PdfDocument { Title = "Quarterly Sales Report" };
var page = doc.AddPage(PageSize.Letter.Landscape());

// Report header
page.DrawText("Quarterly Sales Report", 50, 40, PdfFont.HelveticaBold, 20);
page.DrawText("Generated: April 16, 2026", 50, 65, PdfFont.Helvetica, 10, PdfColor.DarkGray);
page.DrawLine(50, 85, 742, 85, PdfColor.LightGray, 1f);

// Import CSV directly into a table
var table = PdfTable.FromCsv("sales-data.csv",
    firstRowIsHeader: true,
    columnWidths: new float[] { 100, 120, 90, 90, 90, 90 });

// Style it
table.HeaderBackground = PdfColor.Rgb(0, 51, 102);
table.HeaderTextColor = PdfColor.White;
table.HeaderFont = PdfFont.HelveticaBold;
table.HeaderFontSize = 11;
table.CellFont = PdfFont.Helvetica;
table.CellFontSize = 10;
table.AlternateRowShading = true;
table.AlternateRowColor = PdfColor.Rgb(235, 241, 250);
table.CellPadding = 6;

// Right-align the numeric columns
table.SetColumnAlignment(2, TextAlignment.Right);
table.SetColumnAlignment(3, TextAlignment.Right);
table.SetColumnAlignment(4, TextAlignment.Right);
table.SetColumnAlignment(5, TextAlignment.Right);

// Draw — if the data has many rows, it will automatically
// continue on new pages with the header row repeated.
// Use continuationY to start the table higher on subsequent pages.
page.DrawTable(table, 50, 100, bottomMargin: 50, continuationY: 50);

doc.Save("sales-report.pdf");
```

<!-- ## Version History -->

| Version | Date | Changes |
|---|---|---|
| 0.91 | July 28, 2026 | Add Blazor WebAssembly support: file /ID generation now uses SHA-256 (truncated to the customary 16 bytes) instead of MD5, which is unavailable on the browser runtime. Generating, opening, and merging PDFs verified end-to-end inside Blazor WASM; encryption and digital signatures remain server-only (browser runtime lacks AES/RSA). No API changes. |
| 0.90 | July 16, 2026 | Add editing of existing PDFs: open any unencrypted PDF with `PdfReadDocument.Open`, then merge, split, extract, delete, rearrange, or insert pages via `ImportPage` / `ImportPages` / `RemovePage` / `MovePage` / `PdfDocument.Merge`. Imported pages mix freely with generated pages and preserve full appearance; resources shared between imported pages are deduplicated. Pure C# PDF parser supports classic xref tables, cross-reference streams, object streams, incremental updates, hybrid-reference files, and automatic repair of broken cross-reference data. Merged output can be encrypted and digitally signed. Saving is a full rewrite (source signatures are not carried over). |
| 0.80 | July 12, 2026 | Add right-to-left text support for Arabic and Hebrew. Pure C# Unicode Bidirectional Algorithm (UAX #9, adapted from RichTextKit) resolves mixed RTL/LTR lines with correct digit ordering, bracket pairing, and mirroring; Arabic contextual shaping via Unicode presentation forms handles letter joining, lam-alef ligatures, tashkeel marks, and Persian/Urdu extended letters. Works automatically with logical-order input in single-line, wrapped, and justified text — no API changes, no new dependencies (~35 KB). |
| 0.72 | July 3, 2026 | Add advanced layout controls: paragraph indentation (`LeftIndent`, `RightIndent`, `FirstLineIndent` with hanging indent support), `KeepTogether` / `KeepWithNext` pagination control, horizontal rules (`AddHorizontalRule`), single-pass lazy rendering for large documents (`LazyRendering`), and debug helpers (`DebugOptions`: margin/column guides, element bounds, layout warnings). |
| 0.71 | June 27, 2026 | Add document sections with independent page size, margins, and headers/footers per section (`AddSection`), multi-column text flow (`ColumnCount`, `ColumnGap`, `AddColumnBreak`), page lifecycle events (`IPageEventHandler` / `PageEventType`), and custom renderers (`CustomRenderer` base class) to the layout engine. Section page numbering with optional restart. |
| 0.70 | June 23, 2026 | Add high-level document layout engine (`PdfDocumentLayout`) with automatic content flow, pagination, headers/footers with page numbering, tab stops, and two-pass rendering for accurate total page counts. Supports paragraphs, rich text, images, tables, lists, and manual page breaks in a flowing layout. |
| 0.61 | June 13, 2026 | Add `PdfUnit` static class for converting between PDF points and inches, centimeters, and millimeters. Includes fractional inch support via string parsing ("1-1/8", "1 1/8", "3/4") and explicit whole/numerator/denominator parameters. |
| 0.60 | June 8, 2026 | Add interactive form fields (AcroForms): text fields (single/multi-line, password), checkboxes, radio buttons, dropdowns, listboxes (single/multi-select), and push buttons. Fields are editable in Adobe Acrobat and other PDF viewers. Add PKCS#7 digital signatures with X.509 certificates. Visible and invisible signatures, SHA-256/384/512, optional RFC 3161 timestamping (DigiCert, Sectigo, FreeTSA, or custom URL), intermediate CA certificate chains, and custom signer delegate for HSM/smart card/cloud KMS.|
| 0.58 | June 5, 2026 | Add character spacing, full justification (`TextAlignment.Justify`), and faux bold/italic for any font. Character spacing uses the PDF `Tc` operator. Faux bold uses fill+stroke rendering (`Tr 2`). Faux italic applies a 12° text matrix shear. All features work with both built-in and custom fonts, in single-line, wrapped, and rich text modes. Add spot color (Separation) support with named inks, CMYK display fallback, and tint control. Spot colors work everywhere `PdfColor` is accepted. |
| 0.57 | May 31, 2026 | Add TrueType font subsetting — only used glyphs are embedded, dramatically reducing PDF size for large fonts (especially CJK). Composite glyph dependencies are resolved automatically. CFF/OpenType fonts continue to embed in full. |
| 0.56 | May 29, 2026 | Add AES-128 and AES-256 PDF encryption with user/owner passwords and permission flags. Pure C# implementation using built-in System.Security.Cryptography. |
| 0.55 | May 22, 2026 | Add annotations: text (sticky notes), markup (highlight, underline, strikeout), stamps (14 predefined types), and internal links (GoTo page navigation). |
| 0.54 | May 21, 2026 | Add TrueType (.ttf) and OpenType (.otf) font embedding via `PdfFontSource.FromFile()` / `FromBytes()` / `FromStream()` with full Unicode support (BMP + supplementary planes) using CID composite fonts with Identity-H encoding. Supports CJK, Cyrillic, Greek, CJK Extension B, enclosed alphanumerics, and any character the font contains. UTF-16 surrogate pairs are handled transparently. Embedded ToUnicode CMap enables text selection/copy in PDF viewers. |
| 0.53 | May 14, 2026 | Add barcode and QR code support (Code 128, Code 39, EAN-13, UPC-A, QR Code) with vector rendering and configurable options.  Also did some code reorganization and optimization.  Text methods are now consolidated into a single method with overloads... legacy methods are marked as obsolete. |
| 0.52 | May 2026 | Add hierarchical lists with nesting, text wrapping, multi-page flow, and four list styles (Bullet, Numbered, RomanLower, RomanUpper) |
| 0.51 | May 2026 | Add rotation support for text, images, and shapes |
| 0.50 | April 2026 | Initial beta release |

## Development Notes

This library was written as an exploration of what my increasingly good friend Claude could accomplish.  I continue to be amazed at what generative AI can do.

Sample image assets in the tests project are taken from https://www.publicdomainpictures.net/

### Third-Party Code

SimpleTinyPDF remains zero-dependency, but the right-to-left text support (v0.80) gratefully incorporates adapted open-source code and data. Each adapted source file carries its original license header plus a provenance note:

- **[RichTextKit](https://github.com/toptensoftware/RichTextKit)** by Topten Software (Apache License 2.0) — the Unicode Bidirectional Algorithm (UAX #9) implementation in `SimpleTinyPDF/Text/Bidi/` (`Bidi.cs`, `BidiData.cs`, supporting utility classes, and the `BidiClasses.trie` character-class data resource) was adapted from RichTextKit's `BidiAlgorithm`. RichTextKit's trie reader is itself a port of [unicode-trie](https://github.com/foliojs/unicode-trie) (foliojs), which derives from ICU's UTrie2.
- **[Arabic Support for Unity](https://github.com/Konash/arabic-support-unity)** by Abdullah Konash (MIT License) — served as the reference for the presentation-forms approach to Arabic contextual shaping used in `SimpleTinyPDF/Text/ArabicShaper.cs`.
- **[Unicode Character Database](https://www.unicode.org/ucd/)** © Unicode, Inc. ([Unicode License](https://www.unicode.org/license.txt)) — the Arabic contextual-form tables, lam-alef ligature tables, and bracket-mirroring tables were generated from `UnicodeData.txt` and `BidiMirroring.txt`.
- **[DejaVu Sans](https://dejavu-fonts.github.io/)** (DejaVu Fonts License, based on Bitstream Vera) — used as a test asset in the test project only; not distributed with the library.

Thank you to these projects — full shaping engines like [HarfBuzz](https://github.com/harfbuzz/harfbuzz) remain the gold standard, and the presentation-forms approach here is a deliberately tiny alternative that covers standard Arabic and Hebrew without native binaries.

## License

MIT
