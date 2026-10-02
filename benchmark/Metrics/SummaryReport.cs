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
        ("NativeCopy", "E_NativeCopy"),
    };

    public static string ResolveStrategy(string reportFileName)
    {
        foreach (var (token, name) in StrategyMap)
            if (reportFileName.Contains(token, StringComparison.OrdinalIgnoreCase))
                return name;
        return reportFileName;
    }

    /// <summary>Код стратегии (внутренний) → русское название для отчёта.</summary>
    private static readonly Dictionary<string, string> StrategyRu = new()
    {
        ["A_BatchInsert"] = "Пакетный INSERT (A)",
        ["B_RawBinaryCopy"] = "Двоичный COPY (B)",
        ["C_Linq2DbBulk"] = "LINQ2DB BulkCopy (C)",
        ["E_NativeCopy"] = "Нативный COPY (E)",
    };

    private static string RussianName(string code) =>
        StrategyRu.TryGetValue(code, out var ru) ? ru : code;

    /// <summary>Форматирует размер в байтах в человекочитаемые МБ/ГБ.</summary>
    private static string FormatBytes(long bytes)
    {
        const double mb = 1048576.0, gb = 1073741824.0;
        return bytes >= gb
            ? (bytes / gb).ToString("0.0", CultureInfo.InvariantCulture) + " ГБ"
            : (bytes / mb).ToString("0.0", CultureInfo.InvariantCulture) + " МБ";
    }

    /// <summary>Легенда метрик: название колонки → что измеряет и как получено.</summary>
    private static readonly (string name, string what, string how)[] MetricLegend =
    {
        ("Время, мс",
         "Среднее (Mean) время одной загрузки набора данных",
         "из отчёта BenchmarkDotNet (*-report.csv) по итерациям прогона; приводится к миллисекундам"),
        ("Память клиента, МБ",
         "Пиковый рабочий набор процесса-клиента во время загрузки",
         "фоновый сэмплинг WorkingSet64 (каждые 100 мс) через System.Diagnostics.Process, вне тайминга"),
        ("CPU клиента (ядро), %",
         "Средняя утилизация CPU процесса-клиента в % от одного ядра",
         "дельта Process.TotalProcessorTime / затраченное время × 100"),
        ("CPU клиента (все ядра), %",
         "Средняя утилизация CPU клиента в % от всех ядер машины",
         "та же дельта, дополнительно делённая на число ядер"),
        ("Активность сервера (оценка)",
         "Оценочная активность PostgreSQL во время прогона (интенсивность работы с вставками/транзакциями)",
         "разница счётчиков pg_stat_database (xact_commit, tup_inserted) за прогон, делённая на время; оценочное относительное значение"),
        ("Полезные данные, МБ/ГБ",
         "Размер полезных данных (сгенерированный TSV-файл), который загружается",
         "детерминированный размер файла набора данных"),
        ("Накладные, МБ/ГБ",
         "Оценённый объём накладного обмена (служебные команды, разделители, батчинг)",
         "расчёт по проводному механизму стратегии относительно полезных данных; оценочное значение"),
        ("Отношение payload/overhead",
         "Доля полезных данных в общем трафике: payload/(payload+overhead)",
         "вычисляется из полезных и накладных данных; ближе к 1 — эффективнее"),
    };

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
                // Mean из BDN — число с единицей и культурными разделителями
                // (напр. "1,011.94 ms", "2.964 s", "0.123 us"). Приводим к миллисекундам инвариантно.
                var match = System.Text.RegularExpressions.Regex.Match(mean, @"^\s*([\d.,]+)\s*(ns|us|ms|s)?\s*$");
                if (!match.Success) continue;
                var num = double.Parse(match.Groups[1].Value.Replace(",", ""), CultureInfo.InvariantCulture);
                double ms = match.Groups[2].Value switch
                {
                    "s" => num * 1000.0,
                    "ms" => num,
                    "us" => num / 1000.0,
                    "ns" => num / 1_000_000.0,
                    _ => num,
                };
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
            new[] { "Стратегия", "Строк", "Время, мс", "Память клиента, МБ",
                    "CPU клиента (ядро), %", "CPU клиента (все ядра), %",
                    "Активность сервера (оценка)", "Полезные данные, МБ/ГБ",
                    "Накладные, МБ/ГБ", "Отношение payload/overhead", "Примечание" },
        };

        foreach (var ((strategy, rows), meanMs) in timings.OrderBy(k => k.Key.Item1).ThenBy(k => int.Parse(k.Key.Item2)))
        {
            var has = byKey.TryGetValue((strategy, rows), out var m);
            var note = m?.EstimateNote ?? (has ? "" : "нет метрик прогона");
            if (note == "server work & traffic are estimates")
                note = "серверная нагрузка и трафик — оценка";
            rowsLine.Add(new[]
            {
                RussianName(strategy),
                rows,
                meanMs.ToString("0.###", CultureInfo.InvariantCulture),
                has ? (m!.PeakMemoryBytes / 1048576.0).ToString("0.0", CultureInfo.InvariantCulture) : "?",
                has ? m!.ClientCpuCorePct.ToString("0.00", CultureInfo.InvariantCulture) : "?",
                has ? m!.ClientCpuAllPct.ToString("0.00", CultureInfo.InvariantCulture) : "?",
                has ? m!.ServerWorkRate.ToString("0.0", CultureInfo.InvariantCulture) : "?",
                has ? FormatBytes(m!.PayloadBytes) : "?",
                has ? FormatBytes(m!.OverheadBytes) : "?",
                has ? m!.PayloadOverheadRatio.ToString("0.0000", CultureInfo.InvariantCulture) : "?",
                note ?? "",
            });
        }

        Directory.CreateDirectory(resultsDir);
        var csvPath = Path.Combine(resultsDir, "summary.csv");
        using (var writer = new StreamWriter(csvPath, append: false))
        // Разделитель фиксируем явно на ',' — независимо от ListSeparator культуры процесса
        // (на машинах с русской/иной локалью по умолчанию писалась бы ';').
        using (var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture) { Delimiter = "," }))
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
        md.AppendLine("## О метриках");
        foreach (var (name, what, how) in MetricLegend)
            md.AppendLine($"- **{name}** — {what}. Получено: {how}.");
        md.AppendLine();
        md.AppendLine("> Примечания: «Активность сервера», накладные расходы и данные о трафике — оценочные (относительные) величины.");
        File.WriteAllText(Path.Combine(resultsDir, "summary.md"), md.ToString());

        Console.WriteLine($"Summary written: {csvPath}");
    }
}