using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using DbBenchmark.DataAccess;

namespace DbBenchmark.Metrics;

/// <summary>Собранные за один прогон метрики ресурсов для стратегии × размер.</summary>
public sealed class RunMetrics
{
    public string StrategyName { get; set; } = "";
    public long Rows { get; set; }
    public long PeakMemoryBytes { get; set; }
    public double ClientCpuCorePct { get; set; }   // в % от одного ядра (0..~100)
    public double ClientCpuAllPct { get; set; }    // в % от всех ядер (0..100/N)
    public double ServerWorkRate { get; set; }     // оценочная активность сервера (row-работа/сек)
    public long PayloadBytes { get; set; }
    public long OverheadBytes { get; set; }
    public double PayloadOverheadRatio { get; set; }
    public string? EstimateNote { get; set; }      // помечает оценочные значения
}

/// <summary>
/// Модель трафика по проводному механизму для оценки ratio payload/overhead.
/// Оценка строится на детерминированном размере полезных данных (TSV-файл) и
/// известных накладных каждого механизма. Точный packet-level трафик не измеряется.
/// </summary>
public static class TrafficModel
{
    /// <summary>Значение «данные не идут через клиента» для server-side COPY.</summary>
    public const string MechanismServerCopy = "server-side COPY";

    public static string MechanismFor(string strategyName) => strategyName switch
    {
        "A_BatchInsert"  => "multi-row VALUES",
        "B_RawBinaryCopy" => "binary COPY",
        "C_Linq2DbBulk"  => "binary COPY",
        "E_NativeCopy"   => MechanismServerCopy,
        _ => "unknown",
    };

    public static (long payload, long overhead, double ratio) Estimate(long rows, string strategyName)
    {
        long payload = Math.Max(0, new FileInfo(DatasetManager.HostPathFor(rows)).Length);
        string mechanism = MechanismFor(strategyName);
        long overhead;

        if (mechanism == MechanismServerCopy)
        {
            // Данные читаются сервером напрямую из файла; клиент-серверный обмен почти пуст.
            overhead = 512; // константа-минимум (метаданные/команда)
        }
        else if (mechanism == "binary COPY")
        {
            const int colCount = 6;
            // 4-байтовые префиксы длины на колонку + заголовок/маркер строки.
            overhead = rows * (colCount * 4 + 8);
        }
        else // multi-row VALUES
        {
            const int colCount = 6;
            // текстовые разделители/скобки на кортеж + служебные байты SQL-команды.
            overhead = rows * (colCount * 2 + 6) + 512;
        }

        double ratio = (payload + overhead) > 0 ? (double)payload / (payload + overhead) : 0;
        return (payload, overhead, Math.Clamp(ratio, 0, 1));
    }
}

/// <summary>
/// Сбор метрик ресурсов вокруг одного запуска стратегии. Collect происходит вне
/// тайминга `@Benchmark` (в IterationSetup/IterationCleanup), поэтому не искажает
/// измеряемое время. Результат сохраняется в JSON под ключом (стратегия, размер).
/// </summary>
public sealed class ResourceMetrics
{
    private readonly string _strategy;
    private readonly long _rows;
    private readonly int _cores;

    private Process? _proc;
    private TimeSpan _cpuStart;
    private DateTime _t0, _t1;
    private long _peakMem;
    private readonly object _lock = new();
    private System.Threading.Timer? _timer;
    private (long commit, long tupIns) _db0;

    public ResourceMetrics(string strategy, long rows)
    {
        _strategy = strategy;
        _rows = rows;
        _cores = Environment.ProcessorCount;
    }

    public void Start()
    {
        _proc = Process.GetCurrentProcess();
        _cpuStart = _proc.TotalProcessorTime;
        _t0 = DateTime.UtcNow;
        _peakMem = 0;
        _db0 = DbSnapshot();

        _timer = new System.Threading.Timer(_ =>
        {
            lock (_lock)
            {
                long ws = _proc!.WorkingSet64;
                if (ws > _peakMem) _peakMem = ws;
            }
        }, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;

        var cpuEnd = _proc!.TotalProcessorTime;
        _t1 = DateTime.UtcNow;
        double elapsed = (_t1 - _t0).TotalSeconds;
        double cpuSeconds = (cpuEnd - _cpuStart).TotalSeconds;

        double corePct = elapsed > 0 ? (cpuSeconds / elapsed) * 100.0 : 0;
        double allPct = elapsed > 0 ? (cpuSeconds / (elapsed * _cores)) * 100.0 : 0;

        var db1 = DbSnapshot();
        double workPerSec = elapsed > 0
            ? (db1.tupIns - _db0.tupIns + (db1.commit - _db0.commit)) / elapsed
            : 0;

        var (payload, overhead, ratio) = TrafficModel.Estimate(_rows, _strategy);

        var m = new RunMetrics
        {
            StrategyName = _strategy,
            Rows = _rows,
            PeakMemoryBytes = Math.Max(0, _peakMem),
            ClientCpuCorePct = Math.Round(corePct, 2),
            ClientCpuAllPct = Math.Round(allPct, 2),
            ServerWorkRate = Math.Round(workPerSec, 2),
            PayloadBytes = payload,
            OverheadBytes = overhead,
            PayloadOverheadRatio = Math.Round(ratio, 4),
            EstimateNote = "server work & traffic are estimates",
        };

        MetricsStore.Save(m);
    }

    private (long commit, long tupIns) DbSnapshot()
    {
        try
        {
            using var conn = BenchInfra.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT xact_commit, tup_inserted FROM pg_stat_database WHERE datname = current_database();";
            using var r = cmd.ExecuteReader();
            if (r.Read()) return (r.GetInt64(0), r.GetInt64(1));
        }
        catch { /* БД может быть недоступна вне замеров — игнорируем */ }
        return (0, 0);
    }
}

/// <summary>
/// Постоянное хранилище метрик под ключом (стратегия, размер). Пишется в каталог данных,
/// чтобы результаты из дочерних процессов BenchmarkDotNet попали в сводку родительского.
/// </summary>
public static class MetricsStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string MetricsDir =>
        Path.Combine(DatasetManager.DataDirHost, "metrics");

    public static void Save(RunMetrics m)
    {
        Directory.CreateDirectory(MetricsDir);
        var path = Path.Combine(MetricsDir, FileKey(m.StrategyName, m.Rows));
        File.WriteAllText(path, JsonSerializer.Serialize(m, JsonOpts));
    }

    public static IEnumerable<RunMetrics> LoadAll()
    {
        if (!Directory.Exists(MetricsDir)) yield break;
        var files = Directory.EnumerateFiles(MetricsDir, "*.json").ToArray();
        foreach (var f in files)
        {
            RunMetrics? m = null;
            try { m = JsonSerializer.Deserialize<RunMetrics>(File.ReadAllText(f), JsonOpts); }
            catch { }
            if (m is not null) yield return m;
        }
    }

    public static string FileKey(string strategy, long rows) => $"{strategy}-{rows}.json";
}