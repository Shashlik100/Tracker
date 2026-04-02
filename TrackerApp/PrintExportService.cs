using System.Text;

namespace TrackerApp;

internal static class PrintExportService
{
    public static void ExportHtml(string filePath, DateTime startDate, DateTime endDate, IReadOnlyList<PrintableScheduleItem> items)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<!DOCTYPE html>");
        builder.AppendLine("<html lang=\"he\" dir=\"rtl\">");
        builder.AppendLine("<head>");
        builder.AppendLine("<meta charset=\"utf-8\" />");
        builder.AppendLine("<title>דפי לימוד וחזרה</title>");
        builder.AppendLine("<style>");
        builder.AppendLine("body { font-family: 'David', 'Times New Roman', serif; margin: 24px; color: #111; direction: rtl; text-align: right; }");
        builder.AppendLine("h1, h2 { margin: 0 0 8px; }");
        builder.AppendLine(".meta { margin-bottom: 20px; color: #444; }");
        builder.AppendLine(".unit { page-break-inside: avoid; border: 1px solid #245b5b; padding: 12px; margin-bottom: 12px; background: #fff; }");
        builder.AppendLine(".subject { font-weight: bold; color: #245b5b; margin-bottom: 4px; }");
        builder.AppendLine(".topic { font-size: 18px; margin-bottom: 8px; }");
        builder.AppendLine(".label { font-weight: bold; margin-top: 10px; color: #245b5b; }");
        builder.AppendLine("</style>");
        builder.AppendLine("</head>");
        builder.AppendLine("<body>");
        builder.AppendLine("<h1>דפי לימוד וחזרה</h1>");
        builder.AppendLine($"<div class=\"meta\">יחידות לימוד מתוזמנות בין {startDate:dddd, dd/MM/yyyy} לבין {endDate:dddd, dd/MM/yyyy}</div>");

        foreach (var item in items.OrderBy(card => card.DueDate).ThenBy(card => card.SubjectPath).ThenBy(card => card.Topic))
        {
            builder.AppendLine("<div class=\"unit\">");
            builder.AppendLine($"<div class=\"subject\">{Encode(item.SubjectPath)} | חזרה: {item.DueDate:dd/MM/yyyy}</div>");
            builder.AppendLine($"<div class=\"topic\">{Encode(item.Topic)}</div>");
            AppendSection(builder, "מקור", item.SourceText);
            AppendSection(builder, "פשט", item.PshatText);
            AppendSection(builder, "קושיה", item.KushyaText);
            AppendSection(builder, "תירוץ", item.TerutzText);
            AppendSection(builder, "חידוש", item.ChidushText);
            AppendSection(builder, "סיכום אישי", item.PersonalSummary);
            AppendSection(builder, "הערות חזרה", item.ReviewNotes);
            builder.AppendLine("</div>");
        }

        if (items.Count == 0)
        {
            builder.AppendLine("<p>לא נמצאו יחידות לימוד מתוזמנות בטווח שנבחר.</p>");
        }

        builder.AppendLine("</body>");
        builder.AppendLine("</html>");

        File.WriteAllText(filePath, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private static void AppendSection(StringBuilder builder, string title, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        builder.AppendLine($"<div class=\"label\">{Encode(title)}</div>");
        builder.AppendLine($"<div>{Encode(value)}</div>");
    }

    private static string Encode(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace(Environment.NewLine, "<br />", StringComparison.Ordinal);
    }
}
