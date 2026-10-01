using DbBenchmark.Domain;
using DbBenchmark.Generation;
using DbBenchmark.Parsing;

namespace DbBenchmark.DataAccess;

/// <summary>
/// Управление наборами данных: детерминированная генерация TSV-файлов в каталог,
/// смонтированный в контейнер как /data (для нативного COPY). Каждая стратегия
/// читает один и тот же файл как единственный источник истины.
/// </summary>
public static class DatasetManager
{
    /// <summary>Каталог на хосте; монтируется в контейнер как /data (см. docker-compose.yml).
    /// Берётся из env-переменной DBBENCH_DATA_DIR (абсолютный путь, задаваемый Program.cs),
    /// т.к. BenchmarkDotNet запускает прогон в дочернем процессе с другим CWD.</summary>
    public static string DataDirHost
    {
        get
        {
            var env = Environment.GetEnvironmentVariable("DBBENCH_DATA_DIR");
            return env ?? Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "benchmark-data"));
        }
    }

    public static string HostPathFor(long rows) =>
        Path.Combine(DataDirHost, $"rows-{rows}.tsv");

    /// <summary>Путь, видимый процессу PostgreSQL внутри контейнера (/data монтируется из benchmark-data).</summary>
    public static string ContainerPathFor(long rows) => $"/data/rows-{rows}.tsv";

    /// <summary>Генерирует файл заданного размера, если его ещё нет (вне тайминга).</summary>
    public static void EnsureFile(long rows)
    {
        Directory.CreateDirectory(DataDirHost);
        var path = HostPathFor(rows);
        if (File.Exists(path)) return;

        var gen = new DataGenerator(seed: 42);
        var serializer = new TsvSerializer();
        Console.WriteLine($"[gen] writing {rows:N0} rows -> {Path.GetFileName(path)}");
        serializer.Write(gen.Generate(rows), path);
    }

    /// <summary>Потоково читает записи из файла (через source-generated regex).</summary>
    public static IEnumerable<DataRecord> ReadRecords(long rows)
    {
        using var reader = new TsvRegexReader(File.OpenRead(HostPathFor(rows)));
        foreach (var r in reader.ReadRecords())
            yield return r;
    }

    /// <summary>Число физических строк в файле (для валидации count == count in file).</summary>
    public static long CountFileRows(long rows)
    {
        long n = 0;
        using var reader = new TsvRegexReader(File.OpenRead(HostPathFor(rows)));
        foreach (var _ in reader.ReadRecords()) n++;
        return n;
    }
}