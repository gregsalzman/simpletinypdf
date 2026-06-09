using System.Collections.Generic;
using System.Text;

namespace SimpleTinyPDF
{
    /// <summary>
    /// Parses CSV content into rows of string fields.
    /// Handles quoted fields, embedded delimiters, embedded newlines, and escaped double-quotes.
    /// </summary>
    internal static class CsvParser
    {
        internal static List<string[]> Parse(string content, char delimiter = ',')
        {
            var rows = new List<string[]>();
            if (string.IsNullOrEmpty(content))
                return rows;

            var fields = new List<string>();
            var field = new StringBuilder();
            int i = 0;

            while (i < content.Length)
            {
                char c = content[i];

                if (c == '"')
                {
                    i++;
                    while (i < content.Length)
                    {
                        if (content[i] == '"')
                        {
                            if (i + 1 < content.Length && content[i + 1] == '"')
                            {
                                field.Append('"');
                                i += 2;
                            }
                            else
                            {
                                i++;
                                break;
                            }
                        }
                        else
                        {
                            field.Append(content[i]);
                            i++;
                        }
                    }

                    // Skip any trailing content after closing quote until delimiter or newline
                    while (i < content.Length && content[i] != delimiter
                           && content[i] != '\r' && content[i] != '\n')
                        i++;

                    if (i < content.Length && content[i] == delimiter)
                    {
                        fields.Add(field.ToString());
                        field.Clear();
                        i++;
                    }
                    else
                    {
                        fields.Add(field.ToString());
                        field.Clear();
                        if (fields.Count > 1 || fields[0].Length > 0)
                            rows.Add(fields.ToArray());
                        fields.Clear();
                        if (i < content.Length)
                        {
                            if (content[i] == '\r' && i + 1 < content.Length && content[i + 1] == '\n')
                                i++;
                            i++;
                        }
                    }
                }
                else if (c == delimiter)
                {
                    fields.Add(field.ToString());
                    field.Clear();
                    i++;
                }
                else if (c == '\r' || c == '\n')
                {
                    fields.Add(field.ToString());
                    field.Clear();
                    if (fields.Count > 1 || fields[0].Length > 0)
                        rows.Add(fields.ToArray());
                    fields.Clear();
                    if (c == '\r' && i + 1 < content.Length && content[i + 1] == '\n')
                        i++;
                    i++;
                }
                else
                {
                    while (i < content.Length && content[i] != delimiter
                           && content[i] != '\r' && content[i] != '\n')
                    {
                        field.Append(content[i]);
                        i++;
                    }
                }
            }

            // Handle last field/row if file doesn't end with newline
            if (field.Length > 0 || fields.Count > 0)
            {
                fields.Add(field.ToString());
                if (fields.Count > 1 || fields[0].Length > 0)
                    rows.Add(fields.ToArray());
            }

            return rows;
        }
    }
}
