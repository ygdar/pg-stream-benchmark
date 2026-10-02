using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

namespace DbBenchmark.Metrics;

/// <summary>
/// Формирует единый отчёт-сводку по всем запущенным бенчмаркам текущего прогона в
/// одном файле. Объединяет тайминг из отделов BenchmarkDotNet (*-report.csv) и метрики
/// ресурсов из MetricsStore. Файл перезаписывается на каждый прогон.
/// </summary>
public static class SummaryReport
{
    /// <summary>Подстрока имени типа benchmark → имя стратегии.</summary>
    private static readonly (string token, string name)[] StrategyMap =
    {
        ("BatchInsert", "A_BatchInsert"),
        ("RawCopy", "B_RawBinaryCopy"),
        ("Linq2DbBulk", "C_Linq2DbBulk"),
        ("EfChunk", "D_EfChunk"),
        ("NativeCopy", "E_NativeCopy"),
    };

    public static string ResolveStrategy(string reportFileName)
    {
        foreach (var (token, name) in StrategyMap)
            if (reportFileName.Contains(token, StringComparison.OrdinalIgnoreCase))
                return name;
        return reportFileName;
    }

    /// <summary>Читает тайминг из *-report.csv (колонки Rows и Mean).</summary>
    private static Dictionary<(string strategy, string rows), double> ReadTiming(string resultsDir)
    {
        var timings = new Dictionary<(string, string), double>();
        if (!Directory.Exists(resultsDir)) return timings;

        foreach (var file in Directory.EnumerateFiles(resultsDir, "*-report.csv"))
        {
            var strategy = ResolveStrategy(Path.GetFileName(file));
            using var reader = new StreamReader(file);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture));
            csv.Read();
            csv.ReadHeader();
            while (csv.Read())
            {
                var rows = csv.GetField("Rows");
                var mean = csv.GetField("Mean");
                if (rows is null || mean is null || mean == "NA") continue;
                // Mean из BDN — снабжён единицей и культурными разделителями (напр. "1,011.94 ms").
                // Приводим к миллисекундам инвариантно.
                var cleaned = mean.Replace("ms", "", StringComparison.OrdinalIgnoreCase)
                                  .Replace(" ", "")
                                  .Replace(",", "");
                if (double.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var ms))
                    timings[(strategy, rows)] = ms;
            }
        }
        return timings;
    }

    public static void Generate()
    {
        // CWD родителя — каталог проекта (dotnet run), там лежат отчёты BDN.
        var resultsDir = Path.Combine(Directory.GetCurrentDirectory(), "BenchmarkDotNet.Artifacts", "results");
        var timings = ReadTiming(resultsDir);
        var byKey = MetricsStore.LoadAll().ToDictionary(m => (m.StrategyName, m.Rows.ToString()));

        var rowsLine = new List<string[]>
        {
            new[] { "Strategy", "Rows", "TimeMs", "PeakMemMB", "ClientCpuCorePct",
                    "ClientCpuAllPct", "ServerWorkRate(est)", "PayloadBytes",
                    "OverheadBytes", "PayloadOverheadRatio", "Note" },
        };

        foreach (var ((strategy, rows), meanMs) in timings.OrderBy(k => k.Key.Item1).ThenBy(k => int.Parse(k.Key.Item2)))
        {
            var has = byKey.TryGetValue((strategy, rows), out var m);
            var note = m?.EstimateNote ?? (has ? "" : "no per-run metrics");
            rowsLine.Add(new[]
            {
                strategy,
                rows,
                meanMs.ToString("0.###", CultureInfo.InvariantCulture),
                has ? (m!.PeakMemoryBytes / 1048576.0).ToString("0.0", CultureInfo.InvariantCulture) : "?",
                has ? m!.ClientCpuCorePct.ToString("0.00", CultureInfo.InvariantCulture) : "?",
                has ? m!.ClientCpuAllPct.ToString("0.00", CultureInfo.InvariantCulture) : "?",
                has ? m!.ServerWorkRate.ToString("0.0", CultureInfo.InvariantCulture) : "?",
                has ? m!.PayloadBytes.ToString(CultureInfo.InvariantCulture) : "?",
                has ? m!.OverheadBytes.ToString(CultureInfo.InvariantCulture) : "?",
                has ? m!.PayloadOverheadRatio.ToString("0.0000", CultureInfo.InvariantCulture) : "?",
                note ?? "",
            });
        }

        Directory.CreateDirectory(resultsDir);
        var csvPath = Path.Combine(resultsDir, "summary.csv");
        using (var writer = new StreamWriter(csvPath, append: false))
        using (var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)))
            foreach (var line in rowsLine)
            {
                foreach (var cell in line) csv.WriteField(cell);
                csv.NextRecord();
            }

        // Параллельно пишем удобный для чтения markdown.
        var md = new System.Text.StringBuilder();
        md.AppendLine("# db-benchmark — сводка по прогону");
        md.AppendLine("| " + string.Join(" | ", rowsLine[0]) + " |");
        md.AppendLine("|" + string.Join("|", rowsLine[0].Select(_ => "---")) + "|");
        foreach (var line in rowsLine.Skip(1))
            md.AppendLine("| " + string.Join(" | ", line) + " |");
        md.AppendLine();
        md.AppendLine("> Примечания: `ServerWorkRate` и обыден в трафике — оценочные (относительные) значения.");
        File.WriteAllText(Path.Combine(resultsDir, "summary.md"), md.ToString());

        Console.WriteLine($"Summary written: {csvPath}");
    }
}