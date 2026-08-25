namespace OmniChannel.Catalog.Host.Api;

public record CatalogRow(string Product, string Description, string Category, string Size, string Color, decimal Price, int Stock, string[] Channels);

public static class CatalogDataset
{
    private static string Dir(IWebHostEnvironment env) => Path.Combine(env.ContentRootPath, "catalogs");

    public static List<string> List(IWebHostEnvironment env)
    {
        var dir = Dir(env);
        return Directory.Exists(dir)
            ? [.. Directory.EnumerateFiles(dir, "*.csv").Select(f => Path.GetFileNameWithoutExtension(f)).Order()]
            : [];
    }

    public static List<CatalogRow> Load(IWebHostEnvironment env, string? name)
    {
        var datasets = List(env);
        if (datasets.Count == 0)
        {
            return [];
        }

        var chosen = name != null && datasets.Contains(name) ? name
            : datasets.Contains("patike") ? "patike"
            : datasets[0];
        var lines = File.ReadAllLines(Path.Combine(Dir(env), chosen + ".csv"));
        if (lines.Length < 2)
        {
            return [];
        }

        var header = ParseLine(lines[0]).Select(h => h.Trim().ToLowerInvariant()).ToList();
        int Col(string n) => header.IndexOf(n);
        int iProduct = Col("proizvod"), iDesc = Col("opis"), iCat = Col("kategorija"),
            iSize = Col("velicina"), iColor = Col("boja"), iPrice = Col("cena"),
            iStock = Col("zaliha"), iChannels = Col("kanali");

        var rows = new List<CatalogRow>();
        for (var i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                continue;
            }

            var cells = ParseLine(lines[i]);
            string Get(int idx) => idx >= 0 && idx < cells.Count ? cells[idx].Trim() : string.Empty;

            var channels = Get(iChannels).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            rows.Add(new CatalogRow(
                Get(iProduct), Get(iDesc), Get(iCat), Get(iSize), Get(iColor),
                decimal.TryParse(Get(iPrice), NumberStyles.Any, CultureInfo.InvariantCulture, out var price) ? price : 0m,
                int.TryParse(Get(iStock), out var stock) ? stock : 0,
                channels.Length > 0 ? channels : SalesChannel.All));
        }

        return rows;
    }

    private static List<string> ParseLine(string line)
    {
        var cells = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (inQuotes)
            {
                if (ch == '"' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else if (ch == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    sb.Append(ch);
                }
            }
            else if (ch == '"')
            {
                inQuotes = true;
            }
            else if (ch == ',')
            {
                cells.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(ch);
            }
        }

        cells.Add(sb.ToString());
        return cells;
    }
}