using System.Text;

namespace TrackerApp;

internal static class PrintExportService
{
    public static void ExportHtml(string filePath, DateTime startDate, DateTime endDate, IReadOnlyList<PrintableScheduleItem> items)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<!DOCTYPE html>");
        builder.AppendLine("<html lang=\"en\">");
        builder.AppendLine("<head>");
        builder.AppendLine("<meta charset=\"utf-8\" />");
        builder.AppendLine("<title>Print for Shabbat</title>");
        builder.AppendLine("<style>");
        builder.AppendLine("body { font-family: 'Times New Roman', serif; margin: 24px; color: #111; }");
        builder.AppendLine("h1, h2 { margin: 0 0 8px; }");
        builder.AppendLine(".meta { margin-bottom: 20px; color: #444; }");
        builder.AppendLine(".card { page-break-inside: avoid; border: 1px solid #245b5b; padding: 12px; margin-bottom: 12px; }");
        builder.AppendLine(".subject { font-weight: bold; color: #245b5b; margin-bottom: 4px; }");
        builder.AppendLine(".topic { font-size: 18px; margin-bottom: 8px; }");
        builder.AppendLine(".label { font-weight: bold; margin-top: 8px; }");
        builder.AppendLine("</style>");
        builder.AppendLine("</head>");
        builder.AppendLine("<body>");
        builder.AppendLine("<h1>Print for Shabbat / Yom Tov</h1>");
        builder.AppendLine($"<div class=\"meta\">Scheduled cards from {startDate:dddd, dd MMM yyyy} through {endDate:dddd, dd MMM yyyy}</div>");

        foreach (var item in items.OrderBy(card => card.DueDate).ThenBy(card => card.SubjectPath).ThenBy(card => card.Topic))
        {
            builder.AppendLine("<div class=\"card\">");
            builder.AppendLine($"<div class=\"subject\">{Encode(item.SubjectPath)} | Due {item.DueDate:dd/MM/yyyy}</div>");
            builder.AppendLine($"<div class=\"topic\">{Encode(item.Topic)}</div>");
            builder.AppendLine("<div class=\"label\">Question</div>");
            builder.AppendLine($"<div>{Encode(item.Question)}</div>");
            builder.AppendLine("<div class=\"label\">Answer</div>");
            builder.AppendLine($"<div>{Encode(item.Answer)}</div>");
            builder.AppendLine("</div>");
        }

        if (items.Count == 0)
        {
            builder.AppendLine("<p>No scheduled cards were found for the selected range.</p>");
        }

        builder.AppendLine("</body>");
        builder.AppendLine("</html>");

        File.WriteAllText(filePath, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private static string Encode(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
    }
}
