namespace BookSharingApp.Cli.Formatting;

public static class TableFormatter
{
    public static void Print(string[] headers, List<string[]> rows, string itemLabel = "item")
    {
        var columnCount = headers.Length;
        var widths = new int[columnCount];

        for (var i = 0; i < columnCount; i++)
            widths[i] = headers[i].Length;

        foreach (var row in rows)
        {
            for (var i = 0; i < columnCount && i < row.Length; i++)
            {
                if (row[i].Length > widths[i])
                    widths[i] = row[i].Length;
            }
        }

        var format = string.Join("  ", widths.Select((w, i) =>
            i == columnCount - 1 ? $"{{{i}}}" : $"{{{i},-{w}}}"));

        Console.WriteLine(string.Format(format, headers));
        Console.WriteLine(new string('─', widths.Sum() + (columnCount - 1) * 2));

        foreach (var row in rows)
        {
            var padded = new string[columnCount];
            for (var i = 0; i < columnCount; i++)
                padded[i] = i < row.Length ? row[i] : "";
            Console.WriteLine(string.Format(format, padded));
        }

        Console.WriteLine();
        Console.WriteLine($"{rows.Count} {itemLabel}(s)");
    }
}
