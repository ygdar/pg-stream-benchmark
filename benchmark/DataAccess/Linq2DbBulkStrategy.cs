using DbBenchmark.Domain;
using DbBenchmark.Parsing;
using LinqToDB;
using LinqToDB.Data;

namespace DbBenchmark.DataAccess;

/// <summary>
/// C — LINQ2DB BulkCopy с <c>BulkCopyType.ProviderSpecific</c>. Для PostgreSQL это
/// BINARY COPY через npgsql — тот же нативный механизм, что и raw COPY (стратегия B).
/// </summary>
public sealed class Linq2DbBulkStrategy : IInsertStrategy
{
    public string Name => "C_Linq2DbBulk";

    public long Load(string tsvPathHost)
    {
        BenchInfra.Truncate();

        using var dc = new DataConnection(
            new DataOptions().UseConnectionString(LinqToDB.ProviderName.PostgreSQL15, BenchInfra.ConnectionString));

        var opts = new BulkCopyOptions
        {
            BulkCopyType = BulkCopyType.ProviderSpecific,
            MaxBatchSize = 10_000,
            TableName = BenchInfra.TableName,
            BulkCopyTimeout = 0,
        };

        // Потоковое чтение файла; BulkCopy сам батчит последовательность.
        dc.BulkCopy(opts, ReadLazy(tsvPathHost));
        return BenchInfra.CountRows();
    }

    private static IEnumerable<DataRecord> ReadLazy(string path)
    {
        using var reader = new TsvRegexReader(File.OpenRead(path));
        foreach (var r in reader.ReadRecords())
            yield return r;
    }
}