using System;
using System.Collections.Generic;

namespace SimpleTinyPDF
{
    /// <summary>
    /// The shared import state between one source <see cref="PdfReadDocument"/> and one
    /// destination <see cref="PdfDocument"/>. Objects referenced by several imported pages
    /// (fonts, images, shared resources) are cloned once and reused, keyed by their id in
    /// the source file.
    /// </summary>
    internal sealed class ImportContext
    {
        internal readonly PdfReadDocument Source;
        internal readonly Dictionary<PdfObjectId, CosValue> ClonedObjects = new Dictionary<PdfObjectId, CosValue>();
        internal readonly string PdfVersion;

        /// <summary>The source catalog's /OutputIntents (cloned), or null. Restoring this on the
        /// destination catalog keeps PDF/X identity, which viewers like Acrobat use to decide
        /// whether to simulate overprint automatically.</summary>
        internal CosValue OutputIntents;

        /// <summary>The source catalog's XMP /Metadata reference (cloned), or null.</summary>
        internal CosValue Metadata;

        internal ImportContext(PdfReadDocument source)
        {
            Source = source;
            PdfVersion = source.PdfVersion;
        }
    }

    /// <summary>The imported payload carried by a <see cref="PdfPage"/> that wraps a page from an existing PDF.</summary>
    internal sealed class ImportedPageContent
    {
        internal ImportContext Context;
        internal CosDict PageDict;
        internal PdfObjectId SourcePageId;

        /// <summary>Normalized MediaBox corners (x0 &lt;= x1, y0 &lt;= y1) in the source page's user space.</summary>
        internal float BoxX0, BoxY0, BoxX1, BoxY1;

        /// <summary>The page's /Rotate value normalized to 0, 90, 180, or 270.</summary>
        internal int Rotate;
    }

    /// <summary>
    /// Affine transform mapping stamped (viewed-page) coordinates into an imported page's
    /// unrotated PDF user space. Compensates for a non-zero MediaBox origin and the page
    /// /Rotate value so stamped content appears upright at the expected position when the
    /// viewer applies the rotation.
    /// </summary>
    internal struct StampTransform
    {
        internal float A, B, C, D, E, F;

        internal bool IsIdentity => A == 1 && B == 0 && C == 0 && D == 1 && E == 0 && F == 0;

        internal static readonly StampTransform Identity = new StampTransform { A = 1, D = 1 };

        internal static StampTransform For(ImportedPageContent content)
        {
            float x0 = content.BoxX0, y0 = content.BoxY0, x1 = content.BoxX1, y1 = content.BoxY1;
            switch (content.Rotate)
            {
                // /Rotate rotates the page clockwise for display; content is pre-rotated the
                // other way so it reads upright. Derived by mapping the viewed page's corners
                // onto the MediaBox rectangle.
                case 90: return new StampTransform { B = 1, C = -1, E = x1, F = y0 };
                case 180: return new StampTransform { A = -1, D = -1, E = x1, F = y1 };
                case 270: return new StampTransform { B = -1, C = 1, E = x0, F = y1 };
                default: return new StampTransform { A = 1, D = 1, E = x0, F = y0 };
            }
        }

        internal void Apply(float x, float y, out float tx, out float ty)
        {
            tx = A * x + C * y + E;
            ty = B * x + D * y + F;
        }
    }

    /// <summary>
    /// Copies a page out of a <see cref="PdfReadDocument"/>: flattens inherited attributes,
    /// sanitizes document-level references, and deep-copies the transitive closure of
    /// everything the page needs (content streams, resources, kept annotations).
    /// </summary>
    internal static class PageImporter
    {
        /// <summary>
        /// Captures document-level rendering keys from the source catalog into the context
        /// (called once when the context is created). /OutputIntents and the XMP /Metadata
        /// carry the PDF/X identity that controls overprint simulation in viewers.
        /// </summary>
        internal static void CaptureDocumentDefaults(ImportContext context)
        {
            var catalog = context.Source.Catalog;
            if (catalog == null)
                return;
            var outputIntents = catalog.Get("OutputIntents");
            if (outputIntents != null && !(outputIntents is CosNull))
                context.OutputIntents = CloneValue(context, outputIntents);
            var metadata = catalog.Get("Metadata");
            if (metadata != null && !(metadata is CosNull))
                context.Metadata = CloneValue(context, metadata);
        }

        internal static ImportedPageContent Import(ImportContext context, int pageNumber)
        {
            var source = context.Source;
            var record = source.GetPageRecord(pageNumber);

            var pageDict = new CosDict();
            foreach (var kv in record.Dict.Entries)
            {
                switch (kv.Key)
                {
                    case "Type":         // re-added below
                    case "Parent":       // the writer re-parents onto its own /Pages node
                    case "StructParents":// structure tree is not imported
                    case "B":            // article beads reference the whole document
                    case "AA":           // page actions may reference other pages
                    case "Annots":       // sanitized separately below
                    case "MediaBox":     // flattened (inherited) values re-added below
                    case "CropBox":
                    case "Resources":
                    case "Rotate":
                        continue;
                    default:
                        pageDict.Set(kv.Key, CloneValue(context, kv.Value));
                        break;
                }
            }

            pageDict.Set("Type", new CosName("Page"));
            if (record.MediaBox != null)
            {
                pageDict.Set("MediaBox", CloneValue(context, record.MediaBox));
            }
            else
            {
                var fallback = new CosArray();
                fallback.Items.Add(new CosInteger(0));
                fallback.Items.Add(new CosInteger(0));
                fallback.Items.Add(new CosInteger((long)PageSize.Letter.Width));
                fallback.Items.Add(new CosInteger((long)PageSize.Letter.Height));
                pageDict.Set("MediaBox", fallback);
            }
            if (record.CropBox != null)
                pageDict.Set("CropBox", CloneValue(context, record.CropBox));
            if (record.Resources != null)
                pageDict.Set("Resources", CloneValue(context, record.Resources));
            if (record.Rotate.HasValue && record.Rotate.Value != 0)
                pageDict.Set("Rotate", new CosInteger(record.Rotate.Value));

            var annots = ImportAnnotations(context, record.Dict.Get("Annots"));
            if (annots != null && annots.Items.Count > 0)
                pageDict.Set("Annots", annots);

            var content = new ImportedPageContent
            {
                Context = context,
                PageDict = pageDict,
                SourcePageId = record.Id,
            };
            ReadBoxAndRotation(source, record, content);
            return content;
        }

        /// <summary>
        /// Records the page's normalized MediaBox corners and /Rotate value on the payload
        /// so stamped content and annotations can be positioned in viewed-page coordinates.
        /// Falls back to Letter size when the box is absent or degenerate, matching the
        /// fallback MediaBox written by <see cref="Import"/>.
        /// </summary>
        private static void ReadBoxAndRotation(PdfReadDocument source, ReadPageRecord record,
            ImportedPageContent content)
        {
            content.BoxX0 = 0;
            content.BoxY0 = 0;
            content.BoxX1 = PageSize.Letter.Width;
            content.BoxY1 = PageSize.Letter.Height;
            var box = record.MediaBox;
            if (box != null && box.Items.Count >= 4)
            {
                float x0 = ToFloat(source.Resolve(box.Items[0]));
                float y0 = ToFloat(source.Resolve(box.Items[1]));
                float x1 = ToFloat(source.Resolve(box.Items[2]));
                float y1 = ToFloat(source.Resolve(box.Items[3]));
                if (x0 != x1 && y0 != y1)
                {
                    content.BoxX0 = Math.Min(x0, x1);
                    content.BoxY0 = Math.Min(y0, y1);
                    content.BoxX1 = Math.Max(x0, x1);
                    content.BoxY1 = Math.Max(y0, y1);
                }
            }

            long rotate = record.Rotate ?? 0;
            rotate = (rotate % 360 + 360) % 360;
            content.Rotate = rotate == 90 || rotate == 180 || rotate == 270 ? (int)rotate : 0;
        }

        private static float ToFloat(CosValue value)
        {
            if (value is CosInteger i) return i.Value;
            if (value is CosReal r) return (float)r.Value;
            return 0;
        }

        /// <summary>
        /// Clones a value, registering every referenced indirect object into the context's
        /// closure. References keep their source-file ids; the writer maps them to fresh
        /// object numbers at save time.
        /// </summary>
        private static CosValue CloneValue(ImportContext context, CosValue value)
        {
            switch (value)
            {
                case CosReference reference:
                    EnsureObjectCloned(context, reference.Id);
                    return new CosReference(reference.Id.Number, reference.Id.Generation);
                case CosStream stream:
                    var streamClone = new CosStream { RawData = stream.RawData };
                    foreach (var kv in stream.Entries)
                        streamClone.Entries.Add(new KeyValuePair<string, CosValue>(kv.Key, CloneValue(context, kv.Value)));
                    return streamClone;
                case CosDict dict:
                    var dictClone = new CosDict();
                    foreach (var kv in dict.Entries)
                        dictClone.Entries.Add(new KeyValuePair<string, CosValue>(kv.Key, CloneValue(context, kv.Value)));
                    return dictClone;
                case CosArray array:
                    var arrayClone = new CosArray();
                    foreach (var item in array.Items)
                        arrayClone.Items.Add(CloneValue(context, item));
                    return arrayClone;
                case CosString s:
                    return new CosString(s.Raw);
                default:
                    return value ?? CosNull.Instance; // scalars are immutable
            }
        }

        private static void EnsureObjectCloned(ImportContext context, PdfObjectId id)
        {
            if (context.ClonedObjects.ContainsKey(id))
                return;

            var value = context.Source.GetObject(id);

            // Structural guard: never crawl into the source page tree. A stray reference to
            // a page (or the /Pages node) would otherwise drag the entire document into the
            // closure via /Parent and /Kids. Such references serialize as null instead.
            if (value is CosDict guard && !(value is CosStream))
            {
                string type = guard.GetName("Type");
                if (type == "Page" || type == "Pages")
                {
                    context.ClonedObjects[id] = CosNull.Instance;
                    return;
                }
            }

            // Register a shell before recursing so reference cycles terminate
            switch (value)
            {
                case CosStream stream:
                    var streamShell = new CosStream { RawData = stream.RawData };
                    context.ClonedObjects[id] = streamShell;
                    foreach (var kv in stream.Entries)
                        streamShell.Entries.Add(new KeyValuePair<string, CosValue>(kv.Key, CloneValue(context, kv.Value)));
                    break;
                case CosDict dict:
                    var dictShell = new CosDict();
                    context.ClonedObjects[id] = dictShell;
                    foreach (var kv in dict.Entries)
                        dictShell.Entries.Add(new KeyValuePair<string, CosValue>(kv.Key, CloneValue(context, kv.Value)));
                    break;
                case CosArray array:
                    var arrayShell = new CosArray();
                    context.ClonedObjects[id] = arrayShell;
                    foreach (var item in array.Items)
                        arrayShell.Items.Add(CloneValue(context, item));
                    break;
                case CosString s:
                    context.ClonedObjects[id] = new CosString(s.Raw);
                    break;
                default:
                    context.ClonedObjects[id] = value ?? CosNull.Instance;
                    break;
            }
        }

        /// <summary>
        /// Sanitizes and imports a page's annotations. Form-field widgets are dropped (their
        /// AcroForm is not imported); on kept annotations, keys that point at other pages
        /// (/Dest, GoTo actions, /P, /StructParent) are removed before the closure copy so
        /// they cannot drag unrelated pages along.
        /// </summary>
        private static CosArray ImportAnnotations(ImportContext context, CosValue annotsValue)
        {
            var sourceArray = context.Source.Resolve(annotsValue) as CosArray;
            if (sourceArray == null)
                return null;

            var result = new CosArray();
            foreach (var item in sourceArray.Items)
            {
                var imported = ImportAnnotation(context, item);
                if (imported != null)
                    result.Items.Add(imported);
            }
            return result;
        }

        private static CosValue ImportAnnotation(ImportContext context, CosValue item)
        {
            var source = context.Source;
            var srcDict = source.Resolve(item) as CosDict;
            if (srcDict == null || srcDict is CosStream)
                return null;
            if (srcDict.GetName("Subtype") == "Widget")
                return null;

            if (item is CosReference reference)
            {
                if (context.ClonedObjects.ContainsKey(reference.Id))
                    return new CosReference(reference.Id.Number, reference.Id.Generation);
                var shell = new CosDict();
                context.ClonedObjects[reference.Id] = shell;
                FillSanitizedAnnotation(context, shell, srcDict);
                return new CosReference(reference.Id.Number, reference.Id.Generation);
            }

            var inline = new CosDict();
            FillSanitizedAnnotation(context, inline, srcDict);
            return inline;
        }

        private static void FillSanitizedAnnotation(ImportContext context, CosDict target, CosDict srcDict)
        {
            foreach (var kv in srcDict.Entries)
            {
                switch (kv.Key)
                {
                    case "Dest":         // in-document destination -> references other pages
                    case "P":            // back-reference to the source page object
                    case "StructParent": // structure tree is not imported
                        continue;
                    case "A":
                        var action = context.Source.Resolve(kv.Value) as CosDict;
                        if (action != null && action.GetName("S") == "GoTo")
                            continue; // in-document navigation
                        target.Set(kv.Key, CloneValue(context, kv.Value));
                        break;
                    default:
                        target.Set(kv.Key, CloneValue(context, kv.Value));
                        break;
                }
            }
        }
    }
}
