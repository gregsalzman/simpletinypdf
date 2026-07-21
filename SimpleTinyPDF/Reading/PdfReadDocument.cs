using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SimpleTinyPDF
{
    /// <summary>
    /// A parsed page of an existing PDF, with inheritable attributes
    /// (/MediaBox, /CropBox, /Resources, /Rotate) already flattened.
    /// </summary>
    internal sealed class ReadPageRecord
    {
        internal PdfObjectId Id;
        internal CosDict Dict;
        internal CosArray MediaBox;
        internal CosArray CropBox;
        internal CosValue Resources;
        internal long? Rotate;
    }

    /// <summary>
    /// An existing PDF file opened for reading. Pages can be imported into a
    /// <see cref="PdfDocument"/> with <see cref="PdfDocument.ImportPage(PdfReadDocument, int)"/>;
    /// the destination document is then saved as a completely new file.
    /// Note: saving is a full rewrite — any digital signatures present in this
    /// source file are not carried over.
    /// </summary>
    public sealed class PdfReadDocument : IDisposable
    {
        private byte[] _data;
        private PdfParser _parser;
        private XrefTable _xref;
        private readonly Dictionary<PdfObjectId, CosValue> _objectCache = new Dictionary<PdfObjectId, CosValue>();
        private readonly Dictionary<int, Dictionary<int, CosValue>> _objStmCache = new Dictionary<int, Dictionary<int, CosValue>>();
        private readonly HashSet<PdfObjectId> _parsing = new HashSet<PdfObjectId>();
        private List<ReadPageRecord> _pages;
        private CosDict _info;
        private bool _disposed;

        /// <summary>The document catalog. Used by the importer to carry over document-level
        /// rendering keys such as /OutputIntents.</summary>
        internal CosDict Catalog { get; private set; }

        /// <summary>The PDF version of the source file (e.g. "1.7").</summary>
        internal string PdfVersion { get; private set; } = "1.4";

        private PdfReadDocument() { }

        /// <summary>Opens an existing PDF file for reading.</summary>
        /// <param name="filePath">Path of the PDF file.</param>
        /// <exception cref="PdfParseException">The file is not a parseable PDF.</exception>
        /// <exception cref="NotSupportedException">The file is encrypted (password protected).</exception>
        public static PdfReadDocument Open(string filePath)
        {
            if (filePath == null) throw new ArgumentNullException(nameof(filePath));
            return Open(File.ReadAllBytes(filePath));
        }

        /// <summary>Opens an existing PDF from a stream. The stream is read fully into memory.</summary>
        public static PdfReadDocument Open(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                return Open(ms.ToArray());
            }
        }

        /// <summary>Opens an existing PDF from a byte array.</summary>
        public static PdfReadDocument Open(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            var doc = new PdfReadDocument();
            doc.Load(data);
            return doc;
        }

        /// <summary>The number of pages in the document.</summary>
        public int PageCount
        {
            get
            {
                ThrowIfDisposed();
                return _pages.Count;
            }
        }

        /// <summary>Document title from the metadata, or null.</summary>
        public string Title { get; private set; }

        /// <summary>Document author from the metadata, or null.</summary>
        public string Author { get; private set; }

        /// <summary>
        /// Returns the size of a page in points, derived from its (possibly inherited) MediaBox.
        /// </summary>
        /// <param name="pageNumber">The 1-based page number.</param>
        public PageSize GetPageSize(int pageNumber)
        {
            var record = GetPageRecord(pageNumber);
            GetMediaBoxExtents(record, out float width, out float height);
            return new PageSize(width, height);
        }

        /// <summary>Releases the in-memory copy of the file. Pages already imported into a <see cref="PdfDocument"/> stay valid.</summary>
        public void Dispose()
        {
            _disposed = true;
            _data = null;
            _parser = null;
            _xref = null;
            _objectCache.Clear();
            _objStmCache.Clear();
            _pages = null;
        }

        // ── Internal access for the page importer ───────────────────

        internal ReadPageRecord GetPageRecord(int pageNumber)
        {
            ThrowIfDisposed();
            if (pageNumber < 1 || pageNumber > _pages.Count)
                throw new ArgumentOutOfRangeException(nameof(pageNumber),
                    $"Page number must be between 1 and {_pages.Count}.");
            return _pages[pageNumber - 1];
        }

        internal void GetMediaBoxExtents(ReadPageRecord record, out float width, out float height)
        {
            width = PageSize.A4.Width;
            height = PageSize.A4.Height;
            var box = record.MediaBox;
            if (box != null && box.Items.Count >= 4)
            {
                float x0 = ToFloat(Resolve(box.Items[0]));
                float y0 = ToFloat(Resolve(box.Items[1]));
                float x1 = ToFloat(Resolve(box.Items[2]));
                float y1 = ToFloat(Resolve(box.Items[3]));
                float w = Math.Abs(x1 - x0);
                float h = Math.Abs(y1 - y0);
                if (w > 0 && h > 0)
                {
                    width = w;
                    height = h;
                }
            }
        }

        private static float ToFloat(CosValue value)
        {
            if (value is CosInteger i) return i.Value;
            if (value is CosReal r) return (float)r.Value;
            return 0;
        }

        /// <summary>Resolves an object by id via the cross-reference table, with caching.</summary>
        internal CosValue GetObject(PdfObjectId id)
        {
            ThrowIfDisposed();
            if (_objectCache.TryGetValue(id, out var cached))
                return cached;
            if (id.Number <= 0 || !_xref.Entries.TryGetValue(id.Number, out var entry))
                return CosNull.Instance;
            if (!_parsing.Add(id))
                return CosNull.Instance; // self-referential cycle guard

            try
            {
                CosValue value = CosNull.Instance;
                if (entry.Type == 1)
                {
                    if (entry.Value >= 0 && entry.Value < _data.Length)
                    {
                        try
                        {
                            var body = _parser.ParseIndirectObject((int)entry.Value, out var actualId);
                            if (actualId.Number == id.Number)
                                value = body;
                        }
                        catch (PdfParseException)
                        {
                            // Leave as null; the repair path handles files where this matters broadly
                        }
                    }
                }
                else if (entry.Type == 2)
                {
                    var contents = GetObjectStreamContents((int)entry.Value);
                    if (contents != null && contents.TryGetValue(id.Number, out var contained))
                        value = contained;
                }
                _objectCache[id] = value;
                return value;
            }
            finally
            {
                _parsing.Remove(id);
            }
        }

        /// <summary>Follows reference chains until a direct value is reached.</summary>
        internal CosValue Resolve(CosValue value)
        {
            for (int depth = 0; depth < 32 && value is CosReference reference; depth++)
                value = GetObject(reference.Id);
            return value is CosReference ? CosNull.Instance : (value ?? CosNull.Instance);
        }

        private Dictionary<int, CosValue> GetObjectStreamContents(int containerNumber)
        {
            if (_objStmCache.TryGetValue(containerNumber, out var cached))
                return cached;
            Dictionary<int, CosValue> contents = null;
            if (GetObject(new PdfObjectId(containerNumber, 0)) is CosStream container &&
                container.GetName("Type") == "ObjStm")
            {
                try
                {
                    contents = ObjectStreamReader.Expand(container, Resolve);
                }
                catch (PdfParseException)
                {
                    contents = null;
                }
            }
            _objStmCache[containerNumber] = contents;
            return contents;
        }

        // ── Loading ─────────────────────────────────────────────────

        private void Load(byte[] data)
        {
            // The header may be preceded by junk bytes; all offsets are relative to it
            int headerPos = PdfParser.IndexOf(data, "%PDF-", 0);
            if (headerPos < 0 || headerPos > 1024)
                throw new PdfParseException("The data does not contain a PDF header (%PDF-).");
            if (headerPos > 0)
            {
                var sliced = new byte[data.Length - headerPos];
                Array.Copy(data, headerPos, sliced, 0, sliced.Length);
                data = sliced;
            }
            _data = data;
            _parser = new PdfParser(data);
            ParseHeaderVersion();

            try
            {
                _xref = XrefReader.Read(_parser);
                BuildDocument();
            }
            catch (PdfParseException)
            {
                // Broken or missing cross-reference data: rebuild by scanning the file
                _objectCache.Clear();
                _objStmCache.Clear();
                _xref = XrefReader.Repair(_parser);
                BuildDocument();
            }
        }

        private void ParseHeaderVersion()
        {
            var sb = new StringBuilder();
            for (int i = 5; i < _data.Length && i < 16; i++)
            {
                byte b = _data[i];
                if ((b >= (byte)'0' && b <= (byte)'9') || b == (byte)'.')
                    sb.Append((char)b);
                else
                    break;
            }
            if (sb.Length >= 3)
                PdfVersion = sb.ToString();
        }

        private void BuildDocument()
        {
            var encrypt = _xref.Trailer.Get("Encrypt");
            if (encrypt != null && !(encrypt is CosNull))
                throw new NotSupportedException(
                    "This PDF is encrypted (password protected). Opening encrypted PDFs is not supported yet.");

            _parser.LengthResolver = id => (Resolve(new CosReference(id.Number, id.Generation)) as CosInteger)?.Value;

            var catalog = Resolve(_xref.Trailer.Get("Root")) as CosDict;
            if (catalog == null)
                throw new PdfParseException("The document catalog could not be resolved.");
            Catalog = catalog;

            // The catalog may declare a higher version than the header
            string catalogVersion = catalog.GetName("Version");
            if (catalogVersion != null && string.CompareOrdinal(catalogVersion, PdfVersion) > 0)
                PdfVersion = catalogVersion;

            _pages = new List<ReadPageRecord>();
            var pagesRoot = Resolve(catalog.Get("Pages")) as CosDict;
            if (pagesRoot != null)
            {
                var visited = new HashSet<PdfObjectId>();
                var rootRef = catalog.Get("Pages") as CosReference;
                if (rootRef != null)
                    visited.Add(rootRef.Id);
                WalkPageTree(pagesRoot, visited, 0, null, null, null, null);
            }
            if (_pages.Count == 0)
                throw new PdfParseException("The document contains no pages.");

            _info = Resolve(_xref.Trailer.Get("Info")) as CosDict;
            Title = (_info?.Get("Title") is CosString title) ? title.AsText() : null;
            Author = (_info?.Get("Author") is CosString author) ? author.AsText() : null;
        }

        private void WalkPageTree(CosDict node, HashSet<PdfObjectId> visited, int depth,
            CosArray mediaBox, CosArray cropBox, CosValue resources, long? rotate)
        {
            if (depth > 256)
                throw new PdfParseException("The page tree is nested too deeply.");

            // Inheritable attributes: the node's own values override the inherited ones
            if (Resolve(node.Get("MediaBox")) is CosArray mb) mediaBox = mb;
            if (Resolve(node.Get("CropBox")) is CosArray cb) cropBox = cb;
            var ownResources = node.Get("Resources");
            if (ownResources != null && !(ownResources is CosNull)) resources = ownResources;
            if (Resolve(node.Get("Rotate")) is CosInteger rot) rotate = rot.Value;

            var kids = Resolve(node.Get("Kids")) as CosArray;
            if (kids == null)
                return;
            foreach (var kid in kids.Items)
            {
                var kidRef = kid as CosReference;
                if (kidRef != null && !visited.Add(kidRef.Id))
                    continue; // cycle in the page tree
                var kidDict = Resolve(kid) as CosDict;
                if (kidDict == null)
                    continue;

                string type = kidDict.GetName("Type");
                bool isNode = type == "Pages" || (type != "Page" && kidDict.ContainsKey("Kids"));
                if (isNode)
                {
                    WalkPageTree(kidDict, visited, depth + 1, mediaBox, cropBox, resources, rotate);
                }
                else
                {
                    var record = new ReadPageRecord
                    {
                        Id = kidRef?.Id ?? new PdfObjectId(-(_pages.Count + 1), 0),
                        Dict = kidDict,
                        MediaBox = Resolve(kidDict.Get("MediaBox")) as CosArray ?? mediaBox,
                        CropBox = Resolve(kidDict.Get("CropBox")) as CosArray ?? cropBox,
                        Resources = FirstNonNull(kidDict.Get("Resources"), resources),
                        Rotate = (Resolve(kidDict.Get("Rotate")) as CosInteger)?.Value ?? rotate,
                    };
                    _pages.Add(record);
                }
            }
        }

        private static CosValue FirstNonNull(CosValue own, CosValue inherited) =>
            own != null && !(own is CosNull) ? own : inherited;

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(PdfReadDocument));
        }
    }
}
