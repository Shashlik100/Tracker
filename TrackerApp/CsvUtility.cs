using System.Text;

namespace TrackerApp;

internal static class CsvUtility
{
    public static List<string[]> ReadRows(string filePath)
    {
        var rows = new List<string[]>();
        var currentField = new StringBuilder();
        var currentRow = new List<string>();
        var inQuotes = false;
        var text = File.ReadAllText(filePath, Encoding.UTF8);

        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];

            if (inQuotes)
            {
                if (character == '"' && index + 1 < text.Length && text[index + 1] == '"')
                {
                    currentField.Append('"');
                    index++;
                }
                else if (character == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    currentField.Append(character);
                }

                continue;
            }

            if (character == '"')
            {
                inQuotes = true;
            }
            else if (character == ',')
            {
                currentRow.Add(currentField.ToString());
                currentField.Clear();
            }
            else if (character == '\r')
            {
            }
            else if (character == '\n')
            {
                currentRow.Add(currentField.ToString());
                currentField.Clear();
                rows.Add(currentRow.ToArray());
                currentRow = new List<string>();
            }
            else
            {
                currentField.Append(character);
            }
        }

        if (currentField.Length > 0 || currentRow.Count > 0)
        {
            currentRow.Add(currentField.ToString());
            rows.Add(currentRow.ToArray());
        }

        return rows;
    }

    public static void WriteRows(string filePath, IEnumerable<string[]> rows)
    {
        using var writer = new StreamWriter(filePath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        foreach (var row in rows)
        {
            writer.WriteLine(string.Join(",", row.Select(Escape)));
        }
    }

    private static string Escape(string value)
    {
        if (value.Contains('"'))
        {
            value = value.Replace("\"", "\"\"");
        }

        return value.IndexOfAny(new[] { ',', '\n', '\r', '"' }) >= 0
            ? $"\"{value}\""
            : value;
    }
}
