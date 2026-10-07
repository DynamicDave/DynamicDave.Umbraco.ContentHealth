using System.Globalization;
using System.Text;
using DynamicDave.Umbraco.ContentHealth.Models;

namespace DynamicDave.Umbraco.ContentHealth.Export;

internal static class CsvWriter
{
    private static readonly string[] Header =
        ["Source", "SourceKey", "SourcePath", "Culture", "Property", "Version", "TargetKey", "TargetName", "TargetType", "Status", "CheckedUtc"];

    /// <summary>RFC 4180 CSV, UTF-8 with BOM so Excel detects the encoding.</summary>
    public static byte[] Write(IEnumerable<BrokenLinkModel> links)
    {
        var csv = new StringBuilder();
        AppendRow(csv, Header);
        foreach (var link in links)
        {
            AppendRow(csv,
            [
                link.SourceName,
                link.SourceKey.ToString(),
                link.SourcePath,
                link.Culture ?? string.Empty,
                link.PropertyName,
                link.IsDraft ? "Draft" : "Live",
                link.TargetKey.ToString(),
                link.TargetName ?? string.Empty,
                link.TargetType.ToString(),
                link.Status.ToString(),
                DateTime.SpecifyKind(link.CheckedUtc, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            ]);
        }

        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv.ToString())];
    }

    internal static string Escape(string value)
    {
        // Page names are typed by editors: a cell starting with one of these would run as a spreadsheet formula.
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            value = "'" + value;
        }

        return value.IndexOfAny([',', '"', '\n', '\r']) >= 0
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }

    private static void AppendRow(StringBuilder csv, IEnumerable<string> values)
    {
        csv.AppendJoin(',', values.Select(Escape));
        csv.Append("\r\n");
    }
}
