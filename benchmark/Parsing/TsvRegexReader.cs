using System.Globalization;
using System.Text.RegularExpressions;
using DbBenchmark.Domain;

namespace DbBenchmark.Parsing;

/// <summary>
/// Потоковое чтение TSV через source-generated regex. Каждая строка разбирается
/// предгенерированным регулярным выражением (конечный автомат) в поля модели.
/// CsvHelper для чтения НЕ используется (только для записи в Generation).
/// </summary>
public sealed partial class TsvRegexReader : StreamReader
{
    public TsvRegexReader(Stream stream) : base(stream)
    {
    }

    /// <summary>
    /// Лениво читает и парсит записи. Невалидные строки пропускаются (не участвуют в валидации корректности).
    /// </summary>
    public IEnumerable<DataRecord> ReadRecords()
    {
        string? line;
        while ((line = ReadLine()) is not null)
        {
            if (line.Length == 0) continue;
            var m = LineRegex().Match(line);
            if (!m.Success) continue;

            yield return new DataRecord
            {
                Id       = int.Parse(m.Groups["c0"].Value, CultureInfo.InvariantCulture),
                Name     = m.Groups["c1"].Value,
                Value    = double.Parse(m.Groups["c2"].Value, CultureInfo.InvariantCulture),
                Category = m.Groups["c3"].Value,
                Timestamp = DateTime.ParseExact(
                    m.Groups["c4"].Value, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                Amount   = int.Parse(m.Groups["c5"].Value, CultureInfo.InvariantCulture),
            };
        }
    }

    // 6 колонок, разделённых табами. `[^\t]*` не пропускает табы; спецсимволов в данных нет,
    // поэтому экранирование не требуется — совместимо с форматом нативного COPY (text).
    [GeneratedRegex(
        @"^(?<c0>[^\t]*)\t(?<c1>[^\t]*)\t(?<c2>[^\t]*)\t(?<c3>[^\t]*)\t(?<c4>[^\t]*)\t(?<c5>[^\t]*)$",
        RegexOptions.Compiled)]
    private static partial Regex LineRegex();
}