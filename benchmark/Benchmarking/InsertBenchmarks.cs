using BenchmarkDotNet.Attributes;
using DbBenchmark.DataAccess;

namespace DbBenchmark.Benchmarking;

/// <summary>
/// База для стратегий вставки. Размер данных параметризован через [ParamsSource],
/// чтение памяти: файл генерируется один раз на этапе GlobalSetup (вне тайминга),
/// внутри @Benchmark выполняется только сам механизм вставки.
/// </summary>
public abstract class InsertBenchmarkBase
{
    protected string hostPath = "";
    protected IInsertStrategy Strategy = null!;

    [ParamsSource(nameof(SizeValues))]
    public long Rows { get; set; } = 1000;

    public IEnumerable<long> SizeValues() => RunArgs.Sizes;

    [GlobalSetup]
    public void Setup()
    {
        RunArgs.Prepare(); // идемпотентно: таблица + файл(ы)
        hostPath = DatasetManager.HostPathFor(Rows);
        Strategy = CreateStrategy();
    }

    protected abstract IInsertStrategy CreateStrategy();

    [Benchmark]
    public long Insert()
    {
        // Strategy.Load() сам делает TRUNCATE в начале → начальное состояние идентично.
        long inserted = Strategy.Load(hostPath);

        // Валидация: count в таблице == число строк в файле (мы знаем его = Rows).
        long inDb = BenchInfra.CountRows();
        if (inDb != Rows)
            throw new InvalidOperationException($"Invalid run: expected {Rows} rows, got {inDb}.");
        return inserted;
    }
}

public class BatchInsertBenchmark : InsertBenchmarkBase
{
    protected override IInsertStrategy CreateStrategy() => new BatchStrategy();
}

public class RawCopyBenchmark : InsertBenchmarkBase
{
    protected override IInsertStrategy CreateStrategy() => new CopyStrategy();
}

public class Linq2DbBulkBenchmark : InsertBenchmarkBase
{
    protected override IInsertStrategy CreateStrategy() => new Linq2DbBulkStrategy();
}

public class EfChunkBenchmark : InsertBenchmarkBase
{
    protected override IInsertStrategy CreateStrategy() => new EfChunkStrategy();
}

public class NativeCopyBenchmark : InsertBenchmarkBase
{
    protected override IInsertStrategy CreateStrategy() => new NativeCopyStrategy();
}