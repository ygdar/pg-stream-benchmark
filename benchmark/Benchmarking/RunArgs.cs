using DbBenchmark.DataAccess;

namespace DbBenchmark.Benchmarking;

public enum RunMode
{
    Validation,
    Dev,
    Full,
}

/// <summary>
/// Глобальные параметры прогона: выбранный режим и набор размеров данных.
/// Параметры живут в статике, чтобы [ParamsSource] в benchmark-классах читал их
/// (BDN запускает код в том же процессе/сборке).
/// </summary>
public static class RunArgs
{
    public static RunMode Mode { get; private set; } = RunMode.Validation;
    public static long[] Sizes { get; private set; } = { 1000, 10_000 };

    public static void Configure(string mode)
    {
        Mode = mode.ToLowerInvariant() switch
        {
            "validation" or "validate" => RunMode.Validation,
            "dev" => RunMode.Dev,
            "full" => RunMode.Full,
            _ => RunMode.Validation,
        };

        Sizes = Mode switch
        {
            RunMode.Validation => new long[] { 1_000, 10_000 },
            RunMode.Dev       => new long[] { 1_000_000, 2_000_000 },
            RunMode.Full      => new long[] { 5_000_000, 10_000_000, 30_000_000 },
            _ => new long[] { 1_000, 10_000 },
        };
    }

    /// <summary>Подготовка вне тайминга: таблица + генерация всех нужных TSV-файлов.</summary>
    public static void Prepare()
    {
        BenchInfra.EnsureDatabase();
        foreach (var s in Sizes)
            DatasetManager.EnsureFile(s);
    }
}