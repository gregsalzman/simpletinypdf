using System.Collections.Generic;
using System.Text;

namespace SimpleTinyPDF
{
    /// <summary>
    /// Bridges a parsed COS value into the writer's object model. References inside the
    /// body stay in the source file's id space and are resolved through <see cref="RefMap"/>
    /// (source id → destination object) at write time.
    /// </summary>
    internal sealed class ImportedObj : PdfObj
    {
        internal CosValue Body;
        internal Dictionary<PdfObjectId, PdfObj> RefMap;

        /// <summary>When set and the body is a dictionary, a /Parent entry pointing here is emitted (used for imported page dicts).</summary>
        internal PdfObj ParentTarget;

        internal override void WriteTo(PdfBinaryWriter w) =>
            CosSerializer.WriteObjectBody(w, Body, RefMap, ParentTarget);
    }

    /// <summary>
    /// A reference to a writer-side object, injectable into an imported value tree
    /// (e.g. attaching a signature widget to an imported page's /Annots).
    /// </summary>
    internal sealed class CosWriterRef : CosValue
    {
        internal readonly PdfObj Target;
        internal CosWriterRef(PdfObj target) => Target = target;
    }

    /// <summary>
    /// Serializes parsed COS values back to PDF syntax. Strings are emitted in hex form
    /// and names re-escaped with #xx, so no escaping subtleties can corrupt imported data.
    /// </summary>
    internal static class CosSerializer
    {
        internal static void WriteObjectBody(PdfBinaryWriter w, CosValue body,
            Dictionary<PdfObjectId, PdfObj> refMap, PdfObj parentTarget)
        {
            if (body is CosStream stream)
            {
                var sb = new StringBuilder("<<\n");
                AppendDictEntries(sb, stream, refMap, parentTarget, skipLength: true);
                int length = stream.RawData?.Length ?? 0;
                sb.Append("/Length ").Append(length).Append('\n');
                sb.Append(">>\nstream\n");
                w.WriteAscii(sb.ToString());
                if (stream.RawData != null)
                    w.WriteBytes(stream.RawData);
                w.WriteAscii("\nendstream\n");
            }
            else if (body is CosDict dict)
            {
                var sb = new StringBuilder("<<\n");
                AppendDictEntries(sb, dict, refMap, parentTarget, skipLength: false);
                sb.Append(">>\n");
                w.WriteAscii(sb.ToString());
            }
            else
            {
                var sb = new StringBuilder();
                AppendValue(sb, body, refMap);
                sb.Append('\n');
                w.WriteAscii(sb.ToString());
            }
        }

        private static void AppendDictEntries(StringBuilder sb, CosDict dict,
            Dictionary<PdfObjectId, PdfObj> refMap, PdfObj parentTarget, bool skipLength)
        {
            foreach (var kv in dict.Entries)
            {
                if (skipLength && kv.Key == "Length")
                    continue; // recomputed from the actual data
                if (parentTarget != null && kv.Key == "Parent")
                    continue; // replaced below
                sb.Append('/').Append(EscapeName(kv.Key)).Append(' ');
                AppendValue(sb, kv.Value, refMap);
                sb.Append('\n');
            }
            if (parentTarget != null)
                sb.Append("/Parent ").Append(parentTarget.Ref).Append('\n');
        }

        internal static void AppendValue(StringBuilder sb, CosValue value,
            Dictionary<PdfObjectId, PdfObj> refMap)
        {
            switch (value)
            {
                case null:
                case CosNull _:
                    sb.Append("null");
                    break;
                case CosBool b:
                    sb.Append(b.Value ? "true" : "false");
                    break;
                case CosInteger i:
                    sb.Append(i.Value);
                    break;
                case CosReal r:
                    sb.Append(CosNumber.Format(r.Value));
                    break;
                case CosName n:
                    sb.Append('/').Append(EscapeName(n.Value));
                    break;
                case CosString s:
                    sb.Append('<');
                    foreach (byte raw in s.Raw ?? System.Array.Empty<byte>())
                        sb.Append(raw.ToString("X2"));
                    sb.Append('>');
                    break;
                case CosArray a:
                    sb.Append('[');
                    for (int idx = 0; idx < a.Items.Count; idx++)
                    {
                        if (idx > 0) sb.Append(' ');
                        AppendValue(sb, a.Items[idx], refMap);
                    }
                    sb.Append(']');
                    break;
                case CosStream _:
                    // Streams are always indirect objects; a nested one cannot occur in parsed data
                    sb.Append("null");
                    break;
                case CosDict d:
                    sb.Append("<< ");
                    foreach (var kv in d.Entries)
                    {
                        sb.Append('/').Append(EscapeName(kv.Key)).Append(' ');
                        AppendValue(sb, kv.Value, refMap);
                        sb.Append(' ');
                    }
                    sb.Append(">>");
                    break;
                case CosReference reference:
                    if (refMap != null && refMap.TryGetValue(reference.Id, out var target))
                        sb.Append(target.Ref);
                    else
                        sb.Append("null"); // reference to an object outside the imported closure
                    break;
                case CosWriterRef writerRef:
                    sb.Append(writerRef.Target.Ref);
                    break;
            }
        }

        private static string EscapeName(string name)
        {
            var sb = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                bool needsEscape = c <= 32 || c > 126 || c == '#' ||
                    c == '(' || c == ')' || c == '<' || c == '>' || c == '[' || c == ']' ||
                    c == '{' || c == '}' || c == '/' || c == '%';
                if (needsEscape)
                    sb.AppendFormat("#{0:X2}", (int)c & 0xFF);
                else
                    sb.Append(c);
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// Deep-clones a COS value tree. References are leaf nodes and are not followed.
    /// Used before encryption so shared imported objects are never mutated, keeping a
    /// document safe to save more than once.
    /// </summary>
    internal static class CosCloner
    {
        internal static CosValue Clone(CosValue value)
        {
            switch (value)
            {
                case CosStream stream:
                    var streamClone = new CosStream { RawData = stream.RawData };
                    foreach (var kv in stream.Entries)
                        streamClone.Entries.Add(new KeyValuePair<string, CosValue>(kv.Key, Clone(kv.Value)));
                    return streamClone;
                case CosDict dict:
                    var dictClone = new CosDict();
                    foreach (var kv in dict.Entries)
                        dictClone.Entries.Add(new KeyValuePair<string, CosValue>(kv.Key, Clone(kv.Value)));
                    return dictClone;
                case CosArray array:
                    var arrayClone = new CosArray();
                    foreach (var item in array.Items)
                        arrayClone.Items.Add(Clone(item));
                    return arrayClone;
                case CosString s:
                    return new CosString(s.Raw);
                default:
                    return value; // scalars and references are immutable
            }
        }
    }
}
