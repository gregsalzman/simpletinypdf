using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SimpleTinyPDF
{
    internal static partial class PdfWriter
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
            var spotColorObjects = new Dictionary<string, PdfObj>();

            // 6. Pages
            var pageObjRefs = new List<string>();
            var pageDicts = new Dictionary<PdfPage, PdfObj>();

            // Imported pages: one ref map per import context; the whole closure of shared
            // objects (fonts, images, content streams) is emitted once per context.
            var importRefMaps = new Dictionary<ImportContext, Dictionary<PdfObjectId, PdfObj>>();

            void EmitImportedPage(PdfPage page)
            {
                var content = page.Imported;
                if (!importRefMaps.TryGetValue(content.Context, out var refMap))
                {
                    refMap = new Dictionary<PdfObjectId, PdfObj>();
                    importRefMaps[content.Context] = refMap;
                    foreach (var kv in content.Context.ClonedObjects)
                    {
                        // Deep-clone when encrypting so the shared closure survives repeated saves
                        var body = doc.Encryption != null ? CosCloner.Clone(kv.Value) : kv.Value;
                        refMap[kv.Key] = AddObj(new ImportedObj { Body = body, RefMap = refMap });
                    }
                }
                // The page body is always cloned: it may be mutated during this save
                // (signature widget injection, encryption) and must stay reusable
                var pageBody = CosCloner.Clone(content.PageDict);
                var pageObj = new ImportedObj { Body = pageBody, RefMap = refMap, ParentTarget = pagesNode };
                AddObj(pageObj);
                pageObjRefs.Add(pageObj.Ref);
                pageDicts[page] = pageObj;
            }

            foreach (var page in doc.Pages)
            {
                if (page.IsImported)
                {
                    if (page.HasGeneratedContent)
                        throw new InvalidOperationException(
                            "Drawing or adding annotations/form fields on an imported page is not supported yet. " +
                            "Draw on a new page instead.");
                    EmitImportedPage(page);
                    continue;
                }

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
                            // Font file stream (subset TTF binary, or full OTF for CFF)
                            var fontStream = new PdfStream();
                            var subsetData = ttf.GetSubsetData(fontSource.Subset);
                            fontStream.Data = subsetData;
                            if (ttf.IsCff)
                                fontStream.Set("Subtype", "/OpenType");
                            else
                                fontStream.Set("Length1", subsetData.Length.ToString());
                            AddObj(fontStream);

                            // FontDescriptor
                            var descriptor = new PdfDict();
                            descriptor.Set("Type", "/FontDescriptor");
                            descriptor.Set("FontName", "/" + ttf.SubsetPostScriptName);
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
                            cidFont.Set("BaseFont", "/" + ttf.SubsetPostScriptName);
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
                            type0Font.Set("BaseFont", "/" + ttf.SubsetPostScriptName);
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

                // Spot color (Separation) objects for this page
                var usedSpotColors = page.GetUsedSpotColors();
                var csRefParts = new List<string>();
                foreach (var kv in usedSpotColors)
                {
                    string csId = kv.Key;
                    var spot = kv.Value;
                    if (!spotColorObjects.TryGetValue(spot.SpotColorName, out var csArrayObj))
                    {
                        var tintFunc = new PdfDict();
                        tintFunc.Set("FunctionType", "2");
                        tintFunc.Set("Domain", "[0 1]");
                        tintFunc.Set("C0", "[0 0 0 0]");
                        tintFunc.Set("C1", $"[{PdfStringHelper.F(spot.C)} {PdfStringHelper.F(spot.M)} {PdfStringHelper.F(spot.Y)} {PdfStringHelper.F(spot.K)}]");
                        tintFunc.Set("N", "1");
                        AddObj(tintFunc);

                        var csArray = new PdfArray();
                        csArray.Value = $"[/Separation /{EscapeSpotName(spot.SpotColorName)} /DeviceCMYK {tintFunc.Ref}]";
                        AddObj(csArray);

                        spotColorObjects[spot.SpotColorName] = csArray;
                        csArrayObj = csArray;
                    }
                    csRefParts.Add($"/{csId} {csArrayObj.Ref}");
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

                if (csRefParts.Count > 0)
                {
                    resources.Append("/ColorSpace << ");
                    foreach (var part in csRefParts)
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

            // Restore document-level rendering keys from imported sources. /OutputIntents
            // (with /S /GTS_PDFX) and the XMP /Metadata carry PDF/X identity — without them
            // Acrobat's default "overprint preview only for PDF/X" turns off overprint
            // simulation and prepress content renders incorrectly. First source wins.
            foreach (var contextEntry in importRefMaps)
            {
                var importContext = contextEntry.Key;
                var contextRefMap = contextEntry.Value;
                if (importContext.OutputIntents != null && !catalog.Entries.Exists(e => e.Key == "OutputIntents"))
                {
                    var sb = new StringBuilder();
                    CosSerializer.AppendValue(sb, importContext.OutputIntents, contextRefMap);
                    catalog.Set("OutputIntents", sb.ToString());
                }
                if (importContext.Metadata != null && !catalog.Entries.Exists(e => e.Key == "Metadata"))
                {
                    var sb = new StringBuilder();
                    CosSerializer.AppendValue(sb, importContext.Metadata, contextRefMap);
                    catalog.Set("Metadata", sb.ToString());
                }
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
                    ((PdfDict)pageDicts[page]).Set("Annots", "[" + string.Join(" ", annotRefs) + "]");
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

            // 8. AcroForm fields
            var fieldRefs = new List<string>();
            var drFontParts = new List<string>();
            var drFontNames = new HashSet<string>();
            PdfDict sigValueDict = null;

            // Create a shared font object for form fields (like iText does)
            // This ensures DR, AP Resources, and Acrobat's regeneration all use the same font
            PdfDict formFontObj = null;

            // Collect form fields across all pages and build widgets + appearances
            foreach (var page in doc.Pages)
            {
                var formFields = page.GetFormFields();
                if (formFields.Count == 0) continue;

                // Group radio buttons by group for parent field creation
                var radioGroups = new Dictionary<PdfRadioGroup, List<(FormField field, PdfDict widget)>>();

                foreach (var field in formFields)
                {
                    if (field.Type == FormFieldType.RadioButton)
                    {
                        // Radio buttons are collected and processed per-group below
                        if (!radioGroups.ContainsKey(field.RadioGroup))
                            radioGroups[field.RadioGroup] = new List<(FormField, PdfDict)>();

                        var rbWidget = CreateFormWidget(field, (PdfDict)pageDicts[page], objects, AddObj,
                            drFontParts, drFontNames, isRadioChild: true, ref formFontObj);
                        radioGroups[field.RadioGroup].Add((field, rbWidget));
                        continue;
                    }

                    var widget = CreateFormWidget(field, (PdfDict)pageDicts[page], objects, AddObj,
                        drFontParts, drFontNames, isRadioChild: false, ref formFontObj);
                    fieldRefs.Add(widget.Ref);

                    // Add widget to page annots
                    AppendAnnotToPage((PdfDict)pageDicts[page], widget);
                }

                // Process radio groups
                foreach (var kv in radioGroups)
                {
                    var group = kv.Key;
                    var radioWidgets = kv.Value;

                    // Create parent field dict for the radio group
                    var parentField = new PdfDict();
                    AddObj(parentField); // assign object number before referencing
                    parentField.Set("FT", "/Btn");
                    parentField.Set("T", PdfStringHelper.Escape(group.Name));
                    int ff = 49152; // Radio (bit 16) + NoToggleToOff (bit 15)
                    if (group.ReadOnly) ff |= 1;
                    if (group.Required) ff |= 2;
                    parentField.Set("Ff", ff.ToString());

                    // Set value to selected radio
                    if (group.SelectedValue != null)
                        parentField.Set("V", "/" + group.SelectedValue);
                    else
                        parentField.Set("V", "/Off");

                    var kidsRefs = new List<string>();
                    foreach (var (field, widget) in radioWidgets)
                    {
                        widget.Set("Parent", parentField.Ref);
                        kidsRefs.Add(widget.Ref);
                        AppendAnnotToPage((PdfDict)pageDicts[page], widget);
                    }
                    parentField.Set("Kids", "[" + string.Join(" ", kidsRefs) + "]");
                    fieldRefs.Add(parentField.Ref);
                }
            }

            // 9. Digital signature
            bool hasSigFields = false;
            if (doc.Signature != null)
            {
                hasSigFields = true;
                var sigOpts = doc.Signature;
                var cert = PdfSigner.ResolveCertificate(sigOpts);

                // Signature value dictionary
                sigValueDict = new PdfDict();
                sigValueDict.Set("Type", "/Sig");
                sigValueDict.Set("Filter", "/Adobe.PPKLite");
                sigValueDict.Set("SubFilter", "/adbe.pkcs7.detached");

                // Placeholder ByteRange and Contents (patched after writing)
                string brPlaceholder = "[0 0000000000 0000000000 0000000000]";
                sigValueDict.Set("ByteRange", brPlaceholder);
                sigValueDict.Set("Contents", "<" + new string('0', PdfSigner.MaxSignatureHexChars) + ">");

                // Metadata
                sigValueDict.Set("M", PdfStringHelper.Escape("D:" + DateTime.Now.ToString("yyyyMMddHHmmss")));
                sigValueDict.Set("Name", PdfStringHelper.Escape(cert.Subject));
                if (sigOpts.Reason != null)
                    sigValueDict.Set("Reason", PdfStringHelper.Escape(sigOpts.Reason));
                if (sigOpts.Location != null)
                    sigValueDict.Set("Location", PdfStringHelper.Escape(sigOpts.Location));
                if (sigOpts.ContactInfo != null)
                    sigValueDict.Set("ContactInfo", PdfStringHelper.Escape(sigOpts.ContactInfo));
                AddObj(sigValueDict);

                // Signature field widget
                var sigField = new PdfDict();
                sigField.Set("Type", "/Annot");
                sigField.Set("Subtype", "/Widget");
                sigField.Set("FT", "/Sig");
                sigField.Set("V", sigValueDict.Ref);
                sigField.Set("T", PdfStringHelper.Escape("Signature1"));
                sigField.Set("F", "132"); // Print + Locked

                // Determine page for the signature
                var sigPage = sigOpts.Page ?? doc.Pages[0];
                if (pageDicts.TryGetValue(sigPage, out var sigPageDict))
                    sigField.Set("P", sigPageDict.Ref);

                if (sigOpts.Page != null)
                {
                    // Visible signature
                    float sx = sigOpts.X;
                    float sy = sigPage.CoordinateOrigin == CoordinateOrigin.TopDown
                        ? sigPage.Height - sigOpts.Y - sigOpts.Height
                        : sigOpts.Y;
                    float sx2 = sx + sigOpts.Width;
                    float sy2 = sy + sigOpts.Height;
                    sigField.Set("Rect", $"[{PdfStringHelper.F(sx)} {PdfStringHelper.F(sy)} {PdfStringHelper.F(sx2)} {PdfStringHelper.F(sy2)}]");

                    // Build appearance Form XObject
                    var apContent = FormAppearanceBuilder.BuildSignatureAppearance(
                        sigOpts.Width, sigOpts.Height,
                        cert.Subject, sigOpts.Reason, sigOpts.Location, DateTime.Now);
                    var apStream = new PdfStream();
                    apStream.Set("Type", "/XObject");
                    apStream.Set("Subtype", "/Form");
                    apStream.Set("BBox", $"[0 0 {PdfStringHelper.F(sigOpts.Width)} {PdfStringHelper.F(sigOpts.Height)}]");
                    // Ensure shared form font exists for signature AP
                    if (formFontObj == null)
                    {
                        formFontObj = new PdfDict();
                        formFontObj.Set("Type", "/Font");
                        formFontObj.Set("Subtype", "/Type1");
                        formFontObj.Set("BaseFont", "/Helvetica");
                        formFontObj.Set("Encoding", "/WinAnsiEncoding");
                        AddObj(formFontObj);
                    }
                    apStream.Set("Resources", $"<< /Font << /F1 {formFontObj.Ref} >> >>");
                    apStream.Data = apContent;
                    AddObj(apStream);
                    sigField.Set("AP", $"<< /N {apStream.Ref} >>");
                }
                else
                {
                    // Invisible signature
                    sigField.Set("Rect", "[0 0 0 0]");
                }

                AddObj(sigField);
                fieldRefs.Add(sigField.Ref);

                if (sigPageDict != null)
                {
                    if (sigPageDict is PdfDict sigPageDictTyped)
                    {
                        AppendAnnotToPage(sigPageDictTyped, sigField);
                    }
                    else if (sigPageDict is ImportedObj importedPage && importedPage.Body is CosDict importedBody)
                    {
                        var annots = importedBody.Get("Annots") as CosArray;
                        if (annots == null)
                        {
                            annots = new CosArray();
                            importedBody.Set("Annots", annots);
                        }
                        annots.Items.Add(new CosWriterRef(sigField));
                    }
                }

                // Ensure Helvetica is in DR for signature appearance
                if (!drFontNames.Contains("Helvetica"))
                {
                    if (formFontObj == null)
                    {
                        formFontObj = new PdfDict();
                        formFontObj.Set("Type", "/Font");
                        formFontObj.Set("Subtype", "/Type1");
                        formFontObj.Set("BaseFont", "/Helvetica");
                        formFontObj.Set("Encoding", "/WinAnsiEncoding");
                        AddObj(formFontObj);
                    }
                    drFontParts.Add($"/F1 {formFontObj.Ref}");
                    drFontNames.Add("Helvetica");
                }
            }

            // Build AcroForm dict if there are any fields
            if (fieldRefs.Count > 0)
            {
                var acroForm = new PdfDict();
                acroForm.Set("Fields", "[" + string.Join(" ", fieldRefs) + "]");
                if (drFontParts.Count > 0)
                    acroForm.Set("DR", "<< /Font << " + string.Join(" ", drFontParts) + " >> >>");
                acroForm.Set("DA", "(/F1 12 Tf)");
                if (hasSigFields)
                    acroForm.Set("SigFlags", "3");
                AddObj(acroForm);
                catalog.Set("AcroForm", acroForm.Ref);
            }

            // Generate file ID early (needed for encryption key derivation).
            // SHA-256 truncated to the customary 16 bytes: the spec suggests MD5 for /ID
            // but any unique bytes are valid, and MD5 is unavailable on some platforms
            // (e.g. Blazor WebAssembly) while SHA-256 works everywhere.
            byte[] idHash;
            using (var sha256 = SHA256.Create())
            {
                var idSource = Encoding.ASCII.GetBytes(
                    DateTime.Now.ToString("o") + objects.Count);
                var fullHash = sha256.ComputeHash(idSource);
                idHash = new byte[16];
                Array.Copy(fullHash, idHash, 16);
            }
            var idHex = BitConverter.ToString(idHash).Replace("-", "");

            // Set up encryption if configured
            PdfDict encryptDict = null;
            if (doc.Encryption != null)
            {
                var encryptor = new PdfEncryptor(doc.Encryption, idHash);
                encryptDict = encryptor.BuildEncryptionDict();
                AddObj(encryptDict);

                // Encrypt all objects (except encryption dict and signature value dict)
                foreach (var obj in objects)
                {
                    if (obj == encryptDict) continue;
                    if (obj == sigValueDict) continue;
                    EncryptObject(obj, encryptor);
                }
            }

            // Write PDF to stream (or MemoryStream if signing)
            Stream writeTarget = output;
            MemoryStream sigBuffer = null;
            if (doc.Signature != null)
            {
                sigBuffer = new MemoryStream();
                writeTarget = sigBuffer;
            }

            var w = new PdfBinaryWriter(writeTarget);

            // Header — version depends on encryption level and features
            string pdfVersion = "1.4";
            if (doc.Encryption != null)
                pdfVersion = doc.Encryption.Level == PdfEncryptionLevel.Aes256 ? "2.0" : "1.6";
            else if (doc.Signature != null || fieldRefs.Count > 0)
                pdfVersion = "1.6";
            // Imported pages may rely on features from a newer PDF version than we generate
            foreach (var context in importRefMaps.Keys)
            {
                if (string.CompareOrdinal(context.PdfVersion, pdfVersion) > 0)
                    pdfVersion = context.PdfVersion;
            }
            w.WriteAscii($"%PDF-{pdfVersion}\n");
            w.WriteBytes(new byte[] { 0x25, 0xE2, 0xE3, 0xCF, 0xD3, 0x0A }); // binary comment

            // Body — track signature placeholder positions
            long contentsValueStart = 0, contentsValueEnd = 0;
            long byteRangeValueStart = 0, byteRangeValueEnd = 0;

            var offsets = new long[objects.Count];
            for (int i = 0; i < objects.Count; i++)
            {
                offsets[i] = w.Position;
                w.WriteAscii($"{objects[i].ObjectNumber} 0 obj\n");

                if (sigValueDict != null && objects[i] == sigValueDict)
                {
                    // Write sig value dict manually to record placeholder positions
                    w.WriteAscii("<<\n");
                    foreach (var kv in sigValueDict.Entries)
                    {
                        w.WriteAscii($"/{kv.Key} ");
                        if (kv.Key == "ByteRange")
                        {
                            byteRangeValueStart = w.Position;
                            w.WriteAscii(kv.Value);
                            byteRangeValueEnd = w.Position;
                        }
                        else if (kv.Key == "Contents")
                        {
                            contentsValueStart = w.Position;
                            w.WriteAscii(kv.Value);
                            contentsValueEnd = w.Position;
                        }
                        else
                        {
                            w.WriteAscii(kv.Value);
                        }
                        w.WriteAscii("\n");
                    }
                    w.WriteAscii(">>\n");
                }
                else
                {
                    objects[i].WriteTo(w);
                }

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
            var trailer = $"<< /Size {objects.Count + 1} /Root {catalog.Ref} /Info {info.Ref} /ID [<{idHex}> <{idHex}>]";
            if (encryptDict != null)
                trailer += $" /Encrypt {encryptDict.Ref}";
            trailer += " >>\n";
            w.WriteAscii("trailer\n");
            w.WriteAscii(trailer);
            w.WriteAscii("startxref\n");
            w.WriteAscii($"{xrefPos}\n");
            w.WriteAscii("%%EOF\n");

            // Apply signature if configured
            if (sigBuffer != null)
            {
                var pdfBytes = sigBuffer.ToArray();
                sigBuffer.Dispose();
                PdfSigner.ApplySignature(pdfBytes,
                    contentsValueStart, contentsValueEnd,
                    byteRangeValueStart, byteRangeValueEnd,
                    doc.Signature);
                output.Write(pdfBytes, 0, pdfBytes.Length);
            }
        }

        private static string EscapeSpotName(string name)
        {
            var sb = new StringBuilder(name.Length * 2);
            foreach (char c in name)
            {
                if (c > 32 && c < 127 && c != '#' && c != '/' && c != '(' && c != ')'
                    && c != '<' && c != '>' && c != '[' && c != ']' && c != '{' && c != '}' && c != '%')
                    sb.Append(c);
                else
                    sb.AppendFormat("#{0:X2}", (int)c);
            }
            return sb.ToString();
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

        private static void EncryptObject(PdfObj obj, PdfEncryptor encryptor)
        {
            if (obj is ImportedObj imported)
            {
                EncryptCosValue(imported.Body, encryptor, obj.ObjectNumber);
                return;
            }

            if (obj is PdfStream stream)
                stream.Data = encryptor.EncryptStream(stream.Data, obj.ObjectNumber, 0);

            if (obj is PdfDict dict)
            {
                for (int i = 0; i < dict.Entries.Count; i++)
                {
                    var entry = dict.Entries[i];
                    if (entry.Value != null && entry.Value.Length >= 2
                        && entry.Value[0] == '(' && entry.Value[entry.Value.Length - 1] == ')')
                    {
                        var raw = DecodePdfLiteralString(entry.Value);
                        var encrypted = encryptor.EncryptString(raw, obj.ObjectNumber, 0);
                        dict.Entries[i] = new KeyValuePair<string, string>(
                            entry.Key, "<" + PdfEncryptor.BytesToHex(encrypted) + ">");
                    }
                }
            }
        }

        /// <summary>
        /// Encrypts every string and stream inside an imported object's value tree.
        /// Stream bytes are encrypted in their stored (filtered) form, as the spec requires.
        /// </summary>
        private static void EncryptCosValue(CosValue value, PdfEncryptor encryptor, int objectNumber)
        {
            switch (value)
            {
                case CosString s:
                    s.Raw = encryptor.EncryptString(s.Raw ?? Array.Empty<byte>(), objectNumber, 0);
                    break;
                case CosStream stream:
                    stream.RawData = encryptor.EncryptStream(stream.RawData ?? Array.Empty<byte>(), objectNumber, 0);
                    foreach (var kv in stream.Entries)
                        EncryptCosValue(kv.Value, encryptor, objectNumber);
                    break;
                case CosDict dict:
                    foreach (var kv in dict.Entries)
                        EncryptCosValue(kv.Value, encryptor, objectNumber);
                    break;
                case CosArray array:
                    foreach (var item in array.Items)
                        EncryptCosValue(item, encryptor, objectNumber);
                    break;
            }
        }

        /// <summary>
        /// Reverses PdfStringHelper.Escape: converts a PDF literal string like "(Hello\\nWorld)"
        /// back into raw bytes by parsing escape sequences.
        /// </summary>
        private static byte[] DecodePdfLiteralString(string pdfString)
        {
            // Strip surrounding parentheses
            var inner = pdfString.Substring(1, pdfString.Length - 2);
            var bytes = new System.Collections.Generic.List<byte>(inner.Length);
            int pos = 0;
            while (pos < inner.Length)
            {
                if (inner[pos] == '\\' && pos + 1 < inner.Length)
                {
                    pos++;
                    char next = inner[pos];
                    if (next == '\\') { bytes.Add((byte)'\\'); pos++; }
                    else if (next == '(') { bytes.Add((byte)'('); pos++; }
                    else if (next == ')') { bytes.Add((byte)')'); pos++; }
                    else if (next == 'n') { bytes.Add((byte)'\n'); pos++; }
                    else if (next == 'r') { bytes.Add((byte)'\r'); pos++; }
                    else if (next == 't') { bytes.Add((byte)'\t'); pos++; }
                    else if (next >= '0' && next <= '7')
                    {
                        // Octal escape: up to 3 digits
                        int octal = next - '0';
                        int digits = 1;
                        while (digits < 3 && pos + 1 < inner.Length
                               && inner[pos + 1] >= '0' && inner[pos + 1] <= '7')
                        {
                            pos++;
                            octal = octal * 8 + (inner[pos] - '0');
                            digits++;
                        }
                        bytes.Add((byte)(octal & 0xFF));
                        pos++;
                    }
                    else
                    {
                        // Unknown escape — just take the character
                        bytes.Add((byte)next);
                        pos++;
                    }
                }
                else
                {
                    bytes.Add((byte)inner[pos]);
                    pos++;
                }
            }
            return bytes.ToArray();
        }
    }
}
