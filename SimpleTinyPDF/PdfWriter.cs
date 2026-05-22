using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SimpleTinyPDF
{
    internal class PdfObj
    {
        internal int ObjectNumber;
        internal string Ref => $"{ObjectNumber} 0 R";
        internal virtual void WriteTo(PdfBinaryWriter w) { }
    }

    internal class PdfDict : PdfObj
    {
        internal readonly List<KeyValuePair<string, string>> Entries = new List<KeyValuePair<string, string>>();

        internal void Set(string key, string value)
        {
            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].Key == key)
                {
                    Entries[i] = new KeyValuePair<string, string>(key, value);
                    return;
                }
            }
            Entries.Add(new KeyValuePair<string, string>(key, value));
        }

        internal override void WriteTo(PdfBinaryWriter w)
        {
            w.WriteAscii("<<\n");
            foreach (var kv in Entries)
                w.WriteAscii($"/{kv.Key} {kv.Value}\n");
            w.WriteAscii(">>\n");
        }
    }

    internal class PdfStream : PdfDict
    {
        internal byte[] Data = Array.Empty<byte>();

        internal override void WriteTo(PdfBinaryWriter w)
        {
            Set("Length", Data.Length.ToString());
            w.WriteAscii("<<\n");
            foreach (var kv in Entries)
                w.WriteAscii($"/{kv.Key} {kv.Value}\n");
            w.WriteAscii(">>\nstream\n");
            w.WriteBytes(Data);
            w.WriteAscii("\nendstream\n");
        }
    }

    internal class PdfBinaryWriter
    {
        private readonly Stream _stream;
        internal long Position => _stream.Position;

        internal PdfBinaryWriter(Stream stream) => _stream = stream;

        internal void WriteAscii(string text)
        {
            var bytes = Encoding.ASCII.GetBytes(text);
            _stream.Write(bytes, 0, bytes.Length);
        }

        internal void WriteBytes(byte[] data) =>
            _stream.Write(data, 0, data.Length);
    }

    internal static class PdfStringHelper
    {
        // Map Unicode characters to WinAnsiEncoding byte values
        internal static readonly Dictionary<char, byte> UnicodeToWinAnsi = new Dictionary<char, byte>
        {
            { '\u20AC', 0x80 }, // Euro sign
            { '\u201A', 0x82 }, // Single low-9 quotation mark
            { '\u0192', 0x83 }, // Latin small letter f with hook
            { '\u201E', 0x84 }, // Double low-9 quotation mark
            { '\u2026', 0x85 }, // Horizontal ellipsis
            { '\u2020', 0x86 }, // Dagger
            { '\u2021', 0x87 }, // Double dagger
            { '\u02C6', 0x88 }, // Modifier letter circumflex accent
            { '\u2030', 0x89 }, // Per mille sign
            { '\u0160', 0x8A }, // Latin capital letter S with caron
            { '\u2039', 0x8B }, // Single left-pointing angle quotation mark
            { '\u0152', 0x8C }, // Latin capital ligature OE
            { '\u017D', 0x8E }, // Latin capital letter Z with caron
            { '\u2018', 0x91 }, // Left single quotation mark
            { '\u2019', 0x92 }, // Right single quotation mark
            { '\u201C', 0x93 }, // Left double quotation mark
            { '\u201D', 0x94 }, // Right double quotation mark
            { '\u2022', 0x95 }, // Bullet
            { '\u2013', 0x96 }, // En dash
            { '\u2014', 0x97 }, // Em dash
            { '\u02DC', 0x98 }, // Small tilde
            { '\u2122', 0x99 }, // Trade mark sign
            { '\u0161', 0x9A }, // Latin small letter s with caron
            { '\u203A', 0x9B }, // Single right-pointing angle quotation mark
            { '\u0153', 0x9C }, // Latin small ligature oe
            { '\u017E', 0x9E }, // Latin small letter z with caron
            { '\u0178', 0x9F }, // Latin capital letter Y with diaeresis
        };

        internal static string Escape(string text) => Escape(text, null);

        internal static string Escape(string text, EncodingExtension ext)
        {
            if (text == null) return "()";
            var sb = new StringBuilder(text.Length + 10);
            sb.Append('(');
            foreach (char c in text)
            {
                if (c == '\\') sb.Append("\\\\");
                else if (c == '(') sb.Append("\\(");
                else if (c == ')') sb.Append("\\)");
                else if (c >= 32 && c <= 126) sb.Append(c);
                else if (c < 256) sb.AppendFormat("\\{0}", Convert.ToString((int)c, 8).PadLeft(3, '0'));
                else if (UnicodeToWinAnsi.TryGetValue(c, out byte winAnsiCode))
                    sb.AppendFormat("\\{0}", Convert.ToString(winAnsiCode, 8).PadLeft(3, '0'));
                else if (ext != null && GlyphMapping.UnicodeToGlyphName.ContainsKey(c))
                {
                    if (!ext.TryEncode(c, out byte extCode))
                        throw new NotSupportedException(
                            $"Character '{c}' (U+{(int)c:X4}) cannot be encoded: the maximum of {ext.Capacity} " +
                            "extended characters per font per page has been reached.");
                    sb.AppendFormat("\\{0}", Convert.ToString(extCode, 8).PadLeft(3, '0'));
                }
                // Characters with no known glyph mapping are silently dropped
            }
            sb.Append(')');
            return sb.ToString();
        }

        internal static string F(float value) =>
            value.ToString("0.####", CultureInfo.InvariantCulture);
    }

    internal static class PdfWriter
    {
        internal static void Write(PdfDocument doc, Stream output)
        {
            var objects = new List<PdfObj>();
            int nextObjNum = 1;

            PdfObj AddObj(PdfObj obj)
            {
                obj.ObjectNumber = nextObjNum++;
                objects.Add(obj);
                return obj;
            }

            // 1. Catalog
            var catalog = new PdfDict();
            catalog.Set("Type", "/Catalog");
            AddObj(catalog);

            // 2. Pages node
            var pagesNode = new PdfDict();
            pagesNode.Set("Type", "/Pages");
            AddObj(pagesNode);

            catalog.Set("Pages", pagesNode.Ref);

            // 3. Info dictionary
            var info = new PdfDict();
            if (!string.IsNullOrEmpty(doc.Title))
                info.Set("Title", PdfStringHelper.Escape(doc.Title));
            if (!string.IsNullOrEmpty(doc.Author))
                info.Set("Author", PdfStringHelper.Escape(doc.Author));
            info.Set("Producer", PdfStringHelper.Escape("SimpleTinyPDF"));
            info.Set("CreationDate", PdfStringHelper.Escape("D:" + DateTime.Now.ToString("yyyyMMddHHmmss")));
            AddObj(info);

            // 4. Image XObjects (deduplicated)
            var imageObjects = new Dictionary<PdfImage, PdfStream>();
            foreach (var image in doc.GetImages())
            {
                var imgStream = new PdfStream();
                imgStream.Set("Type", "/XObject");
                imgStream.Set("Subtype", "/Image");
                imgStream.Set("Width", image.RawPixelWidth.ToString());
                imgStream.Set("Height", image.RawPixelHeight.ToString());
                imgStream.Set("BitsPerComponent", image.BitsPerComponent.ToString());

                if (image.Format == ImageFormat.Jpeg)
                {
                    imgStream.Set("Filter", "/DCTDecode");
                    switch (image.ComponentCount)
                    {
                        case 1: imgStream.Set("ColorSpace", "/DeviceGray"); break;
                        case 4: imgStream.Set("ColorSpace", "/DeviceCMYK"); break;
                        default: imgStream.Set("ColorSpace", "/DeviceRGB"); break;
                    }
                    imgStream.Data = image.GetData();
                }
                else // PNG
                {
                    imgStream.Set("Filter", "/FlateDecode");
                    switch (image.ComponentCount)
                    {
                        case 1: imgStream.Set("ColorSpace", "/DeviceGray"); break;
                        default: imgStream.Set("ColorSpace", "/DeviceRGB"); break;
                    }
                    imgStream.Set("DecodeParms",
                        $"<< /Predictor 15 /Colors {image.ComponentCount} " +
                        $"/BitsPerComponent {image.BitsPerComponent} /Columns {image.RawPixelWidth} >>");
                    imgStream.Data = image.GetData();

                    // Alpha mask (SMask) for PNG images with transparency
                    if (image.AlphaMask != null)
                    {
                        var smask = new PdfStream();
                        smask.Set("Type", "/XObject");
                        smask.Set("Subtype", "/Image");
                        smask.Set("Width", image.RawPixelWidth.ToString());
                        smask.Set("Height", image.RawPixelHeight.ToString());
                        smask.Set("BitsPerComponent", image.BitsPerComponent.ToString());
                        smask.Set("ColorSpace", "/DeviceGray");
                        smask.Set("Filter", "/FlateDecode");
                        smask.Set("DecodeParms",
                            $"<< /Predictor 15 /Colors 1 " +
                            $"/BitsPerComponent {image.BitsPerComponent} /Columns {image.RawPixelWidth} >>");
                        smask.Data = image.AlphaMask;
                        AddObj(smask);
                        imgStream.Set("SMask", smask.Ref);
                    }
                }

                AddObj(imgStream);
                imageObjects[image] = imgStream;
            }

            // 5. ExtGState objects (deduplicated across pages by opacity value)
            var gsObjects = new Dictionary<float, PdfDict>();
            var customFontObjects = new Dictionary<TrueTypeFont, (PdfStream stream, PdfDict descriptor, PdfDict type0Font)>();

            // 6. Pages
            var pageObjRefs = new List<string>();
            var pageDicts = new Dictionary<PdfPage, PdfDict>();

            foreach (var page in doc.Pages)
            {
                // Font objects for this page
                var usedFonts = page.GetUsedFonts();
                var fontRefParts = new List<string>();
                foreach (var kv in usedFonts)
                {
                    var fontSource = kv.Value;

                    if (fontSource.IsBuiltIn)
                    {
                        var builtIn = fontSource.BuiltInFont;
                        var fontObj = new PdfDict();
                        fontObj.Set("Type", "/Font");
                        fontObj.Set("Subtype", "/Type1");
                        fontObj.Set("BaseFont", "/" + PdfFontNames.GetPdfName(builtIn));
                        if (builtIn != PdfFont.Symbol && builtIn != PdfFont.ZapfDingbats)
                        {
                            var ext = page.GetEncodingExtension(fontSource);
                            if (ext != null && ext.HasExtensions)
                                fontObj.Set("Encoding", ext.GetEncodingDict());
                            else
                                fontObj.Set("Encoding", "/WinAnsiEncoding");
                        }
                        AddObj(fontObj);
                        fontRefParts.Add($"/{kv.Key} {fontObj.Ref}");
                    }
                    else
                    {
                        var ttf = fontSource.CustomFont;

                        // Deduplicate: reuse entire Type0 font object tree if already embedded
                        if (!customFontObjects.TryGetValue(ttf, out var cached))
                        {
                            // Font file stream (full TTF/OTF binary)
                            var fontStream = new PdfStream();
                            fontStream.Data = ttf.RawData;
                            if (ttf.IsCff)
                                fontStream.Set("Subtype", "/OpenType");
                            else
                                fontStream.Set("Length1", ttf.RawData.Length.ToString());
                            AddObj(fontStream);

                            // FontDescriptor
                            var descriptor = new PdfDict();
                            descriptor.Set("Type", "/FontDescriptor");
                            descriptor.Set("FontName", "/" + ttf.PostScriptName);
                            descriptor.Set("Flags", ttf.Flags.ToString());
                            descriptor.Set("FontBBox",
                                $"[{ttf.FontBBox[0]} {ttf.FontBBox[1]} {ttf.FontBBox[2]} {ttf.FontBBox[3]}]");
                            descriptor.Set("ItalicAngle", PdfStringHelper.F(ttf.ItalicAngle));
                            descriptor.Set("Ascent",
                                ((int)(ttf.Ascender * 1000L / ttf.UnitsPerEm)).ToString());
                            descriptor.Set("Descent",
                                ((int)(ttf.Descender * 1000L / ttf.UnitsPerEm)).ToString());
                            descriptor.Set("CapHeight",
                                ((int)(ttf.CapHeight * 1000L / ttf.UnitsPerEm)).ToString());
                            descriptor.Set("StemV", ttf.StemV.ToString());
                            descriptor.Set(ttf.IsCff ? "FontFile3" : "FontFile2", fontStream.Ref);
                            AddObj(descriptor);

                            // ToUnicode CMap stream
                            var glyphToUnicode = ttf.GetUsedGlyphToUnicodeMap();
                            var toUnicodeCMap = new PdfStream();
                            toUnicodeCMap.Data = CidFontHelper.BuildToUnicodeCMap(glyphToUnicode);
                            AddObj(toUnicodeCMap);

                            // CIDFont dictionary
                            var cidFont = new PdfDict();
                            cidFont.Set("Type", "/Font");
                            cidFont.Set("Subtype", ttf.IsCff ? "/CIDFontType0" : "/CIDFontType2");
                            cidFont.Set("BaseFont", "/" + ttf.PostScriptName);
                            cidFont.Set("CIDSystemInfo",
                                "<< /Registry (Adobe) /Ordering (Identity) /Supplement 0 >>");
                            cidFont.Set("FontDescriptor", descriptor.Ref);
                            cidFont.Set("DW", "0");
                            var usedGlyphIds = ttf.GetUsedGlyphIds();
                            cidFont.Set("W", CidFontHelper.BuildWidthArray(ttf, usedGlyphIds));
                            if (!ttf.IsCff)
                                cidFont.Set("CIDToGIDMap", "/Identity");
                            AddObj(cidFont);

                            // Type0 (top-level) font dictionary
                            var type0Font = new PdfDict();
                            type0Font.Set("Type", "/Font");
                            type0Font.Set("Subtype", "/Type0");
                            type0Font.Set("BaseFont", "/" + ttf.PostScriptName);
                            type0Font.Set("Encoding", "/Identity-H");
                            type0Font.Set("DescendantFonts", "[" + cidFont.Ref + "]");
                            type0Font.Set("ToUnicode", toUnicodeCMap.Ref);
                            AddObj(type0Font);

                            cached = (fontStream, descriptor, type0Font);
                            customFontObjects[ttf] = cached;
                        }

                        fontRefParts.Add($"/{kv.Key} {cached.type0Font.Ref}");
                    }
                }

                // Graphics state objects for this page
                var usedGs = page.GetUsedGraphicsStates();
                var gsRefParts = new List<string>();
                foreach (var kv in usedGs)
                {
                    float opacity = kv.Key;
                    string gsId = kv.Value;
                    if (!gsObjects.TryGetValue(opacity, out var gsObj))
                    {
                        gsObj = new PdfDict();
                        gsObj.Set("Type", "/ExtGState");
                        gsObj.Set("ca", PdfStringHelper.F(opacity));
                        gsObj.Set("CA", PdfStringHelper.F(opacity));
                        AddObj(gsObj);
                        gsObjects[opacity] = gsObj;
                    }
                    gsRefParts.Add($"/{gsId} {gsObj.Ref}");
                }

                // Content stream
                var contentStream = new PdfStream();
                contentStream.Data = Encoding.ASCII.GetBytes(page.GetContentStream());
                AddObj(contentStream);

                // Build resources dictionary inline
                var resources = new StringBuilder("<< ");
                if (fontRefParts.Count > 0)
                {
                    resources.Append("/Font << ");
                    foreach (var part in fontRefParts)
                        resources.Append(part).Append(' ');
                    resources.Append(">> ");
                }

                var usedImages = page.GetUsedImages();
                if (usedImages.Count > 0)
                {
                    resources.Append("/XObject << ");
                    foreach (var kv in usedImages)
                    {
                        if (imageObjects.TryGetValue(kv.Value, out var imgObj))
                            resources.Append($"/{kv.Key} {imgObj.Ref} ");
                    }
                    resources.Append(">> ");
                }

                if (gsRefParts.Count > 0)
                {
                    resources.Append("/ExtGState << ");
                    foreach (var part in gsRefParts)
                        resources.Append(part).Append(' ');
                    resources.Append(">> ");
                }
                resources.Append(">>");

                // Page dictionary (created before annotations so all page refs are available)
                var pageDict = new PdfDict();
                pageDict.Set("Type", "/Page");
                pageDict.Set("Parent", pagesNode.Ref);
                pageDict.Set("MediaBox", $"[0 0 {PdfStringHelper.F(page.Width)} {PdfStringHelper.F(page.Height)}]");
                pageDict.Set("Contents", contentStream.Ref);
                pageDict.Set("Resources", resources.ToString());
                AddObj(pageDict);
                pageObjRefs.Add(pageDict.Ref);
                pageDicts[page] = pageDict;
            }

            // Second pass: annotations (after all page dicts exist for internal links)
            foreach (var page in doc.Pages)
            {
                var annotations = page.GetAnnotations();
                if (annotations.Count > 0)
                {
                    var annotRefs = new List<string>();
                    foreach (var annot in annotations)
                    {
                        var annotDict = new PdfDict();
                        annotDict.Set("Type", "/Annot");
                        var rect = $"[{PdfStringHelper.F(annot.X0)} {PdfStringHelper.F(annot.Y0)} {PdfStringHelper.F(annot.X1)} {PdfStringHelper.F(annot.Y1)}]";
                        annotDict.Set("Rect", rect);

                        switch (annot.Kind)
                        {
                            case AnnotationKind.Link:
                                annotDict.Set("Subtype", "/Link");
                                annotDict.Set("Border", "[0 0 0]");
                                annotDict.Set("A", $"<< /S /URI /URI ({EscapeUri(annot.Url)}) >>");
                                break;

                            case AnnotationKind.Text:
                                annotDict.Set("Subtype", "/Text");
                                annotDict.Set("Contents", PdfStringHelper.Escape(annot.Contents));
                                if (annot.Title != null)
                                    annotDict.Set("T", PdfStringHelper.Escape(annot.Title));
                                annotDict.Set("Name", "/" + GetTextAnnotationIconName(annot.Icon));
                                if (annot.Color.HasValue)
                                    annotDict.Set("C", FormatColorArray(annot.Color.Value));
                                annotDict.Set("Open", annot.Open ? "true" : "false");
                                annotDict.Set("F", "4");
                                break;

                            case AnnotationKind.Markup:
                                annotDict.Set("Subtype", "/" + GetMarkupSubtype(annot.MarkupType));
                                if (annot.QuadPoints != null)
                                {
                                    var qp = annot.QuadPoints;
                                    annotDict.Set("QuadPoints",
                                        $"[{PdfStringHelper.F(qp[0])} {PdfStringHelper.F(qp[1])} {PdfStringHelper.F(qp[2])} {PdfStringHelper.F(qp[3])} " +
                                        $"{PdfStringHelper.F(qp[4])} {PdfStringHelper.F(qp[5])} {PdfStringHelper.F(qp[6])} {PdfStringHelper.F(qp[7])}]");
                                }
                                if (annot.Color.HasValue)
                                    annotDict.Set("C", FormatColorArray(annot.Color.Value));
                                if (annot.Contents != null)
                                    annotDict.Set("Contents", PdfStringHelper.Escape(annot.Contents));
                                if (annot.Title != null)
                                    annotDict.Set("T", PdfStringHelper.Escape(annot.Title));
                                break;

                            case AnnotationKind.Stamp:
                                annotDict.Set("Subtype", "/Stamp");
                                annotDict.Set("Name", "/" + GetStampName(annot.Stamp));
                                if (annot.Contents != null)
                                    annotDict.Set("Contents", PdfStringHelper.Escape(annot.Contents));
                                if (annot.Title != null)
                                    annotDict.Set("T", PdfStringHelper.Escape(annot.Title));
                                if (annot.Color.HasValue)
                                    annotDict.Set("C", FormatColorArray(annot.Color.Value));
                                break;

                            case AnnotationKind.InternalLink:
                                annotDict.Set("Subtype", "/Link");
                                annotDict.Set("Border", "[0 0 0]");
                                if (annot.TargetPage != null && pageDicts.TryGetValue(annot.TargetPage, out var targetPageDict))
                                {
                                    if (annot.TargetY.HasValue)
                                    {
                                        float pdfTargetY = annot.TargetPage.CoordinateOrigin == CoordinateOrigin.TopDown
                                            ? annot.TargetPage.Height - annot.TargetY.Value
                                            : annot.TargetY.Value;
                                        annotDict.Set("Dest",
                                            $"[{targetPageDict.Ref} /XYZ 0 {PdfStringHelper.F(pdfTargetY)} 0]");
                                    }
                                    else
                                    {
                                        annotDict.Set("Dest", $"[{targetPageDict.Ref} /Fit]");
                                    }
                                }
                                break;
                        }

                        AddObj(annotDict);
                        annotRefs.Add(annotDict.Ref);
                    }
                    pageDicts[page].Set("Annots", "[" + string.Join(" ", annotRefs) + "]");
                }
            }

            // Finalize pages node
            pagesNode.Set("Kids", "[" + string.Join(" ", pageObjRefs) + "]");
            pagesNode.Set("Count", doc.Pages.Count.ToString());

            // 7. Bookmarks (Outlines)
            var bookmarks = doc.GetBookmarks();
            if (bookmarks.Count > 0)
            {
                var outlineRoot = new PdfDict();
                outlineRoot.Set("Type", "/Outlines");
                AddObj(outlineRoot);

                (PdfDict first, PdfDict last, int count) CreateOutlineItems(
                    IReadOnlyList<PdfBookmark> items, PdfObj parent)
                {
                    var dicts = new List<PdfDict>(items.Count);
                    int totalCount = 0;

                    for (int i = 0; i < items.Count; i++)
                    {
                        var bm = items[i];
                        var dict = new PdfDict();
                        AddObj(dict);
                        dicts.Add(dict);

                        dict.Set("Title", PdfStringHelper.Escape(bm.Title));
                        dict.Set("Parent", parent.Ref);

                        if (pageDicts.TryGetValue(bm.Page, out var pageRef))
                        {
                            if (bm.Y.HasValue)
                            {
                                float pdfY = bm.Page.CoordinateOrigin == CoordinateOrigin.TopDown
                                    ? bm.Page.Height - bm.Y.Value
                                    : bm.Y.Value;
                                dict.Set("Dest",
                                    $"[{pageRef.Ref} /XYZ 0 {PdfStringHelper.F(pdfY)} 0]");
                            }
                            else
                            {
                                dict.Set("Dest", $"[{pageRef.Ref} /Fit]");
                            }
                        }

                        if (bm.Children.Count > 0)
                        {
                            var (childFirst, childLast, childCount) =
                                CreateOutlineItems(bm.Children, dict);
                            dict.Set("First", childFirst.Ref);
                            dict.Set("Last", childLast.Ref);
                            dict.Set("Count", childCount.ToString());
                            totalCount += childCount;
                        }

                        totalCount++;
                    }

                    for (int i = 0; i < dicts.Count; i++)
                    {
                        if (i > 0)
                            dicts[i].Set("Prev", dicts[i - 1].Ref);
                        if (i < dicts.Count - 1)
                            dicts[i].Set("Next", dicts[i + 1].Ref);
                    }

                    return (dicts[0], dicts[dicts.Count - 1], totalCount);
                }

                var (first, last, count) = CreateOutlineItems(bookmarks, outlineRoot);
                outlineRoot.Set("First", first.Ref);
                outlineRoot.Set("Last", last.Ref);
                outlineRoot.Set("Count", count.ToString());

                catalog.Set("Outlines", outlineRoot.Ref);
            }

            // Write PDF to stream
            var w = new PdfBinaryWriter(output);

            // Header
            w.WriteAscii("%PDF-1.4\n");
            w.WriteBytes(new byte[] { 0x25, 0xE2, 0xE3, 0xCF, 0xD3, 0x0A }); // binary comment

            // Body
            var offsets = new long[objects.Count];
            for (int i = 0; i < objects.Count; i++)
            {
                offsets[i] = w.Position;
                w.WriteAscii($"{objects[i].ObjectNumber} 0 obj\n");
                objects[i].WriteTo(w);
                w.WriteAscii("endobj\n");
            }

            // Cross-reference table
            long xrefPos = w.Position;
            w.WriteAscii("xref\n");
            w.WriteAscii($"0 {objects.Count + 1}\n");
            w.WriteAscii("0000000000 65535 f \n");
            for (int i = 0; i < objects.Count; i++)
                w.WriteAscii($"{offsets[i]:D10} 00000 n \n");

            // Trailer
            // Generate a file ID based on creation time and content size to satisfy PDF spec
            byte[] idHash;
            using (var md5 = MD5.Create())
            {
                var idSource = Encoding.ASCII.GetBytes(
                    DateTime.Now.ToString("o") + objects.Count + xrefPos);
                idHash = md5.ComputeHash(idSource);
            }
            var idHex = BitConverter.ToString(idHash).Replace("-", "");
            w.WriteAscii("trailer\n");
            w.WriteAscii($"<< /Size {objects.Count + 1} /Root {catalog.Ref} /Info {info.Ref} /ID [<{idHex}> <{idHex}>] >>\n");
            w.WriteAscii("startxref\n");
            w.WriteAscii($"{xrefPos}\n");
            w.WriteAscii("%%EOF\n");
        }

        private static string EscapeUri(string uri)
        {
            if (uri == null) return "";
            return uri.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        }

        private static string GetTextAnnotationIconName(TextAnnotationIcon icon)
        {
            switch (icon)
            {
                case TextAnnotationIcon.Comment: return "Comment";
                case TextAnnotationIcon.Note: return "Note";
                case TextAnnotationIcon.Key: return "Key";
                case TextAnnotationIcon.Help: return "Help";
                case TextAnnotationIcon.NewParagraph: return "NewParagraph";
                case TextAnnotationIcon.Paragraph: return "Paragraph";
                case TextAnnotationIcon.Insert: return "Insert";
                default: return "Comment";
            }
        }

        private static string GetMarkupSubtype(MarkupAnnotationType type)
        {
            switch (type)
            {
                case MarkupAnnotationType.Highlight: return "Highlight";
                case MarkupAnnotationType.Underline: return "Underline";
                case MarkupAnnotationType.StrikeOut: return "StrikeOut";
                default: return "Highlight";
            }
        }

        private static string GetStampName(StampType stamp)
        {
            switch (stamp)
            {
                case StampType.Approved: return "Approved";
                case StampType.Experimental: return "Experimental";
                case StampType.NotApproved: return "NotApproved";
                case StampType.AsIs: return "AsIs";
                case StampType.Expired: return "Expired";
                case StampType.NotForPublicRelease: return "NotForPublicRelease";
                case StampType.Confidential: return "Confidential";
                case StampType.Final: return "Final";
                case StampType.Sold: return "Sold";
                case StampType.Departmental: return "Departmental";
                case StampType.ForComment: return "ForComment";
                case StampType.TopSecret: return "TopSecret";
                case StampType.Draft: return "Draft";
                case StampType.ForPublicRelease: return "ForPublicRelease";
                default: return "Draft";
            }
        }

        private static string FormatColorArray(PdfColor color)
        {
            if (color.IsCmyk)
            {
                float r = (1f - color.C) * (1f - color.K);
                float g = (1f - color.M) * (1f - color.K);
                float b = (1f - color.Y) * (1f - color.K);
                return $"[{PdfStringHelper.F(r)} {PdfStringHelper.F(g)} {PdfStringHelper.F(b)}]";
            }
            return $"[{PdfStringHelper.F(color.R)} {PdfStringHelper.F(color.G)} {PdfStringHelper.F(color.B)}]";
        }
    }
}
