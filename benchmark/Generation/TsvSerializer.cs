using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using DbBenchmark.Domain;

namespace DbBenchmark.Generation;

/// <summary>
/// Потоковая запись модели в TSV-файл через CsvHelper (таб-делимитер, invariant culture,
/// фиксированный формат даты). Используется ТОЛЬКО на этапе генерации; чтение при загрузке
/// выполняется через source-generated regex (см. Parsing.TsvRegexReader).
/// </summary>
public sealed class TsvSerializer
{
    public void Write(IEnumerable<DataRecord> records, string path)
    {
        var cfg = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = "\t",
            HasHeaderRecord = false,
        };
        using var writer = new StreamWriter(path, append: false);
        using var csv = new CsvWriter(writer, cfg);
        // Фиксируем формат DateTime, чтобы парсинг был однозначным и deterministic.
        csv.Context.TypeConverterOptionsCache.GetOptions<DateTime>()
           .Formats = new[] { "yyyy-MM-dd HH:mm:ss" };
        csv.WriteRecords(records);
        csv.Flush();
    }
}