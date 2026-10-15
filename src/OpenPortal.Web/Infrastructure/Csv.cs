using System.Text;

namespace OpenPortal.Web.Infrastructure;

/// <summary>
/// Reads and writes the CSV files the admin screens exchange with spreadsheets (audit and user exports, user
/// imports). RFC 4180: fields separated by a comma (or a semicolon, which spreadsheets in many European locales
/// write), quoted when they hold a separator, a quote or a line break.
/// </summary>
public static class Csv
{
    public const string ContentType = "text/csv; charset=utf-8";

    /// <summary>
    /// One CSV field: quoted when it holds a separator, a quote or a line break, and defused when it starts
    /// like a formula, because labels, names and user agents come from users and a spreadsheet would run them.
    /// </summary>
    public static string Cell(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            value = "'" + value;
        }

        return value.IndexOfAny([',', ';', '"', '\n', '\r']) >= 0
            ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : value;
    }

    /// <summary>Appends one row of already-formatted values, each passed through <see cref="Cell"/>.</summary>
    public static StringBuilder AppendRow(this StringBuilder csv, params string?[] values)
    {
        ArgumentNullException.ThrowIfNull(csv);
        ArgumentNullException.ThrowIfNull(values);

        return csv.AppendJoin(',', values.Select(Cell)).AppendLine();
    }

    /// <summary>
    /// The file's bytes: UTF-8 with a byte-order mark, which makes spreadsheet applications read it as UTF-8
    /// rather than ANSI.
    /// </summary>
    public static byte[] ToBytes(StringBuilder csv)
    {
        ArgumentNullException.ThrowIfNull(csv);

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
    }

    /// <summary>
    /// Parses a whole file into rows of fields. The separator is a comma unless the first line holds more
    /// semicolons than commas outside quotes. A leading byte-order mark is dropped, blank lines are skipped and
    /// fields are not trimmed (the caller decides what whitespace means).
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<string>> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.Length > 0 && text[0] == '﻿')
        {
            text = text[1..];
        }

        var separator = DetectSeparator(text);
        var rows = new List<IReadOnlyList<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        var fieldStarted = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (quoted)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else
                {
                    field.Append(c);
                }

                continue;
            }

            if (c == '"' && field.Length == 0)
            {
                quoted = true;
                fieldStarted = true;
            }
            else if (c == separator)
            {
                row.Add(field.ToString());
                field.Clear();
                fieldStarted = true;
            }
            else if (c is '\r' or '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                EndRow();
            }
            else
            {
                field.Append(c);
                fieldStarted = true;
            }
        }

        EndRow();
        return rows;

        void EndRow()
        {
            if (fieldStarted || field.Length > 0 || row.Count > 0)
            {
                row.Add(field.ToString());
                if (row.Exists(value => value.Length > 0))
                {
                    rows.Add(row);
                }
            }

            row = [];
            field.Clear();
            fieldStarted = false;
        }
    }

    private static char DetectSeparator(string text)
    {
        int commas = 0, semicolons = 0;
        var quoted = false;

        foreach (var c in text)
        {
            if (c == '"')
            {
                quoted = !quoted;
            }
            else if (!quoted && c is '\r' or '\n')
            {
                break;
            }
            else if (!quoted && c == ',')
            {
                commas++;
            }
            else if (!quoted && c == ';')
            {
                semicolons++;
            }
        }

        return semicolons > commas ? ';' : ',';
    }
}
